using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance;

    [Header("Grid")]
    public int rows = 5;
    public int cols = 10;

    [Header("Puzzle Sets")]
    public PuzzleSet[] puzzleSets;

    [Header("Parents")]
    public Transform pieceParent;
    public Transform bottomParent;

    [Header("Bottom Settings")]
    public float spacing = 150f;
    public float moveSpeed = 8f;
    public float moveDelay = 1.5f;

    private GameObject[] spawnedPieces;
    private List<GameObject> bottomPieces = new List<GameObject>();

    private int maxVisible = 6;
    private GameObject[] slotPieces;

    private Queue<GameObject> overflowQueue = new Queue<GameObject>();
    public RectTransform overflowTarget;

    private int overflowIndex = 0;
    private int placedCount = 0;
    public int totalPieces = 0; // Make this public for access from DragPiece

    [Header("Drag")]
    public RectTransform dragArea;
    private List<Vector2> initialPositions = new List<Vector2>();

    [Header("UI")]
    public GameObject completePanel;
    public GameObject slotPrefab;
    public Transform slotParent;
    private List<GameObject> generatedSlots = new List<GameObject>();

    [Header("Complete UI")]
    public RectTransform banner;
    public RectTransform puzzleImage;
    public RectTransform buttonsParent;
    public RectTransform glow;
    public RectTransform stars;
    public RectTransform completedPuzzleParent;

    [Header("Merge System")]
    public List<PuzzlePiece> allPieces = new List<PuzzlePiece>();
    public float cellSize = 100f; // exact spacing between pieces

    [Header("Completion")]
    public GameObject completionCanvas; // Add reference to your completion canvas here

    private PuzzleSet currentSet;

    Vector2 bannerStartPos, bannerTargetPos;
    Vector2 buttonsStartPos, buttonsTargetPos;

    private Transform originalPuzzleParent;
    private HashSet<PuzzlePiece> placedPiecesSet = new HashSet<PuzzlePiece>();

    // Public property for totalPieces (alternative if you want to keep totalPieces private)
    public int TotalPieces => totalPieces;
    private List<DragPiece> dragOrderList = new List<DragPiece>();

    private List<PuzzleGroup> draggedGroups = new List<PuzzleGroup>();

    [SerializeField] public ScrollRect scrollRect;
    [SerializeField] public RectTransform bottomPanel;

    public RectTransform dragParent;

    [Header("Board Reference")]
    public RectTransform boardRectTransform;  

    [Header("Drag")]
 
        public float snapThreshold = 50f;

    // --------------------------------------------------
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        originalPuzzleParent = puzzleImage.parent;
        slotPieces = new GameObject[maxVisible];
        completePanel.SetActive(false);
        
        // Initialize completion canvas if assigned
        if (completionCanvas != null)
            completionCanvas.SetActive(false);
    }

    // --------------------------------------------------
    public void StartLevel(int targetRows, int targetCols)
    {
        StopAllCoroutines();
        puzzleImage.SetParent(originalPuzzleParent, false);
        puzzleImage.localScale = Vector3.one;
        puzzleImage.localRotation = Quaternion.identity;
        puzzleImage.anchoredPosition = Vector2.zero; 
        placedCount = 0;
        placedPiecesSet.Clear();
        overflowIndex = 0;
        completePanel.SetActive(false);
        
        if (completionCanvas != null)
            completionCanvas.SetActive(false);

        ClearCurrentLevel();

        slotPieces = new GameObject[maxVisible];

        rows = targetRows;
        cols = targetCols;

        currentSet = null;

        foreach (var set in puzzleSets)
        {
            if (set.rows == rows && set.cols == cols)
            {
                currentSet = set;
                break;
            }
        }

        if (currentSet == null)
        {
            Debug.LogError($"❌ No PuzzleSet for {rows}x{cols}");
            return;
        }

        InitLevel();
    }

    // Add this method to PuzzleManager
void ConfigureScrollRectForDrag()
{
    if (scrollRect != null)
    {
        // Make ScrollRect require a minimum drag distance before scrolling
        // This lets pieces be dragged when touched directly
        scrollRect.scrollSensitivity = 15f;
        
        // Add a CanvasGroup to the content to ensure raycasts pass through to pieces
        CanvasGroup contentCG = scrollRect.content.GetComponent<CanvasGroup>();
        if (contentCG == null)
            contentCG = scrollRect.content.gameObject.AddComponent<CanvasGroup>();
        contentCG.blocksRaycasts = true;
        
        Debug.Log("🔧 ScrollRect configured for piece dragging");
    }
}

