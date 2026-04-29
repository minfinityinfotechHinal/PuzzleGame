using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; 

public class DragPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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
    public GameObject ghostImage;
    public GameObject particleObject;

    public PuzzlePiece piece;
    private bool isMerging = false;
    
    private float mergeCooldown = 0.1f;
    private float lastMergeTime = -1f;
     private int originalSiblingIndex;
    private CanvasGroup shadowCanvasGroup;

    private void Awake()
    {
        piece = GetComponent<PuzzlePiece>();
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        // Find shadow's Canvas component if it exists
        if (piece != null && piece.shadowImage != null)
        {
            shadowCanvasGroup = piece.shadowImage.GetComponent<CanvasGroup>();
            if (shadowCanvasGroup == null)
                shadowCanvasGroup = piece.shadowImage.gameObject.AddComponent<CanvasGroup>();
        }

        if (ghostImage != null) ghostImage.SetActive(false);
        if (particleObject != null) particleObject.SetActive(false);
    }


    public void OnBeginDrag(PointerEventData eventData)
{
    if (isPlaced || !canDrag) return;

    // Update drag order - this piece becomes the latest dragged
    if (PuzzleManager.Instance != null)
    {
        // Check if we're dragging a group
        if (piece.group != null && piece.group.pieces.Count > 1)
        {
            // Bring entire group to front
            PuzzleManager.Instance.BringGroupToFront(piece.group);
        }
        else
        {
            // Single piece
            PuzzleManager.Instance.UpdateDragOrder(this);
        }
    }

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
            ClampGroupToDragArea();
        }

        // Show ghost when any piece in group is near correct position
        if (ghostImage != null && piece.group != null)
        {
            bool anyNearCorrect = piece.group.IsAnyPieceNearCorrectPosition(snapThreshold * 1.5f);
            ghostImage.SetActive(anyNearCorrect);
        }
    }

    private void ClampGroupToDragArea()
    {
        if (dragArea == null) return;
        
        Rect areaRect = dragArea.rect;
        
        // Find bounds of entire group
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        
        List<RectTransform> groupRects = new List<RectTransform>();
        foreach (var p in piece.group.pieces)
        {
            RectTransform r = p.GetComponent<RectTransform>();
            if (r != null)
            {
                groupRects.Add(r);
                Vector2 pos = r.anchoredPosition;
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxY = Mathf.Max(maxY, pos.y);
            }
        }
        
        // Calculate correction needed
        float areaMinX = -areaRect.width / 2f;
        float areaMaxX = areaRect.width / 2f;
        float areaMinY = -areaRect.height / 2f;
        float areaMaxY = areaRect.height / 2f;
        
        Vector2 correction = Vector2.zero;
        
        if (minX < areaMinX) correction.x = areaMinX - minX;
        if (maxX > areaMaxX) correction.x = areaMaxX - maxX;
        if (minY < areaMinY) correction.y = areaMinY - minY;
        if (maxY > areaMaxY) correction.y = areaMaxY - maxY;
        
        if (correction != Vector2.zero)
        {
            foreach (var r in groupRects)
            {
                r.anchoredPosition += correction;
            }
        }
    }
public void SetPieceSortingOrder(int baseOrder)
{
    // Use baseOrder for piece, baseOrder+1 for shadow
    int pieceOrder = baseOrder;
    int shadowOrder = baseOrder + 1;
    
    // Set main piece sorting
    Canvas mainCanvas = GetComponent<Canvas>();
    if (mainCanvas == null)
    {
        mainCanvas = gameObject.AddComponent<Canvas>();
        
        GraphicRaycaster graphicRaycaster = GetComponent<GraphicRaycaster>();
        if (graphicRaycaster == null)
        {
            graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
        }
    }
    mainCanvas.overrideSorting = true;
    mainCanvas.sortingOrder = pieceOrder;
    
    // Set shadow sorting to NEXT number
    if (piece != null && piece.shadowImage != null)
    {
        Canvas shadowCanvas = piece.shadowImage.GetComponent<Canvas>();
        if (shadowCanvas == null)
        {
            shadowCanvas = piece.shadowImage.gameObject.AddComponent<Canvas>();
        }
        shadowCanvas.overrideSorting = true;
        shadowCanvas.sortingOrder = shadowOrder;
    }
    
    Debug.Log($"🎨 {gameObject.name} - Piece order: {pieceOrder}, Shadow order: {shadowOrder}");
}

    // Add this new method to DragPiece.cs
