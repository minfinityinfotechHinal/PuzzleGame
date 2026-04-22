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
    private int totalPieces = 0;

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

    private PuzzleSet currentSet;

    Vector2 bannerStartPos, bannerTargetPos;
    Vector2 buttonsStartPos, buttonsTargetPos;

    private Transform originalPuzzleParent;

    // --------------------------------------------------
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ❌ DO NOTHING IN START (important for start screen)
    void Start()
    {
        originalPuzzleParent = puzzleImage.parent;
        slotPieces = new GameObject[maxVisible];
        completePanel.SetActive(false);
    }

    // --------------------------------------------------
    // ✅ MAIN ENTRY POINT
    public void StartLevel(int targetRows, int targetCols)
    {
        StopAllCoroutines();
        // ✅ RESET PARENT BACK
        puzzleImage.SetParent(originalPuzzleParent, false);
        puzzleImage.localScale = Vector3.one;
        puzzleImage.localRotation = Quaternion.identity;
        placedCount = 0;
        overflowIndex = 0;
        completePanel.SetActive(false);

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

    // --------------------------------------------------
    void SpawnPieces()
    {
        GameObject[] prefabs = currentSet.prefabs;

        totalPieces = prefabs.Length;
        spawnedPieces = new GameObject[prefabs.Length];
        initialPositions.Clear();

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject obj = Instantiate(prefabs[i], pieceParent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            initialPositions.Add(rect.anchoredPosition);

            // Setup stencil
            PuzzlePiece piece = obj.GetComponent<PuzzlePiece>();
            if (piece != null) piece.Setup(i);

            // Setup drag
            DragPiece drag = obj.GetComponent<DragPiece>();
            if (drag != null)
            {
                drag.canDrag = false;
                drag.correctPosition = rect.anchoredPosition;
                drag.dragArea = dragArea;
            }

            spawnedPieces[i] = obj;
        }
    }

    // --------------------------------------------------
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

    // --------------------------------------------------
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

    // --------------------------------------------------
    public void MoveToBottom(GameObject piece)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.SetParent(bottomParent, true);

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

        float x = (maxVisible + overflowIndex) * spacing;
        overflowIndex++;

        Vector2 target = new Vector2(x, 0);

        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime * moveSpeed;
            Vector2 start = rect.anchoredPosition;

            while (t < 1)
            {
                t += Time.deltaTime * moveSpeed;
                rect.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }
            yield return null;
        }

        rect.anchoredPosition = target;
        piece.SetActive(false);
    }

    int GetEmptySlot()
    {
        for (int i = 0; i < maxVisible; i++)
            if (slotPieces[i] == null)
                return i;

        return -1;
    }

    // --------------------------------------------------
    public void OnPiecePlaced(DragPiece piece)
    {
        placedCount++;

        // 🔥 VERY IMPORTANT
        RemoveFromBottom(piece.gameObject);

        if (placedCount >= totalPieces)
            OnPuzzleComplete();
    }

   void OnPuzzleComplete()
{
    completePanel.SetActive(true);

    SetupUI();

    PlayFullCompleteSequence(); // 👈 ONLY ONE ENTRY POINT
}

void PlayFullCompleteSequence()
{
    RectTransform rect = puzzleImage;

    rect.SetParent(completedPuzzleParent, false);

    // ✅ Start slightly bigger (important!)
    rect.localScale = Vector3.one * 1.05f;
    rect.localRotation = Quaternion.identity;

    Sequence seq = DOTween.Sequence();

    // 🔥 STEP 1: subtle shake ONLY (no scale up)
    seq.Append(rect.DOShakeRotation(0.3f, 3f));

    // 🔥 STEP 2: smooth scale DOWN + rotate
    seq.Append(rect.DOScale(0.9f, 0.4f).SetEase(Ease.InOutQuad))
       .Join(rect.DORotate(new Vector3(0, 0, 2.5f), 0.4f)
       .SetEase(Ease.InOutSine));

    // 🔥 optional micro settle (very premium feel)
    seq.Append(rect.DOScale(0.92f, 0.15f))
       .Append(rect.DOScale(0.9f, 0.1f));

    // ⏳ pause
    seq.AppendInterval(0.15f);

    // 🔥 UI after everything
    seq.AppendCallback(() => PlayCompleteUI());
}

void PlayCompleteUI()
{
    Sequence seq = DOTween.Sequence();

    // Banner first
    seq.Append(banner.DOAnchorPos(bannerTargetPos, 0.6f)
        .SetEase(Ease.OutBack));

    // Buttons container
    seq.Append(buttonsParent.DOAnchorPos(buttonsTargetPos, 0.5f)
        .SetEase(Ease.OutBack));

    // Prepare buttons
    List<RectTransform> buttons = new List<RectTransform>();

    for (int i = 0; i < buttonsParent.childCount; i++)
    {
        RectTransform btn = buttonsParent.GetChild(i).GetComponent<RectTransform>();
        btn.localScale = Vector3.zero;
        buttons.Add(btn);
    }

    // Animate one by one
    foreach (var btn in buttons)
    {
        seq.Append(btn.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
        seq.AppendInterval(0.08f);
    }

    // Effects start at end
    seq.AppendCallback(() => PlayEffects());
}
    void SetupUI()
    {
        bannerTargetPos = banner.anchoredPosition;
        buttonsTargetPos = buttonsParent.anchoredPosition;

        banner.anchoredPosition += Vector2.up * 500;
        buttonsParent.anchoredPosition -= Vector2.up * 500;
    }

   void MovePuzzleAnim()
    {
        RectTransform rect = puzzleImage;

        rect.SetParent(completedPuzzleParent, false);

        // ✅ Reset scale to avoid inherited scaling issues
        rect.localScale = Vector3.one;

        Sequence seq = DOTween.Sequence();

            // 🔹 Step 1: slight grow
    seq.Append(rect.DOScale(1.05f, 0.25f).SetEase(Ease.OutQuad))

    // 🔹 Step 2: scale down + smooth rotate together
    .Append(rect.DOScale(0.9f, 0.4f).SetEase(Ease.InOutQuad))
    .Join(rect.DORotate(new Vector3(0, 0, 2.5f), 0.4f).SetEase(Ease.InOutSine));
    }
    void PlayCompleteAnimation()
    {
        Sequence seq = DOTween.Sequence();

        // 🔹 Move banner
        seq.Append(banner.DOAnchorPos(bannerTargetPos, 0.6f)
            .SetEase(Ease.OutBack));

        // 🔹 Move buttons container
        seq.Append(buttonsParent.DOAnchorPos(buttonsTargetPos, 0.5f)
            .SetEase(Ease.OutBack));

        // 🔥 IMPORTANT: prepare buttons FIRST
        List<RectTransform> buttons = new List<RectTransform>();

        for (int i = 0; i < buttonsParent.childCount; i++)
        {
            RectTransform btn = buttonsParent.GetChild(i).GetComponent<RectTransform>();

            btn.localScale = Vector3.zero; // 👈 reset scale
            buttons.Add(btn);
        }

        // 🔹 Animate one by one
        foreach (var btn in buttons)
        {
            seq.Append(btn.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
            seq.AppendInterval(0.08f); // spacing
        }
    }

    void PlayEffects()
    {
        glow.DOScale(1.1f, 1.2f).SetLoops(-1, LoopType.Yoyo);
        stars.DORotate(new Vector3(0, 0, 5f), 2f).SetLoops(-1, LoopType.Yoyo);
    }

    // --------------------------------------------------
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
 