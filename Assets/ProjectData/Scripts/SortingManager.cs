using UnityEngine;

public class SortingManager : MonoBehaviour
{
    private static int order = 1000;

    public static int GetTopOrder()
    {
        order += 10;
        return order;
    }
}