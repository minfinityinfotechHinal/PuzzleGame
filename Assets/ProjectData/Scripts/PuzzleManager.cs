using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    public PuzzlePiece[] pieces;

    void Start()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].Setup(i); // 👈 THIS IS MUST
        }
    }
}