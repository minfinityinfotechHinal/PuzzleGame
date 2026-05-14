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

    public bool parentChanged = false;

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

    // ── LIFT THRESHOLD ────────────────────────────────────────────────────
    // How many screen pixels upward the user must drag before the piece
    // is "lifted" out of the bottom scroll panel.  Below this distance any
    // gesture that is more horizontal than vertical stays as a scroll.
    // Tune in the Inspector — 40 px works well on most mobile resolutions.
    [SerializeField] private float liftThreshold = 200f;

    // True once the user has dragged far enough upward to commit to a lift.
    private bool liftCommitted = false;
    // ─────────────────────────────────────────────────────────────────────

    private CanvasGroup shadowCanvasGroup;

    private Transform originalParentBeforeDrag;
    private int originalSiblingIndex;

    // ✅ CLIPPING FIX: tracks whether piece lives in the bottom scroll panel.
    // While true → overrideSorting stays FALSE so RectMask2D can clip the piece.
    // While false → overrideSorting is TRUE so sorting orders work on the board.
    private bool isInBottomPanel = false;

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

        if (ghostImage != null) ghostImage.SetActive(false);
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
        if (piece != null &&
            piece.group != null &&
            piece.group.isPlacedGroup)
        {
            return;
        }
        if (isPlaced || !canDrag) return;
        parentChanged = false;
        dragStartPosition = eventData.position;
        dragInitiated = false;
        liftCommitted = false;          // reset every new touch
        pieceTakenFromScrollView = IsInsideScrollView();
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
    }

    public void OnPointerUp(PointerEventData eventData) { }

    // =========================================================
    // BEGIN DRAG
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag)
            return;

        if (piece != null &&
            piece.group != null &&
            piece.group.isPlacedGroup)
        {
            return;
        }

        float dragDistance = Vector2.Distance(eventData.position, dragStartPosition);
        if (dragDistance < dragStartThreshold) return;

        // ── BOTTOM-PANEL PIECE: defer the actual lift to OnDrag ─────────────
        // We only store state here; we do NOT call RemoveFromBottomWithoutFill
        // yet.  OnDrag will commit the lift once verticalDelta >= liftThreshold.
        bool inBottomPanel = (PuzzleManager.Instance != null &&
            transform.parent == PuzzleManager.Instance.bottomParent);

        if (inBottomPanel && !liftCommitted)
        {
            dragInitiated = true;
            originalParentBeforeDrag = transform.parent;
            originalSiblingIndex = rectTransform.GetSiblingIndex();
            originalParent = transform.parent;
            originalAnchoredPosition = rectTransform.anchoredPosition;
            ScreenToLocalInPieceParent(eventData.position, out prevPointerLocal);
            worldOffset = rectTransform.position - ScreenToWorldPoint(eventData.position);
            // Do NOT lift the piece yet — wait for liftThreshold in OnDrag.
            return;
        }
        // ────────────────────────────────────────────────────────────────────

        // Piece is NOT in the bottom panel (already on the board): normal flow.
        dragInitiated = true;
        originalParentBeforeDrag = transform.parent;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        worldOffset = rectTransform.position - ScreenToWorldPoint(eventData.position);
        ScreenToLocalInPieceParent(eventData.position, out prevPointerLocal);

        // ✅ CLIPPING FIX: board pieces always need overrideSorting ON
        SetCanvasOverride(true);

        canvasGroup.blocksRaycasts = true;

        if (PuzzleManager.Instance != null)
        {
            if (piece.group != null && piece.group.pieces.Count > 1)
                PuzzleManager.Instance.BringGroupToFront(piece.group);
            else
                PuzzleManager.Instance.UpdateDragOrder(this);
        }
    }

    // =========================================================
    // DRAG
    // =========================================================

    public void OnDrag(PointerEventData eventData)
    {
        if (piece != null &&
            piece.group != null &&
            piece.group.isPlacedGroup)
        {
            return;
        }
        if (isPlaced || !canDrag || !dragInitiated) return;

        // ── LIFT COMMIT ────────────────────────────────────────────────────
        // For pieces still sitting in the bottom panel we wait until the user
        // has dragged upward by at least liftThreshold pixels before we pull
        // the piece out.  Until then:
        //   • a horizontal-dominant gesture is forwarded to the ScrollRect
        //   • a small upward nudge is ignored (finger still ambiguous)
        // Once liftThreshold is reached we commit, stop the scroll, reparent
        // the piece to pieceParent, and let the normal drag code take over.
        if (!liftCommitted && !parentChanged &&
            PuzzleManager.Instance != null &&
            originalParentBeforeDrag == PuzzleManager.Instance.bottomParent)
        {
            float verticalDelta   = eventData.position.y - dragStartPosition.y;
            float horizontalDelta = Mathf.Abs(eventData.position.x - dragStartPosition.x);

            if (verticalDelta >= liftThreshold)
            {
                // ── COMMIT LIFT ──────────────────────────────────────────
                liftCommitted = true;

                // Stop any scroll that was in progress
                if (scrollDragStarted && scrollRect != null)
                {
                    scrollRect.OnEndDrag(eventData);
                    scrollRect.velocity = Vector2.zero;
                    scrollRect.StopMovement();
                    scrollRect.enabled = false;
                    scrollDragStarted = false;
                }
                isScrolling = false;

                // Remove from bottom panel
                PuzzleManager.Instance.RemoveFromBottomWithoutFill(gameObject);

                // Reparent to the main piece canvas, preserving world position
                Vector3 worldPos = rectTransform.position;
                transform.SetParent(PuzzleManager.Instance.pieceParent, true);
                rectTransform.position = worldPos;
                rectTransform.localScale = Vector3.one;
                transform.SetAsLastSibling();

                parentChanged   = true;
                isDraggingPiece = true;

                // ✅ FIX: Enable canvas override now that piece left the ScrollRect
                SetCanvasOverride(true);

                // ✅ Restore alpha — LateUpdate clipper only runs for bottomParent children
                if (canvasGroup != null) canvasGroup.alpha = 1f;
                if (piece != null && piece.shadowImage != null && shadowCanvasGroup != null)
                    shadowCanvasGroup.alpha = 1f;

                // Re-seed offsets now that we have a new parent
                worldOffset = rectTransform.position - ScreenToWorldPoint(eventData.position);
                ScreenToLocalInPieceParent(eventData.position, out prevPointerLocal);

                canvasGroup.blocksRaycasts = true;

                if (piece.group != null && piece.group.pieces.Count > 1)
                    PuzzleManager.Instance.BringGroupToFront(piece.group);
                else
                    PuzzleManager.Instance.UpdateDragOrder(this);
                // ── fall through to normal drag below ──
            }
            else if (horizontalDelta > verticalDelta)
            {
                // Horizontal-dominant gesture → scroll the panel
                if (!isScrolling)
                {
                    isScrolling    = true;
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
            else
            {
                // Small upward nudge, not yet committed — forward to scroll if
                // a scroll was already started, otherwise just wait.
                if (isScrolling)
                    scrollRect?.OnDrag(eventData);
                return;
            }
        }
        // ── END LIFT COMMIT ────────────────────────────────────────────────

        bool insideBottom = IsPointerOverBottomPanel(eventData);

        // ── SCROLLING (piece already on board, pointer back over bottom) ───
        if (!parentChanged && insideBottom)
        {
            if (!isScrolling)
            {
                isScrolling    = true;
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

        // ── EXIT BOTTOM PANEL (non-lift-committed path) ────────────────────
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
            rectTransform.position = worldPos;
            rectTransform.localScale = Vector3.one;
            transform.SetAsLastSibling();

            parentChanged   = true;
            isDraggingPiece = true;

            // ✅ FIX: Enable canvas override now that piece left the ScrollRect
            SetCanvasOverride(true);

            // ✅ Restore alpha — LateUpdate clipper only runs for bottomParent children
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (piece != null && piece.shadowImage != null && shadowCanvasGroup != null)
                shadowCanvasGroup.alpha = 1f;

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
                
                // ✅ CLAMP to screen bounds
                ClampToScreen(rectTransform);
            }
            else
            {
                // GROUP: pointer-delta approach.
                ScreenToLocalInPieceParent(eventData.position, out Vector2 currentLocal);
                Vector2 delta = currentLocal - prevPointerLocal;
                
                // ✅ CLAMP delta so group doesn't go off screen
                delta = ClampGroupDelta(delta);
                
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

    private void ClampToScreen(RectTransform rect)
{
    Vector2 screenSize = new Vector2(Screen.width, Screen.height);
    
    // Convert world position to screen position
    Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(renderCamera, rect.position);
    
    // Get piece size in screen pixels
    Vector2 sizeDelta = rect.sizeDelta;
    float halfW = (sizeDelta.x * rect.lossyScale.x) * 0.5f;
    float halfH = (sizeDelta.y * rect.lossyScale.y) * 0.5f;
    
    // Clamp within screen with padding
    screenPos.x = Mathf.Clamp(screenPos.x, halfW + screenEdgePadding, screenSize.x - halfW - screenEdgePadding);
    screenPos.y = Mathf.Clamp(screenPos.y, halfH + bottomPadding,      screenSize.y - halfH - topPadding);
    
    // Convert back to world position
    Vector3 clampedWorld;
    RectTransformUtility.ScreenPointToWorldPointInRectangle(
        canvasRectTransform, screenPos, renderCamera, out clampedWorld);
    rect.position = clampedWorld;
}

private Vector2 ClampGroupDelta(Vector2 proposedDelta)
{
    if (piece.group == null) return proposedDelta;

    Vector2 screenSize = new Vector2(Screen.width, Screen.height);

    foreach (PuzzlePiece p in piece.group.pieces)
    {
        RectTransform r = p.GetComponent<RectTransform>();
        if (r == null) continue;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(renderCamera, r.position);
        float halfW = (r.sizeDelta.x * r.lossyScale.x) * 0.5f;
        float halfH = (r.sizeDelta.y * r.lossyScale.y) * 0.5f;

        // Convert proposed delta (local space) to screen space delta
        // by checking if the resulting position would go out of bounds
        float minX = halfW + screenEdgePadding;
        float maxX = screenSize.x - halfW - screenEdgePadding;
        float minY = halfH + bottomPadding;
        float maxY = screenSize.y - halfH - topPadding;

        // If already out of bounds in a direction, block delta in that direction
        if (screenPos.x < minX) proposedDelta.x = Mathf.Max(proposedDelta.x, 0);
        if (screenPos.x > maxX) proposedDelta.x = Mathf.Min(proposedDelta.x, 0);
        if (screenPos.y < minY) proposedDelta.y = Mathf.Max(proposedDelta.y, 0);
        if (screenPos.y > maxY) proposedDelta.y = Mathf.Min(proposedDelta.y, 0);
    }

    return proposedDelta;
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

        isScrolling     = false;
        isDraggingPiece = false;
        scrollDragStarted = false;
        dragInitiated   = false;
        liftCommitted   = false;    // reset for next gesture

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
        if (piece == null || piece.group == null) return;

        PuzzleGroup group = piece.group;
        List<DragPiece> newlyPlaced = new List<DragPiece>();

        foreach (PuzzlePiece p in group.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();
            RectTransform rect = p.GetComponent<RectTransform>();
            if (drag == null || rect == null) continue;

            if (drag.isPlaced)
            {
                drag.canDrag = false;
                continue;
            }

            drag.isPlaced = true;
            drag.canDrag = false;
            drag.parentChanged = true;

            rect.SetParent(PuzzleManager.Instance.pieceParent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = drag.correctPosition;

            // ✅ FIX: Piece is now on the board — enable overrideSorting so
            // sorting orders and stencil rendering work correctly.
            drag.SetCanvasOverride(true);
            drag.SetPieceSortingOrder(1);

            if (drag.canvasGroup != null)
            {
                drag.canvasGroup.blocksRaycasts = false;
                drag.canvasGroup.alpha = 1f;
            }

            if (drag.ghostImage != null)
                drag.ghostImage.SetActive(true);

            rect.DOKill();
            rect.DOScale(Vector3.one * 1.05f, 0.07f)
                .OnComplete(() => rect.DOScale(Vector3.one, 0.07f));

            if (drag.particleObject != null)
            {
                drag.particleObject.SetActive(true);
                StartCoroutine(DisableParticleAfterDelay(drag.particleObject, 2f));
            }

            newlyPlaced.Add(drag);
        }

        // First: register all placements (removes from slots, no rearrange yet)
        foreach (DragPiece drag in newlyPlaced)
            PuzzleManager.Instance.OnPiecePlaced(drag);

        // Then: rearrange bottom ONCE after all pieces removed from slots
        PuzzleManager.Instance.OnGroupPlacementFinished();

        Canvas.ForceUpdateCanvases();
        CheckCompletion();
    }

    private IEnumerator DisableParticleAfterDelay(GameObject particle, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (particle != null)
            particle.SetActive(false);
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
                    if (nDrag == null)
                        continue;

                    // Skip only if BOTH groups are already placed
                    if (piece.group.isPlacedGroup &&
                        neighbour.group.isPlacedGroup)
                    {
                        continue;
                    }

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

    public void SnapGroupToCorrectPosition()
    {
        Vector2 offset = correctPosition - rectTransform.anchoredPosition;

        foreach (PuzzlePiece p in piece.group.pieces)
        {
            DragPiece dp = p.GetComponent<DragPiece>();
            RectTransform rt = p.GetComponent<RectTransform>();

            if (dp != null && rt != null)
            {
                rt.anchoredPosition = dp.correctPosition;
                dp.isPlaced = true;
                dp.canDrag = false;
                rt.SetParent(PuzzleManager.Instance.pieceParent);
                // ✅ FIX: Enable overrideSorting when placed on board
                dp.SetCanvasOverride(true);
            }
        }

        piece.group.isPlacedGroup = true;
        CheckCompletion();
    }

    private bool ArePiecesAligned(PuzzlePiece a, PuzzlePiece b)
    {
        RectTransform aRect = a.GetComponent<RectTransform>();
        RectTransform bRect = b.GetComponent<RectTransform>();
        if (aRect == null || bRect == null) return false;

        Vector2 idealOffset = GetIdealOffset(a, b);
        if (idealOffset == Vector2.zero) return false;

        Vector2 aLocal = ConvertToPieceParentLocal(aRect);
        Vector2 bLocal = ConvertToPieceParentLocal(bRect);

        Vector2 actualOffset = bLocal - aLocal;
        return Vector2.Distance(actualOffset, idealOffset) <= snapThreshold;
    }

    private Vector2 ConvertToPieceParentLocal(RectTransform rect)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PuzzleManager.Instance.pieceParent as RectTransform,
            RectTransformUtility.WorldToScreenPoint(renderCamera, rect.position),
            renderCamera,
            out localPoint);
        return localPoint;
    }

    private Vector2 GetIdealOffset(PuzzlePiece from, PuzzlePiece to)
    {
        float cell = PuzzleManager.Instance.cellSize;
        int dCol = to.col - from.col;
        int dRow = to.row - from.row;

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

        if (movingGroup == null || anchorGroup == null || movingGroup == anchorGroup)
            return;

        bool movingPlaced = movingGroup.isPlacedGroup;
        bool anchorPlaced = anchorGroup.isPlacedGroup;

        if (movingPlaced && !anchorPlaced)
        {
            PuzzlePiece tempPiece = moving;
            moving = anchor;
            anchor = tempPiece;

            PuzzleGroup tempGroup = movingGroup;
            movingGroup = anchorGroup;
            anchorGroup = tempGroup;

            movingPlaced = movingGroup.isPlacedGroup;
            anchorPlaced = anchorGroup.isPlacedGroup;
        }

        DragPiece movingDrag = moving.GetComponent<DragPiece>();
        DragPiece anchorDrag = anchor.GetComponent<DragPiece>();
        RectTransform movingRect = moving.GetComponent<RectTransform>();
        RectTransform anchorRect = anchor.GetComponent<RectTransform>();

        if (movingDrag == null || anchorDrag == null ||
            movingRect == null || anchorRect == null)
            return;

        Vector2 movingLocal = ConvertToPieceParentLocal(movingRect);
        Vector2 anchorLocal = ConvertToPieceParentLocal(anchorRect);
        Vector2 correctOffset = movingDrag.correctPosition - anchorDrag.correctPosition;
        Vector2 targetPosition = anchorLocal + correctOffset;
        Vector2 delta = targetPosition - movingLocal;

        List<PuzzlePiece> movingSnapshot = new List<PuzzlePiece>(movingGroup.pieces);
        foreach (PuzzlePiece p in movingSnapshot)
        {
            DragPiece pDrag = p.GetComponent<DragPiece>();
            RectTransform r = p.GetComponent<RectTransform>();
            if (r == null) continue;

            if (pDrag != null && pDrag.isPlaced) continue;

            Vector2 posInPieceParent = ConvertToPieceParentLocal(r);

            if (r.parent != PuzzleManager.Instance.pieceParent)
                r.SetParent(PuzzleManager.Instance.pieceParent, false);

            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.localScale = Vector3.one;

            r.anchoredPosition = posInPieceParent + delta;
        }

        anchorGroup.Merge(movingGroup);
        piece.group = anchorGroup;

        if (anchorPlaced || movingPlaced)
        {
            anchorGroup.isPlacedGroup = true;
            List<DragPiece> newlyPlaced = new List<DragPiece>();

            foreach (PuzzlePiece p in anchorGroup.pieces)
            {
                DragPiece drag = p.GetComponent<DragPiece>();
                RectTransform rect = p.GetComponent<RectTransform>();
                if (drag == null || rect == null) continue;

                if (drag.isPlaced)
                {
                    drag.canDrag = false;
                    continue;
                }

                drag.isPlaced = true;
                drag.canDrag = false;
                drag.parentChanged = true;

                rect.SetParent(PuzzleManager.Instance.pieceParent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.anchoredPosition = drag.correctPosition;

                // ✅ FIX: Enable overrideSorting now that piece is on the board
                drag.SetCanvasOverride(true);

                if (drag.canvasGroup != null)
                    drag.canvasGroup.blocksRaycasts = false;
                if (drag.ghostImage != null)
                    drag.ghostImage.SetActive(true);

                newlyPlaced.Add(drag);
            }

            foreach (DragPiece drag in newlyPlaced)
                PuzzleManager.Instance.OnPiecePlaced(drag);

            PuzzleManager.Instance.OnGroupPlacementFinished();

            Canvas.ForceUpdateCanvases();
            CheckCompletion();
            return;
        }

        PuzzleManager.Instance.BringGroupToFront(anchorGroup);
        CheckGroupSnapAfterMerge(anchorGroup);
    }

    private void CheckGroupSnapAfterMerge(PuzzleGroup mergedGroup)
    {
        if (mergedGroup == null) return;

        bool allCorrect = true;

        foreach (PuzzlePiece p in mergedGroup.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();
            RectTransform rect = p.GetComponent<RectTransform>();
            if (drag == null || rect == null) continue;

            Vector2 pieceLocal = ConvertToPieceParentLocal(rect);
            float dist = Vector2.Distance(pieceLocal, drag.correctPosition);

            if (dist > snapThreshold)
            {
                allCorrect = false;
                break;
            }
        }

        if (allCorrect)
        {
            mergedGroup.isPlacedGroup = true;
            piece = mergedGroup.anchorPiece;
            PlacePieceImmediate();
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
            scrollRect.enabled = true;
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
            // ✅ CLIPPING FIX: Start with overrideSorting OFF.
            // Pieces begin life in pieceParent (before scatter to bottom),
            // and SetCanvasOverride / SetPieceSortingOrder will enable it
            // correctly once they are on the board.
            pc.overrideSorting = false;
        }
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    public void ResetPiece()
    {
        isPlaced = false;
        canDrag = true;
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

    /// <summary>
    /// Called by PuzzleManager whenever it sends a piece TO the bottom panel.
    /// Sets isInBottomPanel = true and turns overrideSorting OFF on both
    /// the piece Canvas and the shadow Canvas so RectMask2D can clip them.
    /// </summary>
  public void SetCanvasOverride(bool enable)
{
    isInBottomPanel = !enable;

    Canvas pc = GetComponent<Canvas>();
    if (pc != null)
        pc.overrideSorting = enable;

    // ✅ Shadow always inherits — never gets its own overrideSorting
    if (piece != null && piece.shadowImage != null)
    {
        Canvas sc = piece.shadowImage.GetComponent<Canvas>();
        if (sc != null)
            sc.overrideSorting = false; // always inherit, never override
    }
}

    /// <summary>
    /// Sets the Canvas sortingOrder for this piece and its shadow.
    /// SKIPPED while the piece is in the bottom panel (isInBottomPanel == true)
    /// so that RectMask2D clipping is never broken by a sorting-order refresh.
    /// </summary>
    public void SetPieceSortingOrder(int baseOrder)
{
    if (isInBottomPanel) return;

    Canvas mc = GetComponent<Canvas>();
    if (mc == null)
    {
        mc = gameObject.AddComponent<Canvas>();
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }
    mc.overrideSorting = true;
    mc.sortingOrder = baseOrder;

    // ✅ SHADOW FIX: Remove the shadow's own Canvas entirely.
    // Let it inherit from the piece's Canvas so stencil/mask is preserved.
    if (piece != null && piece.shadowImage != null)
    {
        Canvas sc = piece.shadowImage.GetComponent<Canvas>();
        if (sc != null)
        {
            sc.overrideSorting = false; // ← inherit from parent piece Canvas
            // Do NOT set sortingOrder here — let parent handle it
        }
    }
}
}
