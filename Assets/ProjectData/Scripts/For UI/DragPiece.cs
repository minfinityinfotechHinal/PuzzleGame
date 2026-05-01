using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas mainCanvas;
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public float snapThreshold = 120f;
    public bool canDrag = false;
    public RectTransform dragArea;
    public Vector2 correctPosition;

    public GameObject ghostImage;
    public GameObject particleObject;
    public PuzzlePiece piece;

    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float directionThreshold = 10f;
    [SerializeField] private float dragStartThreshold = 5f;

    private bool isScrolling = false;
    private bool isDraggingPiece = false;
    private bool scrollDragStarted = false;
    [SerializeField] private RectTransform bottomPanel;
    private bool directionLocked = false;
    private bool lockedIsHorizontal = false;
    private Vector2 dragOffset;
    private bool parentChanged = false;
    private Vector2 dragStartPosition;
    private bool dragInitiated = false;
    
    [Header("Boundaries")]
    [SerializeField] private float screenEdgePadding = 50f;
    [SerializeField] private bool useStrictScreenBounds = true;
    [SerializeField] private float topPadding = 100f;
    [SerializeField] private float bottomPadding = 50f;
    
    private Vector2 screenBoundsMin;
    private Vector2 screenBoundsMax;
    private Vector2 pieceHalfSize;
    
    [Header("Raycast Settings")]
    [SerializeField] private float alphaHitThreshold = 0.1f;
    [SerializeField] private bool useAlphaHitTest = true;
    
    [Header("Drag Priority")]
    [SerializeField] private bool prioritizeDragOverScroll = true;
    [SerializeField] private float scrollTakeoverThreshold = 20f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private Image pieceImage;
    private Transform originalParent;
    private Vector2 originalAnchoredPosition;
    private bool pieceTakenFromScrollView = false;
    private RectTransform canvasRectTransform;
    private Camera renderCamera;
    private bool scrollWasEnabled = true;
    
    private void Awake()
    {
        piece = GetComponent<PuzzlePiece>();
        rectTransform = GetComponent<RectTransform>();
        
        mainCanvas = FindMainCanvas();
        if (mainCanvas != null)
        {
            canvasRectTransform = mainCanvas.GetComponent<RectTransform>();
            renderCamera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetupRaycaster();
        
        if (scrollRect == null && PuzzleManager.Instance != null)
            scrollRect = PuzzleManager.Instance.scrollRect;
        if (bottomPanel == null && PuzzleManager.Instance != null)
            bottomPanel = PuzzleManager.Instance.bottomPanel;

        if (ghostImage != null) ghostImage.SetActive(false);
        if (particleObject != null) particleObject.SetActive(false);
        
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
        originalParent = transform.parent;
    }
    
    private void Start()
    {
        if (mainCanvas != null)
        {
            canvasRectTransform = mainCanvas.GetComponent<RectTransform>();
            renderCamera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
        }
        
        CalculateScreenBounds();
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
        
        pieceImage = GetComponent<Image>();
        if (pieceImage != null)
        {
            pieceImage.raycastTarget = true;
            if (useAlphaHitTest)
            {
                pieceImage.alphaHitTestMinimumThreshold = alphaHitThreshold;
            }
        }
        
        if (scrollRect == null)
        {
            scrollRect = GetComponentInParent<ScrollRect>();
        }
    }
    
    private bool IsInsideScrollView()
    {
        return GetComponentInParent<ScrollRect>() != null;
    }
    
    private void SetupRaycaster()
    {
        Canvas pieceCanvas = GetComponent<Canvas>();
        if (pieceCanvas == null)
        {
            pieceCanvas = gameObject.AddComponent<Canvas>();
            pieceCanvas.overrideSorting = true;
        }
        
        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }
    }
    
    private Canvas FindMainCanvas()
    {
        if (PuzzleManager.Instance != null && PuzzleManager.Instance.pieceParent != null)
        {
            Canvas c = PuzzleManager.Instance.pieceParent.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        
        GameObject mainCanvasObj = GameObject.Find("MainCanvas");
        if (mainCanvasObj != null)
        {
            Canvas c = mainCanvasObj.GetComponent<Canvas>();
            if (c != null) return c;
        }
        
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in allCanvases)
        {
            if (c.transform.parent == null || c.gameObject.name.Contains("MainCanvas") || c.gameObject.name.Contains("Canvas"))
            {
                if (c.gameObject != gameObject) return c;
            }
        }
        
        return GetComponentInParent<Canvas>();
    }
    
    private void CalculateScreenBounds()
    {
        if (mainCanvas == null || canvasRectTransform == null)
        {
            mainCanvas = FindMainCanvas();
            if (mainCanvas == null) return;
            canvasRectTransform = mainCanvas.GetComponent<RectTransform>();
            renderCamera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
        }
        
        Vector2 canvasSize = canvasRectTransform.sizeDelta;
        float halfWidth = canvasSize.x * 0.5f;
        float halfHeight = canvasSize.y * 0.5f;
        
        screenBoundsMin = new Vector2(
            -halfWidth + screenEdgePadding,
            -halfHeight + bottomPadding
        );
        
        screenBoundsMax = new Vector2(
            halfWidth - screenEdgePadding,
            halfHeight - topPadding
        );
    }
    
    private Vector2 ScreenToCanvasLocal(Vector2 screenPosition)
    {
        if (mainCanvas == null || canvasRectTransform == null) return screenPosition;
        
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            screenPosition,
            renderCamera,
            out localPoint
        );
        return localPoint;
    }
    
    private Vector2 GetPieceCanvasLocalPosition()
    {
        if (mainCanvas == null || canvasRectTransform == null) return rectTransform.anchoredPosition;
        
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(renderCamera, rectTransform.position);
        return ScreenToCanvasLocal(screenPos);
    }
    
    private void SetPieceCanvasLocalPosition(Vector2 localPosition)
    {
        if (mainCanvas == null || canvasRectTransform == null)
        {
            rectTransform.anchoredPosition = localPosition;
            return;
        }
        
        Vector3 worldPos;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRectTransform,
            RectTransformUtility.WorldToScreenPoint(renderCamera, 
                canvasRectTransform.TransformPoint(localPosition)),
            renderCamera,
            out worldPos
        );
        
        rectTransform.position = worldPos;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;
        
        dragStartPosition = eventData.position;
        dragInitiated = false;
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        pieceTakenFromScrollView = IsInsideScrollView();
        scrollWasEnabled = scrollRect != null && scrollRect.enabled;
        
        CalculateScreenBounds();
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        // Not used for drag logic
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;
        
        float dragDistance = Vector2.Distance(eventData.position, dragStartPosition);
        if (dragDistance < dragStartThreshold)
        {
            return;
        }
        
        CalculateScreenBounds();
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
        
        dragInitiated = true;
        parentChanged = false;
        
        if (mainCanvas == null) mainCanvas = FindMainCanvas();
        if (mainCanvas == null) return;
        
        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;
        
        Vector2 currentCanvasPos = GetPieceCanvasLocalPosition();
        Vector2 pointerCanvasPos = ScreenToCanvasLocal(eventData.position);
        dragOffset = currentCanvasPos - pointerCanvasPos;

        isScrolling = false;
        isDraggingPiece = false;
        directionLocked = false;
        lockedIsHorizontal = false;
        scrollDragStarted = false;

        canvasGroup.blocksRaycasts = true;
        
        if (enableDebugLogs)
            Debug.Log($"[{gameObject.name}] BeginDrag - Offset: {dragOffset}");
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag || !dragInitiated) return;

        Vector2 totalDelta = eventData.position - dragStartPosition;

        if (!directionLocked)
        {
            if (totalDelta.magnitude >= directionThreshold)
            {
                // Check if piece is in the scroll view (bottom panel)
                if (pieceTakenFromScrollView && IsInsideBottomPanel())
                {
                    float absX = Mathf.Abs(totalDelta.x);
                    float absY = Mathf.Abs(totalDelta.y);
                    
                    // If horizontal movement is dominant, it's a scroll
                    if (absX > absY)
                    {
                        isScrolling = true;
                        isDraggingPiece = false;
                        lockedIsHorizontal = true;
                        directionLocked = true;
                        
                        canvasGroup.blocksRaycasts = false;
                        
                        if (scrollRect != null)
                        {
                            scrollRect.enabled = true;
                            scrollRect.horizontal = true;
                            scrollRect.vertical = false;
                            
                            if (!scrollDragStarted)
                            {
                                scrollRect.OnBeginDrag(eventData);
                                scrollDragStarted = true;
                            }
                        }
                        
                        if (enableDebugLogs)
                            Debug.Log($"[{gameObject.name}] Started Horizontal Scroll (absX:{absX:F1} > absY:{absY:F1})");
                    }
                    // If vertical movement is dominant, start dragging the piece
                    else
                    {
                        isDraggingPiece = true;
                        isScrolling = false;
                        directionLocked = true;
                        
                        canvasGroup.blocksRaycasts = true;
                        
                        if (enableDebugLogs)
                            Debug.Log($"[{gameObject.name}] Started Vertical Piece Drag (absY:{absY:F1} > absX:{absX:F1})");
                    }
                }
                // Piece is already outside bottom panel or was never in scroll view - free drag
                else
                {
                    isDraggingPiece = true;
                    isScrolling = false;
                    directionLocked = true;
                    
                    canvasGroup.blocksRaycasts = true;
                    
                    if (enableDebugLogs)
                        Debug.Log($"[{gameObject.name}] Started Free Drag (Outside Bottom Panel)");
                }
            }
            else
            {
                // Haven't reached threshold yet, pass to scrollRect if in scroll view
                if (pieceTakenFromScrollView && scrollRect != null && IsInsideBottomPanel())
                {
                    scrollRect.OnDrag(eventData);
                }
                return;
            }
        }

        // ================= SCROLL (Horizontal) =================
        if (isScrolling && lockedIsHorizontal)
        {
            if (scrollRect != null)
            {
                scrollRect.OnDrag(eventData);
            }
            return;
        }

        // ================= PIECE DRAG (Free Movement) =================
        if (isDraggingPiece)
        {
            if (mainCanvas == null) mainCanvas = FindMainCanvas();
            if (mainCanvas == null) return;

            Vector2 pointerCanvasPos = ScreenToCanvasLocal(eventData.position);
            Vector2 targetLocalPos = pointerCanvasPos + dragOffset;
            
            targetLocalPos = ClampPositionToScreen(targetLocalPos);
            
            SetPieceCanvasLocalPosition(targetLocalPos);
        }
    }
    
    /// <summary>
    /// Changes parent without triggering LayoutGroup/ContentSizeFitter recalculation.
    /// This prevents other pieces in the bottom panel from shifting when one piece is dragged out.
    /// </summary>
    private void SetParentWithoutLayoutRebuild(Transform newParent)
{
    // Store world position BEFORE changing parent
    Vector3 worldPosition = rectTransform.position;
    
    // Temporarily disable layout components
    LayoutGroup parentLayout = newParent?.GetComponent<LayoutGroup>();
    ContentSizeFitter parentFitter = newParent?.GetComponent<ContentSizeFitter>();
    
    bool hadLayout = parentLayout != null && parentLayout.enabled;
    bool hadFitter = parentFitter != null && parentFitter.enabled;
    
    if (hadLayout) parentLayout.enabled = false;
    if (hadFitter) parentFitter.enabled = false;
    
    LayoutElement thisLayoutElement = GetComponent<LayoutElement>();
    bool hadLayoutElement = thisLayoutElement != null && thisLayoutElement.enabled;
    if (hadLayoutElement) thisLayoutElement.enabled = false;
    
    // Change parent
    transform.SetParent(newParent, true);
    
    // Restore world position
    rectTransform.position = worldPosition;
    
    // Re-enable layout components after a frame
    if (hadLayout || hadFitter || hadLayoutElement)
    {
        StartCoroutine(ReEnableLayoutAfterFrame(parentLayout, parentFitter, thisLayoutElement, 
            hadLayout, hadFitter, hadLayoutElement, newParent));
    }
}
    
    private IEnumerator ReEnableLayoutAfterFrame(LayoutGroup layout, ContentSizeFitter fitter, 
        LayoutElement layoutElement, bool enableLayout, bool enableFitter, bool enableLayoutElement,
        Transform parentToRebuild)
    {
        yield return null; // Wait one frame for the hierarchy change to settle
        
        // Only rebuild the specific parent, not force rebuild all children
        if (layout != null && enableLayout) 
        {
            layout.enabled = true;
            layout.CalculateLayoutInputHorizontal();
            layout.CalculateLayoutInputVertical();
        }
        
        if (fitter != null && enableFitter) 
        {
            fitter.enabled = true;
            LayoutRebuilder.MarkLayoutForRebuild(fitter.GetComponent<RectTransform>());
        }
        
        if (layoutElement != null && enableLayoutElement)
        {
            layoutElement.enabled = true;
        }
    }
    
    private Vector2 ClampPositionToScreen(Vector2 localPosition)
    {
        if (!useStrictScreenBounds) return localPosition;
        
        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
        
        float minX = screenBoundsMin.x + pieceHalfSize.x;
        float maxX = screenBoundsMax.x - pieceHalfSize.x;
        float minY = screenBoundsMin.y + pieceHalfSize.y;
        float maxY = screenBoundsMax.y - pieceHalfSize.y;
        
        if (minX > maxX) { float temp = minX; minX = maxX; maxX = temp; }
        if (minY > maxY) { float temp = minY; minY = maxY; maxY = temp; }
        
        float clampedX = Mathf.Clamp(localPosition.x, minX, maxX);
        float clampedY = Mathf.Clamp(localPosition.y, minY, maxY);
        
        return new Vector2(clampedX, clampedY);
    }

    bool IsInsideBottomPanel()
    {
        if (bottomPanel == null) return false;
        
        Vector2 pieceLocalPos = GetPieceCanvasLocalPosition();
        
        Vector3[] worldCorners = new Vector3[4];
        bottomPanel.GetWorldCorners(worldCorners);
        
        Vector2 panelMin = ScreenToCanvasLocal(
            RectTransformUtility.WorldToScreenPoint(renderCamera, worldCorners[0])
        );
        Vector2 panelMax = ScreenToCanvasLocal(
            RectTransformUtility.WorldToScreenPoint(renderCamera, worldCorners[2])
        );
        
        float tolerance = 20f;
        
        return pieceLocalPos.x >= panelMin.x - tolerance &&
               pieceLocalPos.x <= panelMax.x + tolerance &&
               pieceLocalPos.y >= panelMin.y - tolerance &&
               pieceLocalPos.y <= panelMax.y + tolerance;
    }
    
    /// <summary>
    /// Converts a position from the piece's current parent space to board-local space.
    /// This ensures consistent comparison with correctPosition which is defined in board space.
    /// </summary>
   /// <summary>
