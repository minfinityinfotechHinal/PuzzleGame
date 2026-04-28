using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PowerUpButtons : MonoBehaviour
{
    public static PowerUpButtons Instance;

    [Header("=== BUTTON REFERENCES ===")]
    public Button hintButton;
    public Button shuffleButton;
    public Button previewButton;
    public Button eraseButton;

    [Header("=== HINT SYSTEM 💡 ===")]
    public int maxHints = 3;
    private int remainingHints;
    public Text hintCountText;
    public GameObject hintIndicatorPrefab;
    public float hintShowDuration = 2f;
    public Color hintGlowColor = new Color(1f, 0.8f, 0.2f, 0.6f);

    [Header("=== SHUFFLE SYSTEM 🔀 ===")]
    public float shuffleDuration = 0.3f;
    public float shuffleDelay = 0.05f;
    public Ease shuffleEase = Ease.OutQuad;
    public bool isShuffling = false;

    [Header("=== PREVIEW SYSTEM 🧩 ===")]
    public GameObject referenceImagePanel;
    public Image referenceImage;
    public float previewFadeDuration = 0.3f;
    private bool isPreviewActive = false;
    public GameObject bottomPanel;

    [Header("=== ERASE SYSTEM 🗑️ ===")]
    public float eraseAnimationDuration = 0.3f;
    public float eraseDelay = 0.08f;

    [Header("=== BUTTON FEEDBACK ===")]
    public Color activeButtonColor = new Color(1f, 0.85f, 0.3f, 1f);
    public Color normalButtonColor = Color.white;
    public float scaleBounceAmount = 0.2f;
    public float scaleBounceDuration = 0.3f;

    private bool isEraseActive = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        remainingHints = maxHints;
        InitializeAllButtons();
        UpdateHintCountText();
        
        if (referenceImagePanel != null)
            referenceImagePanel.SetActive(false);
    }

    private void InitializeAllButtons()
    {
        if (hintButton != null)
            hintButton.onClick.AddListener(OnHintButtonClicked);
        if (shuffleButton != null)
            shuffleButton.onClick.AddListener(OnShuffleButtonClicked);
        if (previewButton != null)
            previewButton.onClick.AddListener(OnPreviewButtonClicked);
        if (eraseButton != null)
            eraseButton.onClick.AddListener(OnEraseButtonClicked);
    }

    // ============================================
    // 💡 HINT
    // ============================================
    public void OnHintButtonClicked()
{
    if (remainingHints <= 0)
    {
        ShakeButton(hintButton);
        ShowNoHintsFeedback();
        return;
    }

    // Find unplaced pieces ONLY from visible bottom slots (first 6)
    List<PuzzlePiece> visibleBottomPieces = new List<PuzzlePiece>();
    
    foreach (var piece in PuzzleManager.Instance.allPieces)
    {
        DragPiece drag = piece.GetComponent<DragPiece>();
        GameObject pieceObj = piece.gameObject;
        
        // Only pick pieces that are:
        // 1. Not placed
        // 2. In bottom panel parent
        // 3. Active (visible) - this ensures only first 6
        if (drag != null && !drag.isPlaced && 
            pieceObj.transform.parent == PuzzleManager.Instance.bottomParent &&
            pieceObj.activeSelf)
        {
            visibleBottomPieces.Add(piece);
        }
    }

    if (visibleBottomPieces.Count == 0)
    {
        Debug.Log("✅ No visible pieces in bottom panel to hint!");
        return;
    }

    // Use one hint
    remainingHints--;
    UpdateHintCountText();
    BounceButton(hintButton);

    // Pick random piece from VISIBLE bottom pieces only
    PuzzlePiece hintPiece = visibleBottomPieces[Random.Range(0, visibleBottomPieces.Count)];
    StartCoroutine(AutoPlacePiece(hintPiece));
    
    Debug.Log($"💡 Hint used! {remainingHints} remaining. Placed: {hintPiece.name}");
}

    private IEnumerator AutoPlacePiece(PuzzlePiece hintPiece)
{
    DragPiece hintDrag = hintPiece.GetComponent<DragPiece>();
    RectTransform hintRect = hintPiece.GetComponent<RectTransform>();
    
    if (hintDrag == null || hintRect == null) yield break;

    Debug.Log($"💡 Auto-placing: {hintPiece.name} from bottom panel");

    // Step 1: Remove from bottom BUT DON'T FILL OVERFLOW YET
    if (PuzzleManager.Instance != null)
    {
        // Manually remove without triggering FillFromOverflow
        PuzzleManager.Instance.RemoveFromBottomWithoutFill(hintPiece.gameObject);
    }

    // Step 2: Move to puzzle parent and bring to front
    hintRect.SetParent(PuzzleManager.Instance.pieceParent, true);
    hintRect.SetAsLastSibling();
    
    // Ensure piece is visible and scaled properly
    hintPiece.gameObject.SetActive(true);
    hintRect.localScale = Vector3.one;

    // Step 3: Lift effect
    hintRect.DOScale(1.3f, 0.15f).SetEase(Ease.OutBack);
    yield return new WaitForSeconds(0.2f);

    // Step 4: Fly to correct position
    float flyDuration = 0.5f;
    hintRect.DOAnchorPos(hintDrag.correctPosition, flyDuration).SetEase(Ease.InOutCubic);
    hintRect.DOScale(1f, flyDuration).SetEase(Ease.InOutQuad);

    // Wait for the piece to fly away from bottom area before filling
    yield return new WaitForSeconds(flyDuration * 0.3f);

    // Step 5: NOW fill the empty slot from overflow (piece has moved away)
    if (PuzzleManager.Instance != null)
    {
        PuzzleManager.Instance.RearrangeBottomPublic();
    }

    // Wait for fly to complete
    yield return new WaitForSeconds(flyDuration * 0.7f);

    // Step 6: Play particle effect
    if (hintDrag.particleObject != null)
    {
        hintDrag.particleObject.SetActive(true);
    }

    // Step 7: Snap to exact position and lock
    hintRect.anchoredPosition = hintDrag.correctPosition;
    hintDrag.isPlaced = true;
    hintDrag.canDrag = false;
    
    if (hintDrag.canvasGroup != null)
    {
        hintDrag.canvasGroup.blocksRaycasts = false;
        hintDrag.canvasGroup.alpha = 1f;
    }

    // Small snap bounce
    hintRect.DOPunchScale(Vector3.one * 0.15f, 0.2f, 2, 0.5f);

    // Step 8: Notify PuzzleManager about placement
    if (PuzzleManager.Instance != null)
    {
        List<PuzzlePiece> placedList = new List<PuzzlePiece> { hintPiece };
        PuzzleManager.Instance.OnGroupPlaced(placedList);
    }

    // Step 9: Turn off particle after 2 seconds
    if (hintDrag.particleObject != null)
    {
        yield return new WaitForSeconds(2f);
        hintDrag.particleObject.SetActive(false);
    }

    // Step 10: Check completion
    hintDrag.CheckCompletion();
    
    Debug.Log($"💡 Hint complete! {hintPiece.name} placed.");
}

    private void ShowHint()
    {
        List<PuzzlePiece> unplacedPieces = new List<PuzzlePiece>();
        foreach (var piece in PuzzleManager.Instance.allPieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null && !drag.isPlaced)
                unplacedPieces.Add(piece);
        }

        if (unplacedPieces.Count == 0)
        {
            remainingHints++;
            UpdateHintCountText();
            return;
        }

        PuzzlePiece hintPiece = unplacedPieces[Random.Range(0, unplacedPieces.Count)];
        StartCoroutine(ShowHintAnimation(hintPiece));
    }

    private IEnumerator ShowHintAnimation(PuzzlePiece hintPiece)
    {
        DragPiece hintDrag = hintPiece.GetComponent<DragPiece>();
        if (hintDrag == null) yield break;

        CanvasGroup pieceCG = hintPiece.GetComponent<CanvasGroup>();
        if (pieceCG == null)
            pieceCG = hintPiece.gameObject.AddComponent<CanvasGroup>();

        if (hintDrag.ghostImage != null)
        {
            hintDrag.ghostImage.SetActive(true);
            RectTransform ghostRect = hintDrag.ghostImage.GetComponent<RectTransform>();
            if (ghostRect != null)
                ghostRect.DOScale(1.3f, 0.4f).SetLoops(-1, LoopType.Yoyo);
        }

        GameObject indicator = null;
        if (hintIndicatorPrefab != null)
        {
            indicator = Instantiate(hintIndicatorPrefab, PuzzleManager.Instance.pieceParent);
            indicator.name = "HintIndicator";
            RectTransform indRect = indicator.GetComponent<RectTransform>();
            indRect.anchoredPosition = hintDrag.correctPosition;
            indRect.DOScale(1.4f, 0.5f).SetLoops(-1, LoopType.Yoyo);
            
            Image indImg = indicator.GetComponent<Image>();
            if (indImg != null)
            {
                indImg.color = hintGlowColor;
                indImg.DOFade(0.2f, 0.6f).SetLoops(-1, LoopType.Yoyo);
            }
        }

        float elapsedTime = 0f;
        float flashInterval = 0.3f;
        bool isVisible = true;
        
        while (elapsedTime < hintShowDuration)
        {
            isVisible = !isVisible;
            pieceCG.alpha = isVisible ? 1f : 0.3f;
            elapsedTime += flashInterval;
            yield return new WaitForSeconds(flashInterval);
        }

        pieceCG.alpha = 1f;
        
        if (hintDrag.ghostImage != null)
        {
            RectTransform ghostRect = hintDrag.ghostImage.GetComponent<RectTransform>();
            if (ghostRect != null)
            {
                ghostRect.DOKill();
                ghostRect.localScale = Vector3.one;
            }
            hintDrag.ghostImage.SetActive(false);
        }

        if (indicator != null)
            Destroy(indicator);
    }

    private void ShowNoHintsFeedback()
    {
        if (hintCountText != null)
        {
            hintCountText.transform.DOShakePosition(0.4f, 5f, 30);
            hintCountText.DOColor(Color.red, 0.3f).OnComplete(() =>
                hintCountText.DOColor(Color.white, 0.3f));
        }
    }

    // ============================================
    // 🔀 SHUFFLE
    // ============================================
    public void OnShuffleButtonClicked()
    {
        if (isShuffling) return;
        StartCoroutine(ShuffleWithScaleAnimation());
        BounceButton(shuffleButton);
    }

    private IEnumerator ShuffleWithScaleAnimation()
    {
        isShuffling = true;

        // Collect ALL pieces in bottom panel
        List<GameObject> allBottomPieces = new List<GameObject>();
        foreach (Transform child in PuzzleManager.Instance.bottomParent)
        {
            DragPiece drag = child.GetComponent<DragPiece>();
            if (drag != null && !drag.isPlaced)
                allBottomPieces.Add(child.gameObject);
        }

        if (allBottomPieces.Count < 2)
        {
            isShuffling = false;
            yield break;
        }

        // Shuffle
        for (int i = allBottomPieces.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            GameObject temp = allBottomPieces[i];
            allBottomPieces[i] = allBottomPieces[j];
            allBottomPieces[j] = temp;
        }

        // Split visible/hidden
        int maxVisible = 6;
        List<GameObject> newVisible = new List<GameObject>();
        List<GameObject> newHidden = new List<GameObject>();

        for (int i = 0; i < allBottomPieces.Count; i++)
        {
            if (i < maxVisible)
                newVisible.Add(allBottomPieces[i]);
            else
                newHidden.Add(allBottomPieces[i]);
        }

        // PHASE 1: Scale down visible pieces
        foreach (Transform child in PuzzleManager.Instance.bottomParent)
        {
            if (child.gameObject.activeSelf)
            {
                RectTransform rect = child.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.DOKill();
                    rect.DOScale(Vector3.zero, shuffleDuration * 0.4f).SetEase(Ease.InBack);
                }
            }
        }

        yield return new WaitForSeconds(shuffleDuration * 0.5f);

        // PHASE 2: Deactivate all, then position
        foreach (var piece in allBottomPieces)
            piece.SetActive(false);

        for (int i = 0; i < newVisible.Count; i++)
        {
            RectTransform rect = newVisible[i].GetComponent<RectTransform>();
            DragPiece drag = newVisible[i].GetComponent<DragPiece>();
            
            if (rect != null)
            {
                rect.SetParent(PuzzleManager.Instance.bottomParent, false);
                rect.anchoredPosition = new Vector2(i * PuzzleManager.Instance.spacing, 0);
                rect.localScale = Vector3.zero;
                newVisible[i].SetActive(true);
                if (drag != null) drag.canDrag = true;
            }
        }

        float overflowX = maxVisible * PuzzleManager.Instance.spacing;
        for (int i = 0; i < newHidden.Count; i++)
        {
            RectTransform rect = newHidden[i].GetComponent<RectTransform>();
            DragPiece drag = newHidden[i].GetComponent<DragPiece>();
            
            if (rect != null)
            {
                rect.SetParent(PuzzleManager.Instance.bottomParent, false);
                rect.anchoredPosition = new Vector2(overflowX + (i * PuzzleManager.Instance.spacing), 0);
                rect.localScale = Vector3.zero;
                newHidden[i].SetActive(false);
                if (drag != null) drag.canDrag = false;
            }
        }

        yield return new WaitForSeconds(0.05f);

        // PHASE 3: Scale up with stagger
        for (int i = 0; i < newVisible.Count; i++)
        {
            RectTransform rect = newVisible[i].GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.DOKill();
                rect.DOScale(Vector3.one, shuffleDuration * 0.5f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(i * shuffleDelay);
            }
        }

        yield return new WaitForSeconds(shuffleDuration * 0.5f + (newVisible.Count * shuffleDelay));

        // PHASE 4: Update PuzzleManager
        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.UpdateSlotPiecesAfterShuffleWithOverflow(newVisible, newHidden);
            PuzzleManager.Instance.ForceRearrangeBottom();
        }

        isShuffling = false;
        Debug.Log($"🔀 Shuffle done! Visible: {newVisible.Count}, Hidden: {newHidden.Count}");
    }

    // ============================================
    // 🧩 PREVIEW
    // ============================================
        
   // ============================================
