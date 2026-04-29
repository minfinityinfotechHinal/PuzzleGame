using UnityEngine;
using UnityEngine.UI;

public class PuzzlePiece : MonoBehaviour
{
    public Image maskImage;
    public Image contentImage;
    public Image shadowImage;

    private int stencilID;
    private Material maskMat;
    private Material contentMat;
    private Material shadowMat;

    public PuzzleGroup group;
    public int row;
    public int col;
    public PuzzlePiece left;
    public PuzzlePiece right;
    public PuzzlePiece top;
    public PuzzlePiece bottom;

    public void Setup(int index)
    {
        stencilID = index + 1;

        ApplyMaterial(maskImage, ref maskMat);
        ApplyMaterial(contentImage, ref contentMat);

        if (shadowImage != null)
        {
            ApplyMaterial(shadowImage, ref shadowMat);
            
            // 👇 ADD THIS: Set shadow to render behind the piece
            SetupShadowSorting();
        }

        ApplyStencil(maskMat);
        ApplyStencil(contentMat);

        if (shadowMat != null)
            ApplyStencil(shadowMat);

        group = new PuzzleGroup();
        group.AddPiece(this);
    }

    // 👇 ADD THIS NEW METHOD
    private void SetupShadowSorting()
{
    // Don't set a fixed order here - let DragPiece manage it
    Canvas shadowCanvas = shadowImage.GetComponent<Canvas>();
    if (shadowCanvas == null)
        shadowCanvas = shadowImage.gameObject.AddComponent<Canvas>();
    
    shadowCanvas.overrideSorting = true;
    // Don't set sortingOrder here - it will be set by DragPiece.SetPieceSortingOrder
    
    // Make sure shadow doesn't block raycasts
    shadowImage.raycastTarget = false;
}

    void ApplyMaterial(Image img, ref Material matRef)
    {
        if (img == null) return;
        matRef = Instantiate(img.material);
        img.material = matRef;
    }

    void ApplyStencil(Material mat)
    {
        if (mat == null) return;
        mat.SetFloat("_StencilID", stencilID);
    }
}