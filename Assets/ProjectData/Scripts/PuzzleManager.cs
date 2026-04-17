using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
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

    
    void Start()
    {
        slotPieces = new GameObject[maxVisible];
        SpawnPieces();
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

        spawnedPieces = new GameObject[prefabs.Length];

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject obj = Instantiate(prefabs[i], pieceParent, false);
            obj.SetActive(true);

            PuzzlePiece piece = obj.GetComponent<PuzzlePiece>();
            if (piece != null)
            {
                piece.Setup(i); // 🔥 THIS IS REQUIRED
            }

            spawnedPieces[i] = obj;
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
    if (!occupySlot)
    {
        piece.SetActive(false);
    }
}
public void RemoveFromBottom(GameObject piece)
{
    int index = System.Array.IndexOf(slotPieces, piece);

    if (index >= 0)
        slotPieces[index] = null;

    bottomPieces.Remove(piece);

    piece.SetActive(false);

    TryFillRandomSlot();
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
}