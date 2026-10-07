using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IDLab.App.UI
{
    /// <summary>Code-only uGUI construction helpers (no prefabs): panels, labels, buttons, sliders.</summary>
    public static class UIFactory
    {
        private static Font _font;
        private static Sprite _rounded;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
#if UNITY_2022_2_OR_NEWER
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                }

                return _font;
            }
        }

        /// <summary>9-sliced rounded-rectangle sprite generated at runtime.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null)
                {
                    const int size = 32;
                    const float radius = 10f;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "IDLab Rounded", wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                            float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
                        }
                    }

                    tex.Apply();
                    _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                        SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
                }

                return _rounded;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Panel(string name, Transform parent, Color color, bool rounded = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }

            return rt;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, Color? background = null, int fontSize = UITheme.FontBody)
        {
            var rt = Panel("Button", parent, background ?? UITheme.Button);
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.3f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.85f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.2f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            var text = Label(rt, label, fontSize, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        public static Slider Slider(Transform parent, float min, float max, Action<float> onChanged)
        {
            var root = Rect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var bg = Panel("Track", root, UITheme.Button);
            bg.anchorMin = new Vector2(0, 0.35f);
            bg.anchorMax = new Vector2(1, 0.65f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;

            var fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, 0.35f);
            fillArea.anchorMax = new Vector2(1, 0.65f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = Panel("Fill", fillArea, UITheme.Accent);
            fill.sizeDelta = Vector2.zero;

            var handleArea = Rect("HandleArea", root);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10, 0);
            handleArea.offsetMax = new Vector2(-10, 0);
            var handle = Panel("Handle", handleArea, Color.white);
            handle.sizeDelta = new Vector2(20, 0);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        public static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Anchors a rect to a corner/edge with an explicit size and offset.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float flexibleWidth = -1, float preferredWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = c.gameObject.AddComponent<LayoutElement>();
            }

            le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            le.preferredWidth = preferredWidth;
            return le;
        }

        public static VerticalLayoutGroup VerticalLayout(Component c, float spacing, RectOffset padding)
        {
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HorizontalLayout(Component c, float spacing, RectOffset padding)
        {
            var h = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