public void UpdateDragOrder(DragPiece draggedPiece)
{
    // Check if this piece belongs to a group
    PuzzleGroup currentGroup = draggedPiece.piece.group;
    
    // If group has multiple pieces, handle the entire group
    if (currentGroup != null && currentGroup.pieces.Count > 1)
    {
        // Remove all pieces in this group from drag list
        foreach (var piece in currentGroup.pieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null && dragOrderList.Contains(drag))
            {
                dragOrderList.Remove(drag);
            }
        }
        
        // Add ALL pieces in the group to the end (same order)
        foreach (var piece in currentGroup.pieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null && !drag.isPlaced)
            {
                dragOrderList.Add(drag);
            }
        }
        
        // Track that this group was dragged as a unit
        if (!draggedGroups.Contains(currentGroup))
        {
            draggedGroups.Add(currentGroup);
        }
    }
    else
    {
        // Single piece - handle normally
        if (dragOrderList.Contains(draggedPiece))
            dragOrderList.Remove(draggedPiece);
        
        dragOrderList.Add(draggedPiece);
    }
    
    // Update ALL sorting orders based on list position
    RefreshSortingOrdersFromList();
}

// Refresh all sorting orders based on drag order list
// Refresh all sorting orders based on drag order list
// Refresh all sorting orders based on drag order list
public void RefreshSortingOrdersFromList()
{
    // Start from 0 for placed pieces (they go to the back)
    int placedOrder = 1;
    // Start from a high number for unplaced/dragged pieces (they go to the front)
    int unplacedOrder = 1000;
    
    Debug.Log($"=== REFRESHING ORDERS: {dragOrderList.Count} items in drag list ===");
    
    // First, assign orders to PLACED pieces (lowest priority - behind everything)
    foreach (var piece in allPieces)
    {
        DragPiece drag = piece.GetComponent<DragPiece>();
        if (drag != null && drag.isPlaced && !dragOrderList.Contains(drag))
        {
            drag.SetPieceSortingOrder(placedOrder);
            //Debug.Log($"  Set PLACED {drag.name} - piece order: {placedOrder}, shadow order: {placedOrder + 1}");
            placedOrder += 2;
        }
    }
    
    // Then, assign orders to UNPLACED pieces that are NOT in drag list (medium priority)
    foreach (var piece in allPieces)
    {
        DragPiece drag = piece.GetComponent<DragPiece>();
        if (drag != null && !drag.isPlaced && !dragOrderList.Contains(drag))
        {
            drag.SetPieceSortingOrder(unplacedOrder);
            //Debug.Log($"  Set UNPLACED (static) {drag.name} - piece order: {unplacedOrder}, shadow order: {unplacedOrder + 1}");
            unplacedOrder += 2;
        }
    }
    
    // Finally, assign orders to DRAGGED pieces (highest priority - on top)
    foreach (var drag in dragOrderList)
    {
        if (drag != null && !drag.isPlaced)
        {
            drag.SetPieceSortingOrder(unplacedOrder);
            //Debug.Log($"  Set DRAGGED {drag.name} - piece order: {unplacedOrder}, shadow order: {unplacedOrder + 1}");
            unplacedOrder += 2;
        }
    }
    
    //Debug.Log($"📊 Updated sorting orders - Placed range: 0-{placedOrder-1}, Unplaced range: 1000-{unplacedOrder-1}");
}

