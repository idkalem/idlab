using System.Text;
using IDLab.App.Views;
using UnityEngine;
using UnityEngine.UI;

namespace IDLab.App.UI
{
    /// <summary>
    /// Bottom-centre card for the selected item: live readouts plus touch-friendly controls
    /// (gas valve for burners, tilt-to-pour and empty for containers, remove for everything).
    /// </summary>
    public sealed class InspectorPanel : MonoBehaviour
    {
        private LabApp _app;
        private RectTransform _root;
        private Text _title;
        private Text _readout;
        private RectTransform _controls;
        private Slider _slider;
        private Text _sliderLabel;
        private EquipmentView _view;
        private readonly StringBuilder _sb = new StringBuilder(256);

        public void Initialize(LabApp app)
        {
            _app = app;
            _root = (RectTransform)transform;
            var bg = gameObject.AddComponent<Image>();
            bg.sprite = UIFactory.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIFactory.Place(_root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-40, 10), new Vector2(470, 200));

            _title = UIFactory.Label(_root, "", UITheme.FontTitle, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(_title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -6), new Vector2(440, 28));
            _readout = UIFactory.Label(_root, "", UITheme.FontSmall, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Place(_readout.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -36), new Vector2(442, 104));

            _controls = UIFactory.Rect("Controls", _root);
            _controls.anchorMin = new Vector2(0, 0);
            _controls.anchorMax = new Vector2(1, 0);
            _controls.pivot = new Vector2(0.5f, 0);
            _controls.offsetMin = new Vector2(12, 10);
            _controls.offsetMax = new Vector2(-12, 50);
            UIFactory.HorizontalLayout(_controls, 8, new RectOffset(0, 0, 0, 0));

            app.SelectionChanged += Show;
            Show(null);
        }

        private void OnDestroy()
        {
            if (_app != null)
            {
                _app.SelectionChanged -= Show;
            }
        }

        private void Show(EquipmentView view)
        {
            _view = view;
            gameObject.SetActive(view != null);
            for (int i = _controls.childCount - 1; i >= 0; i--)
            {
                Destroy(_controls.GetChild(i).gameObject);
            }

            _slider = null;
            if (view == null)
            {
                return;
            }

            _title.text = view.DisplayName;

            if (view is BurnerView burner)
            {
                AddSlider("Gas", 0f, 1f, v => burner.Source.GasValve = v);
            }
            else if (view is ContainerView container)
            {
                AddSlider("Pour", 0f, container.MaxTilt, v =>
                {
                    if (container.HostView == null)
                    {
                        container.TiltDegrees = v;
                    }
                });
                UIFactory.Layout(UIFactory.Button(_controls, "Empty", () => container.Container.Contents.Clear(), null, UITheme.FontSmall), -1, -1, 70);
            }

            UIFactory.Layout(UIFactory.Button(_controls, "Remove", () => _app.Remove(view), UITheme.Danger * 0.6f, UITheme.FontSmall), -1, -1, 80);
        }

        private void AddSlider(string label, float min, float max, System.Action<float> onChange)
        {
            _sliderLabel = UIFactory.Label(_controls, label, UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleLeft);
            UIFactory.Layout(_sliderLabel, -1, -1, 74);
            _slider = UIFactory.Slider(_controls, min, max, onChange);
            UIFactory.Layout(_slider, -1, 1);
        }

        private void LateUpdate()
        {
            if (_view == null)
            {
                return;
            }

            _sb.Clear();
            _view.Describe(_sb);
            if (_view.CanTilt && _view.HostView == null)
            {
                _sb.Append("<color=#8A9BB4>Hold Q / E or use the Pour slider to tilt.</color>\n");
            }

            _readout.text = _sb.ToString();

            // Keep the slider in sync with changes from the 3D knob or keyboard.
            if (_slider != null)
            {
                if (_view is BurnerView burner)
                {
                    _slider.SetValueWithoutNotify((float)burner.Source.GasValve);
                    _sliderLabel.text = $"Gas {burner.Source.GasValve * 100:0}%";
                }
                else
                {
                    _slider.SetValueWithoutNotify(_view.TiltDegrees);
                    _sliderLabel.text = $"Pour {_view.TiltDegrees:0}°";
                }
            }
        }
    }
}
