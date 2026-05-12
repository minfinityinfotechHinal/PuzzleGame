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

        // Anchor = piece with smallest (row, col) — top-left of the cluster
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

    /// <summary>
    /// Moves every piece in this group by <paramref name="delta"/> in
    /// anchored-position space (used during group drag).
    /// </summary>
    public void Move(Vector2 delta)
    {
        foreach (var piece in pieces)
        {
            RectTransform rect = piece.GetComponent<RectTransform>();

            if (rect != null)
                rect.anchoredPosition += delta;
        }
    }

    /// <summary>
    /// Absorbs all pieces from <paramref name="otherGroup"/> into THIS group
    /// and updates every piece's <c>group</c> reference so there are no
    /// dangling pointers.
    ///
    /// This group becomes the SURVIVING group.  The caller should discard
    /// <paramref name="otherGroup"/> after calling this.
    /// </summary>
    public void Merge(PuzzleGroup otherGroup)
    {
        if (otherGroup == null || otherGroup == this) return;

        // Snapshot so we don't mutate while iterating
        List<PuzzlePiece> incoming = new List<PuzzlePiece>(otherGroup.pieces);

        foreach (var piece in incoming)
        {
            if (!pieces.Contains(piece))
            {
                pieces.Add(piece);
            }
            // Always update the reference — even if already in the list —
            // in case a stale reference survived from a previous partial merge.
            piece.group = this;
        }

        // Clear the now-obsolete group so stale references are obvious
        otherGroup.pieces.Clear();
        otherGroup.anchorPiece = null;

        UpdateAnchor();
    }

    // ------------------------------------------------------------------
    // Utility helpers used by DragPiece snap-to-correct-position checks
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns true if ANY piece in the group is within <paramref name="threshold"/>
    /// of its correct anchored position.
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
    /// Returns the piece in the group that is closest to its correct position.
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
