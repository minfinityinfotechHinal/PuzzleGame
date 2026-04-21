using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public GameObject slotPrefab;
    public int rows = 4;
    public int cols = 4;
    public float spacing = 1.2f;

    public RectTransform[] slots;

    public void GenerateGrid()
    {
        slots = new RectTransform[rows * cols];

        int index = 0;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject slot = Instantiate(slotPrefab, transform);

                RectTransform rect = slot.GetComponent<RectTransform>();

                // 🔥 FORCE RectTransform if missing
                if (rect == null)
                    rect = slot.AddComponent<RectTransform>();

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                // 🔥 IMPORTANT: use proper spacing (UI scale)
                float offsetX = (cols - 1) * spacing * 0.5f;
                float offsetY = (rows - 1) * spacing * 0.5f;

                Vector2 pos = new Vector2(
                    x * spacing - offsetX,
                    -(y * spacing - offsetY)
                );

                rect.anchoredPosition = pos;

                slots[index] = rect;
                index++;
            }
        }
    }
}