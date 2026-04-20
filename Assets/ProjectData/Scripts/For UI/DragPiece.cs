using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool isPlaced = false;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Vector2 startPos;
   
   
    public float snapThreshold = 120;   // how close is "correct"   
    public bool canDrag = false; 
    public RectTransform dragArea;
    public Vector2 correctPosition;
    public GameObject ghostImage;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (ghostImage != null)
        ghostImage.SetActive(false);
    }

   public void OnBeginDrag(PointerEventData eventData)
{
    if (isPlaced || !canDrag) return;

    startPos = rectTransform.anchoredPosition;

    canvasGroup.blocksRaycasts = false;

    // 🔥 INFORM MANAGER BEFORE MOVING
    PuzzleManager.Instance.RemoveFromBottom(gameObject);

    transform.SetParent(PuzzleManager.Instance.pieceParent, true);
    transform.SetAsLastSibling();
}

   public void OnDrag(PointerEventData eventData)
{
    if (isPlaced || !canDrag) return;

    rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    float distance = Vector2.Distance(
    rectTransform.anchoredPosition,
    correctPosition
    );

    if (ghostImage != null)
    {
        if (distance <= snapThreshold * 1.5f)
            ghostImage.SetActive(true);
        else
            ghostImage.SetActive(false);
    }
    if (dragArea != null)
    {
        Vector3[] corners = new Vector3[4];
        dragArea.GetWorldCorners(corners);

        Vector3 pos = rectTransform.position;

        pos.x = Mathf.Clamp(pos.x, corners[0].x, corners[2].x);
        pos.y = Mathf.Clamp(pos.y, corners[0].y, corners[2].y);

        rectTransform.position = pos;
    }
}

public void OnEndDrag(PointerEventData eventData)
{
    if (isPlaced || !canDrag) return;

    canvasGroup.blocksRaycasts = true;
    ghostImage?.SetActive(false);
    float distance = Vector2.Distance(
        rectTransform.anchoredPosition,
        correctPosition
    );

    Debug.Log($"Distance: {distance} | Piece: {gameObject.name}");

    if (distance <= snapThreshold)
    {
        Debug.Log("✅ CORRECT DROP: " + gameObject.name);
        StartCoroutine(SmoothSnap());
    }
    else
    {
        Debug.Log("❌ WRONG DROP");
    }
}
    IEnumerator SmoothSnap()
    {
        Vector2 start = rectTransform.anchoredPosition;
        Vector2 target = correctPosition;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * 10f;
            rectTransform.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        rectTransform.anchoredPosition = target;

        isPlaced = true;

        // 🔥 INFORM MANAGER
        PuzzleManager.Instance.OnPiecePlaced(this);
    }


    public void ResetPiece()
    {
        isPlaced = false;
        canvasGroup.blocksRaycasts = true;
    }
}