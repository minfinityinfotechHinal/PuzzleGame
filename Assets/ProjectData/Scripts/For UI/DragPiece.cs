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
   
    public float snapThreshold = 120;   // how close is "correct"   
    public bool canDrag = false; 
    public RectTransform dragArea;
    public Vector2 correctPosition;
    public GameObject ghostImage;
    public GameObject particleObject;

    // NEW: Merge system
    private PuzzlePiece piece;
    private bool isMerging = false;

    private void Awake()
    {
        piece = GetComponent<PuzzlePiece>();
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        if (ghostImage != null)
            ghostImage.SetActive(false);
        
        if (particleObject != null)
        {
            particleObject.SetActive(false); // start OFF
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        startPos = rectTransform.anchoredPosition;
        canvasGroup.blocksRaycasts = false;

        // INFORM MANAGER BEFORE MOVING
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.RemoveFromBottom(gameObject);

        transform.SetParent(PuzzleManager.Instance.pieceParent, true);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        Vector2 delta = eventData.delta / canvas.scaleFactor;

        // Move group if merged, otherwise move single piece
        if (piece != null && piece.group != null && piece.group.pieces.Count > 1)
        {
            piece.group.Move(delta);
        }
        else
        {
            rectTransform.anchoredPosition += delta;
        }

        // Ghost image proximity check
        if (ghostImage != null)
        {
            float distance = Vector2.Distance(rectTransform.anchoredPosition, correctPosition);
            if (distance <= snapThreshold * 1.5f)
                ghostImage.SetActive(true);
            else
                ghostImage.SetActive(false);
        }

        // Drag area clamping
        if (dragArea != null)
        {
            Vector3[] corners = new Vector3[4];
            dragArea.GetWorldCorners(corners);

            Vector3 pos = rectTransform.position;

            pos.x = Mathf.Clamp(pos.x, corners[0].x, corners[2].x);
            pos.y = Mathf.Clamp(pos.y, corners[0].y, corners[2].y);

            rectTransform.position = pos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        canvasGroup.blocksRaycasts = true;
        ghostImage?.SetActive(false);

        // Check if piece is near correct position (single placement)
        float distance = Vector2.Distance(rectTransform.anchoredPosition, correctPosition);

        Debug.Log($"Distance: {distance} | Piece: {gameObject.name}");

        if (distance <= snapThreshold && !isMerging)
        {
            Debug.Log("✅ CORRECT DROP: " + gameObject.name);
            if (particleObject != null)
            {
                particleObject.SetActive(true);
            }
            StartCoroutine(SmoothSnap());
        }
        else
        {
            // Try merging with neighbors
            CheckForMerge();
            
            // If no merge happened and piece is far from correct position, reset position
            if (!isMerging)
            {
                Debug.Log("❌ WRONG DROP - resetting position");
                StartCoroutine(SmoothReset());
            }
        }
    }

    IEnumerator SmoothReset()
    {
        Vector2 start = rectTransform.anchoredPosition;
        Vector2 target = startPos;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * 10f;
            
            if (piece != null && piece.group != null && piece.group.pieces.Count > 1)
            {
                foreach (var p in piece.group.pieces)
                {
                    RectTransform r = p.GetComponent<RectTransform>();
                    r.anchoredPosition = Vector2.Lerp(start, target, t);
                }
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.Lerp(start, target, t);
            }
            yield return null;
        }

        if (piece != null && piece.group != null && piece.group.pieces.Count > 1)
        {
            foreach (var p in piece.group.pieces)
            {
                RectTransform r = p.GetComponent<RectTransform>();
                r.anchoredPosition = target;
            }
        }
        else
        {
            rectTransform.anchoredPosition = target;
        }
    }

    IEnumerator SmoothSnap()
    {
        Vector2 start = rectTransform.anchoredPosition;
        Vector2 target = correctPosition;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * 10f;
            
            if (piece != null && piece.group != null && piece.group.pieces.Count > 1)
            {
                foreach (var p in piece.group.pieces)
                {
                    RectTransform r = p.GetComponent<RectTransform>();
                    r.anchoredPosition = Vector2.Lerp(start, target, t);
                }
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.Lerp(start, target, t);
            }
            yield return null;
        }

        if (piece != null && piece.group != null && piece.group.pieces.Count > 1)
        {
            foreach (var p in piece.group.pieces)
            {
                RectTransform r = p.GetComponent<RectTransform>();
                r.anchoredPosition = target;
            }
        }
        else
        {
            rectTransform.anchoredPosition = target;
        }

        isPlaced = true;

        // INFORM MANAGER
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.OnPiecePlaced(this);
    }

    void CheckForMerge()
    {
        if (isMerging || piece == null) return;

        foreach (var other in PuzzleManager.Instance.allPieces)
        {
            if (other == piece) continue;
            if (other.group == piece.group) continue;
            if (IsNeighbor(other) && IsCorrectMatch(other) && IsEdgeMatch(other))
            {
                isMerging = true;
                SnapExactlyAndMerge(other);
                Invoke(nameof(ResetMerge), 0.1f);
                break;
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

    bool IsEdgeMatch(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();
        RectTransform otherRect = other.GetComponent<RectTransform>();

        Vector2 myPos = myRect.anchoredPosition;
        Vector2 otherPos = otherRect.anchoredPosition;

        float cell = PuzzleManager.Instance.cellSize;
        float snapDistance = 35f;
        float alignTolerance = 25f;

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
        Vector2 offset = Vector2.zero;

        // RIGHT
        if (other == piece.right)
        {
            float errorX = (otherRect.anchoredPosition.x - myRect.anchoredPosition.x) - cell;
            float errorY = otherRect.anchoredPosition.y - myRect.anchoredPosition.y;
            offset = new Vector2(errorX, errorY);
        }
        // LEFT
        else if (other == piece.left)
        {
            float errorX = (myRect.anchoredPosition.x - otherRect.anchoredPosition.x) - cell;
            float errorY = otherRect.anchoredPosition.y - myRect.anchoredPosition.y;
            offset = new Vector2(-errorX, errorY);
        }
        // TOP
        else if (other == piece.top)
        {
            float errorY = (otherRect.anchoredPosition.y - myRect.anchoredPosition.y) - cell;
            float errorX = otherRect.anchoredPosition.x - myRect.anchoredPosition.x;
            offset = new Vector2(errorX, errorY);
        }
        // BOTTOM
        else if (other == piece.bottom)
        {
            float errorY = (myRect.anchoredPosition.y - otherRect.anchoredPosition.y) - cell;
            float errorX = otherRect.anchoredPosition.x - myRect.anchoredPosition.x;
            offset = new Vector2(errorX, -errorY);
        }

        Debug.Log($"OFFSET APPLIED: {offset}");

        // APPLY TO WHOLE GROUP
        foreach (var p in piece.group.pieces)
        {
            RectTransform r = p.GetComponent<RectTransform>();
            r.SetParent(PuzzleManager.Instance.pieceParent, true);
            r.anchoredPosition += offset;
            r.anchoredPosition = new Vector2(
                Mathf.Round(r.anchoredPosition.x),
                Mathf.Round(r.anchoredPosition.y)
            );
        }

        piece.group.Merge(other.group);
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