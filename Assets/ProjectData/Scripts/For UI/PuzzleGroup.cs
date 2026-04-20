using System.Collections.Generic;
using UnityEngine;

public class PuzzleGroup
{
    public List<PuzzlePiece> pieces = new List<PuzzlePiece>();

    public void AddPiece(PuzzlePiece piece)
    {
        if (!pieces.Contains(piece))
        {
            pieces.Add(piece);
            piece.group = this;
        }
    }

    public void Merge(PuzzleGroup other)
    {
        if (other == this) return;

        foreach (var p in other.pieces)
        {
            AddPiece(p);
        }
    }

    public void Move(Vector2 delta)
    {
        foreach (var p in pieces)
        {
            RectTransform rect = p.GetComponent<RectTransform>();
            rect.anchoredPosition += delta;
        }
    }
}