using UnityEngine;
using UnityEngine.UI;

public class PuzzlePiece : MonoBehaviour
{
    public Image maskImage;
    public Image contentImage;
    public Image shadowImage;

    private int stencilID;

    public void Setup(int index)
    {
        stencilID = index + 1;

        // 🔥 Clone materials (VERY IMPORTANT in UI also)
        maskImage.material = new Material(maskImage.material);
        contentImage.material = new Material(contentImage.material);

        if (shadowImage != null)
            shadowImage.material = new Material(shadowImage.material);

        // ✅ Stencil
        maskImage.material.SetFloat("_StencilID", stencilID);
        contentImage.material.SetFloat("_StencilID", stencilID);

        if (shadowImage != null)
            shadowImage.material.SetFloat("_StencilID", stencilID);
    }


}