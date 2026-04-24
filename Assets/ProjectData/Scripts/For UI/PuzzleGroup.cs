using System.Collections.Generic;
using UnityEngine;

public class PuzzleGroup
{
    public List<PuzzlePiece> pieces = new List<PuzzlePiece>();

    public void AddPiece(PuzzlePiece piece)
    {
        if (!pieces.Contains(piece))
        {
            if (piece.group != null && piece.group != this)
            {
                piece.group.pieces.Remove(piece);  // Remove from old group
            }
            pieces.Add(piece);
            piece.group = this;  // Point to new group
        }
    }
    public void Merge(PuzzleGroup other)
    {
        if (other == this) return;

        // Copy the pieces before modifying
        List<PuzzlePiece> piecesToAdd = new List<PuzzlePiece>(other.pieces);
        
        foreach (var p in piecesToAdd)
        {
            AddPiece(p);
        }
        
        // 🔥 Clear the old group so no pieces are left behind
        other.pieces.Clear();
    }

    public void Move(Vector2 delta)
    {
        foreach (var p in pieces)
        {
            if (p != null)
            {
                RectTransform rect = p.GetComponent<RectTransform>();
                DragPiece drag = p.GetComponent<DragPiece>();
                
                // 🔥 Only move pieces in the puzzle area, not in bottom tray
                if (rect != null && drag != null && !drag.isPlaced && rect.parent == PuzzleManager.Instance.pieceParent)
                {
                    rect.anchoredPosition += delta;
                }
            }
        }
    }
}