using UnityEngine;
using UnityEngine.EventSystems;

public class DragPiece : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    private RectTransform rect;
    private Canvas canvas;
    private PuzzlePiece piece;

    private Vector2 offset;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        piece = GetComponent<PuzzlePiece>();
        if (piece == null)
            piece = GetComponentInParent<PuzzlePiece>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (piece == null || piece.isPlaced) return;

        piece.BringToFront();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect,
            eventData.position,
            eventData.pressEventCamera,
            out offset
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (piece == null || piece.isPlaced) return;

        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out pos
        );

        rect.anchoredPosition = pos - offset;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (piece == null || piece.isPlaced) return;

        float dist = Vector3.Distance(rect.position, piece.targetSlot.position);

        if (dist < piece.snapDistance)
        {
            rect.position = piece.targetSlot.position;
            piece.isPlaced = true;
        }
    }
}