/// Converts a position from the piece's current parent space to board-local space.
/// </summary>
private Vector2 ConvertToBoardSpace(Vector2 positionInCurrentParentSpace)
{
    RectTransform boardRect = GetBoardRectTransform();
    
    if (boardRect == null)
    {
        Debug.LogWarning($"[{gameObject.name}] Cannot convert to board space - board reference missing");
        return positionInCurrentParentSpace;
    }
    
    // If piece is already child of board, no conversion needed
    if (transform.parent == boardRect)
        return positionInCurrentParentSpace;
    
    // Convert from current parent's local space to world space
    Vector3 worldPos;
    if (transform.parent is RectTransform parentRect)
    {
        worldPos = parentRect.TransformPoint(positionInCurrentParentSpace);
    }
    else
    {
        worldPos = transform.parent.TransformPoint(positionInCurrentParentSpace);
    }
    
    // Convert from world space to board-local space
    Vector2 boardLocalPos = boardRect.InverseTransformPoint(worldPos);
    
    if (enableDebugLogs)
    {
        Debug.Log($"[{gameObject.name}] Position Conversion:\n" +
                $"  Current Parent Space: {positionInCurrentParentSpace}\n" +
                $"  World Space: {worldPos}\n" +
                $"  Board Space: {boardLocalPos}\n" +
                $"  Correct Position (Board Space): {correctPosition}\n" +
                $"  Distance: {Vector2.Distance(boardLocalPos, correctPosition):F2}");
    }
    
    return boardLocalPos;
}

    private RectTransform GetBoardRectTransform()
    {
        // Option 1: If PuzzleManager has the reference (uncomment if you added it)
        // if (PuzzleManager.Instance != null && PuzzleManager.Instance.boardRectTransform != null)
        //     return PuzzleManager.Instance.boardRectTransform;
        
        // Option 2: Find by name (change "Board" to your actual board GameObject name)
        GameObject boardObj = GameObject.Find("Board");
        if (boardObj != null)
            return boardObj.GetComponent<RectTransform>();
        
        // Option 3: Find by tag (you can set the board's tag to "Board" in inspector)
        // boardObj = GameObject.FindWithTag("Board");
        // if (boardObj != null)
        //     return boardObj.GetComponent<RectTransform>();
        
        // Option 4: Look for the parent named "Board" near the piece
        Transform current = transform;
        while (current != null)
        {
            if (current.name.Contains("Board"))
                return current.GetComponent<RectTransform>();
            current = current.parent;
        }
        
        // Option 5: If you have a reference in another way, use that
        
        Debug.LogError($"[{gameObject.name}] Could not find Board RectTransform! " +
                    "Make sure there's a GameObject named 'Board' in the scene.");
        return null;
    }
    
    /// <summary>
    /// Checks if the piece is close enough to its correct position on the board.
    /// Uses board-local coordinates for accurate comparison.
    /// </summary>
   /// <summary>
