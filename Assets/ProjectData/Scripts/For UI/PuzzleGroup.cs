using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PuzzleGroup
{
    public List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    public PuzzlePiece anchorPiece;
    
    public void AddPiece(PuzzlePiece piece)
    {
        if (!pieces.Contains(piece))
        {
            pieces.Add(piece);
            piece.group = this;
            UpdateAnchor();
        }
    }
    
    public void RemovePiece(PuzzlePiece piece)
    {
        pieces.Remove(piece);
        if (pieces.Count > 0)
            UpdateAnchor();
    }
    
    private void UpdateAnchor()
    {
        if (pieces.Count == 0)
        {
            anchorPiece = null;
            return;
        }
        
        // Find piece with smallest row and col as anchor
        anchorPiece = pieces[0];
        foreach (var piece in pieces)
        {
            if (piece.row < anchorPiece.row || 
                (piece.row == anchorPiece.row && piece.col < anchorPiece.col))
            {
                anchorPiece = piece;
            }
        }
    }
    
    public void Move(Vector2 delta)
    {
        foreach (var piece in pieces)
        {
            RectTransform rect = piece.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition += delta;
            }
        }
    }
    
    public void Merge(PuzzleGroup otherGroup)
    {
        if (otherGroup == this || otherGroup == null) return;
        
        // Add all pieces from other group
        List<PuzzlePiece> otherPieces = new List<PuzzlePiece>(otherGroup.pieces);
        foreach (var piece in otherPieces)
        {
            if (!pieces.Contains(piece))
            {
                pieces.Add(piece);
                piece.group = this;
            }
        }
        
        // Update anchor after merge
        UpdateAnchor();
    }
    
    /// <summary>
    /// Check if ANY piece in group is near its correct position
    /// </summary>
    public bool IsAnyPieceNearCorrectPosition(float threshold)
    {
        foreach (var piece in pieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            RectTransform rect = piece.GetComponent<RectTransform>();
            
            if (drag != null && rect != null)
            {
                float dist = Vector2.Distance(rect.anchoredPosition, drag.correctPosition);
                if (dist <= threshold)
                    return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get the piece closest to its correct position
    /// </summary>
    public PuzzlePiece GetClosestToCorrectPosition()
    {
        PuzzlePiece closest = null;
        float closestDist = float.MaxValue;
        
        foreach (var piece in pieces)
        {
            DragPiece drag = piece.GetComponent<DragPiece>();
            RectTransform rect = piece.GetComponent<RectTransform>();
            
            if (drag != null && rect != null)
            {
                float dist = Vector2.Distance(rect.anchoredPosition, drag.correctPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = piece;
                }
            }
        }
        return closest;
    }
}