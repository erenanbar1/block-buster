using UnityEngine;

namespace BlockBlast.Game
{
    /// <summary>
    /// Uniformly scales a fixed-size design frame so it always fits inside its parent.
    /// Everything in the game is laid out against a 1080x1920 box; this keeps that box
    /// intact on any aspect ratio instead of letting elements drift apart.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesignFitter : MonoBehaviour
    {
        public Vector2 DesignSize = new Vector2(1080f, 1920f);

        RectTransform rt;
        RectTransform parentRect;
        Vector2 lastParentSize;

        void Awake()
        {
            rt = (RectTransform)transform;
            parentRect = rt.parent as RectTransform;
            Apply();
        }

        void OnEnable() => Apply();

        void Update()
        {
            if (parentRect != null && parentRect.rect.size != lastParentSize) Apply();
        }

        public void Apply()
        {
            if (rt == null) rt = (RectTransform)transform;
            if (parentRect == null) parentRect = rt.parent as RectTransform;
            rt.sizeDelta = DesignSize;
            if (parentRect == null) return;

            lastParentSize = parentRect.rect.size;
            if (lastParentSize.x <= 0f || lastParentSize.y <= 0f) return;

            float scale = Mathf.Min(lastParentSize.x / DesignSize.x, lastParentSize.y / DesignSize.y);
            rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