/// Checks if the piece is close enough to its correct position on the board.
/// Uses board-local coordinates for accurate comparison.
/// </summary>
private bool IsSnapToPlace(Vector2 piecePositionInParentSpace)
{
    // Convert piece position to board space for consistent comparison
    Vector2 pieceBoardPos = ConvertToBoardSpace(piecePositionInParentSpace);
    
    float distance = Vector2.Distance(pieceBoardPos, correctPosition);
    
    Debug.Log($"[SNAP CHECK] {gameObject.name}:\n" +
              $"  Position in Parent Space: {piecePositionInParentSpace}\n" +
              $"  Parent: {(transform.parent != null ? transform.parent.name : "null")}\n" +
              $"  Converted Board Pos: {pieceBoardPos}\n" +
              $"  Target Pos (Board Space): {correctPosition}\n" +
              $"  Distance: {distance:F2}\n" +
              $"  Threshold: {snapThreshold}\n" +
              $"  Should Snap: {distance <= snapThreshold}");
    
    return distance <= snapThreshold;
}
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragInitiated)
        {
            ReEnableScrollView();
            return;
        }

        Vector2 dropScreenPos = eventData.position;
        Vector2 dropCanvasPos = ScreenToCanvasLocal(eventData.position);

        Debug.Log($"[DROP] {gameObject.name} dropped at:\n" +
                $"Screen: {dropScreenPos}\n" +
                $"Canvas: {dropCanvasPos}\n" +
                $"Anchored: {rectTransform.anchoredPosition}");
        
        if (isScrolling && scrollRect != null)
        {
            scrollRect.OnEndDrag(eventData);
        }

        isScrolling = false;
        isDraggingPiece = false;
        directionLocked = false;
        scrollDragStarted = false;
        dragInitiated = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        
        ReEnableScrollView();
        pieceTakenFromScrollView = false;
        
        if (isPlaced) return;
        
        bool insideBottomPanel = IsInsideBottomPanel();
        
        if (insideBottomPanel)
        {
            if (parentChanged || (bottomPanel != null && transform.parent != bottomPanel))
            {
                Transform targetParent = bottomPanel;
                if (targetParent == null) targetParent = originalParent;
                
                if (targetParent != null)
                {
                    SetParentWithoutLayoutRebuild(targetParent);
                    parentChanged = false;
                    rectTransform.anchoredPosition = originalAnchoredPosition;
                    
                    if (enableDebugLogs)
                        Debug.Log($"[{gameObject.name}] Returned to {targetParent.name} at position {originalAnchoredPosition}");
                }
            }
        }
        else
        {
            // Piece is dropped outside bottom panel - check for placement
            if (!parentChanged && PuzzleManager.Instance != null && 
                PuzzleManager.Instance.pieceParent != null && 
                transform.parent != PuzzleManager.Instance.pieceParent)
            {
                SetParentWithoutLayoutRebuild(PuzzleManager.Instance.pieceParent);
                parentChanged = true;
            }
            
            // Check if piece should snap to board position
            TryPlacePiece();
        }
    }
    
    /// <summary>
    /// Attempts to place the piece at its correct board position.
    /// Handles parent changes and coordinate conversions automatically.
    /// </summary>
   /// <summary>