// 🧩 PREVIEW
// ============================================

public void OnPreviewButtonClicked()
{
    isPreviewActive = !isPreviewActive;
    if (isPreviewActive)
    {
        ShowReferenceImage();
        HideBottomPanel();
        HighlightButton(previewButton);
    }
    else
    {
        HideReferenceImage();
        ShowBottomPanel();
        ResetButtonColor(previewButton);
    }
    BounceButton(previewButton);
}

private void ShowReferenceImage()
{
    if (referenceImagePanel == null) return;
    referenceImagePanel.SetActive(true);
}

private void HideReferenceImage()
{
    if (referenceImagePanel == null) return;
    referenceImagePanel.SetActive(false);
}

private void HideBottomPanel()
{
    if (bottomPanel != null)
    {
        bottomPanel.SetActive(false);
    }
    else if (PuzzleManager.Instance != null && PuzzleManager.Instance.bottomParent != null)
    {
        PuzzleManager.Instance.bottomParent.gameObject.SetActive(false);
    }
}

private void ShowBottomPanel()
{
    if (bottomPanel != null)
    {
        bottomPanel.SetActive(true);
    }
    else if (PuzzleManager.Instance != null && PuzzleManager.Instance.bottomParent != null)
    {
        PuzzleManager.Instance.bottomParent.gameObject.SetActive(true);
    }
}
    // ============================================
    // 🗑️ ERASE
    // ============================================
    public void OnEraseButtonClicked()
    {
        if (isEraseActive) return;

        List<PuzzlePiece> piecesToErase = GetIncorrectPieces();
        if (piecesToErase.Count == 0)
        {
            ShakeButton(eraseButton);
            return;
        }

        StartCoroutine(ErasePiecesCoroutine(piecesToErase));
        BounceButton(eraseButton);
    }

    private List<PuzzlePiece> GetIncorrectPieces()
    {
        List<PuzzlePiece> incorrectPieces = new List<PuzzlePiece>();

        foreach (var piece in PuzzleManager.Instance.allPieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            RectTransform rect = piece.GetComponent<RectTransform>();

            // Skip pieces that are null
            if (drag == null || rect == null) continue;
            
            // Skip pieces not in puzzle area (still in bottom panel)
            if (rect.parent != PuzzleManager.Instance.pieceParent) continue;
            
            // Skip pieces that are correctly placed (snapped)
            if (drag.isPlaced) continue;

            // Check if piece is at its correct position
            float distance = Vector2.Distance(rect.anchoredPosition, drag.correctPosition);
            
            // If piece is not within snap threshold, it's incorrect
            // Use a smaller threshold to catch more incorrect pieces
            if (distance > drag.snapThreshold * 0.5f)
            {
                incorrectPieces.Add(piece);
                Debug.Log($"🗑️ Found incorrect piece: {piece.name}, distance: {distance:F1}, threshold: {drag.snapThreshold * 0.3f:F1}");
            }
        }

        Debug.Log($"🔍 Found {incorrectPieces.Count} incorrect pieces in puzzle area");
        return incorrectPieces;
    }
    private IEnumerator ErasePiecesCoroutine(List<PuzzlePiece> piecesToErase)
{
    isEraseActive = true;
    int erasedCount = piecesToErase.Count;
    int completedCount = 0;

    // Start ALL animations at once
    foreach (var piece in piecesToErase)
    {
        DragPiece drag = piece.GetComponent<DragPiece>();
        RectTransform rect = piece.GetComponent<RectTransform>();
        
        if (drag == null || rect == null) 
        {
            completedCount++;
            continue;
        }
        if (drag.isPlaced) 
        {
            completedCount++;
            continue;
        }

        // Separate from group
        if (piece.group != null && piece.group.pieces.Count > 1)
        {
            piece.group.pieces.Remove(piece);
            PuzzleGroup newGroup = new PuzzleGroup();
            newGroup.AddPiece(piece);
        }

        // Kill existing tweens
        rect.DOKill();
        
        // Create shrink animation
        Sequence eraseSeq = DOTween.Sequence();
        eraseSeq.Append(rect.DOScale(0.1f, eraseAnimationDuration).SetEase(Ease.InBack));
        
        CanvasGroup cg = piece.GetComponent<CanvasGroup>();
        if (cg == null) cg = piece.gameObject.AddComponent<CanvasGroup>();
        eraseSeq.Join(cg.DOFade(0, eraseAnimationDuration));
        
        // When animation completes, reset and move to bottom
        eraseSeq.OnComplete(() =>
        {
            rect.localScale = Vector3.one;
            cg.alpha = 1f;
            drag.isPlaced = false;
            drag.canDrag = false;
            drag.ResetPiece();
            
            PuzzleManager.Instance.MoveToBottom(piece.gameObject);
            
            completedCount++;
        });
    }

    // Wait until all pieces have completed their animation
    float timeout = 0f;
    float maxTimeout = eraseAnimationDuration + 2f; // Safety timeout
    while (completedCount < erasedCount && timeout < maxTimeout)
    {
        yield return new WaitForSeconds(0.1f);
        timeout += 0.1f;
    }
    
    // Small extra delay for MoveToBottom coroutines to start
    yield return new WaitForSeconds(0.3f);
    
    // Force rearrange bottom
    if (PuzzleManager.Instance != null)
    {
        PuzzleManager.Instance.RearrangeBottomPublic();
    }
    
    isEraseActive = false;
    Debug.Log($"🗑️ Erased {erasedCount} incorrect pieces back to bottom panel");
}
    
