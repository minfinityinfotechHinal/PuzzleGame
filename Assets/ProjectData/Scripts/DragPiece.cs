using UnityEngine;

public class DragPiece : MonoBehaviour
{
    private Vector3 offset;
    private PuzzlePiece piece;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;

        piece = GetComponent<PuzzlePiece>();

        if (piece == null)
            piece = GetComponentInParent<PuzzlePiece>();

        if (piece == null)
            Debug.LogError("PuzzlePiece script NOT found on " + gameObject.name);
    }

    void OnMouseDown()
    {
        if (piece == null || piece.isPlaced) return;

        // 🔥 Bring to top
        piece.BringToFront();

        // (Optional) Move out of panel to board
        // transform.SetParent(piece.boardParent);

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = cam.WorldToScreenPoint(transform.position).z;
        mousePos = cam.ScreenToWorldPoint(mousePos);

        offset = transform.position - mousePos;
    }

    void OnMouseDrag()
    {
        if (piece == null || piece.isPlaced) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = cam.WorldToScreenPoint(transform.position).z;
        mousePos = cam.ScreenToWorldPoint(mousePos);

        transform.position = mousePos + offset;
    }

    void OnMouseUp()
    {
        if (piece == null || piece.isPlaced) return;

        // 🎯 SNAP CHECK
        float dist = Vector3.Distance(transform.position, piece.targetSlot.position);

        if (dist < piece.snapDistance)
        {
            // Snap to correct position
            transform.position = piece.targetSlot.position;

            piece.isPlaced = true;

            // Disable further dragging
            enabled = false;
        }
    }
}