// Remove from drag order when piece is placed
public void RemoveFromDragOrder(DragPiece drag)
{
    if (dragOrderList.Contains(drag))
    {
        dragOrderList.Remove(drag);
        
        // Also remove from group tracking if group is fully placed
        if (drag.piece != null && drag.piece.group != null)
        {
            PuzzleGroup group = drag.piece.group;
            bool allPlaced = true;
            
            foreach (var piece in group.pieces)
            {
                DragPiece groupDrag = piece.GetComponent<DragPiece>();
                if (groupDrag != null && !groupDrag.isPlaced)
                {
                    allPlaced = false;
                    break;
                }
            }
            
            if (allPlaced && draggedGroups.Contains(group))
            {
                draggedGroups.Remove(group);
            }
        }
        
        RefreshSortingOrdersFromList();
    }
}


    // Add this method to PuzzleManager class
    // Add this public method to your PuzzleManager class
    // Add this to PuzzleManager class - handles visible + overflow properly
   public void UpdateSlotPiecesAfterShuffleWithOverflow(List<GameObject> visiblePieces, List<GameObject> hiddenPieces)
{
    // Clear all slots
    for (int i = 0; i < maxVisible; i++)
    {
        slotPieces[i] = null;
    }
    
    // Assign visible pieces to slots 0-5 with correct positions
    for (int i = 0; i < visiblePieces.Count && i < maxVisible; i++)
    {
        slotPieces[i] = visiblePieces[i];
        
        RectTransform rect = visiblePieces[i].GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetParent(bottomParent, false);
            rect.anchoredPosition = new Vector2(i * spacing, 0);
            rect.localScale = Vector3.one;
        }
        
        visiblePieces[i].SetActive(true);
        
        DragPiece drag = visiblePieces[i].GetComponent<DragPiece>();
        if (drag != null)
        {
            drag.canDrag = true; // ✅ Visible pieces always draggable
        }
    }
    
    // Clear and rebuild overflow queue with hidden pieces
    overflowQueue.Clear();
    foreach (var piece in hiddenPieces)
    {
        piece.SetActive(true); // ✅ KEEP VISIBLE
        piece.transform.SetParent(bottomParent, false);
        
        RectTransform rect = piece.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one; // ✅ Full scale
        }
        
        DragPiece drag = piece.GetComponent<DragPiece>();
        if (drag != null)
        {
            drag.canDrag = true; // ✅ OVERFLOW PIECES ALSO DRAGGABLE
        }
        
        overflowQueue.Enqueue(piece);
    }
    
    // Update bottomPieces list with ALL pieces
    bottomPieces.Clear();
    foreach (var piece in visiblePieces)
    {
        bottomPieces.Add(piece);
    }
    foreach (var piece in hiddenPieces)
    {
        bottomPieces.Add(piece);
    }
    
    // Reset overflow index
    overflowIndex = hiddenPieces.Count;
    
    Debug.Log($"📋 Shuffle update: {visiblePieces.Count} visible in slots, {hiddenPieces.Count} in overflow queue - ALL DRAGGABLE");
}

    public void OnGroupPlaced(List<PuzzlePiece> placedPieces)
{
    int newlyPlaced = 0;
    
    foreach (var p in placedPieces)
    {
        if (!placedPiecesSet.Contains(p))
        {
            placedPiecesSet.Add(p);
            newlyPlaced++;
        }
    }
    
    placedCount += newlyPlaced;
    
    // Remove these pieces from drag order list
    foreach (var p in placedPieces)
    {
        DragPiece drag = p.GetComponent<DragPiece>();
        if (drag != null && dragOrderList.Contains(drag))
        {
            dragOrderList.Remove(drag);
        }
    }
    
    // Refresh sorting orders (this will put placed pieces at low orders)
    RefreshSortingOrdersFromList();
    
    Debug.Log($"📊 Placed: {placedCount}/{totalPieces} (newly placed: {newlyPlaced})");
    
    if (placedCount >= totalPieces)
        OnPuzzleComplete();
}

    public void UpdatePiecesSortingOrder()
    {
        // Sort pieces by their grid position (top to bottom, left to right)
        List<PuzzlePiece> sortedPieces = new List<PuzzlePiece>(allPieces);
        sortedPieces.Sort((a, b) => 
        {
            if (a.row == b.row)
                return a.col.CompareTo(b.col);
            return a.row.CompareTo(b.row);
        });
        
        // Set sibling index based on position (higher row = lower index)
        for (int i = 0; i < sortedPieces.Count; i++)
        {
            if (sortedPieces[i] != null)
            {
                sortedPieces[i].transform.SetSiblingIndex(i);
            }
        }
    }

    void InitLevel()
    {
        SpawnPieces();
        GenerateSlots();
        AssignGhostImages();

        StartCoroutine(StartFlow());
    }
    
    public void LoadLevel(int levelIndex)
    {
        if (puzzleSets == null || puzzleSets.Length == 0)
        {
            Debug.LogError("❌ No puzzle sets assigned!");
            return;
        }

        PuzzleSet set = puzzleSets[levelIndex % puzzleSets.Length];

        StartLevel(set.rows, set.cols);
    }

    public void RemoveFromBottom(GameObject piece)
{
    int index = System.Array.IndexOf(slotPieces, piece);

    if (index >= 0)
        slotPieces[index] = null;

    bottomPieces.Remove(piece);

    RearrangeBottom();
    FillFromOverflow(); // This should pull from overflowQueue
}
    void AssignNeighbors()
    {
        for (int i = 0; i < spawnedPieces.Length; i++)
        {
            PuzzlePiece piece = spawnedPieces[i].GetComponent<PuzzlePiece>();
            if (piece == null) continue;

            int row = i / cols;
            int col = i % cols;

            piece.row = row;
            piece.col = col;

            if (col > 0)
                piece.left = spawnedPieces[i - 1].GetComponent<PuzzlePiece>();

            if (col < cols - 1)
                piece.right = spawnedPieces[i + 1].GetComponent<PuzzlePiece>();

            if (row > 0)
                piece.top = spawnedPieces[i - cols].GetComponent<PuzzlePiece>();

            if (row < rows - 1)
                piece.bottom = spawnedPieces[i + cols].GetComponent<PuzzlePiece>();
        }
    }


   /// </summary>
    public void RemoveFromBottomWithoutFill(GameObject piece)
    {
        int index = System.Array.IndexOf(slotPieces, piece);

        if (index >= 0)
            slotPieces[index] = null;

        bottomPieces.Remove(piece);
        
        // Don't call RearrangeBottom or FillFromOverflow yet
        Debug.Log($"📤 Removed {piece.name} from bottom (slot {index} empty, will fill later)");
    }
    // Add this public method so PowerUpButtons can call RearrangeBottom
    public void RearrangeBottomPublic()
    {
        RearrangeBottom();
        FillFromOverflow();
    }
    // ✅ FIXED: RearrangeBottom should skip placed/grouped pieces
    void RearrangeBottom()
    {
        List<GameObject> valid = new List<GameObject>();

        foreach (var p in slotPieces)
        {
            if (p == null) continue;
            
            // ✅ Skip if destroyed
            if (p == null || !p) continue;

            DragPiece drag = p.GetComponent<DragPiece>();
            PuzzlePiece pp = p.GetComponent<PuzzlePiece>();
            
            // ✅ Skip placed pieces
            if (drag != null && drag.isPlaced) continue;
            
            // ✅ Skip pieces in placed groups
            if (pp != null && pp.group != null && pp.group.isPlacedGroup) continue;
            
            // ✅ Skip pieces not in bottomParent (they're on the board)
            if (p.transform.parent != bottomParent) continue;

            valid.Add(p);
        }

        for (int i = 0; i < maxVisible; i++)
            slotPieces[i] = null;

        for (int i = 0; i < valid.Count; i++)
        {
            RectTransform rect = valid[i].GetComponent<RectTransform>();
            if (rect != null)
                rect.localScale = Vector3.one;

            valid[i].SetActive(true);
            slotPieces[i] = valid[i];
            StartCoroutine(MoveToSlot(valid[i], i));
        }
        
        // Clean bottomPieces list
        for (int i = bottomPieces.Count - 1; i >= 0; i--)
        {
            if (bottomPieces[i] == null || !bottomPieces[i])
            {
                bottomPieces.RemoveAt(i);
                continue;
            }
            
            DragPiece drag = bottomPieces[i].GetComponent<DragPiece>();
            PuzzlePiece pp = bottomPieces[i].GetComponent<PuzzlePiece>();
            
            if ((drag != null && drag.isPlaced) ||
                (pp != null && pp.group != null && pp.group.isPlacedGroup) ||
                bottomPieces[i].transform.parent != bottomParent)
            {
                bottomPieces.RemoveAt(i);
            }
        }
    }

    void FillFromOverflow()
{
    for (int i = 0; i < maxVisible; i++)
    {
        if (slotPieces[i] == null && overflowQueue.Count > 0)
        {
            GameObject piece = overflowQueue.Dequeue();

            piece.SetActive(true);
            piece.transform.SetParent(bottomParent, true);
            
            // FIX: Ensure scale is set to 1 when bringing from overflow
            RectTransform rect = piece.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one; // Reset scale to visible
            }

            slotPieces[i] = piece;
            bottomPieces.Add(piece);

            StartCoroutine(MoveToSlot(piece, i));
        }
    }
}

    IEnumerator StartFlow()
    {
        yield return new WaitForSeconds(1f);
        yield return ScatterToBottom();
    }

  void SpawnPieces()
{
    GameObject[] prefabs = currentSet.prefabs;

    totalPieces = prefabs.Length;
    spawnedPieces = new GameObject[prefabs.Length];
    initialPositions.Clear();
    allPieces.Clear();

    Debug.Log($"=== SPAWNING {prefabs.Length} PIECES ===");
    Debug.Log($"[SPAWN INFO] pieceParent: {pieceParent.name} | pieceParent anchoredPos: {((RectTransform)pieceParent).anchoredPosition} | pieceParent size: {((RectTransform)pieceParent).sizeDelta}");

    for (int i = 0; i < prefabs.Length; i++)
    {
        GameObject obj = Instantiate(prefabs[i], pieceParent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        
        // ✅ Store initial anchored position (relative to pieceParent)
        Vector2 initialAnchoredPos = rect.anchoredPosition;
        Vector3 initialWorldPos = rect.position;
        Vector3 initialLocalPos = rect.localPosition;
        
        initialPositions.Add(initialAnchoredPos);

        PuzzlePiece puzzlePiece = obj.GetComponent<PuzzlePiece>();
        if (puzzlePiece != null) 
        {
            puzzlePiece.Setup(i);
            allPieces.Add(puzzlePiece);
        }

        DragPiece drag = obj.GetComponent<DragPiece>();
        if (drag != null)
        {
            drag.canDrag = true; // Start as false, becomes true after scatter
            
            // ✅ CRITICAL: Store position relative to pieceParent
            // This is the anchored position when the piece is a child of pieceParent
            drag.correctPosition = initialAnchoredPos;
            drag.dragArea = dragArea;
            
            // ✅ DEBUG: Print all position info
            Debug.Log($"[SPAWN {i}] {obj.name} | " +
                      $"AnchoredPos: {initialAnchoredPos} | " +
                      $"LocalPos: {initialLocalPos} | " +
                      $"WorldPos: {initialWorldPos} | " +
                      $"SizeDelta: {rect.sizeDelta} | " +
                      $"Pivot: {rect.pivot} | " +
                      $"AnchorMin: {rect.anchorMin} | " +
                      $"AnchorMax: {rect.anchorMax}");
        }
        else
        {
            Debug.LogError($"[SPAWN {i}] {obj.name} has NO DragPiece component!");
        }

        spawnedPieces[i] = obj;
    }
    
    // Calculate cell size from first two pieces
    if (spawnedPieces.Length >= 2)
    {
        RectTransform rect0 = spawnedPieces[0].GetComponent<RectTransform>();
        RectTransform rect1 = spawnedPieces[1].GetComponent<RectTransform>();
        
        Vector2 pos0 = rect0.anchoredPosition;
        Vector2 pos1 = rect1.anchoredPosition;
        
        cellSize = Mathf.Abs(pos1.x - pos0.x);
        
        Debug.Log($"🟢 DETECTED CELL SIZE: {cellSize} | Piece0: {pos0} | Piece1: {pos1} | DeltaX: {Mathf.Abs(pos1.x - pos0.x)}");
    }
    else
    {
        Debug.LogWarning($"⚠️ Only {spawnedPieces.Length} pieces spawned, cannot calculate cell size");
    }
    
    // Verify all pieces have correctPosition set
    for (int i = 0; i < spawnedPieces.Length; i++)
    {
        DragPiece drag = spawnedPieces[i].GetComponent<DragPiece>();
        if (drag != null)
        {
            // Verify correctPosition is valid
            if (drag.correctPosition == Vector2.zero && i > 0)
            {
                Debug.LogWarning($"[SPAWN WARNING] {spawnedPieces[i].name} has correctPosition = (0,0) - this may be incorrect!");
            }
        }
    }
    
    AssignNeighbors();
    
    Debug.Log($"=== SPAWN COMPLETE: {spawnedPieces.Length} pieces created ===");
}

    void GenerateSlots()
    {
        generatedSlots.Clear();

        for (int i = 0; i < spawnedPieces.Length; i++)
        {
            RectTransform pieceRect = spawnedPieces[i].GetComponent<RectTransform>();

            GameObject slot = Instantiate(slotPrefab, slotParent);
            RectTransform slotRect = slot.GetComponent<RectTransform>();

            slotRect.anchoredPosition = pieceRect.anchoredPosition;

            if (currentSet.slotSprites != null && i < currentSet.slotSprites.Length)
            {
                Image img = slot.GetComponent<Image>();
                img.sprite = currentSet.slotSprites[i];
                img.SetNativeSize();
                slot.SetActive(false);
            }

            generatedSlots.Add(slot);
        }
    }

    void AssignGhostImages()
    {
        for (int i = 0; i < spawnedPieces.Length; i++)
        {
            DragPiece drag = spawnedPieces[i].GetComponent<DragPiece>();

            if (drag != null && i < generatedSlots.Count)
                drag.ghostImage = generatedSlots[i];
        }
    }

    IEnumerator ScatterToBottom()
    {
        yield return new WaitForSeconds(moveDelay);

        List<GameObject> remaining = new List<GameObject>(spawnedPieces);

        while (remaining.Count > 0)
        {
            int group = Random.Range(3, 5);

            for (int i = 0; i < group && remaining.Count > 0; i++)
            {
                int index = Random.Range(0, remaining.Count);
                GameObject piece = remaining[index];
                remaining.RemoveAt(index);

                MoveToBottom(piece);
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    // In PuzzleManager.cs - Fix MoveToBottom() to allow initial scatter

    public void MoveToBottom(GameObject piece)
    {
        // ✅ GUARD: Never move placed or grouped pieces to bottom
        if (piece == null) return;
        
        PuzzlePiece puzzlePiece = piece.GetComponent<PuzzlePiece>();
        DragPiece drag = piece.GetComponent<DragPiece>();
        
        // Check if piece is placed
        if (drag != null && drag.isPlaced)
        {
            Debug.LogWarning($"⚠️ BLOCKED: Cannot move placed piece {piece.name} to bottom panel");
            return;
        }
        
        // Check if piece belongs to a placed group
        if (puzzlePiece != null && puzzlePiece.group != null && puzzlePiece.group.isPlacedGroup)
        {
            Debug.LogWarning($"⚠️ BLOCKED: Cannot move group-placed piece {piece.name} to bottom panel");
            return;
        }
        
        // ✅ FIX: Only block if piece is ALREADY in pieceParent AND is placed/grouped
        // Don't block if we're doing the initial scatter (piece hasn't been placed yet)
        // The above checks already handle placed/grouped pieces, so we can remove the parent check
        // Or make it more specific:
        
        // ✅ REPLACE THIS BLOCK:
        // if (piece.transform.parent == pieceParent)
        // {
        //     Debug.LogWarning($"⚠️ BLOCKED: Cannot move piece {piece.name} from board to bottom panel");
        //     return;
        // }
        
        // ✅ WITH THIS: Only block if piece is on the board AND placed/grouped
        // (already handled by the checks above, so we can just remove this block entirely)

        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.SetParent(bottomParent, true);
        
        if (puzzlePiece != null)
        {
            if (puzzlePiece.group != null)
            {
                puzzlePiece.group.pieces.Remove(puzzlePiece);
            }
            
            puzzlePiece.group = new PuzzleGroup();
            puzzlePiece.group.AddPiece(puzzlePiece);
        }

        int slot = GetEmptySlot();

        if (slot != -1)
        {
            slotPieces[slot] = piece;
            bottomPieces.Add(piece);
            StartCoroutine(MoveToSlot(piece, slot));
        }
        else
        {
            overflowQueue.Enqueue(piece);
            StartCoroutine(MoveToOverflow(piece));
        }
    }

    IEnumerator MoveToSlot(GameObject piece, int index)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();
        DragPiece drag = piece.GetComponent<DragPiece>();

        // ✅ Abort if already placed
        if (drag != null && drag.isPlaced) yield break;

        rect.localScale = Vector3.one;
        piece.SetActive(true);

        Vector2 start = rect.anchoredPosition;
        Vector2 target = new Vector2(index * spacing, 0);

        float t = 0;
        while (t < 1)
        {
            if (drag != null && drag.isPlaced) yield break; // ✅ Check every frame
            t += Time.deltaTime * moveSpeed;
            rect.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        if (drag != null && drag.isPlaced) yield break; // ✅ Final check
        rect.anchoredPosition = target;
        if (drag != null) drag.canDrag = true;
    }

    IEnumerator MoveToOverflow(GameObject piece)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();

        Vector2 start = rect.anchoredPosition;

        float x = (maxVisible + overflowIndex) * spacing;
        Vector2 finalTarget = new Vector2(x, 0);

        overflowIndex++;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            rect.anchoredPosition = Vector2.Lerp(start, overflowTarget.anchoredPosition, t);
            yield return null;
        }

        rect.anchoredPosition = finalTarget;
        // rect.localScale = Vector3.zero; // Scale to 0 when hidden
        // piece.SetActive(false);
    }

        int GetEmptySlot()
        {
            for (int i = 0; i < maxVisible; i++)
                if (slotPieces[i] == null)
                    return i;

            return -1;
        }

        // ✅ FIXED: OnPiecePlaced should also clean bottom tracking
        public void OnPiecePlaced(DragPiece piece)
        {
            if (piece == null) return;
            
            PuzzlePiece p = piece.GetComponent<PuzzlePiece>();
            if (p != null && !placedPiecesSet.Contains(p))
            {
                placedPiecesSet.Add(p);
                placedCount++;
            }

            // ✅ Remove from ALL bottom tracking
            int index = System.Array.IndexOf(slotPieces, piece.gameObject);
            if (index >= 0)
            {
                Debug.Log($"🔧 Removing placed piece {piece.name} from slot {index}");
                slotPieces[index] = null;
            }

            bottomPieces.Remove(piece.gameObject);

            // ✅ Remove from drag order
            if (dragOrderList.Contains(piece))
                dragOrderList.Remove(piece);

            Debug.Log($"📊 Placed: {placedCount}/{totalPieces}");
        }


        // ✅ FIXED: OnGroupPlacementFinished() - Clean placed pieces before rearranging
        public void OnGroupPlacementFinished()
        {
            // ✅ ADD THIS: Remove any placed group pieces from bottom tracking FIRST
            CleanPlacedPiecesFromBottomSlots();
            
            RearrangeBottom();
            FillFromOverflow();
            RefreshSortingOrdersFromList();

            if (placedCount >= totalPieces)
                OnPuzzleComplete();
        }

        // ✅ NEW METHOD: Remove placed group pieces from bottom slots and list
    private void CleanPlacedPiecesFromBottomSlots()
    {
        for (int i = 0; i < maxVisible; i++)
        {
            if (slotPieces[i] != null)
            {
                PuzzlePiece pp = slotPieces[i].GetComponent<PuzzlePiece>();
                DragPiece drag = slotPieces[i].GetComponent<DragPiece>();
                
                bool shouldRemove = false;
                
                // Check if piece is individually placed
                if (drag != null && drag.isPlaced)
                    shouldRemove = true;
                
                // Check if piece belongs to a placed group
                if (pp != null && pp.group != null && pp.group.isPlacedGroup)
                    shouldRemove = true;
                
                // ✅ CHECK: If piece is now child of pieceParent (not bottomParent)
                if (slotPieces[i].transform.parent == pieceParent)
                    shouldRemove = true;
                
                if (shouldRemove)
                {
                    Debug.Log($"🧹 Cleaning placed/grouped piece {slotPieces[i].name} from slot {i}");
                    bottomPieces.Remove(slotPieces[i]);
                    slotPieces[i] = null;
                }
            }
        }
        
        // Clean placed pieces from bottomPieces list (not in slots but in list)
        for (int i = bottomPieces.Count - 1; i >= 0; i--)
        {
            if (bottomPieces[i] == null)
            {
                bottomPieces.RemoveAt(i);
                continue;
            }
            
            PuzzlePiece pp = bottomPieces[i].GetComponent<PuzzlePiece>();
            DragPiece drag = bottomPieces[i].GetComponent<DragPiece>();
            
            bool shouldRemove = false;
            
            if (drag != null && drag.isPlaced)
                shouldRemove = true;
            
            if (pp != null && pp.group != null && pp.group.isPlacedGroup)
                shouldRemove = true;
            
            if (bottomPieces[i].transform.parent == pieceParent)
                shouldRemove = true;
            
            if (shouldRemove)
            {
                Debug.Log($"🧹 Cleaning placed/grouped piece {bottomPieces[i].name} from bottomPieces list");
                bottomPieces.RemoveAt(i);
            }
        }
        
        // Clean placed pieces from overflow queue
        Queue<GameObject> cleanedQueue = new Queue<GameObject>();
        while (overflowQueue.Count > 0)
        {
            GameObject piece = overflowQueue.Dequeue();
            
            if (piece == null) continue;
            
            PuzzlePiece pp = piece.GetComponent<PuzzlePiece>();
            DragPiece drag = piece.GetComponent<DragPiece>();
            
            bool isPlacedOrGrouped = false;
            
            if (drag != null && drag.isPlaced)
                isPlacedOrGrouped = true;
            
            if (pp != null && pp.group != null && pp.group.isPlacedGroup)
                isPlacedOrGrouped = true;
            
            if (piece.transform.parent == pieceParent)
                isPlacedOrGrouped = true;
            
            if (!isPlacedOrGrouped)
            {
                cleanedQueue.Enqueue(piece);
            }
            else
            {
                Debug.Log($"🧹 Removing placed/grouped piece {piece.name} from overflow queue");
            }
        }
        overflowQueue = cleanedQueue;
        overflowIndex = cleanedQueue.Count;
    }


    // Call this after any group placement or merge
    public void UpdateAllPiecesSortingOrder()
    {
        // Create a list of all pieces and sort by grid position
        List<PuzzlePiece> sortedPieces = new List<PuzzlePiece>(allPieces);
        
        // Sort: top to bottom, then left to right
        // Pieces in higher rows (smaller row number) should be rendered BEHIND
        sortedPieces.Sort((a, b) => 
        {
            if (a.row == b.row)
                return a.col.CompareTo(b.col);  // Left to right
            return a.row.CompareTo(b.row);       // Top to bottom
        });
        
        // Assign sorting orders based on grid position
        int order = 0;
        foreach (var piece in sortedPieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null)
            {
                // Base order on position (top rows get lower numbers = behind)
                drag.SetPieceSortingOrder(order);
                order++;
            }
        }
        
        Debug.Log($"📊 Updated sorting order for {sortedPieces.Count} pieces");
    }

    // For groups, bring the entire group to front when dragged/placed
    // Get the highest order for a group (so all pieces in group have same or sequential order)
    public void BringGroupToFront(PuzzleGroup group)
    {
        if (group == null || group.pieces.Count == 0) return;
        
        // Remove all pieces in group from drag list
        foreach (var piece in group.pieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null && dragOrderList.Contains(drag))
            {
                dragOrderList.Remove(drag);
            }
        }
        
        // Add all pieces in group to the end
        foreach (var piece in group.pieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null && !drag.isPlaced)
            {
                dragOrderList.Add(drag);
            }
        }
        
        // Refresh orders
        RefreshSortingOrdersFromList();
    }

    // Add to PuzzleManager.cs
    public void ResetAllSortingOrders()
    {
        int order = 1;
        foreach (var piece in allPieces)
        {
            if (piece != null)
            {
                DragPiece drag = piece.GetComponent<DragPiece>();
                if (drag != null && drag.isPlaced)
                {
                    drag.SetPieceSortingOrder(order);
                    order++;
                }
            }
        }
        
        // Then handle unplaced pieces
        foreach (var piece in allPieces)
        {
            if (piece != null)
            {
                DragPiece drag = piece.GetComponent<DragPiece>();
                if (drag != null && !drag.isPlaced)
                {
                    drag.SetPieceSortingOrder(order);
                    order++;
                }
            }
        }
    }

    // ADD THIS METHOD FOR COMPLETION CHECK FROM DRAGPIECE
    public void ShowCompletionCanvas()
    {
        Debug.Log("🎉 PUZZLE COMPLETE! Showing completion canvas.");
        
        if (completionCanvas != null)
        {
            completionCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Completion canvas not assigned in PuzzleManager!");
            // Fallback to completePanel if completionCanvas is not assigned
            if (completePanel != null)
                completePanel.SetActive(true);
        }
    }

    void OnPuzzleComplete()
    {
        // Show either completionCanvas or completePanel
        if (completionCanvas != null)
        {
            completionCanvas.SetActive(true);
        }
        else if (completePanel != null)
        {
            completePanel.SetActive(true);
            SetupUI();
            ResetCompletePanel();
            PlayFullCompleteSequence();
        }
    }

    void PlayFullCompleteSequence()
    {
        RectTransform rect = puzzleImage;

        rect.SetParent(completedPuzzleParent, false);

        rect.localScale = Vector3.one * 1.05f;
        rect.localRotation = Quaternion.identity;

        Sequence seq = DOTween.Sequence();

        seq.Append(rect.DOShakeRotation(0.3f, 3f));

        seq.Append(rect.DOScale(0.9f, 0.4f).SetEase(Ease.InOutQuad))
           .Join(rect.DORotate(new Vector3(0, 0, 2.5f), 0.4f)
           .SetEase(Ease.InOutSine));

        seq.Append(rect.DOScale(0.92f, 0.15f))
           .Append(rect.DOScale(0.9f, 0.1f));

        seq.AppendInterval(0.15f);

        seq.AppendCallback(() => PlayCompleteUI());
    }

    void PlayCompleteUI()
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(banner.DOAnchorPos(bannerTargetPos, 0.6f)
            .SetEase(Ease.OutBack));

        seq.Append(buttonsParent.DOAnchorPos(buttonsTargetPos, 0.5f)
            .SetEase(Ease.OutBack));

        List<RectTransform> buttons = new List<RectTransform>();

        for (int i = 0; i < buttonsParent.childCount; i++)
        {
            RectTransform btn = buttonsParent.GetChild(i).GetComponent<RectTransform>();
            btn.localScale = Vector3.zero;
            buttons.Add(btn);
        }

        foreach (var btn in buttons)
        {
            seq.Append(btn.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
            seq.AppendInterval(0.08f);
        }

        seq.AppendCallback(() => PlayEffects());
    }
    
    void SetupUI()
    {
        if (bannerStartPos == Vector2.zero)
        {
            bannerStartPos = banner.anchoredPosition;
            buttonsStartPos = buttonsParent.anchoredPosition;
        }

        bannerTargetPos = bannerStartPos;
        buttonsTargetPos = buttonsStartPos;

        banner.anchoredPosition = bannerStartPos + Vector2.up * 500;
        buttonsParent.anchoredPosition = buttonsStartPos - Vector2.up * 500;
    }
    
    void PlayEffects()
    {
        glow.DOScale(1.1f, 1.2f).SetLoops(-1, LoopType.Yoyo);
        stars.DORotate(new Vector3(0, 0, 5f), 2f).SetLoops(-1, LoopType.Yoyo);
    }

    void ClearCurrentLevel()
    {
        if (spawnedPieces != null)
        {
            foreach (var p in spawnedPieces)
                if (p) Destroy(p);
        }

        foreach (var s in generatedSlots)
            if (s) Destroy(s);

        generatedSlots.Clear();
        bottomPieces.Clear();
        overflowQueue.Clear();
        allPieces.Clear();
    }

    public void HideCompletePanel(System.Action onComplete)
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(buttonsParent.DOAnchorPos(
            buttonsTargetPos - Vector2.up * 500, 0.4f)
            .SetEase(Ease.InBack));

        seq.Join(banner.DOAnchorPos(
            bannerTargetPos + Vector2.up * 500, 0.4f)
            .SetEase(Ease.InBack));

        seq.Join(puzzleImage.DOScale(0.85f, 0.3f));

        seq.AppendCallback(() =>
        {
            glow.DOKill();
            stars.DOKill();
        });

        seq.AppendCallback(() =>
        {
            completePanel.SetActive(false);
            onComplete?.Invoke();
        });
    }

    // Add this method to PuzzleManager class (put it near other public methods)
    // ✅ FIXED: ForceRearrangeBottom should also clean placed pieces
    public void ForceRearrangeBottom()
    {
        // First, clean placed pieces
        CleanPlacedPiecesFromBottomSlots();
        
        // Collect all currently active pieces in bottom parent
        List<GameObject> activePieces = new List<GameObject>();
        
        for (int i = 0; i < maxVisible; i++)
        {
            if (slotPieces[i] != null && slotPieces[i].activeSelf)
            {
                DragPiece drag = slotPieces[i].GetComponent<DragPiece>();
                PuzzlePiece pp = slotPieces[i].GetComponent<PuzzlePiece>();
                
                // Skip placed/grouped pieces
                if ((drag != null && drag.isPlaced) ||
                    (pp != null && pp.group != null && pp.group.isPlacedGroup))
                    continue;
                
                activePieces.Add(slotPieces[i]);
            }
        }

        // Clear slots
        for (int i = 0; i < maxVisible; i++)
        {
            slotPieces[i] = null;
        }

        // Reassign active pieces to correct slot positions
        for (int i = 0; i < activePieces.Count && i < maxVisible; i++)
        {
            slotPieces[i] = activePieces[i];
            
            RectTransform rect = activePieces[i].GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.SetParent(bottomParent, false);
                rect.anchoredPosition = new Vector2(i * spacing, 0);
                rect.localScale = Vector3.one;
                activePieces[i].SetActive(true);
            }
            
            DragPiece drag = activePieces[i].GetComponent<DragPiece>();
            if (drag != null)
            {
                drag.canDrag = true;
            }
        }

        // Fill any empty slots from overflow
        FillFromOverflow();
        
        Debug.Log($"📋 Rearranged bottom: {activePieces.Count} pieces visible, overflow queue: {overflowQueue.Count}");
    }

    void ResetCompletePanel()
    {
        banner.DOKill();
        buttonsParent.DOKill();
        puzzleImage.DOKill();
        glow.DOKill();
        stars.DOKill();

        banner.anchoredPosition = bannerStartPos + Vector2.up * 500;
        buttonsParent.anchoredPosition = buttonsStartPos - Vector2.up * 500;

        puzzleImage.localScale = Vector3.one;
        puzzleImage.localRotation = Quaternion.identity;

        puzzleImage.SetParent(originalPuzzleParent, false);
        puzzleImage.anchoredPosition = Vector2.zero;

        for (int i = 0; i < buttonsParent.childCount; i++)
        {
            RectTransform btn = buttonsParent.GetChild(i).GetComponent<RectTransform>();
            if (btn != null)
                btn.localScale = Vector3.zero;
        }

        glow.localScale = Vector3.one;
        stars.localRotation = Quaternion.identity;

        completePanel.SetActive(false);
    }
}

[System.Serializable]
public class PuzzleSet
{
    public int rows;
    public int cols;
    public GameObject[] prefabs;
    public Sprite[] slotSprites;
}