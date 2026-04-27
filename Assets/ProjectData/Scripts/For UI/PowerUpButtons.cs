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

        remainingHints--;
        UpdateHintCountText();
        ShowHint();
        BounceButton(hintButton);
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
        referenceImagePanel.transform.DOScale(1f, previewFadeDuration).SetEase(Ease.OutBack);
        
        CanvasGroup cg = referenceImagePanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = referenceImagePanel.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        cg.DOFade(0.75f, previewFadeDuration);
    }

    private void HideReferenceImage()
    {
        if (referenceImagePanel == null) return;
        
        CanvasGroup cg = referenceImagePanel.GetComponent<CanvasGroup>();
        if (cg != null) cg.DOFade(0, previewFadeDuration * 0.5f);
        
        referenceImagePanel.transform.DOScale(0.8f, previewFadeDuration * 0.5f)
            .SetEase(Ease.InBack)
            .OnComplete(() => referenceImagePanel.SetActive(false));
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
            if (drag == null || rect == null) continue;
            if (rect.parent != PuzzleManager.Instance.pieceParent) continue;
            if (drag.isPlaced) continue;

            float distance = Vector2.Distance(rect.anchoredPosition, drag.correctPosition);
            if (distance > PuzzleManager.Instance.cellSize * 2f)
                incorrectPieces.Add(piece);
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

            if (piece.group != null && piece.group.pieces.Count > 1)
            {
                piece.group.pieces.Remove(piece);
                piece.group = new PuzzleGroup();
                piece.group.AddPiece(piece);
            }

            Sequence eraseSeq = DOTween.Sequence();
            eraseSeq.Append(rect.DOScale(0.1f, eraseAnimationDuration).SetEase(Ease.InBack));
            
            CanvasGroup cg = piece.GetComponent<CanvasGroup>();
            if (cg == null) cg = piece.gameObject.AddComponent<CanvasGroup>();
            eraseSeq.Join(cg.DOFade(0, eraseAnimationDuration));

            yield return eraseSeq.WaitForCompletion();

            rect.localScale = Vector3.one;
            cg.alpha = 1f;
            drag.isPlaced = false;
            drag.canDrag = false;
            drag.ResetPiece();

            PuzzleManager.Instance.MoveToBottom(piece.gameObject);
            erasedCount++;
            yield return new WaitForSeconds(eraseDelay);
        }

        yield return new WaitForSeconds(0.2f);
        isEraseActive = false;
        Debug.Log($"🗑️ Erased {erasedCount} incorrect pieces");
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