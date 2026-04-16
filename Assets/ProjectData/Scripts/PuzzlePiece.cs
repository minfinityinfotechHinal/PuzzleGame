using UnityEngine;

public class PuzzlePiece : MonoBehaviour
{
    public SpriteRenderer maskRenderer;
    public SpriteRenderer contentRenderer;
    public SpriteRenderer shadowRenderer;

    // 🎯 Drag & Snap
    public Transform targetSlot;
    public float snapDistance = 0.5f;
    public bool isPlaced = false;
    private Vector3 correctPosition;

    private int stencilID;

    public void Setup(int index)
    {
        stencilID = index + 1;

        // 🔥 Clone materials (IMPORTANT)
        maskRenderer.material = new Material(maskRenderer.material);
        contentRenderer.material = new Material(contentRenderer.material);

        if (shadowRenderer != null)
            shadowRenderer.material = new Material(shadowRenderer.material);

        // ✅ Assign stencil
        maskRenderer.material.SetFloat("_StencilID", stencilID);
        contentRenderer.material.SetFloat("_StencilID", stencilID);

        if (shadowRenderer != null)
            shadowRenderer.material.SetFloat("_StencilID", stencilID);
    }

    public void SetImageOffset(int index, int rows, int cols)
{
    int row = index / cols;
    int col = index % cols;

    float offsetX = (float)col / cols;
    float offsetY = (float)row / rows;

    float scaleX = 1f / cols;
    float scaleY = 1f / rows;

    // Apply to material
    contentRenderer.material.SetTextureScale("_MainTex", new Vector2(scaleX, scaleY));
    contentRenderer.material.SetTextureOffset("_MainTex", new Vector2(offsetX, offsetY));
}

    public void StoreCorrectPosition()
    {
        correctPosition = transform.position;
    }

    public bool CanSnap(Vector3 currentPos)
    {
        return Vector3.Distance(currentPos, correctPosition) < snapDistance;
    }

    public void Snap()
    {
        transform.position = correctPosition;
        isPlaced = true;
    }

    public void BringToFront()
    {
        int top = SortingManager.GetTopOrder();

        maskRenderer.sortingOrder = top;
        contentRenderer.sortingOrder = top + 1;

        if (shadowRenderer != null)
            shadowRenderer.sortingOrder = top + 2; // 👈 shadow on TOP (your requirement)
    }

    
}