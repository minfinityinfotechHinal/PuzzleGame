using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect currentSafeArea = new Rect(0, 0, 0, 0);
    private ScreenOrientation currentOrientation = ScreenOrientation.AutoRotation;

    [Header("Banner Offset Settings")]
    [Tooltip("Offset at bottom when banner is visible")]
    public float bannerBottomHeight = 150f;

    [Tooltip("Offset at bottom when banner is NOT visible")]
    public float noBannerBottomHeight = 0f;

    [Header("ScrollView Settings")]
    public GameObject ScrollView;

    [Tooltip("Y position when banner is visible")]
    public float scrollViewYWithBanner = 380f;

    [Tooltip("Y position when banner is NOT visible")]
    public float scrollViewYWithoutBanner = 530f;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void OnEnable()
    {
        // if (AdsManager.Instance != null)
        //     AdsManager.OnBannerVisibilityChanged += SetBannerVisible;
    }

    void OnDisable()
    {
        // if (AdsManager.Instance != null)
        //     AdsManager.OnBannerVisibilityChanged -= SetBannerVisible;
    }

    void Start()
    {
        // if (AdsManager.Instance != null)
        // {
        //     SetBannerVisible(AdsManager.IsBanner);
        // }
    }

    void Update()
    {
        if (Application.isEditor || currentSafeArea != Screen.safeArea || currentOrientation != Screen.orientation)
            ApplySafeArea();
    }

    public void SetBannerVisible(bool isVisible)
    {
        if (ScrollView != null)
        {
            RectTransform scrollRect = ScrollView.GetComponent<RectTransform>();
            Vector2 anchoredPos = scrollRect.anchoredPosition;

            anchoredPos.y = isVisible ? scrollViewYWithBanner : scrollViewYWithoutBanner;
            scrollRect.anchoredPosition = anchoredPos;
        }

        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        currentSafeArea = Screen.safeArea;
        currentOrientation = Screen.orientation;

        Vector2 anchorMin = currentSafeArea.position;
        Vector2 anchorMax = currentSafeArea.position + currentSafeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
         rectTransform.offsetMin = new Vector2(0, noBannerBottomHeight);

        // Apply bottom offset based on banner
        // if (AdsManager.IsBanner)
        // {
        //     rectTransform.offsetMin = new Vector2(0, bannerBottomHeight);
        // }
        // else
        // {
        //     rectTransform.offsetMin = new Vector2(0, noBannerBottomHeight);
        // }
    }
}
