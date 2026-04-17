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

    public void Setup(int index)
    {
        stencilID = index + 1;

        ApplyMaterial(maskImage, ref maskMat);
        ApplyMaterial(contentImage, ref contentMat);

        if (shadowImage != null)
            ApplyMaterial(shadowImage, ref shadowMat);

        ApplyStencil(maskMat);
        ApplyStencil(contentMat);

        if (shadowMat != null)
            ApplyStencil(shadowMat);
    }

    void ApplyMaterial(Image img, ref Material matRef)
    {
        if (img == null) return;

        // IMPORTANT: fully isolate material instance
        matRef = Instantiate(img.material);
        img.material = matRef;
    }

    void ApplyStencil(Material mat)
    {
        if (mat == null) return;

        mat.SetFloat("_StencilID", stencilID);
    }
}