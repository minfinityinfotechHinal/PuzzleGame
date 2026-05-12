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
    public Vector2 correctPosition; // Anchored position relative to PIECEPARENT

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
    private Vector3 worldOffset;
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
    // AWAKE
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
                ? null
                : mainCanvas.worldCamera;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

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
        if (isPlaced || !canDrag) return;
        parentChanged = false;
        dragStartPosition = eventData.position;
        dragInitiated = false;
        pieceTakenFromScrollView = IsInsideScrollView();
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
    }

    public void OnPointerUp(PointerEventData eventData) { }

    // =========================================================
    // BEGIN DRAG - Store WORLD offset
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        float dragDistance = Vector2.Distance(eventData.position, dragStartPosition);
        if (dragDistance < dragStartThreshold) return;

        dragInitiated = true;

        originalParentBeforeDrag = transform.parent;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        // Store offset in WORLD SPACE (independent of parent)
        Vector3 pieceWorldPos = rectTransform.position;
        Vector3 pointerWorldPos = ScreenToWorldPoint(eventData.position);
        worldOffset = pieceWorldPos - pointerWorldPos;

        canvasGroup.blocksRaycasts = true;

        if (PuzzleManager.Instance != null)
        {
            if (piece.group != null && piece.group.pieces.Count > 1)
                PuzzleManager.Instance.BringGroupToFront(piece.group);
            else
                PuzzleManager.Instance.UpdateDragOrder(this);
            
            // Only call RemoveFromBottomWithoutFill if piece is in bottom parent
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
    if (isPlaced || !canDrag || !dragInitiated)
        return;

    bool insideBottom = IsPointerOverBottomPanel(eventData);

    // =====================================================
    // MODE 1 = SCROLLING INSIDE BOTTOM PANEL
    // =====================================================

    if (!parentChanged && insideBottom)
    {
        // Start scrolling only once
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

        // Continue scroll
        if (scrollRect != null)
        {
            scrollRect.OnDrag(eventData);
        }

        return;
    }

    // =====================================================
    // EXIT BOTTOM PANEL -> SWITCH TO PIECE DRAG
    // =====================================================

    if (!parentChanged && !insideBottom)
    {
        // STOP SCROLL COMPLETELY
        if (scrollRect != null)
        {
            if (scrollDragStarted)
            {
                scrollRect.OnEndDrag(eventData);
            }

            scrollRect.velocity = Vector2.zero;
            scrollRect.StopMovement();
            scrollRect.enabled = false;
        }

        scrollDragStarted = false;
        isScrolling = false;

        // MOVE TO PIECE PARENT
        Vector3 worldPos = rectTransform.position;

        transform.SetParent(PuzzleManager.Instance.pieceParent, true);

        rectTransform.position = worldPos;
        rectTransform.localScale = Vector3.one;

        transform.SetAsLastSibling();

        parentChanged = true;
        isDraggingPiece = true;

        // RECALCULATE OFFSET
        Vector3 pointerWorld = ScreenToWorldPoint(eventData.position);
        worldOffset = rectTransform.position - pointerWorld;
    }

    // =====================================================
    // MODE 2 = NORMAL PIECE DRAG
    // =====================================================

    if (parentChanged && isDraggingPiece)
    {
        Vector3 pointerWorld = ScreenToWorldPoint(eventData.position);

        Vector3 targetWorldPos = pointerWorld + worldOffset;

        Vector3 delta = targetWorldPos - rectTransform.position;

        rectTransform.position = targetWorldPos;

        // GROUP MOVE
        if (piece.group != null && piece.group.pieces.Count > 1)
        {
            piece.group.Move(new Vector2(delta.x, delta.y));
        }

        // GHOST
        if (ghostImage != null)
        {
            Vector2 localPos =
                ConvertWorldToPieceParentAnchored(rectTransform.position);

            float dist =
                Vector2.Distance(localPos, correctPosition);

            ghostImage.SetActive(dist <= snapThreshold * 1.5f);
        }
    }
}
    // =========================================================
    // END DRAG - Piece stays in place if not snapped
    // =========================================================

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragInitiated) { ReEnableScrollView(); return; }

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

        bool isOverBottom = IsPointerOverBottomPanel(eventData);

        // ✅ Check snap ONLY if piece was moved to pieceParent
        if (parentChanged && transform.parent == PuzzleManager.Instance.pieceParent)
        {
            // Convert world position to pieceParent anchored space
            Vector2 currentPos = ConvertWorldToPieceParentAnchored(rectTransform.position);
            float distance = Vector2.Distance(currentPos, correctPosition);

            if (enableDebugLogs)
                Debug.Log($"🎯 {name} | AnchoredPos: {currentPos} | Target: {correctPosition} | Distance: {distance:F1} | Threshold: {snapThreshold}");

            if (distance <= snapThreshold)
            {
                // ✅ CORRECT POSITION - Snap and place
                if (enableDebugLogs) Debug.Log($"✅ SNAP! {name} placed!");
                if (particleObject != null) particleObject.SetActive(true);
                PlacePieceImmediate();
                return;
            }

            // GROUP SNAP CHECK
            if (piece.group != null && piece.group.pieces.Count > 1)
            {
                foreach (var p in piece.group.pieces)
                {
                    DragPiece drag = p.GetComponent<DragPiece>();
                    if (drag != null && !drag.isPlaced)
                    {
                        Vector2 groupLocalPos = ConvertWorldToPieceParentAnchored(drag.rectTransform.position);
                        float pDist = Vector2.Distance(groupLocalPos, drag.correctPosition);
                        if (pDist <= snapThreshold)
                        {
                            if (enableDebugLogs) Debug.Log($"✅ GROUP SNAP! {drag.name}");
                            piece = p;
                            if (particleObject != null) particleObject.SetActive(true);
                            PlacePieceImmediate();
                            return;
                        }
                    }
                }
            }
            
            // ✅ WRONG POSITION - Piece STAYS in puzzle area at current position
            // Don't return to bottom panel, don't change position
            // Just re-enable dragging and keep the piece where it is
            if (enableDebugLogs) Debug.Log($"📍 {name} dropped in wrong position - staying in puzzle area");
            
            // Reset parentChanged so piece can be dragged again from current position
            parentChanged = true; // Keep true so we know it's in pieceParent
            canvasGroup.blocksRaycasts = true;
            
            // Check for merge with nearby pieces
            CheckForMerge();
            return;
        }

        // If piece was NOT moved to pieceParent (still in bottom panel during drag)
        // Just return it to its original position in bottom panel
        if (isOverBottom || originalParentBeforeDrag != null)
        {
            if (enableDebugLogs) Debug.Log($"📍 {name} staying in bottom panel");
            
            // If parent was changed but not to pieceParent, return to original
            if (transform.parent != originalParentBeforeDrag && originalParentBeforeDrag != null)
            {
                Vector3 worldPos = rectTransform.position;
                transform.SetParent(originalParentBeforeDrag, true);
                rectTransform.position = worldPos;
                rectTransform.localScale = Vector3.one;
                parentChanged = false;
                
                if (originalSiblingIndex >= 0 && originalSiblingIndex < transform.parent.childCount)
                    transform.SetSiblingIndex(originalSiblingIndex);
            }
            
            ReEnableScrollView();
            return;
        }

        // Check for merge
        CheckForMerge();
    }

    // =========================================================
    // HELPER: Convert world position to pieceParent anchored position
    // =========================================================
    private Vector2 ConvertWorldToPieceParentAnchored(Vector3 worldPos)
    {
        if (PuzzleManager.Instance == null || PuzzleManager.Instance.pieceParent == null)
            return Vector2.zero;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PuzzleManager.Instance.pieceParent as RectTransform,
            RectTransformUtility.WorldToScreenPoint(renderCamera, worldPos),
            renderCamera,
            out localPoint
        );
        return localPoint;
    }

    // =========================================================
    // PLACE PIECE
    // =========================================================

   // =========================================================
