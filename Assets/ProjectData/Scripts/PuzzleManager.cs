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
        
        // Update sorting order to prevent overlap issues
        UpdatePiecesSortingOrder();
        
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
        FillFromOverflow();
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

    void RearrangeBottom()
    {
        List<GameObject> valid = new List<GameObject>();

        foreach (var p in slotPieces)
            if (p != null)
                valid.Add(p);

        for (int i = 0; i < maxVisible; i++)
            slotPieces[i] = null;

        for (int i = 0; i < valid.Count; i++)
        {
            slotPieces[i] = valid[i];
            StartCoroutine(MoveToSlot(valid[i], i));
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

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject obj = Instantiate(prefabs[i], pieceParent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            initialPositions.Add(rect.anchoredPosition);

            PuzzlePiece piece = obj.GetComponent<PuzzlePiece>();
            if (piece != null) 
            {
                piece.Setup(i);
                allPieces.Add(piece);
            }

            DragPiece drag = obj.GetComponent<DragPiece>();
            if (drag != null)
            {
                drag.canDrag = false;
                drag.correctPosition = rect.anchoredPosition;
                drag.dragArea = dragArea;
            }

            spawnedPieces[i] = obj;
        }
        
        if (spawnedPieces.Length >= 2)
        {
            Vector2 pos0 = spawnedPieces[0].GetComponent<RectTransform>().anchoredPosition;
            Vector2 pos1 = spawnedPieces[1].GetComponent<RectTransform>().anchoredPosition;
            cellSize = Mathf.Abs(pos1.x - pos0.x);
            Debug.Log($"🟢 DETECTED CELL SIZE: {cellSize}");
        }
        
        AssignNeighbors();
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

    public void MoveToBottom(GameObject piece)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.SetParent(bottomParent, true);
        
        PuzzlePiece puzzlePiece = piece.GetComponent<PuzzlePiece>();
        if (puzzlePiece != null)
        {
            if (puzzlePiece.group != null)
            {
                puzzlePiece.group.pieces.Remove(puzzlePiece);
            }
            
            puzzlePiece.group = new PuzzleGroup();
            puzzlePiece.group.AddPiece(puzzlePiece);
            
            Debug.Log($"🔄 {piece.name} reset to independent group in bottom tray");
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

        Vector2 start = rect.anchoredPosition;
        Vector2 target = new Vector2(index * spacing, 0);

        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime * moveSpeed;
            rect.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        rect.anchoredPosition = target;

        DragPiece drag = piece.GetComponent<DragPiece>();
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
        piece.SetActive(false);
    }

    int GetEmptySlot()
    {
        for (int i = 0; i < maxVisible; i++)
            if (slotPieces[i] == null)
                return i;

        return -1;
    }

    public void OnPiecePlaced(DragPiece piece)
    {
        PuzzlePiece p = piece.GetComponent<PuzzlePiece>();
        if (p != null && !placedPiecesSet.Contains(p))
        {
            placedPiecesSet.Add(p);
            placedCount++;
        }
        
        RemoveFromBottom(piece.gameObject);
        
        Debug.Log($"📊 Placed: {placedCount}/{totalPieces}");
        
        if (placedCount >= totalPieces)
            OnPuzzleComplete();
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