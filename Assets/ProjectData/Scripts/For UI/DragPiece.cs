using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
{
    public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas canvas;
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public float snapThreshold = 120f;
    public bool canDrag = false;
    public RectTransform dragArea;
    public Vector2 correctPosition;

    // ✅ REQUIRED (used in other scripts)
    public GameObject ghostImage;
    public GameObject particleObject;

    public PuzzlePiece piece;

    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float directionThreshold = 10f;

    private bool isScrolling = false;
    private bool isDraggingPiece = false;
    private bool directionDecided = false;
    private bool scrollDragStarted = false;

    private void Awake()
    {
        piece = GetComponent<PuzzlePiece>();
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (scrollRect == null && PuzzleManager.Instance != null)
            scrollRect = PuzzleManager.Instance.scrollRect;

        if (ghostImage != null) ghostImage.SetActive(false);
        if (particleObject != null) particleObject.SetActive(false);
    }

    // ========================= START =========================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        isScrolling = false;
        isDraggingPiece = false;
        directionDecided = false;
        scrollDragStarted = false;

        canvasGroup.blocksRaycasts = true;

        scrollRect?.OnInitializePotentialDrag(eventData);
    }

    // ========================= DRAG =========================
    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        Vector2 delta = eventData.delta / canvas.scaleFactor;

        if (!directionDecided)
        {
            if (delta.magnitude < directionThreshold)
                return;

            directionDecided = true;

            bool isHorizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);

            if (isHorizontal)
            {
                isScrolling = true;
                isDraggingPiece = false;
            }
            else
            {
                isDraggingPiece = true;
                isScrolling = false;

                transform.SetParent(PuzzleManager.Instance.pieceParent, true);
                transform.SetAsLastSibling();
            }
        }

        // 👉 SCROLL
        if (isScrolling)
        {
            canvasGroup.blocksRaycasts = false;

            if (!scrollDragStarted)
            {
                scrollRect?.OnBeginDrag(eventData);
                scrollDragStarted = true;
            }

            scrollRect?.OnDrag(eventData);
            return;
        }

        // 👉 DRAG PIECE
        if (isDraggingPiece)
        {
            canvasGroup.blocksRaycasts = true;

            if (piece != null && piece.group != null && piece.group.pieces.Count > 1)
                piece.group.Move(delta);
            else
                rectTransform.anchoredPosition += delta;
        }
    }

    // ========================= END =========================
    public void OnEndDrag(PointerEventData eventData)
    {
        if (isScrolling)
            scrollRect?.OnEndDrag(eventData);

        isScrolling = false;
        isDraggingPiece = false;

        canvasGroup.blocksRaycasts = true;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        scrollRect?.OnInitializePotentialDrag(eventData);
    }

    // ========================= REQUIRED METHODS =========================

    // ✅ Fix for PuzzleManager
    public void SetPieceSortingOrder(int baseOrder)
    {
        Canvas c = GetComponent<Canvas>();
        if (c == null)
        {
            c = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }

        c.overrideSorting = true;
        c.sortingOrder = baseOrder;
    }

    // ✅ Fix for PowerUpButtons
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

    // ✅ Fix for completion system
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