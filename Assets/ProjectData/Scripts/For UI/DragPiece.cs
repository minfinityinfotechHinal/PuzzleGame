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
        // ✅ Store the actual anchored position relative to its parent
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
        // Temporarily disable layout components on the new parent AND its children's layout controllers
        LayoutGroup parentLayout = newParent?.GetComponent<LayoutGroup>();
        ContentSizeFitter parentFitter = newParent?.GetComponent<ContentSizeFitter>();
        
        bool hadLayout = parentLayout != null && parentLayout.enabled;
        bool hadFitter = parentFitter != null && parentFitter.enabled;
        
        if (hadLayout) parentLayout.enabled = false;
        if (hadFitter) parentFitter.enabled = false;
        
        // ✅ NEW: Disable LayoutElement on this piece if it has one
        LayoutElement thisLayoutElement = GetComponent<LayoutElement>();
        bool hadLayoutElement = thisLayoutElement != null && thisLayoutElement.enabled;
        if (hadLayoutElement) thisLayoutElement.enabled = false;
        
        // Change parent while preserving world position
        Vector3 worldPos = rectTransform.position;
        transform.SetParent(newParent, true);
        rectTransform.position = worldPos;
        
        // ✅ NEW: Mark the RectTransform to NOT be driven by layout
        rectTransform.SetAsLastSibling();
        
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
        
        // ✅ Only rebuild the specific parent, not force rebuild all children
        if (layout != null && enableLayout) 
        {
            layout.enabled = true;
            // Use CalculateLayoutInputHorizontal/Vertical instead of full rebuild
            layout.CalculateLayoutInputHorizontal();
            layout.CalculateLayoutInputVertical();
        }
        
        if (fitter != null && enableFitter) 
        {
            fitter.enabled = true;
            // Set Layout fitter dirty WITHOUT forcing children to reposition
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
    
    public void OnEndDrag(PointerEventData eventData)
{
    if (!dragInitiated)
    {
        ReEnableScrollView();
        return;
    }
    
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
                // ✅ FIXED: Return to bottom panel without layout glitch
                SetParentWithoutLayoutRebuild(targetParent);
                parentChanged = false;
                
                // ✅ CRITICAL FIX: Restore the original anchored position
                rectTransform.anchoredPosition = originalAnchoredPosition;
                
                if (enableDebugLogs)
                    Debug.Log($"[{gameObject.name}] Returned to {targetParent.name} at position {originalAnchoredPosition}");
            }
        }
    }
    else
    {
        if (!parentChanged && PuzzleManager.Instance != null && 
            PuzzleManager.Instance.pieceParent != null && 
            transform.parent != PuzzleManager.Instance.pieceParent)
        {
            Vector3 worldPos = rectTransform.position;
            transform.SetParent(PuzzleManager.Instance.pieceParent, true);
            rectTransform.position = worldPos;
            parentChanged = true;
        }
    }
}
    
    private void ReEnableScrollView()
    {
        if (scrollRect != null)
        {
            scrollRect.enabled = true;
            //scrollRect.vertical = true;
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