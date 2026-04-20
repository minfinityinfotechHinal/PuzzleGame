using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Vector2 startPos;

    public bool canDrag = false;
    public RectTransform dragArea;
    public Vector2 correctPosition;

    private PuzzlePiece piece;

    bool isMerging = false;

    private void Awake()
    {
        piece = GetComponent<PuzzlePiece>();
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        startPos = rectTransform.anchoredPosition;
        canvasGroup.blocksRaycasts = false;

        PuzzleManager.Instance.RemoveFromBottom(gameObject);

        transform.SetParent(PuzzleManager.Instance.pieceParent, true);
        transform.SetAsLastSibling();
    }

    Vector2 GetGridPosition(PuzzlePiece p)
{
    float x = PuzzleManager.Instance.gridOrigin.x + p.col * PuzzleManager.Instance.cellSize;
    float y = PuzzleManager.Instance.gridOrigin.y - p.row * PuzzleManager.Instance.cellSize;

    return new Vector2(x, y);
}

    public void OnDrag(PointerEventData eventData)
{
    if (isPlaced || !canDrag) return;

    Vector2 delta = eventData.delta / canvas.scaleFactor;

    // Move group first
    piece.group.Move(delta);

    if (dragArea != null)
    {
        RectTransform mainRect = rectTransform;

        Vector2 pos = mainRect.anchoredPosition;

        float minX = -dragArea.rect.width / 2f;
        float maxX = dragArea.rect.width / 2f;
        float minY = -dragArea.rect.height / 2f;
        float maxY = dragArea.rect.height / 2f;

        Vector2 clamped = new Vector2(
            Mathf.Clamp(pos.x, minX, maxX),
            Mathf.Clamp(pos.y, minY, maxY)
        );

        Vector2 correction = clamped - pos;

        // Apply correction to whole group (IMPORTANT)
        piece.group.Move(correction);
    }
}

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        canvasGroup.blocksRaycasts = true;

        CheckForMerge(); // ✅ Only here
    }

void CheckForMerge()
{
    if (isMerging) return;

    foreach (var other in PuzzleManager.Instance.allPieces)
    {
        if (other == piece) continue;
        if (other.group == piece.group) continue;

        if (IsNeighbor(other) && IsCorrectMatch(other) && IsEdgeMatch(other))
        {
            if (IsNearCorrectPosition(other))
            {
                isMerging = true;
                SnapExactlyAndMerge(other);
                Invoke(nameof(ResetMerge), 0.1f);
                break;
            }
        }
    }
}
bool IsNeighbor(PuzzlePiece other)
{
    return other == piece.left ||
           other == piece.right ||
           other == piece.top ||
           other == piece.bottom;
}

bool IsNearCorrectPosition(PuzzlePiece other)
{
    RectTransform myRect = piece.GetComponent<RectTransform>();
    RectTransform otherRect = other.GetComponent<RectTransform>();

    float cell = PuzzleManager.Instance.cellSize;

    Vector2 expected = otherRect.anchoredPosition;

    if (other == piece.right)
            expected += new Vector2(-cell, 0);
        else if (other == piece.left)
            expected += new Vector2(cell, 0);
        else if (other == piece.top)
            expected += new Vector2(0, -cell);
        else if (other == piece.bottom)
            expected += new Vector2(0, cell);
    float dist = Vector2.Distance(myRect.anchoredPosition, expected);

    Debug.Log($"REL DIST: {dist}");

    return dist < 80f; // you can tune
}

    // 🔥 EDGE MATCH LOGIC (MAIN FIX)
   bool IsEdgeMatch(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();
        RectTransform otherRect = other.GetComponent<RectTransform>();

        Vector2 myPos = myRect.anchoredPosition;
        Vector2 otherPos = otherRect.anchoredPosition;

        float cell = PuzzleManager.Instance.cellSize;

        float snapDistance = 35f;     // 🔥 increased (IMPORTANT)
        float alignTolerance = 25f;   // 🔥 increased (IMPORTANT)

        // RIGHT
        if (other == piece.right)
        {
            float gap = (otherPos.x - myPos.x) - cell;
            float align = Mathf.Abs(otherPos.y - myPos.y);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }

        // LEFT
        if (other == piece.left)
        {
            float gap = (myPos.x - otherPos.x) - cell;
            float align = Mathf.Abs(otherPos.y - myPos.y);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }

        // TOP
        if (other == piece.top)
        {
            float gap = (otherPos.y - myPos.y) - cell;
            float align = Mathf.Abs(otherPos.x - myPos.x);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }

        // BOTTOM
        if (other == piece.bottom)
        {
            float gap = (myPos.y - otherPos.y) - cell;
            float align = Mathf.Abs(otherPos.x - myPos.x);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }

        return false;
    }

    void SnapExactlyAndMerge(PuzzlePiece other)
{
    RectTransform myRect = piece.GetComponent<RectTransform>();
    RectTransform otherRect = other.GetComponent<RectTransform>();

    float cell = PuzzleManager.Instance.cellSize;

    Vector2 myPos = myRect.anchoredPosition;
    Vector2 otherPos = otherRect.anchoredPosition;

    Vector2 offset = Vector2.zero;

    // ✅ RIGHT
    if (other == piece.right)
    {
        float errorX = (otherPos.x - myPos.x) - cell; // signed gap
        float errorY = otherPos.y - myPos.y;

        offset = new Vector2(errorX, errorY);
    }

    // ✅ LEFT
    else if (other == piece.left)
    {
        float errorX = (myPos.x - otherPos.x) - cell;
        float errorY = otherPos.y - myPos.y;

        offset = new Vector2(-errorX, errorY);
    }

    // ✅ TOP
    else if (other == piece.top)
    {
        float errorY = (otherPos.y - myPos.y) - cell;
        float errorX = otherPos.x - myPos.x;

        offset = new Vector2(errorX, errorY);
    }

    // ✅ BOTTOM
    else if (other == piece.bottom)
    {
        float errorY = (myPos.y - otherPos.y) - cell;
        float errorX = otherPos.x - myPos.x;

        offset = new Vector2(errorX, -errorY);
    }

    Debug.Log($"OFFSET APPLIED: {offset}");

    // 🔥 APPLY TO WHOLE GROUP
    foreach (var p in piece.group.pieces)
    {
        RectTransform r = p.GetComponent<RectTransform>();

        r.SetParent(PuzzleManager.Instance.pieceParent, true);
        r.anchoredPosition += offset;

        // pixel perfect (IMPORTANT)
        r.anchoredPosition = new Vector2(
            Mathf.Round(r.anchoredPosition.x),
            Mathf.Round(r.anchoredPosition.y)
        );
    }

    piece.group.Merge(other.group);
}

    bool IsCorrectMatch(PuzzlePiece other)
    {
        if (other == piece.right && other.col == piece.col + 1 && other.row == piece.row)
            return true;

        if (other == piece.left && other.col == piece.col - 1 && other.row == piece.row)
            return true;

        if (other == piece.top && other.row == piece.row - 1 && other.col == piece.col)
            return true;

        if (other == piece.bottom && other.row == piece.row + 1 && other.col == piece.col)
            return true;

        return false;
    }

    void ResetMerge()
    {
        isMerging = false;
    }

    public void ResetPiece()
    {
        isPlaced = false;
        canvasGroup.blocksRaycasts = true;
    }
}