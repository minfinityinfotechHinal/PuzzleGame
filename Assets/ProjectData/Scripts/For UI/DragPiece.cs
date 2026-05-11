using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(PuzzlePiece))]
public class DragPiece : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerDownHandler
{
    [HideInInspector] public Vector2 correctPosition;
    [HideInInspector] public RectTransform dragArea;
    [HideInInspector] public GameObject ghostImage;
    [HideInInspector] public PuzzlePiece piece;
    [HideInInspector] public bool canDrag = false;
    [HideInInspector] public bool isPlaced = false;

    [HideInInspector] public GameObject particleObject;
    [HideInInspector] public CanvasGroup canvasGroup;
    [HideInInspector] public float snapThreshold = 50f;

    private Canvas pieceCanvas;
    [HideInInspector] public RectTransform rt;
    private ScrollRect scrollRect;
    private Canvas parentCanvas;

    private RectTransform shadow;
    private Vector2 lastPointerPosition;
    
    private bool isDragging = false;
    private RectTransform bottomPanelRef;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        piece = GetComponent<PuzzlePiece>();
        pieceCanvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (pieceCanvas == null)
            pieceCanvas = gameObject.AddComponent<Canvas>();

        pieceCanvas.overrideSorting = true;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (piece.shadowImage != null)
            shadow = piece.shadowImage.rectTransform;

        scrollRect = GetComponentInParent<ScrollRect>();
        parentCanvas = GetComponentInParent<Canvas>();
        
