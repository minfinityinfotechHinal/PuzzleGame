using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    private bool parentChanged = false;
    private Vector2 dragOffset;
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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Image pieceImage;
    private Transform originalParent;
    private Vector2 originalAnchoredPosition;
    private bool pieceTakenFromScrollView = false;

    private RectTransform canvasRectTransform;
    private Camera renderCamera;

    [SerializeField] private float exitBottomPanelThreshold = 40f;

    // =========================
    // MERGE VARIABLES
    // =========================

    private bool isMerging = false;

    private float mergeCooldown = 0.1f;
    private float lastMergeTime = -1f;

    [Header("Exit Settings")]
    [SerializeField] private float verticalExitDistance = 120f;

    private CanvasGroup shadowCanvasGroup;

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

        if (ghostImage != null)
            ghostImage.SetActive(false);

        if (particleObject != null)
            particleObject.SetActive(false);

        pieceHalfSize = rectTransform.sizeDelta * 0.5f;

        originalParent = transform.parent;
    }

    private void Start()
    {
        CalculateScreenBounds();

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
        if (isPlaced || !canDrag)
            return;

        parentChanged = false;

        dragStartPosition = eventData.position;

        dragInitiated = false;

        pieceTakenFromScrollView = IsInsideScrollView();

        CalculateScreenBounds();

        pieceHalfSize = rectTransform.sizeDelta * 0.5f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }

    // =========================================================
    // BEGIN DRAG
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag)
            return;

        float dragDistance = Vector2.Distance(eventData.position, dragStartPosition);

        if (dragDistance < dragStartThreshold)
            return;

        dragInitiated = true;

        CalculateScreenBounds();

        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        Vector2 currentCanvasPos = GetPieceCanvasLocalPosition();
        Vector2 pointerCanvasPos = ScreenToCanvasLocal(eventData.position);

        dragOffset = currentCanvasPos - pointerCanvasPos;

        canvasGroup.blocksRaycasts = true;

        // ============================================
        // SORTING ORDER
        // ============================================

        if (PuzzleManager.Instance != null)
        {
            if (piece.group != null && piece.group.pieces.Count > 1)
            {
                PuzzleManager.Instance.BringGroupToFront(piece.group);
            }
            else
            {
                PuzzleManager.Instance.UpdateDragOrder(this);
            }
        }

        // ============================================
        // MOVE TO PIECE PARENT
        // ============================================

        // if (PuzzleManager.Instance != null &&
        //     PuzzleManager.Instance.pieceParent != null)
        // {
        //     transform.SetParent(PuzzleManager.Instance.pieceParent, true);
        //     transform.SetAsLastSibling();
        // }
    }

    // =========================================================
    // DRAG
    // =========================================================

    public void OnDrag(PointerEventData eventData)
{
    if (isPlaced || !canDrag || !dragInitiated)
        return;

    // =====================================================
    // IF PIECE ALREADY OUTSIDE BOTTOM PANEL
    // THEN DIRECT FREE DRAG
    // =====================================================

    bool isFreePiece =
        transform.parent == PuzzleManager.Instance.pieceParent;

    if (isFreePiece)
    {
        isDraggingPiece = true;
        isScrolling = false;

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.enabled = false;
        }

        Vector2 pointerCanvasPos =
            ScreenToCanvasLocal(eventData.position);

        Vector2 targetLocalPos =
            pointerCanvasPos + dragOffset;

        targetLocalPos =
            ClampPositionToScreen(targetLocalPos);

        Vector2 currentPos =
            GetPieceCanvasLocalPosition();

        Vector2 delta =
            targetLocalPos - currentPos;

        // GROUP MOVE
        if (piece.group != null &&
            piece.group.pieces.Count > 1)
        {
            piece.group.Move(delta);
        }
        else
        {
            SetPieceCanvasLocalPosition(targetLocalPos);
        }

        // Ghost Preview
        if (ghostImage != null &&
            piece.group != null)
        {
            bool anyNearCorrect =
                piece.group.IsAnyPieceNearCorrectPosition(
                    snapThreshold * 1.5f);

            ghostImage.SetActive(anyNearCorrect);
        }

        return;
    }

    // =====================================================
    // BOTTOM PANEL SCROLL LOGIC
    // =====================================================

    Vector2 totalDelta =
        eventData.position - dragStartPosition;

    float absX = Mathf.Abs(totalDelta.x);
    float absY = Mathf.Abs(totalDelta.y);

    // =====================================================
    // SCROLL MODE
    // =====================================================

   if (!isDraggingPiece)
{
    // TOTAL MOVEMENT
    Vector2 totalMovement =
        eventData.position - dragStartPosition;

    float horizontal =
        Mathf.Abs(totalMovement.x);

    float vertical =
        totalMovement.y;

    // =====================================================
    // SWITCH TO PIECE DRAG IF USER MOVES UP ENOUGH
    // EVEN AFTER SCROLL STARTED
    // =====================================================

    if (vertical > verticalExitDistance)
    {
        // STOP SCROLL
        if (scrollRect != null)
        {
            if (scrollDragStarted)
            {
                scrollRect.OnEndDrag(eventData);
            }

            scrollRect.StopMovement();
            scrollRect.enabled = false;
        }

        isScrolling = false;
        scrollDragStarted = false;

        // CHANGE PARENT
        if (!parentChanged &&
            PuzzleManager.Instance != null &&
            PuzzleManager.Instance.pieceParent != null)
        {
            SetParentWithoutLayoutRebuild(
                PuzzleManager.Instance.pieceParent);

            transform.SetAsLastSibling();

            parentChanged = true;
        }

        isDraggingPiece = true;

        canvasGroup.blocksRaycasts = true;
    }
    else
    {
        // =====================================================
        // NORMAL HORIZONTAL SCROLL
        // =====================================================

        if (horizontal > Mathf.Abs(vertical))
        {
            isScrolling = true;

            canvasGroup.blocksRaycasts = false;

            if (scrollRect != null)
            {
                scrollRect.enabled = true;

                if (!scrollDragStarted)
                {
                    scrollRect.OnBeginDrag(eventData);
                    scrollDragStarted = true;
                }

                scrollRect.OnDrag(eventData);
            }

            return;
        }
    }
}

    // =====================================================
    // DRAG PIECE
    // =====================================================

    if (isDraggingPiece)
    {
        Vector2 pointerCanvasPos =
            ScreenToCanvasLocal(eventData.position);

        Vector2 targetLocalPos =
            pointerCanvasPos + dragOffset;

        targetLocalPos =
            ClampPositionToScreen(targetLocalPos);

        Vector2 currentPos =
            GetPieceCanvasLocalPosition();

        Vector2 delta =
            targetLocalPos - currentPos;

        // GROUP MOVE
        if (piece.group != null &&
            piece.group.pieces.Count > 1)
        {
            piece.group.Move(delta);
        }
        else
        {
            SetPieceCanvasLocalPosition(targetLocalPos);
        }

        // Ghost Preview
        if (ghostImage != null &&
            piece.group != null)
        {
            bool anyNearCorrect =
                piece.group.IsAnyPieceNearCorrectPosition(
                    snapThreshold * 1.5f);

            ghostImage.SetActive(anyNearCorrect);
        }
    }
}
    private bool HasExitedBottomPanelByPiecePosition()
    {
        if (bottomPanel == null)
            return true;

        Vector3[] corners = new Vector3[4];

        bottomPanel.GetWorldCorners(corners);

        float panelTopY = corners[1].y;

        float pieceBottomY =
            rectTransform.position.y -
            (rectTransform.rect.height * rectTransform.lossyScale.y * 0.5f);

        return pieceBottomY > panelTopY + exitBottomPanelThreshold;
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
        {
            scrollRect.OnEndDrag(eventData);
        }

        isScrolling = false;
        isDraggingPiece = false;
        scrollDragStarted = false;

        dragInitiated = false;

        canvasGroup.blocksRaycasts = true;

        ReEnableScrollView();

        if (isPlaced)
            return;

        ghostImage?.SetActive(false);

        // =====================================================
        // SNAP TO CORRECT POSITION
        // =====================================================

        float distance = Vector2.Distance(rectTransform.anchoredPosition, correctPosition);

        if (distance <= snapThreshold)
        {
            if (particleObject != null)
                particleObject.SetActive(true);

            StartCoroutine(SmoothSnapToCorrectPositions());

            return;
        }

        // =====================================================
        // GROUP SNAP CHECK
        // =====================================================

        if (piece.group != null && piece.group.pieces.Count > 1)
        {
            PuzzlePiece closestPiece = piece.group.GetClosestToCorrectPosition();

            if (closestPiece != null)
            {
                DragPiece closestDrag = closestPiece.GetComponent<DragPiece>();

                RectTransform closestRect =
                    closestPiece.GetComponent<RectTransform>();

                if (closestDrag != null && closestRect != null)
                {
                    float closestDist =
                        Vector2.Distance(
                            closestRect.anchoredPosition,
                            closestDrag.correctPosition
                        );

                    if (closestDist <= snapThreshold)
                    {
                        piece = closestPiece;

                        if (particleObject != null)
                            particleObject.SetActive(true);

                        StartCoroutine(SmoothSnapToCorrectPositions());

                        return;
                    }
                }
            }
        }

        // =====================================================
        // MERGE
        // =====================================================

        CheckForMerge();
    }

    // =========================================================
    // MERGE LOGIC
    // =========================================================

    private void CheckForMerge()
    {
        if (Time.time - lastMergeTime < mergeCooldown)
            return;

        if (isMerging || piece == null)
            return;

        foreach (var other in PuzzleManager.Instance.allPieces)
        {
            if (other == piece)
                continue;

            if (other.group == piece.group)
                continue;

            float physicalDistance = Vector2.Distance(
                piece.GetComponent<RectTransform>().anchoredPosition,
                other.GetComponent<RectTransform>().anchoredPosition
            );

            float maxMergeDistance = PuzzleManager.Instance.cellSize * 2.5f;

            if (physicalDistance > maxMergeDistance)
                continue;

            bool neighbor = IsNeighbor(other);

            bool correctMatch = IsCorrectMatch(other);

            if (neighbor && correctMatch)
            {
                bool edgeMatch = IsEdgeMatch(other);

                if (edgeMatch)
                {
                    lastMergeTime = Time.time;

                    SnapExactlyAndMerge(other);

                    break;
                }
            }
        }
    }

    private bool IsNeighbor(PuzzlePiece other)
    {
        return other == piece.left ||
               other == piece.right ||
               other == piece.top ||
               other == piece.bottom;
    }

    private bool IsCorrectMatch(PuzzlePiece other)
    {
        if (other == piece.right &&
            other.col == piece.col + 1 &&
            other.row == piece.row)
            return true;

        if (other == piece.left &&
            other.col == piece.col - 1 &&
            other.row == piece.row)
            return true;

        if (other == piece.top &&
            other.row == piece.row - 1 &&
            other.col == piece.col)
            return true;

        if (other == piece.bottom &&
            other.row == piece.row + 1 &&
            other.col == piece.col)
            return true;

        return false;
    }

    private bool IsEdgeMatch(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();

        RectTransform otherRect = other.GetComponent<RectTransform>();

        Vector2 myPos = myRect.anchoredPosition;

        Vector2 otherPos = otherRect.anchoredPosition;

        float dist = Vector2.Distance(myPos, otherPos);

        float maxDist =
            Mathf.Max(myRect.rect.width, myRect.rect.height) * 1.5f;

        return dist <= maxDist && dist >= 20f;
    }

    private void SnapExactlyAndMerge(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();

        RectTransform otherRect = other.GetComponent<RectTransform>();

        DragPiece myDrag = piece.GetComponent<DragPiece>();

        DragPiece otherDrag = other.GetComponent<DragPiece>();

        if (myDrag == null || otherDrag == null)
            return;

        Vector2 correctOffset =
            myDrag.correctPosition - otherDrag.correctPosition;

        Vector2 targetMyPos =
            otherRect.anchoredPosition + correctOffset;

        Vector2 offset =
            targetMyPos - myRect.anchoredPosition;

        if (offset.magnitude > snapThreshold * 3f)
            return;

        foreach (var p in piece.group.pieces)
        {
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

            if (otherGroup != null &&
                myGroup != null &&
                otherGroup != myGroup)
            {
                myGroup.Merge(otherGroup);

                if (PuzzleManager.Instance != null)
                {
                    PuzzleManager.Instance.BringGroupToFront(myGroup);
                }

                CheckGroupSnapAfterMerge(myGroup);
            }
        }
    }

    private void CheckGroupSnapAfterMerge(PuzzleGroup mergedGroup)
    {
        if (mergedGroup == null)
            return;

        bool shouldSnap = false;

        PuzzlePiece closestPiece = null;

        float closestDistance = float.MaxValue;

        foreach (var p in mergedGroup.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();

            RectTransform rect = p.GetComponent<RectTransform>();

            if (drag != null && rect != null && !drag.isPlaced)
            {
                float dist =
                    Vector2.Distance(
                        rect.anchoredPosition,
                        drag.correctPosition
                    );

                if (dist <= snapThreshold &&
                    dist < closestDistance)
                {
                    closestDistance = dist;
                    closestPiece = p;
                    shouldSnap = true;
                }
            }
        }

        if (shouldSnap && closestPiece != null)
        {
            piece = closestPiece;

            StartCoroutine(SmoothSnapToCorrectPositions());
        }
    }

    // =========================================================
    // SNAP
    // =========================================================

    IEnumerator SmoothSnapToCorrectPositions()
    {
        if (piece == null || piece.group == null)
            yield break;

        Dictionary<PuzzlePiece, Vector2> startPositions =
            new Dictionary<PuzzlePiece, Vector2>();

        Dictionary<PuzzlePiece, Vector2> targetPositions =
            new Dictionary<PuzzlePiece, Vector2>();

        List<PuzzlePiece> piecesToSnap =
            new List<PuzzlePiece>();

        foreach (var p in piece.group.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();

            RectTransform r = p.GetComponent<RectTransform>();

            if (drag != null &&
                r != null &&
                !drag.isPlaced)
            {
                float dist =
                    Vector2.Distance(
                        r.anchoredPosition,
                        drag.correctPosition
                    );

                if (dist <= snapThreshold)
                {
                    startPositions[p] = r.anchoredPosition;
                    targetPositions[p] = drag.correctPosition;

                    piecesToSnap.Add(p);
                }
            }
        }

        if (piecesToSnap.Count == 0)
            yield break;

        float duration = 0.35f;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            float progress =
                t * t * (3f - 2f * t);

            foreach (var p in piecesToSnap)
            {
                RectTransform r =
                    p.GetComponent<RectTransform>();

                if (r != null)
                {
                    r.anchoredPosition =
                        Vector2.Lerp(
                            startPositions[p],
                            targetPositions[p],
                            progress
                        );
                }
            }

            yield return null;
        }

        foreach (var p in piecesToSnap)
        {
            RectTransform r =
                p.GetComponent<RectTransform>();

            DragPiece drag =
                p.GetComponent<DragPiece>();

            if (r != null)
            {
                r.anchoredPosition =
                    targetPositions[p];
            }

            if (drag != null)
            {
                drag.isPlaced = true;
                drag.canDrag = false;

                if (drag.canvasGroup != null)
                {
                    drag.canvasGroup.blocksRaycasts = false;
                    drag.canvasGroup.alpha = 1f;
                }

                if (drag.ghostImage != null)
                    drag.ghostImage.SetActive(true);

                if (drag.particleObject != null)
                    drag.particleObject.SetActive(true);
            }
        }

        CheckCompletion();
    }

    // =========================================================
    // SORTING
    // =========================================================

    public void SetPieceSortingOrder(int baseOrder)
    {
        int pieceOrder = baseOrder;

        int shadowOrder = baseOrder + 1;

        Canvas mainCanvas = GetComponent<Canvas>();

        if (mainCanvas == null)
        {
            mainCanvas = gameObject.AddComponent<Canvas>();

            GraphicRaycaster raycaster =
                GetComponent<GraphicRaycaster>();

            if (raycaster == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        mainCanvas.overrideSorting = true;
        mainCanvas.sortingOrder = pieceOrder;

        if (piece != null && piece.shadowImage != null)
        {
            Canvas shadowCanvas =
                piece.shadowImage.GetComponent<Canvas>();

            if (shadowCanvas == null)
            {
                shadowCanvas =
                    piece.shadowImage.gameObject.AddComponent<Canvas>();
            }

            shadowCanvas.overrideSorting = true;
            shadowCanvas.sortingOrder = shadowOrder;
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private bool IsInsideScrollView()
    {
        return GetComponentInParent<ScrollRect>() != null;
    }

    private void ReEnableScrollView()
    {
        if (scrollRect != null)
            scrollRect.enabled = true;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (pieceTakenFromScrollView && scrollRect != null)
        {
            scrollRect.OnInitializePotentialDrag(eventData);
        }
    }

    private bool HasExitedBottomPanel(PointerEventData eventData)
    {
        if (bottomPanel == null)
            return true;

        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bottomPanel,
            eventData.position,
            renderCamera,
            out localPoint
        );

        Rect rect = bottomPanel.rect;

        rect.yMax += exitBottomPanelThreshold;

        return !rect.Contains(localPoint);
    }

    private Vector2 ClampPositionToScreen(Vector2 localPosition)
    {
        if (!useStrictScreenBounds)
            return localPosition;

        pieceHalfSize = rectTransform.sizeDelta * 0.5f;

        float minX = screenBoundsMin.x + pieceHalfSize.x;
        float maxX = screenBoundsMax.x - pieceHalfSize.x;

        float minY = screenBoundsMin.y + pieceHalfSize.y;
        float maxY = screenBoundsMax.y - pieceHalfSize.y;

        float clampedX =
            Mathf.Clamp(localPosition.x, minX, maxX);

        float clampedY =
            Mathf.Clamp(localPosition.y, minY, maxY);

        return new Vector2(clampedX, clampedY);
    }

    private void CalculateScreenBounds()
    {
        if (mainCanvas == null || canvasRectTransform == null)
            return;

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

    private Canvas FindMainCanvas()
    {
        Canvas[] allCanvases =
            FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (Canvas c in allCanvases)
        {
            if (c.transform.parent == null)
                return c;
        }

        return GetComponentInParent<Canvas>();
    }

    private void SetupRaycaster()
    {
        Canvas pieceCanvas = GetComponent<Canvas>();

        if (pieceCanvas == null)
        {
            pieceCanvas = gameObject.AddComponent<Canvas>();
            pieceCanvas.overrideSorting = true;
        }

        GraphicRaycaster raycaster =
            GetComponent<GraphicRaycaster>();

        if (raycaster == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private Vector2 ScreenToCanvasLocal(Vector2 screenPosition)
    {
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
        Vector2 screenPos =
            RectTransformUtility.WorldToScreenPoint(
                renderCamera,
                rectTransform.position
            );

        return ScreenToCanvasLocal(screenPos);
    }

    private void SetPieceCanvasLocalPosition(Vector2 localPosition)
    {
        Vector3 worldPos;

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRectTransform,
            RectTransformUtility.WorldToScreenPoint(
                renderCamera,
                canvasRectTransform.TransformPoint(localPosition)
            ),
            renderCamera,
            out worldPos
        );

        rectTransform.position = worldPos;
    }

    private void SetParentWithoutLayoutRebuild(Transform newParent)
    {
        Vector3 worldPosition = rectTransform.position;

        transform.SetParent(newParent, true);

        rectTransform.position = worldPosition;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetPiece()
    {
        isPlaced = false;

        canDrag = true;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }
    }

    // =========================================================
    // COMPLETION
    // =========================================================

    public void CheckCompletion()
    {
        int count = 0;

        foreach (var p in PuzzleManager.Instance.allPieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();

            if (drag != null && drag.isPlaced)
                count++;
        }

        if (count >= PuzzleManager.Instance.TotalPieces)
        {
            PuzzleManager.Instance.ShowCompletionCanvas();
        }
    }
}