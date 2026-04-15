using UnityEngine;

public class PuzzlePiece : MonoBehaviour
{
    public SpriteRenderer maskRenderer;
    public SpriteRenderer contentRenderer;
    public SpriteRenderer shadowRenderer; // 🔥 ADD THIS

    // 🔥 Drag & Snap system
    public Transform targetSlot;
    public float snapDistance = 0.5f;
    public bool isPlaced = false;

    private int stencilID;

    public void Setup(int index)
    {
        stencilID = index + 1;

        // Create unique material instances
        Material maskMat = maskRenderer.material;
        Material contentMat = contentRenderer.material;

        maskMat.SetFloat("_StencilID", stencilID);
        contentMat.SetFloat("_StencilID", stencilID);

        // 🔥 Apply to shadow also (IMPORTANT)
        if (shadowRenderer != null)
        {
            Material shadowMat = shadowRenderer.material;
            shadowMat.SetFloat("_StencilID", stencilID);
        }

        Debug.Log(gameObject.name + " Stencil ID: " + stencilID);
    }

    // 🔥 FIXED: proper bring to front
    public void BringToFront()
    {
        int top = SortingManager.GetTopOrder();

        maskRenderer.sortingOrder = top;
        
        if (shadowRenderer != null)
            shadowRenderer.sortingOrder = top + 1;

        contentRenderer.sortingOrder = top + 1; // 🔥 ALWAYS TOP
    }
    }