using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PuzzlePiece : MonoBehaviour
{
    public Image maskImage;
    public Image contentImage;
    public Image shadowImage;

    private int stencilID;

    private Material maskMat;
    private Material contentMat;
    private Material shadowMat;

    // 🔥 NEW
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
            ApplyMaterial(shadowImage, ref shadowMat);

        ApplyStencil(maskMat);
        ApplyStencil(contentMat);

        if (shadowMat != null)
            ApplyStencil(shadowMat);

        // 🔥 CREATE GROUP
        group = new PuzzleGroup();
        group.AddPiece(this);
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