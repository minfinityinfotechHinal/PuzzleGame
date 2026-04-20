using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Watermelon
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public class CanvasConfiguratorLandscape : MonoBehaviour
    {
        [Tooltip("Reference resolution used for landscape mode. E.g. 1920x1080")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);

        [Tooltip("Match value for iPad (tablets).")]
        [Range(0, 1)] public float matchWidthOrHeightForIPad = 0.5f; // ✅ force iPad to use 0

        [Tooltip("Match value for phones (Android/iPhone).")]
        [Range(0, 1)] public float matchWidthOrHeightForPhone = 0.5f;

        [Tooltip("Match value for other tablets (e.g. Android wide screens).")]
        [Range(0, 1)] public float matchWidthOrHeightForTablet = 1f;

        [Tooltip("Aspect ratio threshold to consider device as tablet. Example: 1.6 ≈ iPad (4:3)")]
        public float tabletAspectThreshold = 1.6f;

        private void Awake()
        {
            ConfigureCanvasScaler();
        }

        private void ConfigureCanvasScaler()
        {
            CanvasScaler canvasScaler = GetComponent<CanvasScaler>();

            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            float aspectRatio = (float)Screen.width / Screen.height;

            string deviceModel = SystemInfo.deviceModel;

        #if UNITY_IOS
            // ✅ Real iPad detection (on device, not Editor)
            if (deviceModel.Contains("iPad"))
            {
                canvasScaler.matchWidthOrHeight = matchWidthOrHeightForIPad; 
                return;
            }
        #endif

        #if UNITY_EDITOR
            // ✅ Editor fallback: detect iPad by resolution/aspect ratio
            if ((Screen.width == 2048 && Screen.height == 1536) ||  // iPad Air, Mini
                (Screen.width == 2224 && Screen.height == 1668) ||  // iPad Pro 10.5"
                (Screen.width == 2388 && Screen.height == 1668) ||  // iPad Pro 11"
                (Screen.width == 2732 && Screen.height == 2048))    // iPad Pro 12.9"
            {
                //Debug.Log($"[CanvasConfiguratorLandscape] Simulated iPad resolution detected ({Screen.width}x{Screen.height}) → Force Match = {matchWidthOrHeightForIPad}");
                canvasScaler.matchWidthOrHeight = matchWidthOrHeightForIPad; 
                return;
            }
        #endif

            // Default aspect ratio logic
            bool isTablet = aspectRatio <= tabletAspectThreshold;
            canvasScaler.matchWidthOrHeight = isTablet ? matchWidthOrHeightForTablet : matchWidthOrHeightForPhone;

            ///Debug.Log($"[CanvasConfiguratorLandscape] Resolution: {Screen.width}x{Screen.height}, Aspect: {aspectRatio:F2}, Model: {deviceModel}, Match: {canvasScaler.matchWidthOrHeight}");
        }
    }
}
