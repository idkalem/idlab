using System.Collections.Generic;
using IDLab.Core.Events;
using UnityEngine;
using UnityEngine.UI;

namespace IDLab.App.UI
{
    /// <summary>Stack of short-lived notifications under the top bar (hazards are red, info is blue).</summary>
    public sealed class ToastPresenter : MonoBehaviour
    {
        private const int MaxToasts = 3;
        private const float Lifetime = 4.5f;

        private sealed class Toast
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public float Age;
        }

        private readonly List<Toast> _toasts = new List<Toast>();
        private RectTransform _root;

        public void Initialize(float topOffset)
        {
            _root = (RectTransform)transform;
            _root.anchorMin = new Vector2(0.5f, 1);
            _root.anchorMax = new Vector2(0.5f, 1);
            _root.pivot = new Vector2(0.5f, 1);
            _root.anchoredPosition = new Vector2(-40, -topOffset);
            _root.sizeDelta = new Vector2(560, 200);
            var layout = UIFactory.VerticalLayout(_root, 6, new RectOffset(0, 0, 0, 0));
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        public void Show(LabEvent e) => Show(e.Message, ColorFor(e.Type), e.IsHazard ? "<b>HAZARD</b>  " : "");

        public void Show(string message, Color accent, string prefix = "")
        {
            if (_toasts.Count >= MaxToasts)
            {
                Destroy(_toasts[0].Root.gameObject);
                _toasts.RemoveAt(0);
            }

            var root = UIFactory.Panel("Toast", _root, UITheme.PanelRaised);
            UIFactory.Layout(root, 46);
            var bar = UIFactory.Panel("Accent", root, accent);
            UIFactory.Place(bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(5, 34));
            var text = UIFactory.Label(root, prefix + message, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(20, 0);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            _toasts.Add(new Toast { Root = root, Group = group });
        }

        private static Color ColorFor(LabEventType type)
        {
            switch (type)
            {
                case LabEventType.Explosion:
                case LabEventType.FuseBlown:
                    return UITheme.Danger;
                case LabEventType.ToxicGas:
                case LabEventType.Smoke:
                case LabEventType.Spark:
                case LabEventType.Spill:
                    return UITheme.Warning;
                default:
                    return UITheme.Accent;
            }
        }

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.Age += Time.unscaledDeltaTime;
                t.Group.alpha = Mathf.Clamp01((Lifetime - t.Age) / 0.6f);
                if (t.Age >= Lifetime)
                {
                    Destroy(t.Root.gameObject);
                    _toasts.RemoveAt(i);
                }
            }
        }
    }
}