// PLACE PIECE - Sets sorting order to 0 (piece) and 1 (shadow)
// =========================================================

private void PlacePieceImmediate()
{
    PuzzleGroup currentGroup = piece.group;

    if (currentGroup != null && currentGroup.pieces.Count > 1)
    {
        // === GROUP PLACEMENT ===
        Debug.Log($"[GROUP PLACE] Placing group with {currentGroup.pieces.Count} pieces");
        
        foreach (var p in currentGroup.pieces)
        {
            DragPiece d = p.GetComponent<DragPiece>();
            if (d != null && !d.isPlaced)
            {
                d.isPlaced = true;
                d.canDrag = false;
                
                // Store current world position for debug
                Vector3 worldBeforeParent = d.rectTransform.position;
                
                // Change parent to pieceParent (worldPositionStays = true preserves world position temporarily)
                d.rectTransform.SetParent(PuzzleManager.Instance.pieceParent, true);
                
                // Calculate target world position from correctPosition (which is in pieceParent local space)
                Vector3 targetWorldPos = PuzzleManager.Instance.pieceParent.TransformPoint(
                    new Vector3(d.correctPosition.x, d.correctPosition.y, 0)
                );
                
                // Set to target world position
                d.rectTransform.position = new Vector3(targetWorldPos.x, targetWorldPos.y, d.rectTransform.position.z);
                d.rectTransform.localScale = Vector3.one;
                d.rectTransform.localRotation = Quaternion.identity;
                
                // ✅ SET SORTING ORDER: Piece = 0, Shadow = 1
                d.SetPieceSortingOrder(0);
                
                Debug.Log($"[GROUP PLACE] {d.name} | " +
                          $"WorldBefore: {worldBeforeParent} | " +
                          $"TargetWorld: {targetWorldPos} | " +
                          $"FinalWorld: {d.rectTransform.position} | " +
                          $"FinalAnchored: {d.rectTransform.anchoredPosition} | " +
                          $"CorrectPos: {d.correctPosition} | " +
                          $"SortingOrder: Piece=0, Shadow=1");
                
                if (d.ghostImage != null) d.ghostImage.SetActive(true);
                d.rectTransform.DOScale(Vector3.one * 1.05f, 0.07f)
                    .OnComplete(() => d.rectTransform.DOScale(Vector3.one, 0.07f));
                    
                if (d.canvasGroup != null) 
                { 
                    d.canvasGroup.blocksRaycasts = false; 
                    d.canvasGroup.alpha = 1f; 
                }
                
                if (d.particleObject != null)
                {
                    d.particleObject.SetActive(true);
                    StartCoroutine(DisableParticleAfterDelay(d.particleObject, 2f));
                }
            }
        }
        
        foreach (var p in currentGroup.pieces)
            p.GetComponent<DragPiece>().MergeWithNeighbours();
            
        PuzzleManager.Instance.OnGroupPlaced(currentGroup.pieces);
    }
    else
    {
        // === SINGLE PIECE PLACEMENT ===
        isPlaced = true;
        canDrag = false;
        
        // Store current world position for debug
        Vector3 worldBeforeParent = rectTransform.position;
        
        // Change parent to pieceParent (worldPositionStays = true preserves world position temporarily)
        rectTransform.SetParent(PuzzleManager.Instance.pieceParent, true);
        
        // Calculate target world position from correctPosition (which is in pieceParent local space)
        Vector3 targetWorldPos = PuzzleManager.Instance.pieceParent.TransformPoint(
            new Vector3(correctPosition.x, correctPosition.y, 0)
        );
        
        // Set to target world position
        rectTransform.position = new Vector3(targetWorldPos.x, targetWorldPos.y, rectTransform.position.z);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        
        // ✅ SET SORTING ORDER: Piece = 0, Shadow = 1
        SetPieceSortingOrder(0);
        
        Debug.Log($"[PLACE] {name} | " +
                  $"WorldBefore: {worldBeforeParent} | " +
                  $"TargetWorld: {targetWorldPos} | " +
                  $"FinalWorld: {rectTransform.position} | " +
                  $"FinalAnchored: {rectTransform.anchoredPosition} | " +
                  $"CorrectPos: {correctPosition} | " +
                  $"SortingOrder: Piece=0, Shadow=1");
        
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
    }
    
    // Force canvas update
    Canvas.ForceUpdateCanvases();
    
    // ✅ Refresh sorting orders - this will properly set all placed pieces to low orders
    PuzzleManager.Instance.RefreshSortingOrdersFromList();
}
    
    private IEnumerator DisableParticleAfterDelay(GameObject particle, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (particle != null)
            particle.SetActive(false);
    }

    // =========================================================
    // BOTTOM PANEL
    // =========================================================

    private bool IsPointerOverBottomPanel(PointerEventData eventData)
    {
        if (bottomPanel == null) return false;
        Camera cam = (mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay) 
            ? null : mainCanvas?.worldCamera;
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(bottomPanel, eventData.position, cam, out localPoint))
            return bottomPanel.rect.Contains(localPoint);
        return false;
    }

        private void ReturnToBottomPanel()
    {
        // Only used when piece is explicitly dropped on bottom panel
        Vector3 worldPos = rectTransform.position;
        
        // Handle group pieces too
        if (piece.group != null && piece.group.pieces.Count > 1)
        {
            foreach (var p in piece.group.pieces)
            {
                DragPiece d = p.GetComponent<DragPiece>();
                if (d != null && d != this)
                {
                    Vector3 groupWorldPos = d.rectTransform.position;
                    d.transform.SetParent(PuzzleManager.Instance.bottomParent, true);
                    d.rectTransform.position = groupWorldPos;
                    d.parentChanged = false;
                    
                    // Re-enable drag for returned pieces
                    d.canDrag = true;
                    d.canvasGroup.blocksRaycasts = true;
                }
            }
        }

        transform.SetParent(PuzzleManager.Instance.bottomParent, true);
        rectTransform.position = worldPos;
        rectTransform.localScale = Vector3.one;
        parentChanged = false;
        
        // Re-enable drag
        canDrag = true;
        canvasGroup.blocksRaycasts = true;
        
        PuzzleManager.Instance.ForceRearrangeBottom();
    }

    private void ReturnToContentParent()
    {
        Vector3 worldPos = rectTransform.position;
        transform.SetParent(originalParentBeforeDrag, true);
        rectTransform.position = worldPos;
        rectTransform.localScale = Vector3.one;
        parentChanged = false;
        
        if (originalSiblingIndex >= 0 && originalSiblingIndex < transform.parent.childCount)
            transform.SetSiblingIndex(originalSiblingIndex);
    }

    // =========================================================
    // MERGE LOGIC
    // =========================================================

    private void CheckForMerge()
    {
        if (Time.time - lastMergeTime < mergeCooldown) return;
        if (isMerging || piece == null) return;

        foreach (var other in PuzzleManager.Instance.allPieces)
        {
            if (other == piece) continue;

            // ✅ Don't merge with fully placed pieces
            DragPiece otherDrag = other.GetComponent<DragPiece>();

            if (otherDrag != null && otherDrag.isPlaced)
                continue;
            if (other.group == piece.group) continue;

            float physicalDistance = Vector2.Distance(
                piece.GetComponent<RectTransform>().anchoredPosition,
                other.GetComponent<RectTransform>().anchoredPosition);

            float maxMergeDistance = PuzzleManager.Instance.cellSize * 2.5f;
            if (physicalDistance > maxMergeDistance) continue;

            if (IsNeighbor(other) && IsCorrectMatch(other) && IsEdgeMatch(other))
            {
                lastMergeTime = Time.time;
                SnapExactlyAndMerge(other);
                break;
            }
        }
    }

    private bool IsNeighbor(PuzzlePiece other) 
    { 
        return other == piece.left || other == piece.right || other == piece.top || other == piece.bottom; 
    }

    private bool IsCorrectMatch(PuzzlePiece other)
    {
        if (other == piece.right && other.col == piece.col + 1 && other.row == piece.row) return true;
        if (other == piece.left && other.col == piece.col - 1 && other.row == piece.row) return true;
        if (other == piece.top && other.row == piece.row - 1 && other.col == piece.col) return true;
        if (other == piece.bottom && other.row == piece.row + 1 && other.col == piece.col) return true;
        return false;
    }

    private bool IsEdgeMatch(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();
        RectTransform otherRect = other.GetComponent<RectTransform>();
        float dist = Vector2.Distance(myRect.anchoredPosition, otherRect.anchoredPosition);
        float maxDist = Mathf.Max(myRect.rect.width, myRect.rect.height) * 1.5f;
        return dist <= maxDist && dist >= 20f;
    }

    private void SnapExactlyAndMerge(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();
        RectTransform otherRect = other.GetComponent<RectTransform>();
        DragPiece otherDrag = other.GetComponent<DragPiece>();
        if (otherDrag == null) return;

        Vector2 correctOffset = correctPosition - otherDrag.correctPosition;
        Vector2 targetMyPos = otherRect.anchoredPosition + correctOffset;
        Vector2 offset = targetMyPos - myRect.anchoredPosition;
        if (offset.magnitude > snapThreshold * 3f) return;

        foreach (var p in piece.group.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();

            // ✅ DO NOT MOVE ALREADY PLACED PIECES
            if (drag != null && drag.isPlaced)
                continue;

            RectTransform r = p.GetComponent<RectTransform>();

            if (r != null)
            {
                r.SetParent(PuzzleManager.Instance.pieceParent, true);
                r.anchoredPosition += offset;
            }
        }

        if (otherRect.parent == PuzzleManager.Instance.pieceParent)
        {
            PuzzleGroup otherGroup = other.group;
            PuzzleGroup myGroup = piece.group;
            if (otherGroup != null && myGroup != null && otherGroup != myGroup)
            {
                myGroup.Merge(otherGroup);
                PuzzleManager.Instance.BringGroupToFront(myGroup);
                CheckGroupSnapAfterMerge(myGroup);
            }
        }
    }

    private void CheckGroupSnapAfterMerge(PuzzleGroup mergedGroup)
    {
        if (mergedGroup == null) return;
        foreach (var p in mergedGroup.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();
            if (drag != null && !drag.isPlaced)
            {
                float dist = Vector2.Distance(drag.rectTransform.anchoredPosition, drag.correctPosition);
                if (dist <= snapThreshold)
                {
                    piece = p;
                    PlacePieceImmediate();
                    return;
                }
            }
        }
    }

    private void MergeWithNeighbours()
    {
        float cell = PuzzleManager.Instance.cellSize;
        TryMergeWith(piece.left, new Vector2(-cell, 0));
        TryMergeWith(piece.right, new Vector2(cell, 0));
        TryMergeWith(piece.top, new Vector2(0, cell));
        TryMergeWith(piece.bottom, new Vector2(0, -cell));
    }

    private void TryMergeWith(PuzzlePiece neighbour, Vector2 expectedOffset)
    {
        if (neighbour == null) return;
        DragPiece nd = neighbour.GetComponent<DragPiece>();
        if (nd == null || !nd.isPlaced) return;
        if (piece.group != null && piece.group == neighbour.group) return;
        
        Vector2 actualOffset = nd.rectTransform.anchoredPosition - rectTransform.anchoredPosition;
        if (Vector2.Distance(actualOffset, expectedOffset) > 5f) return;
        
        PuzzleGroup myGroup = piece.group ?? CreateSoloGroup();
        PuzzleGroup neighbourGroup = neighbour.group ?? nd.CreateSoloGroup();
        if (myGroup == neighbourGroup) return;
        
        PuzzleGroup dominant = myGroup.pieces.Count >= neighbourGroup.pieces.Count ? myGroup : neighbourGroup;
        PuzzleGroup absorbed = dominant == myGroup ? neighbourGroup : myGroup;
        foreach (var p in absorbed.pieces) { p.group = dominant; dominant.pieces.Add(p); }
        absorbed.pieces = new List<PuzzlePiece>();
    }

    private PuzzleGroup CreateSoloGroup() 
    { 
        PuzzleGroup g = new PuzzleGroup(); 
        g.AddPiece(piece); 
        piece.group = g; 
        return g; 
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private Vector3 ScreenToWorldPoint(Vector2 screenPosition)
    {
        Camera cam = renderCamera;
        Vector3 worldPos;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectTransform, screenPosition, cam, out worldPos);
        return worldPos;
    }

    private bool IsInsideScrollView() => GetComponentInParent<ScrollRect>() != null;
    
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
            if (c.transform.parent == null) 
                return c; 
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
        canDrag = true; 
        parentChanged = false;
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
            if (d != null && d.isPlaced) 
                count++; 
        } 
        if (count >= PuzzleManager.Instance.TotalPieces) 
            PuzzleManager.Instance.ShowCompletionCanvas(); 
    }

    public void SetPieceSortingOrder(int baseOrder)
    {
        int pieceOrder = baseOrder, shadowOrder = baseOrder + 1;
        Canvas mc = GetComponent<Canvas>();
        if (mc == null) 
        { 
            mc = gameObject.AddComponent<Canvas>(); 
            if (GetComponent<GraphicRaycaster>() == null) 
                gameObject.AddComponent<GraphicRaycaster>(); 
        }
        mc.overrideSorting = true; 
        mc.sortingOrder = pieceOrder;
        
        if (piece != null && piece.shadowImage != null)
        {
            Canvas sc = piece.shadowImage.GetComponent<Canvas>();
            if (sc == null) 
                sc = piece.shadowImage.gameObject.AddComponent<Canvas>();
            sc.overrideSorting = true; 
            sc.sortingOrder = shadowOrder;
        }
    }
}