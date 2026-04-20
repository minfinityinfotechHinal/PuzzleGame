using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public GameObject slotPrefab;
    public int rows = 4;
    public int cols = 4;
    public float spacing = 1.2f;

    public Transform[] slots;

    public void GenerateGrid()
    {
        slots = new Transform[rows * cols];

        int index = 0;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Vector3 pos = new Vector3(x * spacing, -y * spacing, 0);

                GameObject slot = Instantiate(slotPrefab, pos, Quaternion.identity, transform);

                slots[index] = slot.transform;
                index++;
            }
        }
    }
}