// Helper coroutine to clean up each piece after its animation
private IEnumerator EraseSinglePieceAfterAnimation(PuzzlePiece piece, DragPiece drag, RectTransform rect, CanvasGroup cg, float duration)
{
    // Wait for the animation duration
    yield return new WaitForSeconds(duration);
    
    // Reset piece properties
    rect.localScale = Vector3.one;
    cg.alpha = 1f;
    drag.isPlaced = false;
    drag.canDrag = false;
    drag.ResetPiece();

    // Send back to bottom panel
    PuzzleManager.Instance.MoveToBottom(piece.gameObject);
}

    // ============================================
    // UTILITY
    // ============================================
    private void UpdateHintCountText()
    {
        if (hintCountText != null)
        {
            hintCountText.text = $"x{remainingHints}";
            hintCountText.color = remainingHints <= 0 ? Color.red : Color.white;
        }
    }

    private void BounceButton(Button button)
    {
        if (button != null)
            button.transform.DOPunchScale(Vector3.one * scaleBounceAmount, scaleBounceDuration, 5, 0.5f);
    }

    private void ShakeButton(Button button)
    {
        if (button != null)
            button.transform.DOShakePosition(0.4f, 8f, 30);
    }

    private void HighlightButton(Button button)
    {
        if (button != null)
        {
            Image btnImage = button.GetComponent<Image>();
            if (btnImage != null) btnImage.DOColor(activeButtonColor, 0.2f);
        }
    }

    private void ResetButtonColor(Button button)
    {
        if (button != null)
        {
            Image btnImage = button.GetComponent<Image>();
            if (btnImage != null) btnImage.DOColor(normalButtonColor, 0.2f);
        }
    }

    public void AddHints(int amount)
    {
        remainingHints += amount;
        UpdateHintCountText();
        
        if (hintCountText != null)
        {
            hintCountText.transform.DOPunchScale(Vector3.one * 0.4f, 0.3f, 3, 0.5f);
            hintCountText.DOColor(Color.green, 0.3f).OnComplete(() =>
                hintCountText.DOColor(Color.white, 0.3f));
        }
    }
}