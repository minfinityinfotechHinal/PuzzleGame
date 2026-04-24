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
    public float jigsawTabOverlap = 10f;  

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

            // 🔥 Only check pieces that are physically close on screen
            float physicalDistance = Vector2.Distance(
                piece.GetComponent<RectTransform>().anchoredPosition,
                other.GetComponent<RectTransform>().anchoredPosition
            );
            
            // Skip if pieces are too far apart (more than 2x cell size)
            float maxMergeDistance = PuzzleManager.Instance.cellSize * 2.5f;
            if (physicalDistance > maxMergeDistance)
                continue;

            bool neighbor = IsNeighbor(other);
            bool correctMatch = IsCorrectMatch(other);
            bool edgeMatch = false;
            
            if (neighbor && correctMatch)
            {
                edgeMatch = IsEdgeMatch(other);
                Debug.Log($"🧩 {gameObject.name} ↔ {other.name}: neighbor={neighbor}, correct={correctMatch}, edgeMatch={edgeMatch}, physicalDist={physicalDistance:F1}");
            }

            if (neighbor && correctMatch && edgeMatch)
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
        
        float snapDistance = 50f;
        float dist = Vector2.Distance(myPos, otherPos);
        
        // Only consider pieces that are within reasonable range
        // One piece-width distance maximum
        float maxDist = Mathf.Max(myRect.rect.width, myRect.rect.height) * 1.5f;
        
        Debug.Log($"🧩 {gameObject.name} ↔ {other.name}: dist={dist:F1}, maxDist={maxDist:F1}");
        
        return dist <= maxDist && dist >= 20f; // Not overlapping, but close
    }

   private void SnapExactlyAndMerge(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();
        RectTransform otherRect = other.GetComponent<RectTransform>();
        
        Vector2 myPos = myRect.anchoredPosition;
        Vector2 otherPos = otherRect.anchoredPosition;
        
        // 🔥 Instead of calculating offset, directly snap the moving group
        // so that the two connecting pieces are at the correct relative positions
        
        DragPiece myDrag = piece.GetComponent<DragPiece>();
        DragPiece otherDrag = other.GetComponent<DragPiece>();
        
        // Get the exact grid offset from the original correct positions
        Vector2 gridOffset = otherDrag.correctPosition - myDrag.correctPosition;
        
        // Move the dragged piece so the other piece is at: draggedPiecePos + gridOffset
        Vector2 targetOtherPos = myPos + gridOffset;
        Vector2 offset = targetOtherPos - otherPos;
        
        // 🔥 Apply to ALL pieces in the moving group (the group being dragged)
        foreach (var p in piece.group.pieces)
        {
            RectTransform r = p.GetComponent<RectTransform>();
            r.SetParent(PuzzleManager.Instance.pieceParent, true);
            r.anchoredPosition += offset;
        }
        
        // 🔥 After moving, verify and snap to exact grid positions
        foreach (var p in piece.group.pieces)
        {
            RectTransform r = p.GetComponent<RectTransform>();
            DragPiece drag = p.GetComponent<DragPiece>();
            
            // Calculate where this piece should be relative to the other piece's current position
            Vector2 relativeToOther = drag.correctPosition - otherDrag.correctPosition;
            Vector2 exactPos = otherRect.anchoredPosition + relativeToOther;
            
            r.anchoredPosition = exactPos;
        }
        
        piece.group.Merge(other.group);
        Debug.Log($"🔗 Merged! Grid-based alignment applied to {piece.group.pieces.Count} pieces");
    }

    // ──────────────────────────
    // SNAP TO CORRECT GRID POSITION
    // ──────────────────────────
    IEnumerator SmoothSnap()
{
    // 🔥 Instead of moving by one delta, move each piece to its own correct position
    Dictionary<PuzzlePiece, Vector2> startPositions = new Dictionary<PuzzlePiece, Vector2>();
    Dictionary<PuzzlePiece, Vector2> targetPositions = new Dictionary<PuzzlePiece, Vector2>();
    
    foreach (var p in piece.group.pieces)
    {
        DragPiece drag = p.GetComponent<DragPiece>();
        if (drag != null)
        {
            RectTransform r = p.GetComponent<RectTransform>();
            startPositions[p] = r.anchoredPosition;
            targetPositions[p] = drag.correctPosition;
        }
    }
    
    float t = 0f;
    while (t < 1f)
    {
        t += Time.deltaTime * 8f;
        float progress = t * t * (3f - 2f * t); // Smooth step
        
        foreach (var kv in startPositions)
        {
            PuzzlePiece p = kv.Key;
            RectTransform r = p.GetComponent<RectTransform>();
            if (r != null)
            {
                r.anchoredPosition = Vector2.Lerp(kv.Value, targetPositions[p], progress);
            }
        }
        yield return null;
    }
    
    // Final exact positions
    foreach (var kv in targetPositions)
    {
        PuzzlePiece p = kv.Key;
        RectTransform r = p.GetComponent<RectTransform>();
        if (r != null)
        {
            r.anchoredPosition = kv.Value;
        }
    }
    
    // Mark ALL pieces as placed
    foreach (var p in piece.group.pieces)
    {
        DragPiece drag = p.GetComponent<DragPiece>();
        if (drag != null)
        {
            drag.isPlaced = true;
            drag.canDrag = false;
        }
    }
    
    // Notify manager
    if (PuzzleManager.Instance != null)
        PuzzleManager.Instance.OnGroupPlaced(piece.group.pieces);
    
    Debug.Log($"✅ Group snapped: {piece.group.pieces.Count} pieces to their correct positions");
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