        if (PuzzleManager.Instance != null)
        {
            bottomPanelRef = PuzzleManager.Instance.bottomPanel;
        }
    }

    void Start()
    {
        if (PuzzleManager.Instance != null && PuzzleManager.Instance.dragArea != null)
        {
            dragArea = PuzzleManager.Instance.dragArea;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Required for drag to work
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!canDrag || isPlaced) return;

        // Convert screen point to drag area local point
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragArea,
            eventData.position,
            eventData.pressEventCamera,
            out lastPointerPosition);

        isDragging = true;

        if (scrollRect != null)
            scrollRect.enabled = false;

        PuzzleManager.Instance.UpdateDragOrder(this);

        MoveGroupToParent(PuzzleManager.Instance.dragParent);
        ShowShadow(true);
        
        Debug.Log($"🟢 DRAG START - {gameObject.name} | World: {rt.position} | Local: {rt.anchoredPosition}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!canDrag || isPlaced || !isDragging) return;

        Vector2 currentPointerPosition;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragArea,
            eventData.position,
            eventData.pressEventCamera,
            out currentPointerPosition))
        {
            Vector2 delta = currentPointerPosition - lastPointerPosition;

            if (piece.group != null && piece.group.pieces.Count > 1)
            {
                foreach (var p in piece.group.pieces)
                {
                    DragPiece d = p.GetComponent<DragPiece>();
                    if (d != null) d.rt.anchoredPosition += delta;
                }
            }
            else
            {
                rt.anchoredPosition += delta;
            }

            lastPointerPosition = currentPointerPosition;
            
            Debug.Log($"🔵 DRAGGING - {gameObject.name} | Pos: {rt.anchoredPosition} | Dist to Bottom: {GetDistanceToBottomPanel():F2}");
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!canDrag || isPlaced || !isDragging) return;

        isDragging = false;

        if (scrollRect != null)
            scrollRect.enabled = true;

        ShowShadow(false);
        
        Debug.Log($"🔴 DRAG END - {gameObject.name} | Pos: {rt.anchoredPosition} | Dist to Bottom: {GetDistanceToBottomPanel():F2}");

        TrySnap();
    }

    private float GetDistanceToBottomPanel()
    {
        if (bottomPanelRef == null) return -1f;
        return Vector3.Distance(rt.position, bottomPanelRef.position);
    }

    void TrySnap()
    {
        // Use the same coordinate space as correctPosition
        float dist = Vector2.Distance(rt.anchoredPosition, correctPosition);

        Debug.Log($"🎯 SNAP CHECK - {gameObject.name} | Distance: {dist:F2} | Current: {rt.anchoredPosition} | Target: {correctPosition}");

        if (dist <= snapThreshold)
        {
            PlacePiece();
        }
        else
        {
            // Return to piece parent
            MoveGroupToParent(PuzzleManager.Instance.pieceParent.GetComponent<RectTransform>());
        }
    }

    void PlacePiece()
    {
        PuzzleGroup currentGroup = piece.group;

        if (currentGroup != null && currentGroup.pieces.Count > 1)
        {
            foreach (var p in currentGroup.pieces)
            {
                DragPiece d = p.GetComponent<DragPiece>();
                d.isPlaced = true;
                d.canDrag = false;
                d.rt.SetParent(PuzzleManager.Instance.pieceParent, false);
                d.rt.anchoredPosition = d.correctPosition;
                if (d.ghostImage != null) d.ghostImage.SetActive(true);
                d.rt.DOScale(Vector3.one * 1.05f, 0.07f).OnComplete(() => d.rt.DOScale(Vector3.one, 0.07f));
            }

            foreach (var p in currentGroup.pieces)
                p.GetComponent<DragPiece>().MergeWithNeighbours();

            PuzzleManager.Instance.OnGroupPlaced(currentGroup.pieces);
        }
        else
        {
            isPlaced = true;
            canDrag = false;
            rt.SetParent(PuzzleManager.Instance.pieceParent, false);
            rt.anchoredPosition = correctPosition;
            if (ghostImage != null) ghostImage.SetActive(true);
            rt.DOScale(Vector3.one * 1.05f, 0.07f).OnComplete(() => rt.DOScale(Vector3.one, 0.07f));
            MergeWithNeighbours();
            PuzzleManager.Instance.OnPiecePlaced(this);
        }

        PuzzleManager.Instance.RefreshSortingOrdersFromList();
    }

    void MergeWithNeighbours()
    {
        float cell = PuzzleManager.Instance.cellSize;
        TryMergeWith(piece.left, new Vector2(-cell, 0));
        TryMergeWith(piece.right, new Vector2(cell, 0));
        TryMergeWith(piece.top, new Vector2(0, cell));
        TryMergeWith(piece.bottom, new Vector2(0, -cell));
    }

    void TryMergeWith(PuzzlePiece neighbour, Vector2 expectedOffset)
    {
        if (neighbour == null) return;
        DragPiece nd = neighbour.GetComponent<DragPiece>();
        if (nd == null || !nd.isPlaced) return;
        if (piece.group != null && piece.group == neighbour.group) return;

        Vector2 actualOffset = nd.rt.anchoredPosition - rt.anchoredPosition;
        if (Vector2.Distance(actualOffset, expectedOffset) > 5f) return;

        PuzzleGroup myGroup = piece.group ?? CreateSoloGroup();
        PuzzleGroup neighbourGroup = neighbour.group ?? nd.CreateSoloGroup();
        if (myGroup == neighbourGroup) return;

        PuzzleGroup dominant = myGroup.pieces.Count >= neighbourGroup.pieces.Count ? myGroup : neighbourGroup;
        PuzzleGroup absorbed = dominant == myGroup ? neighbourGroup : myGroup;

        foreach (var p in absorbed.pieces)
        {
            p.group = dominant;
            dominant.pieces.Add(p);
        }
        absorbed.pieces = new List<PuzzlePiece>();
    }

    PuzzleGroup CreateSoloGroup()
    {
        PuzzleGroup g = new PuzzleGroup();
        g.AddPiece(piece);
        piece.group = g;
        return g;
    }

    void MoveGroupToParent(RectTransform targetParent)
    {
        if (piece.group != null && piece.group.pieces.Count > 1)
        {
            foreach (var p in piece.group.pieces)
            {
                DragPiece d = p.GetComponent<DragPiece>();
                Vector3 keepPos = d.rt.position;
                d.rt.SetParent(targetParent, true);
                d.rt.position = keepPos;
            }
        }
        else
        {
            Vector3 keepPos = rt.position;
            rt.SetParent(targetParent, true);
            rt.position = keepPos;
        }
    }

    void ShowShadow(bool show)
    {
        if (shadow == null) return;
        shadow.gameObject.SetActive(show);
        if (show)
        {
            // Position shadow relative to piece
            shadow.SetParent(rt, false);
            shadow.anchoredPosition = new Vector2(12, -12);
            shadow.SetAsFirstSibling();
        }
        else
        {
            shadow.anchoredPosition = Vector2.zero;
        }
    }

    public void SetPieceSortingOrder(int order)
    {
        pieceCanvas.overrideSorting = true;
        pieceCanvas.sortingOrder = order;

        if (shadow != null)
        {
            Canvas shadowCanvas = shadow.GetComponent<Canvas>();
            if (shadowCanvas == null)
            {
                shadowCanvas = shadow.gameObject.AddComponent<Canvas>();
                shadowCanvas.overrideSorting = true;
            }
            shadowCanvas.sortingOrder = order - 1;
            shadow.SetAsFirstSibling();
        }
    }
    
    public void ResetPiece()
    {
        isPlaced = false;
        canDrag = true;
        
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }
        
        if (ghostImage != null)
            ghostImage.SetActive(false);
    }
    
    public void CheckCompletion()
    {
        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.ShowCompletionCanvas();
        }
    }
    
    public void DebugPrintPosition()
    {
        Debug.Log($"📍 DEBUG - {gameObject.name} | World: {rt.position} | Local: {rt.anchoredPosition} | Dist to Bottom: {GetDistanceToBottomPanel():F2} | Dragging: {isDragging}");
    }
}