using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PuzzleGroup
{
    public List<PuzzlePiece> pieces = new List<PuzzlePiece>();

    public PuzzlePiece anchorPiece;

    // IMPORTANT
    public bool isPlacedGroup = false;

    public void AddPiece(PuzzlePiece piece)
    {
        if (piece == null)
            return;

        if (!pieces.Contains(piece))
        {
            pieces.Add(piece);

            piece.group = this;

            UpdateAnchor();
        }
    }

    public void RemovePiece(PuzzlePiece piece)
    {
        if (piece == null)
            return;

        pieces.Remove(piece);

        if (pieces.Count > 0)
        {
            UpdateAnchor();
        }
        else
        {
            anchorPiece = null;
        }
    }

    private void UpdateAnchor()
    {
        if (pieces.Count == 0)
        {
            anchorPiece = null;
            return;
        }

        anchorPiece = pieces[0];

        foreach (PuzzlePiece piece in pieces)
        {
            if (piece.row < anchorPiece.row ||
               (piece.row == anchorPiece.row &&
                piece.col < anchorPiece.col))
            {
                anchorPiece = piece;
            }
        }
    }

    public void Move(Vector2 delta)
    {
        foreach (PuzzlePiece piece in pieces)
        {
            if (piece == null) continue;

            RectTransform rect = piece.GetComponent<RectTransform>();
            if (rect == null) continue;

            // Only move pieces that are NOT already placed
            DragPiece drag = piece.GetComponent<DragPiece>();
            if (drag != null && drag.isPlaced) continue;

            rect.anchoredPosition += delta;
        }
    }

    public void Merge(PuzzleGroup otherGroup)
    {
        if (otherGroup == null ||
            otherGroup == this)
            return;

        foreach (PuzzlePiece piece in otherGroup.pieces)
        {
            if (piece == null)
                continue;

            if (!pieces.Contains(piece))
            {
                pieces.Add(piece);

                piece.group = this;
            }
        }

        // PRESERVE PLACED STATE
        if (otherGroup.isPlacedGroup)
        {
            isPlacedGroup = true;
        }

        UpdateAnchor();
    }

    public bool IsAnyPieceNearCorrectPosition(float threshold)
    {
        foreach (PuzzlePiece piece in pieces)
        {
            if (piece == null)
                continue;

            DragPiece drag =
                piece.GetComponent<DragPiece>();

            RectTransform rect =
                piece.GetComponent<RectTransform>();

            if (drag != null && rect != null)
            {
                float dist =
                    Vector2.Distance(
                        rect.anchoredPosition,
                        drag.correctPosition);

                if (dist <= threshold)
                    return true;
            }
        }

        return false;
    }

    public PuzzlePiece GetClosestToCorrectPosition()
    {
        PuzzlePiece closest = null;

        float closestDist = float.MaxValue;

        foreach (PuzzlePiece piece in pieces)
        {
            if (piece == null)
                continue;

            DragPiece drag =
                piece.GetComponent<DragPiece>();

            RectTransform rect =
                piece.GetComponent<RectTransform>();

            if (drag != null && rect != null)
            {
                float dist =
                    Vector2.Distance(
                        rect.anchoredPosition,
                        drag.correctPosition);

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