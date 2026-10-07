using System.Text;
using IDLab.App.Rendering;
using IDLab.Core.Components;
using UnityEngine;

namespace IDLab.App.Views
{
    /// <summary>Digital thermometer / pH probe: steel or glass shaft, handle, and a floating live readout.</summary>
    public sealed class ProbeView : EquipmentView
    {
        private const float Length = 0.2f;
        private Probe _probe;
        private TextMesh _readout;

        public Probe Probe => _probe;
        protected override float MassKg => 0.05f;

        protected override void BuildVisuals()
        {
            _probe = Instance.Get<Probe>();
            Color accent = Instance.Definition.AccentColor.ToUnity();
            bool isPh = _probe.Quantity == ProbeQuantity.PH;

            var shaft = isPh
                ? MaterialFactory.Transparent(new Color(0.85f, 0.93f, 1f, 0.35f), 0.95f, 2)
                : Metal(new Color(0.75f, 0.77f, 0.8f));
            Part("Shaft", MeshFactory.Cylinder(isPh ? 0.005f : 0.0022f, Length * 0.68f), shaft, Vector3.zero);
            if (isPh)
            {
                Part("Bulb", MeshFactory.Lathe(new[] { new Vector2(0, 0), new Vector2(0.0055f, 0.004f), new Vector2(0.005f, 0.012f), new Vector2(0, 0.012f) }),
                    MaterialFactory.Transparent(new Color(0.7f, 0.85f, 1f, 0.5f), 0.95f, 2), Vector3.zero);
            }

            Part("Handle", MeshFactory.Cylinder(0.008f, Length * 0.32f), Plastic(new Color(0.12f, 0.13f, 0.15f)), new Vector3(0, Length * 0.68f, 0));
            Part("Band", MeshFactory.Cylinder(0.0085f, 0.008f), Plastic(accent), new Vector3(0, Length * 0.7f, 0));
            AddBoxCollider(new Vector3(0.018f, Length, 0.018f), new Vector3(0, Length * 0.5f, 0));

            _readout = FloatingLabel("--", new Vector3(0, Length + 0.012f, 0), 0.0022f, accent);
        }

        public override void SyncVisuals(float deltaTime) => _readout.text = _probe.FormattedReading;

        public override void Describe(StringBuilder sb)
        {
            sb.Append($"Reading   {_probe.FormattedReading}\n");
            sb.Append(_probe.ImmersedIn != null
                ? $"Immersed  in {_probe.ImmersedIn.Owner.Definition.DisplayName}\n"
                : "Immersed  no — drop the tip into a container\n");
            sb.Append($"Response  τ = {_probe.TimeConstantS:0.0} s\n");
        }
    }
}
