using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PowerUpButtons : MonoBehaviour
{
    public static PowerUpButtons Instance;

    [Header("=== BUTTON REFERENCES ===")]
    public Button hintButton;        // 💡 Hint
    public Button shuffleButton;     // 🔀 Shuffle bottom pieces
    public Button previewButton;     // 🧩 Preview reference image
    public Button eraseButton;       // 🗑️ Erase incorrect pieces

    [Header("=== HINT SYSTEM 💡 ===")]
    public int maxHints = 3;
    private int remainingHints;
    public Text hintCountText;
    public GameObject hintIndicatorPrefab;  // Glowing circle prefab
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
        
        // Hide preview panel initially
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
    // 💡 HINT BUTTON - Shows where a piece goes
    // ============================================
    public void OnHintButtonClicked()
    {
        if (remainingHints <= 0)
        {
            Debug.Log("❌ No hints remaining!");
            ShakeButton(hintButton);
            ShowNoHintsFeedback();
            return;
        }

        remainingHints--;
        UpdateHintCountText();
        ShowHint();
        
        // Button bounce animation
        BounceButton(hintButton);
        
        Debug.Log($"💡 Hint used! {remainingHints} remaining");
    }

    private void ShowHint()
    {
        // Find unplaced pieces
        List<PuzzlePiece> unplacedPieces = new List<PuzzlePiece>();
        foreach (var piece in PuzzleManager.Instance.allPieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null && !drag.isPlaced)
            {
                unplacedPieces.Add(piece);
            }
        }

        if (unplacedPieces.Count == 0)
        {
            Debug.Log("✅ All pieces already placed!");
            remainingHints++; // Refund hint
            UpdateHintCountText();
            return;
        }

        // Pick random unplaced piece
        PuzzlePiece hintPiece = unplacedPieces[Random.Range(0, unplacedPieces.Count)];
        StartCoroutine(ShowHintAnimation(hintPiece));
    }

    private IEnumerator ShowHintAnimation(PuzzlePiece hintPiece)
    {
        DragPiece hintDrag = hintPiece.GetComponent<DragPiece>();
        RectTransform hintRect = hintPiece.GetComponent<RectTransform>();
        
        if (hintDrag == null || hintRect == null) yield break;

        // 1. Highlight the piece
        CanvasGroup pieceCG = hintPiece.GetComponent<CanvasGroup>();
        if (pieceCG == null)
            pieceCG = hintPiece.gameObject.AddComponent<CanvasGroup>();

        // 2. Show ghost at correct position
        if (hintDrag.ghostImage != null)
        {
            hintDrag.ghostImage.SetActive(true);
            RectTransform ghostRect = hintDrag.ghostImage.GetComponent<RectTransform>();
            if (ghostRect != null)
            {
                ghostRect.DOScale(1.3f, 0.4f).SetLoops(-1, LoopType.Yoyo);
            }
        }

        // 3. Create glowing indicator at correct position
        GameObject indicator = null;
        if (hintIndicatorPrefab != null)
        {
            indicator = Instantiate(hintIndicatorPrefab, PuzzleManager.Instance.pieceParent);
            indicator.name = "HintIndicator";
            RectTransform indRect = indicator.GetComponent<RectTransform>();
            indRect.anchoredPosition = hintDrag.correctPosition;
            
            // Pulse animation
            indRect.DOScale(1.4f, 0.5f).SetLoops(-1, LoopType.Yoyo);
            indRect.DORotate(new Vector3(0, 0, 360), 2f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart);
            
            // Fade pulse
            Image indImg = indicator.GetComponent<Image>();
            if (indImg != null)
            {
                indImg.color = hintGlowColor;
                indImg.DOFade(0.2f, 0.6f).SetLoops(-1, LoopType.Yoyo);
            }
        }

        // 4. Flash the piece
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

        // 5. Cleanup
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
        {
            Destroy(indicator);
        }

        Debug.Log($"💡 Hint completed for piece: {hintPiece.name}");
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
    // 🔀 SHUFFLE BUTTON - Shuffles bottom panel pieces
    // ============================================
    public void OnShuffleButtonClicked()
    {
        if (isShuffling) return;

        StartCoroutine(ShuffleAllBottomPiecesWithOverflow());
        BounceButton(shuffleButton);
    }

   private IEnumerator ShuffleAllBottomPiecesWithOverflow()
{
    isShuffling = true;

    // STEP 1: Collect ALL pieces in bottom panel (both active and inactive)
    List<GameObject> allBottomPieces = new List<GameObject>();
    
    foreach (Transform child in PuzzleManager.Instance.bottomParent)
    {
        DragPiece drag = child.GetComponent<DragPiece>();
        if (drag != null && !drag.isPlaced)
        {
            allBottomPieces.Add(child.gameObject);
        }
    }

    if (allBottomPieces.Count < 2)
    {
        Debug.Log("⚠️ Not enough pieces to shuffle!");
        isShuffling = false;
        yield break;
    }

    Debug.Log($"🔀 Found {allBottomPieces.Count} total pieces in bottom panel. Max visible: 6");

    // STEP 2: Shuffle ALL pieces randomly
    for (int i = allBottomPieces.Count - 1; i > 0; i--)
    {
        int j = Random.Range(0, i + 1);
        GameObject temp = allBottomPieces[i];
        allBottomPieces[i] = allBottomPieces[j];
        allBottomPieces[j] = temp;
    }

    // STEP 3: Separate into visible (first 6) and hidden (rest)
    int maxVisible = 6; // Your max visible slots
    List<GameObject> newVisiblePieces = new List<GameObject>();
    List<GameObject> newHiddenPieces = new List<GameObject>();

    for (int i = 0; i < allBottomPieces.Count; i++)
    {
        if (i < maxVisible)
        {
            newVisiblePieces.Add(allBottomPieces[i]);
        }
        else
        {
            newHiddenPieces.Add(allBottomPieces[i]);
        }
    }

    // STEP 4: Position and activate visible pieces with animation
    for (int i = 0; i < newVisiblePieces.Count; i++)
    {
        GameObject piece = newVisiblePieces[i];
        RectTransform rect = piece.GetComponent<RectTransform>();
        DragPiece drag = piece.GetComponent<DragPiece>();
        
        if (rect != null)
        {
            // Ensure parent is bottomParent
            rect.SetParent(PuzzleManager.Instance.bottomParent, true);
            
            // ACTIVATE the piece
            piece.SetActive(true);
            
            // Enable dragging
            if (drag != null)
            {
                drag.canDrag = true;
            }
            
            // Target position with proper spacing
            Vector2 targetPos = new Vector2(i * PuzzleManager.Instance.spacing, 0);
            
            // Animate to position
            rect.DOAnchorPos(targetPos, shuffleDuration)
                .SetEase(shuffleEase);
            
            // Bounce effect
            rect.DOPunchScale(Vector3.one * 0.15f, shuffleDuration, 2, 0.5f);
            
            yield return new WaitForSeconds(shuffleDelay);
        }
    }

    // STEP 5: Hide overflow pieces (move them off-screen and deactivate)
    float overflowStartX = maxVisible * PuzzleManager.Instance.spacing;
    
    for (int i = 0; i < newHiddenPieces.Count; i++)
    {
        GameObject piece = newHiddenPieces[i];
        RectTransform rect = piece.GetComponent<RectTransform>();
        DragPiece drag = piece.GetComponent<DragPiece>();
        
        if (rect != null)
        {
            // Move to overflow position
            Vector2 overflowPos = new Vector2(overflowStartX + (i * PuzzleManager.Instance.spacing), 0);
            rect.anchoredPosition = overflowPos;
            
            // DEACTIVATE the piece
            piece.SetActive(false);
            
            // Disable dragging
            if (drag != null)
            {
                drag.canDrag = false;
            }
        }
    }

    // Wait for all animations
    yield return new WaitForSeconds(shuffleDuration);

    // STEP 6: Update PuzzleManager tracking
    if (PuzzleManager.Instance != null)
    {
        PuzzleManager.Instance.UpdateSlotPiecesAfterShuffleWithOverflow(newVisiblePieces, newHiddenPieces);
    }

    isShuffling = false;
    Debug.Log($"🔀 Shuffle done! Visible: {newVisiblePieces.Count}, Hidden: {newHiddenPieces.Count}");
}

    // ============================================
    // 🧩 PREVIEW BUTTON - Shows/hides reference image
    // ============================================
    public void OnPreviewButtonClicked()
    {
        isPreviewActive = !isPreviewActive;

        if (isPreviewActive)
        {
            ShowReferenceImage();
            HighlightButton(previewButton);
        }
        else
        {
            HideReferenceImage();
            ResetButtonColor(previewButton);
        }

        BounceButton(previewButton);
    }

    private void ShowReferenceImage()
    {
        if (referenceImagePanel == null) return;

        referenceImagePanel.SetActive(true);
        referenceImagePanel.transform.localScale = Vector3.zero;
        
        // Animate appearance
        referenceImagePanel.transform.DOScale(1f, previewFadeDuration)
            .SetEase(Ease.OutBack);

        CanvasGroup cg = referenceImagePanel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = referenceImagePanel.AddComponent<CanvasGroup>();
        
        cg.alpha = 0;
        cg.DOFade(0.75f, previewFadeDuration);
        
        Debug.Log("🧩 Reference image shown");
    }

    private void HideReferenceImage()
    {
        if (referenceImagePanel == null) return;

        CanvasGroup cg = referenceImagePanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOFade(0, previewFadeDuration * 0.5f);
        }

        referenceImagePanel.transform.DOScale(0.8f, previewFadeDuration * 0.5f)
            .SetEase(Ease.InBack)
            .OnComplete(() => referenceImagePanel.SetActive(false));
        
        Debug.Log("🧩 Reference image hidden");
    }

    // ============================================
    // 🗑️ ERASE BUTTON - Returns incorrect pieces to bottom
    // ============================================
    public void OnEraseButtonClicked()
    {
        if (isEraseActive) return;

        List<PuzzlePiece> piecesToErase = GetIncorrectPieces();

        if (piecesToErase.Count == 0)
        {
            Debug.Log("✅ No incorrect pieces to erase!");
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

            // Skip pieces not in puzzle area
            if (drag == null || rect == null) continue;
            if (rect.parent != PuzzleManager.Instance.pieceParent) continue;
            if (drag.isPlaced) continue;

            // Check if piece is far from its correct position
            float distance = Vector2.Distance(rect.anchoredPosition, drag.correctPosition);
            
            // If distance is more than 2 cells away, consider it incorrect
            if (distance > PuzzleManager.Instance.cellSize * 2f)
            {
                incorrectPieces.Add(piece);
            }
        }

        return incorrectPieces;
    }

    private IEnumerator ErasePiecesCoroutine(List<PuzzlePiece> piecesToErase)
    {
        isEraseActive = true;
        int erasedCount = 0;

        foreach (var piece in piecesToErase)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            RectTransform rect = piece.GetComponent<RectTransform>();
            
            if (drag == null || rect == null) continue;

            // Separate from group
            if (piece.group != null && piece.group.pieces.Count > 1)
            {
                piece.group.pieces.Remove(piece);
                piece.group = new PuzzleGroup();
                piece.group.AddPiece(piece);
            }

            // Shrink animation
            Sequence eraseSeq = DOTween.Sequence();
            eraseSeq.Append(rect.DOScale(0.1f, eraseAnimationDuration).SetEase(Ease.InBack));
            
            CanvasGroup cg = piece.GetComponent<CanvasGroup>();
            if (cg == null) cg = piece.gameObject.AddComponent<CanvasGroup>();
            eraseSeq.Join(cg.DOFade(0, eraseAnimationDuration));

            yield return eraseSeq.WaitForCompletion();

            // Reset piece properties
            rect.localScale = Vector3.one;
            cg.alpha = 1f;
            drag.isPlaced = false;
            drag.canDrag = false;
            drag.ResetPiece();

            // Send back to bottom panel
            PuzzleManager.Instance.MoveToBottom(piece.gameObject);
            
            erasedCount++;
            
            yield return new WaitForSeconds(eraseDelay);
        }

        // Rearrange bottom panel after all pieces are returned
        yield return new WaitForSeconds(0.2f);
        
        // Note: You might need to add a public method in PuzzleManager for this
        // or call RearrangeBottom via reflection/other means
        
        isEraseActive = false;
        Debug.Log($"🗑️ Erased {erasedCount} incorrect pieces back to bottom panel");
    }

    // ============================================
    // UTILITY METHODS
    // ============================================
    private void UpdateHintCountText()
    {
        if (hintCountText != null)
        {
            hintCountText.text = $"x{remainingHints}";
            
            if (remainingHints <= 0)
            {
                hintCountText.color = Color.red;
            }
            else
            {
                hintCountText.color = Color.white;
            }
        }
    }

    private void BounceButton(Button button)
    {
        if (button != null)
        {
            button.transform.DOPunchScale(Vector3.one * scaleBounceAmount, scaleBounceDuration, 5, 0.5f);
        }
    }

    private void ShakeButton(Button button)
    {
        if (button != null)
        {
            button.transform.DOShakePosition(0.4f, 8f, 30);
        }
    }

    private void HighlightButton(Button button)
    {
        if (button != null)
        {
            Image btnImage = button.GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.DOColor(activeButtonColor, 0.2f);
            }
        }
    }

    private void ResetButtonColor(Button button)
    {
        if (button != null)
        {
            Image btnImage = button.GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.DOColor(normalButtonColor, 0.2f);
            }
        }
    }

    // Public method to add hints (for rewards, ads, etc.)
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
        
        Debug.Log($"💡 Added {amount} hints! Total: {remainingHints}");
    }
}