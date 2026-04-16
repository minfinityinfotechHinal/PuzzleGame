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
    public Transform pieceParent;   // spawn area
    public Transform bottomParent;  // bottom tray root

    [Header("Bottom Settings")]
    public float spacing = 1.5f;
    public float moveSpeed = 8f;
    public float moveDelay = 1.5f;

    private GameObject[] spawnedPieces;
    private List<GameObject> bottomPieces = new List<GameObject>();

    void Start()
    {
        SpawnPieces();
     //   StartCoroutine(MoveToBottomAreaAfterDelay());
    }

    // ---------------- SELECT PREFABS ----------------
   Vector3 GetSlotPosition(int index)
{
    int count = bottomPieces.Count;

    float totalWidth = (count - 1) * spacing;
    float startX = -totalWidth / 2f;

    float x = startX + index * spacing;

    return bottomParent.position + new Vector3(x, 0f, 0f);
}
    // ---------------- SPAWN ----------------
    void SpawnPieces()
    {
        GameObject[] selectedPrefabs = GetSelectedPrefabs();

        if (selectedPrefabs == null || selectedPrefabs.Length == 0)
            return;

        spawnedPieces = new GameObject[selectedPrefabs.Length];

        for (int i = 0; i < selectedPrefabs.Length; i++)
        {
            GameObject obj = Instantiate(selectedPrefabs[i]);

            obj.transform.SetParent(pieceParent, false);

            PuzzlePiece piece = obj.GetComponent<PuzzlePiece>();

            if (piece != null)
            {
                piece.Setup(i); // ✅ assign unique stencil
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

    // ---------------- MOVE TO BOTTOM AFTER DELAY ----------------
    IEnumerator MoveToBottomAreaAfterDelay()
    {
        yield return new WaitForSeconds(moveDelay);

        for (int i = 0; i < spawnedPieces.Length; i++)
        {
            AddToBottom(spawnedPieces[i]);
        }
    }

    // ---------------- ADD TO BOTTOM ----------------
    public void AddToBottom(GameObject piece)
    {
        if (piece == null) return;

        bottomPieces.Add(piece);

        piece.transform.SetParent(bottomParent, true);

        StartCoroutine(MoveToSlot(piece));
    }

    IEnumerator MoveToSlot(GameObject piece)
    {
        int index = bottomPieces.IndexOf(piece);

        Vector3 start = piece.transform.position;
        Vector3 target = GetSlotPosition(index);

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            piece.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        piece.transform.position = target;
    }

    // ---------------- SLOT POSITION ----------------
 

    // ---------------- REMOVE (CALL FROM DRAG SCRIPT) ----------------
    public void RemoveFromBottom(GameObject piece)
    {
        if (!bottomPieces.Contains(piece)) return;

        bottomPieces.Remove(piece);

        ReArrangeBottom();
    }

    // ---------------- REARRANGE AFTER REMOVE ----------------
    void ReArrangeBottom()
    {
        for (int i = 0; i < bottomPieces.Count; i++)
        {
            StartCoroutine(MoveToNewSlot(bottomPieces[i], i));
        }
    }

    IEnumerator MoveToNewSlot(GameObject piece, int index)
    {
        Vector3 start = piece.transform.position;
        Vector3 target = GetSlotPosition(index);

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            piece.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        piece.transform.position = target;
    }
}

[System.Serializable]
public class PuzzleSet
{
    public int rows;
    public int cols;
    public GameObject[] prefabs;
}