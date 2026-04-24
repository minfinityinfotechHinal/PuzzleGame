using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    [Header("Settings")]
    public float snapThreshold = 120f;   // distance to correct position for snapping
    public bool canDrag = false;
    public RectTransform dragArea;
    public Vector2 correctPosition;      // where this piece should finally sit
    public GameObject ghostImage;
    public GameObject particleObject;

    // Merge system
    private PuzzlePiece piece;
    private bool isMerging = false;
    
    // Cooldown to avoid double‑merge in one frame
    private float mergeCooldown = 0.1f;
    private float lastMergeTime = -1f;

    // 👇 NEW: stores the start position of every piece in the group when drag begins
    private Dictionary<PuzzlePiece, Vector2> groupStartPositions = new Dictionary<PuzzlePiece, Vector2>();

    private void Awake()
    {
        piece = GetComponent<PuzzlePiece>();
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (ghostImage != null) ghostImage.SetActive(false);
        if (particleObject != null) particleObject.SetActive(false);
    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        canvasGroup.blocksRaycasts = false;

        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.RemoveFromBottom(gameObject);

        transform.SetParent(PuzzleManager.Instance.pieceParent, true);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        Vector2 delta = eventData.delta / canvas.scaleFactor;

        if (piece.group != null && piece.group.pieces.Count > 1)
            piece.group.Move(delta);
        else
            rectTransform.anchoredPosition += delta;

        // Clamp within drag area
        if (dragArea != null)
        {
            Rect areaRect = dragArea.rect;
            Vector2 pos = rectTransform.anchoredPosition;

            float minX = -areaRect.width / 2f;
            float maxX = areaRect.width / 2f;
            float minY = -areaRect.height / 2f;
            float maxY = areaRect.height / 2f;

            Vector2 clamped = new Vector2(
                Mathf.Clamp(pos.x, minX, maxX),
                Mathf.Clamp(pos.y, minY, maxY)
            );

            Vector2 correction = clamped - pos;
            if (piece.group != null && piece.group.pieces.Count > 1)
                piece.group.Move(correction);
            else
                rectTransform.anchoredPosition += correction;
        }

        if (ghostImage != null)
        {
            float distance = Vector2.Distance(rectTransform.anchoredPosition, correctPosition);
            ghostImage.SetActive(distance <= snapThreshold * 1.5f);
        }
    }


    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced || !canDrag) return;

        canvasGroup.blocksRaycasts = true;
        ghostImage?.SetActive(false);

        float distance = Vector2.Distance(rectTransform.anchoredPosition, correctPosition);

        if (distance <= snapThreshold)
        {
            Debug.Log("✅ CORRECT DROP: " + gameObject.name);
            if (particleObject != null) particleObject.SetActive(true);
            StartCoroutine(SmoothSnap());
            return;
        }

        CheckForMerge();
        // If neither correct nor merge, piece stays where it was dropped.
    }

    private void CheckForMerge()
    {
        if (Time.time - lastMergeTime < mergeCooldown) return;
        if (isMerging || piece == null) return;

        foreach (var other in PuzzleManager.Instance.allPieces)
        {
            if (other == piece) continue;
            if (other.group == piece.group) continue;

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
        return other == piece.left ||
               other == piece.right ||
               other == piece.top ||
               other == piece.bottom;
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
        Vector2 myPos = myRect.anchoredPosition;
        Vector2 otherPos = otherRect.anchoredPosition;
        float cell = PuzzleManager.Instance.cellSize;
        float snapDistance = 35f;
        float alignTolerance = 25f;

        if (other == piece.right)
        {
            float gap = (otherPos.x - myPos.x) - cell;
            float align = Mathf.Abs(otherPos.y - myPos.y);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }
        if (other == piece.left)
        {
            float gap = (myPos.x - otherPos.x) - cell;
            float align = Mathf.Abs(otherPos.y - myPos.y);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }
        if (other == piece.top)
        {
            float gap = (otherPos.y - myPos.y) - cell;
            float align = Mathf.Abs(otherPos.x - myPos.x);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }
        if (other == piece.bottom)
        {
            float gap = (myPos.y - otherPos.y) - cell;
            float align = Mathf.Abs(otherPos.x - myPos.x);
            return Mathf.Abs(gap) <= snapDistance && align <= alignTolerance;
        }
        return false;
    }

    private void SnapExactlyAndMerge(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();
        RectTransform otherRect = other.GetComponent<RectTransform>();
        float cell = PuzzleManager.Instance.cellSize;
        Vector2 myPos = myRect.anchoredPosition;
        Vector2 otherPos = otherRect.anchoredPosition;
        Vector2 offset = Vector2.zero;

        if (other == piece.right)
        {
            float errorX = (otherPos.x - myPos.x) - cell;
            float errorY = otherPos.y - myPos.y;
            offset = new Vector2(errorX, errorY);
        }
        else if (other == piece.left)
        {
            float errorX = (myPos.x - otherPos.x) - cell;
            float errorY = otherPos.y - myPos.y;
            offset = new Vector2(-errorX, errorY);
        }
        else if (other == piece.top)
        {
            float errorY = (otherPos.y - myPos.y) - cell;
            float errorX = otherPos.x - myPos.x;
            offset = new Vector2(errorX, errorY);
        }
        else if (other == piece.bottom)
        {
            float errorY = (myPos.y - otherPos.y) - cell;
            float errorX = otherPos.x - myPos.x;
            offset = new Vector2(errorX, -errorY);
        }

        // Move the entire moving group to snap exactly
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
        Debug.Log("🔗 Merged with neighbour!");
    }

    // ──────────────────────────
    // SNAP TO CORRECT GRID POSITION
    // ──────────────────────────
    IEnumerator SmoothSnap()
    {
        Vector2 myCurrent = rectTransform.anchoredPosition;
        Vector2 myTarget = correctPosition;
        Vector2 groupDelta = myTarget - myCurrent;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 10f;
            Vector2 step = Vector2.Lerp(Vector2.zero, groupDelta, t) - (rectTransform.anchoredPosition - myCurrent);
            if (piece.group.pieces.Count > 1)
                piece.group.Move(step);
            else
                rectTransform.anchoredPosition += step;
            yield return null;
        }

        if (piece.group.pieces.Count > 1)
        {
            Vector2 finalCorrection = myTarget - rectTransform.anchoredPosition;
            piece.group.Move(finalCorrection);
        }
        else
        {
            rectTransform.anchoredPosition = myTarget;
        }

        // 🔥 FIX: Mark ALL pieces in the group as placed
        foreach (var p in piece.group.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();
            if (drag != null)
            {
                drag.isPlaced = true;
                drag.canDrag = false;
            }
        }

        // Notify manager (only once, not per piece)
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.OnGroupPlaced(piece.group.pieces);
    }
 

    // ──────────────────────────
    // RESET TO DRAG‑START POSITIONS (FIXED)
    // ──────────────────────────
    

   public void ResetPiece()
    {
        isPlaced = false;
        canvasGroup.blocksRaycasts = true;
    }

}