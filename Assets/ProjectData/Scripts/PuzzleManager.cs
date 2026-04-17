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
    private Dictionary<GameObject, Coroutine> moveRoutines = new Dictionary<GameObject, Coroutine>();
    
    void Start()
    {
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

    for (int i = 0; i < spawnedPieces.Length; i++)
    {
        MoveToBottom(spawnedPieces[i]);
        yield return new WaitForSeconds(0.05f);
    }
}

public void MoveToBottom(GameObject piece)
{
    RectTransform rect = piece.GetComponent<RectTransform>();

    rect.SetParent(bottomParent, true); // 🔥 KEY FIX

    bottomPieces.Add(piece);

    int index = bottomPieces.Count - 1;

    StartCoroutine(MoveAndSet(piece, index));
}

IEnumerator MoveAndSet(GameObject piece, int index)
{
    RectTransform rect = piece.GetComponent<RectTransform>();

    Vector2 start = rect.anchoredPosition;
    Vector2 target = new Vector2(index * spacing, 0);

    yield return null;

    float t = 0;

    while (t < 1f)
    {
        t += Time.deltaTime * moveSpeed;
        rect.anchoredPosition = Vector2.Lerp(start, target, t);
        yield return null;
    }

    rect.anchoredPosition = target;

    // 🔥 IMPORTANT RULE (AFTER ARRIVAL ONLY)
    piece.SetActive(index < maxVisible);
}

void UpdateSlots()
{
    for (int i = 0; i < activeBottom.Count; i++)
    {
        bool visible = i < maxVisible;
        activeBottom[i].SetActive(visible);
    }
}

   IEnumerator RearrangeAll()
    {
        yield return null;

        for (int i = 0; i < bottomPieces.Count; i++)
        {
            GameObject piece = bottomPieces[i];

            Vector2 targetPos = GetSlotPositionUI(i);

            // Stop old movement if exists
            if (moveRoutines.ContainsKey(piece) && moveRoutines[piece] != null)
            {
                StopCoroutine(moveRoutines[piece]);
            }

            // Start new movement
            Coroutine move = StartCoroutine(MoveToSlot(piece, targetPos));
            moveRoutines[piece] = move;
        }
    }


    void TryAddNextPiece()
    {
        if (nextSpawnIndex >= spawnedPieces.Length) return;

        if (bottomPieces.Count >= maxVisible) return;

        GameObject nextPiece = spawnedPieces[nextSpawnIndex];
        nextSpawnIndex++;

        AddToBottom(nextPiece);
    }

    void RefillSlots()
{
    for (int i = 0; i < activeBottom.Count; i++)
    {
        GameObject piece = activeBottom[i];

        StartCoroutine(Move(piece, GetSlotPositionUI(i)));

        piece.SetActive(i < maxVisible);
    }
}

IEnumerator Move(GameObject piece, Vector2 target)
{
    RectTransform rect = piece.GetComponent<RectTransform>();

    Vector2 start = rect.anchoredPosition;

    yield return null;

    float t = 0;

    while (t < 1f)
    {
        t += Time.deltaTime * moveSpeed;
        rect.anchoredPosition = Vector2.Lerp(start, target, t);
        yield return null;
    }

    rect.anchoredPosition = target;
}

   void RearrangeVisible()
{
    for (int i = 0; i < bottomPieces.Count; i++)
    {
        GameObject piece = bottomPieces[i];

        Vector2 targetPos = GetSlotPositionUI(i);

        if (moveRoutines.ContainsKey(piece))
        {
            StopCoroutine(moveRoutines[piece]);
        }

        moveRoutines[piece] = StartCoroutine(MoveToSlot(piece, targetPos));
    }

    
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

    moveRoutines[piece] = StartCoroutine(MoveToSlot(piece, targetPos));
}


    // ---------------- SLOT POSITION ----------------
    Vector2 GetSlotPositionUI(int index)
{
    float x = index * spacing; // ✅ fixed, no shifting
    return new Vector2(x, 0f);
}

    // ---------------- MOVE ----------------
IEnumerator MoveToSlot(GameObject piece, Vector2 target)
{
    RectTransform rect = piece.GetComponent<RectTransform>();

    Vector2 start = rect.anchoredPosition;

    yield return null;
    start = rect.anchoredPosition;

    float t = 0f;

    while (t < 1f)
    {
        t += Time.deltaTime * moveSpeed;
        rect.anchoredPosition = Vector2.Lerp(start, target, t);
        yield return null;
    }

    rect.anchoredPosition = target;

    int index = bottomPieces.IndexOf(piece);

    // 🔥 ONLY AFTER reaching position decide visibility
    if (index >= maxVisible)
    {
        piece.SetActive(false);
    }
}

public void RemoveFromBottom(GameObject piece)
{
    if (!bottomPieces.Contains(piece)) return;

    bottomPieces.Remove(piece);
    piece.SetActive(false);

    Rearrange();
    TryAddNextPiece();
}
void Rearrange()
{
    for (int i = 0; i < bottomPieces.Count; i++)
    {
        StartCoroutine(MoveAndSet(bottomPieces[i], i));
    }
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