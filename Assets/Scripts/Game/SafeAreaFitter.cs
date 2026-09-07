using UnityEngine;

namespace BlockBlast.Game
{
    /// <summary>
    /// Keeps a RectTransform inside the device safe area (notches, punch-holes, gesture bars).
    /// Re-evaluates when the safe area or orientation changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rt;
        Rect lastSafeArea;
        Vector2Int lastResolution;

        void Awake()
        {
            rt = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea ||
                Screen.width != lastResolution.x || Screen.height != lastResolution.y)
                Apply();
        }

        void Apply()
        {
            lastSafeArea = Screen.safeArea;
            lastResolution = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Some devices (and the editor after a resolution change) report a safe area
            // that spills outside the screen. Clamp it, or the whole UI lands off-screen.
            var area = Rect.MinMaxRect(
                Mathf.Max(0f, lastSafeArea.xMin),
                Mathf.Max(0f, lastSafeArea.yMin),
                Mathf.Min(Screen.width, lastSafeArea.xMax),
                Mathf.Min(Screen.height, lastSafeArea.yMax));
            if (area.width < 1f || area.height < 1f)
                area = new Rect(0f, 0f, Screen.width, Screen.height);

            var min = area.position;
            var max = area.position + area.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