// Add this method to DragPiece.cs
public void BringPlacedPieceToFront()
{
    if (piece == null || piece.group == null) return;
    
    // Find the highest sorting order among ALL pieces
    int maxOrder = 0;
    foreach (var p in PuzzleManager.Instance.allPieces)
    {
        if (p != null)
        {
            Canvas pieceCanvas = p.GetComponent<Canvas>();
            if (pieceCanvas != null && pieceCanvas.overrideSorting)
            {
                maxOrder = Mathf.Max(maxOrder, pieceCanvas.sortingOrder);
            }
        }
    }
    
    // Set this group to be on top (highest order)
    int newOrder = maxOrder + 1;
    
    foreach (var p in piece.group.pieces)
    {
        DragPiece drag = p.GetComponent<DragPiece>();
        if (drag != null)
        {
            drag.SetPieceSortingOrder(newOrder);
        }
    }
    
    Debug.Log($"✨ Brought group to front with order {newOrder}");
}
    public void OnEndDrag(PointerEventData eventData)
{
    if (isPlaced || !canDrag) return;
    
    canvasGroup.blocksRaycasts = true;
    ghostImage?.SetActive(false);

    // Check if the reference piece itself is near its correct position
    float distance = Vector2.Distance(rectTransform.anchoredPosition, correctPosition);

    if (distance <= snapThreshold)
    {
        Debug.Log($"✅ CORRECT DROP: {gameObject.name} (distance: {distance:F1})");
        if (particleObject != null) particleObject.SetActive(true);
        StartCoroutine(SmoothSnapToCorrectPositions());
        return;
    }

    // Check if ANY piece in group is near its correct position
    if (piece.group != null && piece.group.pieces.Count > 1)
    {
        PuzzlePiece closestPiece = piece.group.GetClosestToCorrectPosition();
        if (closestPiece != null)
        {
            DragPiece closestDrag = closestPiece.GetComponent<DragPiece>();
            RectTransform closestRect = closestPiece.GetComponent<RectTransform>();
            
            if (closestDrag != null && closestRect != null)
            {
                float closestDist = Vector2.Distance(closestRect.anchoredPosition, closestDrag.correctPosition);
                
                if (closestDist <= snapThreshold)
                {
                    Debug.Log($"✅ CORRECT DROP via group: {closestPiece.name} (distance: {closestDist:F1})");
                    piece = closestPiece;
                    if (particleObject != null) particleObject.SetActive(true);
                    StartCoroutine(SmoothSnapToCorrectPositions());
                    return;
                }
            }
        }
    }

    // If not near correct position, check for merge with neighbors
    CheckForMerge();
    
    // Refresh orders after drag ends (in case no snap/merge happened)
    if (PuzzleManager.Instance != null)
    {
        PuzzleManager.Instance.RefreshSortingOrdersFromList();
    }
}

    private void CheckForMerge()
    {
        if (Time.time - lastMergeTime < mergeCooldown) return;
        if (isMerging || piece == null) return;

        foreach (var other in PuzzleManager.Instance.allPieces)
        {
            if (other == piece) continue;
            if (other.group == piece.group) continue;

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

    public void CheckCompletion()
    {
        int placedCount = 0;
        foreach (var p in PuzzleManager.Instance.allPieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();
            if (drag != null && drag.isPlaced)
            {
                placedCount++;
            }
        }
        
        Debug.Log($"🔍 Manual check: {placedCount}/{PuzzleManager.Instance.TotalPieces} pieces placed");
        
        if (placedCount >= PuzzleManager.Instance.TotalPieces)
        {
            PuzzleManager.Instance.ShowCompletionCanvas();
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
        
        float dist = Vector2.Distance(myPos, otherPos);
        float maxDist = Mathf.Max(myRect.rect.width, myRect.rect.height) * 1.5f;
        
        return dist <= maxDist && dist >= 20f;
    }

    private void SnapExactlyAndMerge(PuzzlePiece other)
    {
        RectTransform myRect = piece.GetComponent<RectTransform>();
        RectTransform otherRect = other.GetComponent<RectTransform>();
        
        DragPiece myDrag = piece.GetComponent<DragPiece>();
        DragPiece otherDrag = other.GetComponent<DragPiece>();
        
        if (myDrag == null || otherDrag == null) return;
        
        // Calculate EXACT offset based on correct positions
        Vector2 correctOffset = myDrag.correctPosition - otherDrag.correctPosition;
        Vector2 targetMyPos = otherRect.anchoredPosition + correctOffset;
        Vector2 offset = targetMyPos - myRect.anchoredPosition;
        
        // Safety check - don't merge if offset is too large
        if (offset.magnitude > snapThreshold * 3f)
        {
            Debug.Log($"⚠️ Merge offset too large ({offset.magnitude:F1}) - skipping merge");
            return;
        }
        
        // Move all pieces in my group to correct relative position
        foreach (var p in piece.group.pieces)
        {
            RectTransform r = p.GetComponent<RectTransform>();
            if (r != null)
            {
                r.SetParent(PuzzleManager.Instance.pieceParent, true);
                r.anchoredPosition += offset;
            }
        }
        
        // Only merge if other piece is in puzzle area
        if (otherRect.parent == PuzzleManager.Instance.pieceParent)
        {
            PuzzleGroup otherGroup = other.group;
            PuzzleGroup myGroup = piece.group;
            
            if (otherGroup != null && myGroup != null && otherGroup != myGroup)
            {
                myGroup.Merge(otherGroup);
                
                if (PuzzleManager.Instance != null)
                    PuzzleManager.Instance.RemoveFromBottom(other.gameObject);
                
                Debug.Log($"🔗 Merged! Group now has {myGroup.pieces.Count} pieces.");
                
                // 👇 ADD THIS: Bring the merged group to front
                if (PuzzleManager.Instance != null)
                {
                    PuzzleManager.Instance.BringGroupToFront(myGroup);
                }
                
                // Check if group should snap after merge
                CheckGroupSnapAfterMerge(myGroup);
            }
        }
    }

    /// <summary>
    /// After merge, check if group is at correct position and snap if needed
    /// </summary>
    private void CheckGroupSnapAfterMerge(PuzzleGroup mergedGroup)
    {
        if (mergedGroup == null) return;
        
        bool shouldSnap = false;
        PuzzlePiece closestPiece = null;
        float closestDistance = float.MaxValue;
        
        foreach (var p in mergedGroup.pieces)
        {
            DragPiece drag = p.GetComponent<DragPiece>();
            RectTransform rect = p.GetComponent<RectTransform>();
            
            if (drag != null && rect != null && !drag.isPlaced)
            {
                float dist = Vector2.Distance(rect.anchoredPosition, drag.correctPosition);
                
                if (dist <= snapThreshold && dist < closestDistance)
                {
                    closestDistance = dist;
                    closestPiece = p;
                    shouldSnap = true;
                }
            }
        }
        
        if (shouldSnap && closestPiece != null)
        {
            Debug.Log($"🔧 Auto-snapping group after merge via {closestPiece.name} (distance: {closestDistance:F1})");
            
            piece = closestPiece;
            StartCoroutine(SmoothSnapToCorrectPositions());
        }
        else
        {
            Debug.Log($"🔗 Merged group but no piece is near correct position - not snapping");
        }
    }

    /// <summary>
    /// NEW: Only snaps pieces that are actually near their individual correct positions
    /// </summary>
    /// 
    IEnumerator SmoothSnapToCorrectPositions()
{
    if (piece == null || piece.group == null) yield break;
    
    // Collect pieces that need to snap - each to their OWN correct position
    Dictionary<PuzzlePiece, Vector2> startPositions = new Dictionary<PuzzlePiece, Vector2>();
    Dictionary<PuzzlePiece, Vector2> targetPositions = new Dictionary<PuzzlePiece, Vector2>();
    List<PuzzlePiece> piecesToSnap = new List<PuzzlePiece>();
    
    foreach (var p in piece.group.pieces)
    {
        DragPiece drag = p.GetComponent<DragPiece>();
        RectTransform r = p.GetComponent<RectTransform>();
        
        if (drag != null && r != null && !drag.isPlaced && r.parent == PuzzleManager.Instance.pieceParent)
        {
            float dist = Vector2.Distance(r.anchoredPosition, drag.correctPosition);
            
            // ONLY snap if this specific piece is within threshold
            if (dist <= snapThreshold)
            {
                startPositions[p] = r.anchoredPosition;
                targetPositions[p] = drag.correctPosition;
                piecesToSnap.Add(p);
                
                Debug.Log($"🎯 Will snap {p.name}: from {r.anchoredPosition} to {drag.correctPosition} (dist: {dist:F1})");
            }
            else
            {
                Debug.Log($"⚠️ Skipping {p.name}: too far from correct position (dist: {dist:F1} > threshold: {snapThreshold})");
            }
        }
    }
    
    if (piecesToSnap.Count == 0)
    {
        Debug.Log($"❌ No pieces within snap threshold - aborting snap");
        yield break;
    }
    
    Debug.Log($"🎯 Snapping {piecesToSnap.Count} pieces to their individual correct positions");
    
    // Smooth animation
    float duration = 0.35f;
    float t = 0f;
    
    while (t < 1f)
    {
        t += Time.deltaTime / duration;
        float progress = t * t * (3f - 2f * t); // Smoothstep
        
        foreach (var p in piecesToSnap)
        {
            RectTransform r = p.GetComponent<RectTransform>();
            if (r != null && startPositions.ContainsKey(p) && targetPositions.ContainsKey(p))
            {
                r.anchoredPosition = Vector2.Lerp(startPositions[p], targetPositions[p], progress);
            }
        }
        yield return null;
    }
    
    // Set final positions exactly
    foreach (var p in piecesToSnap)
    {
        RectTransform r = p.GetComponent<RectTransform>();
        if (r != null && targetPositions.ContainsKey(p))
        {
            r.anchoredPosition = targetPositions[p];
        }
        
        DragPiece drag = p.GetComponent<DragPiece>();
        if (drag != null)
        {
            drag.isPlaced = true;
            drag.canDrag = false;
            
            if (drag.canvasGroup != null)
            {
                drag.canvasGroup.blocksRaycasts = false;
                drag.canvasGroup.alpha = 1f;
            }
        }
    }
    
    // Remove this piece from drag order list since it's now placed
    if (PuzzleManager.Instance != null)
    {
        PuzzleManager.Instance.RemoveFromDragOrder(this);
    }
    
    if (PuzzleManager.Instance != null)
    {
        PuzzleManager.Instance.OnGroupPlaced(piecesToSnap);
        CheckCompletion();
    }
    
    Debug.Log($"✅ Snapped {piecesToSnap.Count} pieces to their correct positions");
}
    // Keep old SmoothSnap for backward compatibility if needed
    IEnumerator SmoothSnap()
    {
        yield return StartCoroutine(SmoothSnapToCorrectPositions());
    }

    public void ResetPiece()
    {
        isPlaced = false;
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }
}