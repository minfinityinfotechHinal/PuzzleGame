using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class DragPiece : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IInitializePotentialDragHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas mainCanvas;
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public float snapThreshold = 50f;
    public bool canDrag = false;
    public RectTransform dragArea;
    public Vector2 correctPosition;

    public GameObject ghostImage;
    public GameObject particleObject;
    public PuzzlePiece piece;

    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float dragStartThreshold = 5f;

    private bool isScrolling = false;
    private bool isDraggingPiece = false;
    private bool scrollDragStarted = false;

    [SerializeField] private RectTransform bottomPanel;

    private bool parentChanged = false;

    // Group drag: track pointer's LOCAL position each frame and move all
    // group pieces by the delta.  Allows grabbing from any piece in the group.
    private Vector2 prevPointerLocal;
    private Vector3 worldOffset;          // single-piece drag only

    private Vector2 dragStartPosition;
    private bool dragInitiated = false;

    [Header("Boundaries")]
    [SerializeField] private float screenEdgePadding = 50f;
    [SerializeField] private bool useStrictScreenBounds = true;
    [SerializeField] private float topPadding = 100f;
    [SerializeField] private float bottomPadding = 50f;

    private Vector2 pieceHalfSize;

    [Header("Raycast Settings")]
    [SerializeField] private float alphaHitThreshold = 0.1f;
    [SerializeField] private bool useAlphaHitTest = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Image pieceImage;
    private Transform originalParent;
    private Vector2 originalAnchoredPosition;
    private bool pieceTakenFromScrollView = false;

    private RectTransform canvasRectTransform;
    private Camera renderCamera;

    [SerializeField] private float exitBottomPanelThreshold = 40f;

    private bool isMerging = false;
    private float mergeCooldown = 0.1f;
    private float lastMergeTime = -1f;

    [Header("Exit Settings")]
    [SerializeField] private float verticalExitDistance = 120f;

    private CanvasGroup shadowCanvasGroup;

    private Transform originalParentBeforeDrag;
    private int originalSiblingIndex;

    // =========================================================
    // AWAKE / START
    // =========================================================

    private void Awake()
    {
        piece = GetComponent<PuzzlePiece>();
        rectTransform = GetComponent<RectTransform>();
        mainCanvas = FindMainCanvas();

        if (mainCanvas != null)
        {
            canvasRectTransform = mainCanvas.GetComponent<RectTransform>();
            renderCamera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : mainCanvas.worldCamera;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetupRaycaster();

        if (scrollRect == null && PuzzleManager.Instance != null)
            scrollRect = PuzzleManager.Instance.scrollRect;
        if (bottomPanel == null && PuzzleManager.Instance != null)
            bottomPanel = PuzzleManager.Instance.bottomPanel;

        if (piece != null && piece.shadowImage != null)
        {
            shadowCanvasGroup = piece.shadowImage.GetComponent<CanvasGroup>();
            if (shadowCanvasGroup == null)
                shadowCanvasGroup = piece.shadowImage.gameObject.AddComponent<CanvasGroup>();
        }

        if (ghostImage != null)    ghostImage.SetActive(false);
        if (particleObject != null) particleObject.SetActive(false);

        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
        originalParent = transform.parent;
    }

    private void Start()
    {
        pieceImage = GetComponent<Image>();
        if (pieceImage != null)
        {
            pieceImage.raycastTarget = true;
            if (useAlphaHitTest)
                pieceImage.alphaHitTestMinimumThreshold = alphaHitThreshold;
        }
    }

    // =========================================================
    // POINTER
    // =========================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;
        parentChanged = false;
        dragStartPosition = eventData.position;
        dragInitiated = false;
        pieceTakenFromScrollView = IsInsideScrollView();
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
    }

    public void OnPointerUp(PointerEventData eventData) { }

    // =========================================================
    // BEGIN DRAG
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        float dragDistance = Vector2.Distance(eventData.position, dragStartPosition);
        if (dragDistance < dragStartThreshold) return;

        dragInitiated = true;
        originalParentBeforeDrag = transform.parent;
        originalSiblingIndex     = rectTransform.GetSiblingIndex();
        originalParent           = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        // Single-piece world offset
        worldOffset = rectTransform.position - ScreenToWorldPoint(eventData.position);

        // Group drag: seed the previous pointer local position
        ScreenToLocalInPieceParent(eventData.position, out prevPointerLocal);

        canvasGroup.blocksRaycasts = true;

        if (PuzzleManager.Instance != null)
        {
            if (piece.group != null && piece.group.pieces.Count > 1)
                PuzzleManager.Instance.BringGroupToFront(piece.group);
            else
                PuzzleManager.Instance.UpdateDragOrder(this);

            if (transform.parent == PuzzleManager.Instance.bottomParent ||
                originalParentBeforeDrag == PuzzleManager.Instance.bottomParent)
            {
                PuzzleManager.Instance.RemoveFromBottomWithoutFill(gameObject);
            }
        }
    }

    // =========================================================
    // DRAG
    // =========================================================

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag || !dragInitiated) return;

        bool insideBottom = IsPointerOverBottomPanel(eventData);

        // ── SCROLLING ──────────────────────────────────────────────────────
        if (!parentChanged && insideBottom)
        {
            if (!isScrolling)
            {
                isScrolling = true;
                isDraggingPiece = false;
                if (scrollRect != null)
                {
                    scrollRect.enabled = true;
                    scrollRect.OnInitializePotentialDrag(eventData);
                    scrollRect.OnBeginDrag(eventData);
                    scrollDragStarted = true;
                }
            }
            scrollRect?.OnDrag(eventData);
            return;
        }

        // ── EXIT BOTTOM PANEL ──────────────────────────────────────────────
        if (!parentChanged && !insideBottom)
        {
            if (scrollRect != null)
            {
                if (scrollDragStarted) scrollRect.OnEndDrag(eventData);
                scrollRect.velocity = Vector2.zero;
                scrollRect.StopMovement();
                scrollRect.enabled = false;
            }

            scrollDragStarted = false;
            isScrolling = false;

            Vector3 worldPos = rectTransform.position;
            transform.SetParent(PuzzleManager.Instance.pieceParent, true);
            rectTransform.position   = worldPos;
            rectTransform.localScale = Vector3.one;
            transform.SetAsLastSibling();

            parentChanged   = true;
            isDraggingPiece = true;

            // Recalculate offsets after reparent
            worldOffset = rectTransform.position - ScreenToWorldPoint(eventData.position);
            ScreenToLocalInPieceParent(eventData.position, out prevPointerLocal);
        }

        // ── NORMAL DRAG ────────────────────────────────────────────────────
        if (parentChanged && isDraggingPiece)
        {
            bool isGroup = piece.group != null && piece.group.pieces.Count > 1;

            if (!isGroup)
            {
                // Single piece: world-offset approach (no jump)
                rectTransform.position = ScreenToWorldPoint(eventData.position) + worldOffset;
            }
            else
            {
                // GROUP: pointer-delta approach.
                // We translate ALL pieces by (currentLocal - prevLocal) so it
                // doesn't matter which piece in the cluster the user grabbed.
                ScreenToLocalInPieceParent(eventData.position, out Vector2 currentLocal);
                Vector2 delta = currentLocal - prevPointerLocal;
                prevPointerLocal = currentLocal;
                piece.group.Move(delta);
            }

            // Ghost hint
            if (ghostImage != null)
            {
                Vector2 localPos = ConvertWorldToPieceParentAnchored(rectTransform.position);
                ghostImage.SetActive(
                    Vector2.Distance(localPos, correctPosition) <= snapThreshold * 1.5f);
            }
        }
    }

    // =========================================================
    // END DRAG
    // =========================================================

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragInitiated)
        {
            ReEnableScrollView();
            return;
        }

        if (scrollDragStarted && scrollRect != null)
            scrollRect.OnEndDrag(eventData);

        isScrolling = false;
        isDraggingPiece = false;
        scrollDragStarted = false;
        dragInitiated = false;

        canvasGroup.blocksRaycasts = true;
        ReEnableScrollView();

        if (isPlaced) return;
        ghostImage?.SetActive(false);

        if (parentChanged &&
            transform.parent == PuzzleManager.Instance.pieceParent)
        {
            Vector2 currentPos = ConvertWorldToPieceParentAnchored(rectTransform.position);
            float distance = Vector2.Distance(currentPos, correctPosition);

            if (distance <= snapThreshold)
            {
                particleObject?.SetActive(true);
                PlacePieceImmediate();
                return;
            }

            CheckForMerge();
        }
    }

    // =========================================================
    // PLACE PIECE
    // =========================================================

    private void PlacePieceImmediate()
    {
        isPlaced = true;
        canDrag  = false;

        rectTransform.SetParent(PuzzleManager.Instance.pieceParent, true);

        Vector3 targetWorldPos = PuzzleManager.Instance.pieceParent.TransformPoint(
            new Vector3(correctPosition.x, correctPosition.y, 0));

        rectTransform.position = new Vector3(
            targetWorldPos.x, targetWorldPos.y, rectTransform.position.z);

        rectTransform.localScale    = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;

        SetPieceSortingOrder(0);
        if (ghostImage != null) ghostImage.SetActive(true);

        rectTransform.DOScale(Vector3.one * 1.05f, 0.07f)
            .OnComplete(() => rectTransform.DOScale(Vector3.one, 0.07f));

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 1f;
        }

        if (particleObject != null)
        {
            particleObject.SetActive(true);
            StartCoroutine(DisableParticleAfterDelay(particleObject, 2f));
        }

        MergeWithNeighbours();
        PuzzleManager.Instance.OnPiecePlaced(this);
        Canvas.ForceUpdateCanvases();
        PuzzleManager.Instance.RefreshSortingOrdersFromList();
    }

    private IEnumerator DisableParticleAfterDelay(GameObject particle, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (particle != null) particle.SetActive(false);
    }

    // =========================================================
    // MERGE LOGIC
    // =========================================================

    private void CheckForMerge()
    {
        if (isMerging || piece == null) return;
        if (Time.time - lastMergeTime < mergeCooldown) return;

        bool mergedAny = true;
        while (mergedAny)
        {
            mergedAny = false;
            List<PuzzlePiece> snapshot = new List<PuzzlePiece>(piece.group.pieces);

            foreach (PuzzlePiece groupMember in snapshot)
            {
                PuzzlePiece[] neighbours =
                {
                    groupMember.left, groupMember.right,
                    groupMember.top,  groupMember.bottom
                };

                foreach (PuzzlePiece neighbour in neighbours)
                {
                    if (neighbour == null) continue;
                    if (neighbour.group == piece.group) continue;

                    DragPiece nDrag = neighbour.GetComponent<DragPiece>();
                    if (nDrag != null && nDrag.isPlaced) continue;

                    if (ArePiecesAligned(groupMember, neighbour))
                    {
                        lastMergeTime = Time.time;
                        SnapExactlyAndMerge(groupMember, neighbour);
                        mergedAny = true;
                        break;
                    }
                }
                if (mergedAny) break;
            }
        }
    }

    private bool ArePiecesAligned(PuzzlePiece a, PuzzlePiece b)
    {
        RectTransform aRect = a.GetComponent<RectTransform>();
        RectTransform bRect = b.GetComponent<RectTransform>();
        if (aRect == null || bRect == null) return false;

        Vector2 idealOffset  = GetIdealOffset(a, b);
        if (idealOffset == Vector2.zero) return false;

        Vector2 actualOffset = bRect.anchoredPosition - aRect.anchoredPosition;
        return Vector2.Distance(actualOffset, idealOffset) <= snapThreshold;
    }

    private Vector2 GetIdealOffset(PuzzlePiece from, PuzzlePiece to)
    {
        float cell = PuzzleManager.Instance.cellSize;
        int dCol   = to.col - from.col;
        int dRow   = to.row - from.row;

        if (Mathf.Abs(dCol) + Mathf.Abs(dRow) != 1) return Vector2.zero;

        return new Vector2(dCol * cell, -dRow * cell);
    }

    /// <summary>
    /// Snaps and merges two groups.
    /// The SMALLER group moves; the larger group is the positional anchor.
    /// groupToAnchor.Merge(groupToMove) is called — groupToAnchor SURVIVES.
    /// </summary>
    private void SnapExactlyAndMerge(PuzzlePiece moving, PuzzlePiece anchor)
    {
        PuzzleGroup movingGroup = moving.group;
        PuzzleGroup anchorGroup = anchor.group;
        if (movingGroup == anchorGroup || movingGroup == null || anchorGroup == null) return;

        RectTransform movingRect = moving.GetComponent<RectTransform>();
        RectTransform anchorRect = anchor.GetComponent<RectTransform>();
        if (movingRect == null || anchorRect == null) return;

        // ── Which group translates? (smaller group moves) ─────────────────
        bool moveMoving = movingGroup.pieces.Count <= anchorGroup.pieces.Count;

        PuzzleGroup groupToMove   = moveMoving ? movingGroup : anchorGroup;
        PuzzleGroup groupToAnchor = moveMoving ? anchorGroup : movingGroup;
        PuzzlePiece pivotMove     = moveMoving ? moving      : anchor;
        PuzzlePiece pivotAnchor   = moveMoving ? anchor      : moving;

        RectTransform pivotMoveRect   = pivotMove.GetComponent<RectTransform>();
        RectTransform pivotAnchorRect = pivotAnchor.GetComponent<RectTransform>();

        // ── Snap delta: where does pivotMove need to go? ──────────────────
        Vector2 idealOffset   = GetIdealOffset(pivotAnchor, pivotMove);
        Vector2 idealPosition = pivotAnchorRect.anchoredPosition + idealOffset;
        Vector2 snapDelta     = idealPosition - pivotMoveRect.anchoredPosition;

        // ── Translate groupToMove rigidly ─────────────────────────────────
        foreach (PuzzlePiece p in groupToMove.pieces)
        {
            RectTransform pRect = p.GetComponent<RectTransform>();
            if (pRect != null) pRect.anchoredPosition += snapDelta;
        }

        // ── Merge: groupToAnchor absorbs groupToMove ──────────────────────
        // PuzzleGroup.Merge() copies all pieces into groupToAnchor,
        // sets every piece.group = groupToAnchor, and clears groupToMove.
        groupToAnchor.Merge(groupToMove);

        // Ensure the dragged piece (this.piece) also points at surviving group
        piece.group = groupToAnchor;

        if (enableDebugLogs)
            Debug.Log($"[Merge] {moving.name} ↔ {anchor.name} → group size {groupToAnchor.pieces.Count}");

        // ── Snap feedback ─────────────────────────────────────────────────
        foreach (PuzzlePiece p in groupToAnchor.pieces)
        {
            RectTransform pRect = p.GetComponent<RectTransform>();
            if (pRect != null)
            {
                pRect.DOKill();
                pRect.DOScale(Vector3.one * 1.04f, 0.06f)
                     .OnComplete(() => pRect.DOScale(Vector3.one, 0.06f));
            }
        }

        PuzzleManager.Instance.RefreshSortingOrdersFromList();
        CheckGroupCompletion(groupToAnchor);
    }

    private void CheckGroupCompletion(PuzzleGroup group)
    {
        float tolerance = snapThreshold * 0.5f;

        foreach (PuzzlePiece p in group.pieces)
        {
            DragPiece pDrag   = p.GetComponent<DragPiece>();
            RectTransform pRect = p.GetComponent<RectTransform>();
            if (pDrag == null || pRect == null) return;
            if (pDrag.isPlaced) continue;
            if (Vector2.Distance(pRect.anchoredPosition, pDrag.correctPosition) > tolerance)
                return;
        }

        foreach (PuzzlePiece p in group.pieces)
        {
            DragPiece pDrag = p.GetComponent<DragPiece>();
            if (pDrag != null && !pDrag.isPlaced)
            {
                pDrag.isPlaced = true;
                pDrag.canDrag  = false;
                if (pDrag.canvasGroup != null)
                {
                    pDrag.canvasGroup.blocksRaycasts = false;
                    pDrag.canvasGroup.alpha = 1f;
                }
                pDrag.SetPieceSortingOrder(0);
                PuzzleManager.Instance.OnPiecePlaced(pDrag);
            }
        }
    }

    private void MergeWithNeighbours()
    {
        if (piece == null) return;

        PuzzlePiece[] neighbours =
        {
            piece.left, piece.right, piece.top, piece.bottom
        };

        foreach (PuzzlePiece neighbour in neighbours)
        {
            if (neighbour == null) continue;
            if (neighbour.group == piece.group) continue;

            DragPiece nDrag = neighbour.GetComponent<DragPiece>();
            if (nDrag == null || !nDrag.isPlaced) continue;

            if (ArePiecesAligned(piece, neighbour))
            {
                neighbour.group.AddPiece(piece);
                piece.group = neighbour.group;

                if (enableDebugLogs)
                    Debug.Log($"[PlaceMerge] {piece.name} joined {neighbour.name}'s group");
            }
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    /// <summary>Converts a screen point to local space inside pieceParent.</summary>
    private bool ScreenToLocalInPieceParent(Vector2 screenPos, out Vector2 localPoint)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PuzzleManager.Instance.pieceParent as RectTransform,
            screenPos,
            renderCamera,
            out localPoint);
    }

    private Vector2 ConvertWorldToPieceParentAnchored(Vector3 worldPos)
    {
        if (PuzzleManager.Instance == null ||
            PuzzleManager.Instance.pieceParent == null)
            return Vector2.zero;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PuzzleManager.Instance.pieceParent as RectTransform,
            RectTransformUtility.WorldToScreenPoint(renderCamera, worldPos),
            renderCamera,
            out localPoint);
        return localPoint;
    }

    private Vector3 ScreenToWorldPoint(Vector2 screenPosition)
    {
        Vector3 worldPos;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRectTransform,
            screenPosition,
            renderCamera,
            out worldPos);
        return worldPos;
    }

    private bool IsPointerOverBottomPanel(PointerEventData eventData)
    {
        if (bottomPanel == null) return false;

        Camera cam = (mainCanvas != null &&
                      mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null : mainCanvas?.worldCamera;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bottomPanel, eventData.position, cam, out localPoint))
            return bottomPanel.rect.Contains(localPoint);

        return false;
    }

    private bool IsInsideScrollView()
    {
        return GetComponentInParent<ScrollRect>() != null;
    }

    private void ReEnableScrollView()
    {
        if (scrollRect != null)
        {
            scrollRect.enabled  = true;
            scrollRect.velocity = Vector2.zero;
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (pieceTakenFromScrollView && scrollRect != null)
            scrollRect.OnInitializePotentialDrag(eventData);
    }

    private Canvas FindMainCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in all)
            if (c.transform.parent == null) return c;
        return GetComponentInParent<Canvas>();
    }

    private void SetupRaycaster()
    {
        Canvas pc = GetComponent<Canvas>();
        if (pc == null)
        {
            pc = gameObject.AddComponent<Canvas>();
            pc.overrideSorting = true;
        }
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    public void ResetPiece()
    {
        isPlaced = false;
        canDrag  = true;
        parentChanged = transform.parent == PuzzleManager.Instance.pieceParent;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }
        rectTransform.localScale = Vector3.one;
    }

    public void CheckCompletion()
    {
        int count = 0;
        foreach (var p in PuzzleManager.Instance.allPieces)
        {
            DragPiece d = p.GetComponent<DragPiece>();
            if (d != null && d.isPlaced) count++;
        }
        if (count >= PuzzleManager.Instance.TotalPieces)
            PuzzleManager.Instance.ShowCompletionCanvas();
    }

    public void SetPieceSortingOrder(int baseOrder)
    {
        Canvas mc = GetComponent<Canvas>();
        if (mc == null)
        {
            mc = gameObject.AddComponent<Canvas>();
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }
        mc.overrideSorting = true;
        mc.sortingOrder    = baseOrder;

        if (piece != null && piece.shadowImage != null)
        {
            Canvas sc = piece.shadowImage.GetComponent<Canvas>();
            if (sc == null)
                sc = piece.shadowImage.gameObject.AddComponent<Canvas>();
            sc.overrideSorting = true;
            sc.sortingOrder    = baseOrder + 1;
        }
    }
}
