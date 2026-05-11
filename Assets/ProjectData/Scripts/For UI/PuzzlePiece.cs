using UnityEngine;
using UnityEngine.UI;

public class PuzzlePiece : MonoBehaviour
{
    [Header("Images")]
    public Image maskImage;
    public Image contentImage;
    public Image shadowImage;

    [Header("Stencil")]
    private int stencilID;
    private Material maskMat;
    private Material contentMat;
    private Material shadowMat;

    [Header("Group")]
    public PuzzleGroup group;

    [Header("Grid Info")]
    public int row;
    public int col;

    [Header("Neighbours")]
    public PuzzlePiece left;
    public PuzzlePiece right;
    public PuzzlePiece top;
    public PuzzlePiece bottom;

    // =========================================================
    // SETUP
    // =========================================================

    public void Setup(int index)
    {
        stencilID = index + 2;

        // Create unique materials
        ApplyMaterial(maskImage, ref maskMat);
        ApplyMaterial(contentImage, ref contentMat);

        // Shadow
        if (shadowImage != null)
        {
            ApplyMaterial(shadowImage, ref shadowMat);

            // Shadow should never block touches
            shadowImage.raycastTarget = false;

            // Keep shadow behind all visuals
            shadowImage.transform.SetAsFirstSibling();
        }

        // Apply stencil values
        ApplyStencil(maskMat);
        ApplyStencil(contentMat);

        if (shadowMat != null)
            ApplyStencil(shadowMat);

        // Create initial group
        group = new PuzzleGroup();
        group.AddPiece(this);
    }

    // =========================================================
    // MATERIAL HELPERS
    // =========================================================

    private void ApplyMaterial(Image img, ref Material matRef)
    {
        if (img == null)
            return;

        matRef = Instantiate(img.material);
        img.material = matRef;
    }

    private void ApplyStencil(Material mat)
    {
        if (mat == null)
            return;

        mat.SetFloat("_StencilID", stencilID);
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (maskMat != null)
            Destroy(maskMat);

        if (contentMat != null)
            Destroy(contentMat);

        if (shadowMat != null)
            Destroy(shadowMat);
    }
}