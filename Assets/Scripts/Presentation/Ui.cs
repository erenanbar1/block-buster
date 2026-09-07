using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlast.Presentation
{
    /// <summary>Terse helpers for hand-building the uGUI hierarchy in code.</summary>
    public static class Ui
    {
        public static RectTransform Rect(string name, Transform parent, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            if (raycast)
            {
                var blocker = go.AddComponent<Image>();
                blocker.color = new Color(0, 0, 0, 0);
            }
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color,
            Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string content, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        /// <summary>Rounded pill button with a label. Returns the Button component.</summary>
        public static Button Button(string name, Transform parent, string label, Vector2 size, Color color,
            float fontSize = 46f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = SpriteFactory.RoundedRect(96, 42);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            if (!string.IsNullOrEmpty(label))
            {
                var text = Text(name + "Label", rt, label, fontSize, Color.white);
                Fill(text.rectTransform);
            }
            return btn;
        }

        public static void Fill(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Anchors to a corner/edge of the parent, e.g. (0,1) for top-left.</summary>
        public static RectTransform AnchorTo(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static void SetAlpha(CanvasGroup group, float alpha)
        {
            group.alpha = alpha;
            group.interactable = alpha > 0.99f;
            group.blocksRaycasts = alpha > 0.01f;
        }
    }
}
