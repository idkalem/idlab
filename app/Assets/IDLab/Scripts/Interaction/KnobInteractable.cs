using System;
using UnityEngine;

namespace IDLab.App.Interaction
{
    /// <summary>
    /// A 3D control turned by dragging (gas valves, rheostats, microscope focus knobs...). Horizontal pointer
    /// movement maps to a 0..1 value; the owning view renders the knob from that value.
    /// </summary>
    public sealed class KnobInteractable : MonoBehaviour
    {
        private Func<float> _get;
        private Action<float> _set;

        /// <summary>Screen pixels for a full 0→1 sweep (scaled by DPI on high-density mobile screens).</summary>
        public float PixelsPerFullTurn = 260f;

        public string Label { get; private set; }

        public float Value => _get != null ? _get() : 0f;

        public void Bind(Func<float> get, Action<float> set, string label)
        {
            _get = get;
            _set = set;
            Label = label;
        }

        public void ApplyDrag(Vector2 screenDelta)
        {
            if (_set == null)
            {
                return;
            }

            float dpiScale = Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
            float delta = (screenDelta.x + screenDelta.y) / (PixelsPerFullTurn * dpiScale);
            _set(Mathf.Clamp01(_get() + delta));
        }
    }
}
