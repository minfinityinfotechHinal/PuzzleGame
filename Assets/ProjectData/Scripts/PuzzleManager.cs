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

    [Header("Prefab Pieces")]
    public PuzzleSet[] puzzleSets;

    [Header("Parents")]
    public Transform pieceParent;
    public Transform bottomParent;

    [Header("Bottom Settings")]
    public float spacing = 150f;
    public float moveSpeed = 8f;
    public float moveDelay = 1.5f;

    private GameObject[] spawnedPieces;

    private Coroutine rearrangeRoutine;

    private int maxVisible = 6;
    private int nextSpawnIndex = 0;
    private List<GameObject> allPieces = new List<GameObject>();
    private List<GameObject> activeBottom = new List<GameObject>();
    private List<GameObject> bottomPieces = new List<GameObject>();
    [SerializeField]
    private GameObject[] slotPieces;
    private int overflowIndex = 0;
    private Dictionary<GameObject, Coroutine> moveRoutines = new Dictionary<GameObject, Coroutine>();

    private Queue<GameObject> overflowQueue = new Queue<GameObject>();
    public RectTransform overflowTarget;

    private int placedCount = 0;
    private int totalPieces = 0;

    public RectTransform dragArea;
    [SerializeField]
    private List<Vector2> initialPositions = new List<Vector2>();
    [Header("UI")]
    public GameObject completePanel;
    public GameObject slotPrefab;
    public Transform slotParent;
    private List<GameObject> generatedSlots = new List<GameObject>();
    public RectTransform banner;
    public RectTransform puzzleImage;
    public RectTransform buttonsParent;

    Vector2 bannerStartPos;
    Vector2 bannerTargetPos;

    Vector2 buttonsStartPos;
    Vector2 buttonsTargetPos;
    public RectTransform glow;
    public RectTransform stars;
    [Header("Complete UI")]
    public RectTransform completedPuzzleParent;
    

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        slotPieces = new GameObject[maxVisible];

        SpawnPieces();
        GenerateSlotsFromPieces();
        AssignGhostImages();
        StartCoroutine(StartFlow());
    }

    // ---------------- START FLOW ----------------
    IEnumerator StartFlow()
    {
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(ScatterAndMoveToBottom());
    }

    // ---------------- SPAWN ----------------
    void SpawnPieces()
    {
        var prefabs = GetSelectedPrefabs();
        totalPieces = prefabs.Length;
        spawnedPieces = new GameObject[prefabs.Length];
        initialPositions.Clear(); // ✅ reset list

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject obj = Instantiate(prefabs[i], pieceParent, false);
            obj.SetActive(true);

            // ✅ STORE POSITION IMMEDIATELY AFTER CLONE
            RectTransform rect = obj.GetComponent<RectTransform>();
            initialPositions.Add(rect.anchoredPosition);

            PuzzlePiece piece = obj.GetComponent<PuzzlePiece>();
            if (piece != null)
            {
                piece.Setup(i);
            }

            DragPiece drag = obj.GetComponent<DragPiece>();
            if (drag != null)
            {
                drag.canDrag = false;


                drag.correctPosition = initialPositions[i];

                drag.dragArea = dragArea;
                // 🔥 POSITION GHOST
                if (drag.ghostImage != null)
                {
                    RectTransform ghostRect = drag.ghostImage.GetComponent<RectTransform>();
                    ghostRect.anchoredPosition = initialPositions[i];
                }
            }

            spawnedPieces[i] = obj;
        }
    }
    void AssignGhostImages()
    {
        for (int i = 0; i < spawnedPieces.Length; i++)
        {
            DragPiece drag = spawnedPieces[i].GetComponent<DragPiece>();

            if (drag != null && i < generatedSlots.Count)
            {
                drag.ghostImage = generatedSlots[i]; // ✅ assign slot as ghost
            }
        }
    }
    PuzzleSet GetSelectedSet()
    {
        foreach (var set in puzzleSets)
        {
            if (set.rows == rows && set.cols == cols)
                return set;
        }

        return null;
    }
   void PlayGlowEffect()
{
    glow.localScale = Vector3.one;

    glow.DOScale(1.1f, 1.2f)
        .SetEase(Ease.InOutSine)
        .SetLoops(-1, LoopType.Yoyo);
}

    void PlayBannerIdle()
    {
        // slight rotation only (premium feel)
        banner.DORotate(new Vector3(0, 0, 2f), 2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        // very small scale breathing
        banner.DOScale(1.02f, 1.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    void PlayStarsEffect()
    {
        stars.localRotation = Quaternion.identity;

        stars.DORotate(new Vector3(0, 0, 5f), 2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }
    
    void GenerateSlotsFromPieces()
    {
        var set = GetSelectedSet();
        generatedSlots.Clear();

        for (int i = 0; i < spawnedPieces.Length; i++)
        {
            RectTransform pieceRect = spawnedPieces[i].GetComponent<RectTransform>();

            GameObject slot = Instantiate(slotPrefab, slotParent);
            generatedSlots.Add(slot); // ✅ STORE SLOT

            RectTransform slotRect = slot.GetComponent<RectTransform>();

            if (slotRect == null)
            {
                Debug.LogError("❌ slotPrefab must be UI with RectTransform");
                return;
            }

            slotRect.anchoredPosition = pieceRect.anchoredPosition;

            if (set != null && set.slotSprites != null && i < set.slotSprites.Length)
            {
                UnityEngine.UI.Image img = slot.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.sprite = set.slotSprites[i];
                    img.SetNativeSize();
                    slot.SetActive(false);
                }
            }
        }
    }

    public void OnPiecePlaced(DragPiece piece)
    {
        placedCount++;

        Debug.Log($"Placed: {placedCount}/{totalPieces}");

        if (placedCount >= totalPieces)
        {
            OnPuzzleComplete();
        }
    }
    void SetupInitialPositions()
    {
        bannerTargetPos = banner.anchoredPosition;
        buttonsTargetPos = buttonsParent.anchoredPosition;

        // Move off-screen
        bannerStartPos = bannerTargetPos + new Vector2(0, 500);
        buttonsStartPos = buttonsTargetPos - new Vector2(0, 500);

        banner.anchoredPosition = bannerStartPos;
        buttonsParent.anchoredPosition = buttonsStartPos;

      //  puzzleImage.localScale = Vector3.zero;
    }

void OnPuzzleComplete()
{
    completePanel.SetActive(true);

    SetupInitialPositions();   // 🔹 prepare UI first

    MovePuzzleAnimated();      // 🔹 puzzle reacts

    // 🔹 delay banner slightly (feels premium)
    DOVirtual.DelayedCall(0.2f, () =>
    {
        PlayCompleteAnimation();
    });

    DOVirtual.DelayedCall(1f, () =>
    {
        PlayGlowEffect();
        PlayStarsEffect();
        PlayBannerIdle();
    });

    StopAllCoroutines();
}
void PlayCompleteAnimation()
{
    Sequence seq = DOTween.Sequence();

    // 🎉 Banner drop first
    seq.Append(banner.DOAnchorPos(bannerTargetPos, 0.6f)
        .SetEase(Ease.OutBack));

    // 🔘 Move buttons container
    seq.Append(buttonsParent.DOAnchorPos(buttonsTargetPos, 0.5f)
        .SetEase(Ease.OutBack));

    // 🔥 IMPORTANT: prepare buttons BEFORE animating
    List<RectTransform> buttons = new List<RectTransform>();

    for (int i = 0; i < buttonsParent.childCount; i++)
    {
        RectTransform btn = buttonsParent.GetChild(i).GetComponent<RectTransform>();

        btn.localScale = Vector3.zero;   // reset
        buttons.Add(btn);
    }

    // 👉 Now animate one-by-one
    foreach (var btn in buttons)
    {
        seq.Append(btn.DOScale(1f, 0.35f)
            .SetEase(Ease.OutBack));

        seq.AppendInterval(0.08f); // spacing between buttons
    }
}
    GameObject[] GetSelectedPrefabs()
    {
        foreach (var set in puzzleSets)
        {
            if (set.rows == rows && set.cols == cols)
                return set.prefabs;
        }

        Debug.LogError("❌ No matching prefab set found!");
        return null;
    }

    // ---------------- COMMON FLOW (USED BY START + RESET) ----------------
    IEnumerator ScatterAndMoveToBottom()
    {
        yield return new WaitForSeconds(moveDelay);

        List<GameObject> remaining = new List<GameObject>(spawnedPieces);

        while (remaining.Count > 0)
        {
            int groupSize = Random.Range(3, 5); // 3–4 pieces

            for (int i = 0; i < groupSize && remaining.Count > 0; i++)
            {
                int randIndex = Random.Range(0, remaining.Count);

                GameObject piece = remaining[randIndex];
                remaining.RemoveAt(randIndex);

                MoveToBottom(piece);
            }

            yield return new WaitForSeconds(0.1f); // small delay between groups
        }
    }
    

    public void MoveToBottom(GameObject piece)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.SetParent(bottomParent, true);

        int slot = GetRandomEmptySlot();

        if (slot != -1)
        {
            slotPieces[slot] = piece;
            bottomPieces.Add(piece);

            StartCoroutine(MoveToSlot(piece, slot, true));
        }
        else
        {
            // 🔥 NO SLOT AVAILABLE → go to overflow queue
            overflowQueue.Enqueue(piece);
            StartCoroutine(MoveToOverflowPosition(piece));
        }
    }

    void MovePuzzleAnimated()
    {
        RectTransform puzzleRect = puzzleImage.GetComponent<RectTransform>();

        // 🔹 1. Change parent FIRST (important)
        puzzleRect.SetParent(completedPuzzleParent, false);
        puzzleRect.SetAsLastSibling();

        // optional: ensure correct final position
        puzzleRect.anchoredPosition = Vector2.zero;

        Sequence seq = DOTween.Sequence();

        // 🔹 2. Shake
        seq.Append(puzzleRect.DOShakeRotation(0.4f, new Vector3(0, 0, 8f), 10, 90, false)
            .SetEase(Ease.OutCubic));

        // 🔹 3. Scale punch
        seq.Join(puzzleRect.DOScale(1.1f, 0.3f)
            .SetEase(Ease.OutBack));

        // 🔹 4. Settle
        seq.Append(puzzleRect.DOScale(0.85f, 0.3f)
            .SetEase(Ease.OutBack));
    }
    IEnumerator MoveToOverflowPosition(GameObject piece)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();

        Vector2 start = rect.anchoredPosition;

        // 👉 each overflow piece gets unique position
        float x = (maxVisible + overflowIndex) * spacing;

        Vector2 target = new Vector2(x, 0f);

        overflowIndex++;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            rect.anchoredPosition = Vector2.Lerp(start, overflowTarget.anchoredPosition, t);
            yield return null;
        }

        rect.anchoredPosition = target;

        piece.SetActive(false);
        // rect.anchoredPosition = new Vector3
    }
    int GetRandomEmptySlot()
    {
        List<int> empty = new List<int>();

        for (int i = 0; i < maxVisible; i++)
        {
            if (slotPieces[i] == null)
                empty.Add(i);
        }

        if (empty.Count == 0)
            return -1;

        return empty[Random.Range(0, empty.Count)];
    }




    // ---------------- ADD TO BOTTOM ----------------
    public void AddToBottom(GameObject piece)
    {
        if (piece == null) return;
        if (bottomPieces.Contains(piece)) return;

        piece.SetActive(true);
        piece.transform.SetParent(bottomParent, false);

        bottomPieces.Add(piece);

        int index = bottomPieces.Count - 1;
        Vector2 targetPos = GetSlotPositionUI(index);

        if (moveRoutines.ContainsKey(piece))
        {
            StopCoroutine(moveRoutines[piece]);
        }

        Coroutine move = StartCoroutine(MoveToSlot(piece, index, true));
    }


    // ---------------- SLOT POSITION ----------------
    Vector2 GetSlotPositionUI(int index)
    {
        float x = index * spacing; // ✅ fixed, no shifting
        return new Vector2(x, 0f);
    }

    // ---------------- MOVE ----------------
    IEnumerator MoveToSlot(GameObject piece, int slotIndex, bool occupySlot)
    {
        RectTransform rect = piece.GetComponent<RectTransform>();

        Vector2 start = rect.anchoredPosition;
        Vector2 target;

        if (slotIndex == -1)
        {
            // move somewhere off-screen or default bottom position
            target = new Vector2(0, -300);
        }
        else
        {
            target = GetSlotPositionUI(slotIndex);
        }

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            rect.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        rect.anchoredPosition = target;

        // ✔ after reaching destination
        DragPiece drag = piece.GetComponent<DragPiece>();
        if (drag != null)
        {
            drag.canDrag = true; // ✅ ENABLE DRAG HERE
        }

        if (!occupySlot)
        {
            piece.SetActive(false);
        }
    }
    public void RemoveFromBottom(GameObject piece)
    {
        int index = System.Array.IndexOf(slotPieces, piece);

        if (index >= 0)
        {
            slotPieces[index] = null;
        }

        bottomPieces.Remove(piece);

        // ❌ DO NOT disable → user is dragging it
        // piece.SetActive(false);

        // 🔥 REARRANGE remaining pieces
        RearrangeBottom();

        // 🔥 FILL EMPTY SLOTS FROM OVERFLOW
        FillFromOverflow();
    }

    void RearrangeBottom()
    {
        List<GameObject> newList = new List<GameObject>();

        // collect valid pieces
        foreach (var p in slotPieces)
        {
            if (p != null)
                newList.Add(p);
        }

        // reset slots
        for (int i = 0; i < maxVisible; i++)
        {
            slotPieces[i] = null;
        }

        // reassign compact positions
        for (int i = 0; i < newList.Count; i++)
        {
            GameObject piece = newList[i];
            slotPieces[i] = piece;

            StartCoroutine(MoveToSlot(piece, i, true));
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

                RectTransform rect = piece.GetComponent<RectTransform>();
                rect.SetParent(bottomParent, true);

                slotPieces[i] = piece;
                bottomPieces.Add(piece);

                StartCoroutine(MoveToSlot(piece, i, true));
            }
        }
    }
    void TryFillRandomSlot()
    {
        if (nextSpawnIndex >= spawnedPieces.Length)
            return;

        int slot = GetRandomEmptySlot();
        if (slot == -1)
            return;

        GameObject piece = spawnedPieces[nextSpawnIndex];
        nextSpawnIndex++;

        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.SetParent(bottomParent, true);

        slotPieces[slot] = piece;
        bottomPieces.Add(piece);

        StartCoroutine(MoveToSlot(piece, slot, true));
    }

    // ---------------- RESET (🔥 MAIN FEATURE) ----------------
    public void ResetAllPieces()
    {
        StopAllCoroutines();

        bottomPieces.Clear();
        moveRoutines.Clear();

        foreach (var piece in spawnedPieces)
        {
            var drag = piece.GetComponent<DragPiece>();
            if (drag != null)
                drag.ResetPiece();
        }

        StartCoroutine(ScatterAndMoveToBottom());
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