/// Attempts to place the piece at its correct board position.
/// </summary>
  /// <summary>
/// Attempts to place the piece at its correct board position.
/// </summary>
    /// <summary>
/// Attempts to place the piece at its correct board position.
/// </summary>
    private void TryPlacePiece()
    {
        RectTransform boardRect = GetBoardRectTransform();
        
        if (boardRect == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Cannot check placement - board reference missing");
            return;
        }
        
        // Get the piece's current world position
        Vector3 pieceWorldPos = rectTransform.position;
        
        // Convert world position to board-local space
        Vector2 pieceBoardPos = boardRect.InverseTransformPoint(pieceWorldPos);
        
        float distance = Vector2.Distance(pieceBoardPos, correctPosition);
        
        Debug.Log($"[SNAP CHECK] {gameObject.name}:\n" +
                $"  World Pos: {pieceWorldPos}\n" +
                $"  Board Local Pos: {pieceBoardPos}\n" +
                $"  Target Pos (Board Space): {correctPosition}\n" +
                $"  Distance: {distance:F2}\n" +
                $"  Threshold: {snapThreshold}");
        
        if (distance <= snapThreshold)
        {
            // Move piece to board parent
            if (transform.parent != boardRect)
            {
                // IMPORTANT: Store world position before changing parent
                Vector3 worldPosBeforeChange = rectTransform.position;
                
                SetParentWithoutLayoutRebuild(boardRect);
                
                // After parent change, convert world position to new parent's local space
                Vector2 newLocalPos = boardRect.InverseTransformPoint(worldPosBeforeChange);
                rectTransform.anchoredPosition = newLocalPos;
            }
            
            // Now set the exact correct position in board-local space
            rectTransform.anchoredPosition = correctPosition;
            
            // Reset anchors to match board's expected anchor settings
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // Mark as placed
            isPlaced = true;
            canDrag = false;
            
            // Visual feedback
            if (ghostImage != null) ghostImage.SetActive(true);
            if (particleObject != null) particleObject.SetActive(true);
            
            Debug.Log($"[PLACED] {gameObject.name} snapped to correct position: {correctPosition}");
            
            // Check if all pieces are placed
            CheckCompletion();
        }
        else
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[MISSED] {gameObject.name} was {distance:F2} away from target " +
                        $"(threshold: {snapThreshold}). Current: {pieceBoardPos}, Target: {correctPosition}");
            }
        }
    }
    
    private void ReEnableScrollView()
    {
        if (scrollRect != null)
        {
            scrollRect.enabled = true;
            scrollRect.horizontal = true;
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (pieceTakenFromScrollView && scrollRect != null)
        {
            scrollRect.OnInitializePotentialDrag(eventData);
        }
    }

    public void SetPieceSortingOrder(int baseOrder)
    {
        Canvas c = GetComponent<Canvas>();
        if (c == null)
        {
            c = gameObject.AddComponent<Canvas>();
        }
        
        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        c.overrideSorting = true;
        c.sortingOrder = baseOrder;
        
        mainCanvas = FindMainCanvas();
        if (mainCanvas != null)
        {
            canvasRectTransform = mainCanvas.GetComponent<RectTransform>();
            renderCamera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
        }
    }

    public void ResetPiece()
    {
        isPlaced = false;
        canDrag = true;
        dragInitiated = false;
        parentChanged = false;
        pieceTakenFromScrollView = false;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }
        
        pieceImage = GetComponent<Image>();
        if (pieceImage != null && useAlphaHitTest)
        {
            pieceImage.alphaHitTestMinimumThreshold = alphaHitThreshold;
        }
        
        CalculateScreenBounds();
    }

    public void CheckCompletion()
    {
        int count = 0;
        foreach (var p in PuzzleManager.Instance.allPieces)
        {
            if (p.GetComponent<DragPiece>().isPlaced)
                count++;
        }

        if (count >= PuzzleManager.Instance.TotalPieces)
            PuzzleManager.Instance.ShowCompletionCanvas();
    }
}