using System.Text;
using IDLab.App.Interaction;
using IDLab.App.Rendering;
using IDLab.Core.Components;
using UnityEngine;

namespace IDLab.App.Views
{
    /// <summary>
    /// Bunsen burner under a tripod with wire gauze. The red gas valve is a 3D knob: drag it sideways
    /// (or use the inspector slider) to open the gas. Flame size, colour and light follow the valve.
    /// </summary>
    public sealed class BurnerView : EquipmentView
    {
        private const float BarrelTop = 0.13f;
        private const float GaugeY = 0.205f;

        private HeatSource _source;
        private Transform _knob;
        private Transform _outerFlame;
        private Transform _innerFlame;
        private Material _outerMaterial;
        private Material _innerMaterial;
        private Light _flameLight;
        private Material _gauzeGlow;
        private float _flicker;

        public HeatSource Source => _source;
        protected override float MassKg => 1.2f;

        protected override void BuildVisuals()
        {
            _source = Instance.Get<HeatSource>();
            var steel = Metal(new Color(0.28f, 0.3f, 0.33f));
            var brass = Metal(new Color(0.78f, 0.62f, 0.32f));
            var dark = Plastic(new Color(0.08f, 0.09f, 0.11f));

            // Burner
            Part("Base", MeshFactory.Lathe(new[]
            {
                new Vector2(0, 0), new Vector2(0.042f, 0), new Vector2(0.042f, 0.006f), new Vector2(0.03f, 0.014f), new Vector2(0, 0.014f),
            }), dark, Vector3.zero);
            Part("Barrel", MeshFactory.Cylinder(0.0085f, BarrelTop - 0.014f), brass, new Vector3(0, 0.014f, 0));
            Part("AirCollar", MeshFactory.Cylinder(0.011f, 0.014f), brass, new Vector3(0, 0.03f, 0));
            Part("Hose", MeshFactory.Cylinder(0.005f, 0.06f), Plastic(new Color(0.9f, 0.45f, 0.1f)), new Vector3(0, 0.02f, 0))
                .transform.localRotation = Quaternion.Euler(0, 0, 90);

            // Gas valve knob (interactive)
            var knob = Part("GasValve", MeshFactory.Cylinder(0.009f, 0.008f), Plastic(new Color(0.85f, 0.15f, 0.15f)), new Vector3(0, 0.009f, -0.046f));
            knob.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            Part("Pointer", MeshFactory.Cylinder(0.0015f, 0.01f), Plastic(Color.white), new Vector3(0, 0.008f, 0), knob.transform)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            var knobCollider = knob.AddComponent<BoxCollider>();
            knobCollider.size = new Vector3(0.03f, 0.02f, 0.03f);
            knobCollider.center = new Vector3(0, 0.006f, 0);
            var knobInteraction = knob.AddComponent<KnobInteractable>();
            knobInteraction.Bind(() => (float)_source.GasValve, v => _source.GasValve = v, "Gas valve");
            _knob = knob.transform;

            // Tripod + gauze
            var ring = Part("TripodRing", MeshFactory.Ring(0.06f, 0.066f, 0.004f), steel, new Vector3(0, GaugeY - 0.004f, 0));
            ring.name = "TripodRing";
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f + 30f;
                var leg = Box($"Leg{i}", new Vector3(0.005f, GaugeY + 0.012f, 0.005f), steel, Vector3.zero, collider: false);
                Vector3 top = Quaternion.Euler(0, a, 0) * new Vector3(0.063f, GaugeY - 0.002f, 0);
                Vector3 foot = Quaternion.Euler(0, a, 0) * new Vector3(0.085f, 0, 0);
                leg.transform.localPosition = (top + foot) * 0.5f;
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (top - foot).normalized);
                leg.transform.localScale = new Vector3(0.005f, Vector3.Distance(top, foot), 0.005f);
            }

            var gauzeMat = MaterialFactory.Opaque(Color.white, 0.2f, 0.6f);
            gauzeMat = new Material(gauzeMat) { mainTexture = MaterialFactory.GridTexture(new Color(0.35f, 0.36f, 0.38f), new Color(0.12f, 0.12f, 0.13f), 64, 16) };
            gauzeMat.mainTextureScale = new Vector2(2, 2);
            Box("Gauze", new Vector3(0.14f, 0.003f, 0.14f), gauzeMat, new Vector3(0, GaugeY + 0.0015f, 0), collider: false);
            _gauzeGlow = new Material(MaterialFactory.Emissive(new Color(0.85f, 0.85f, 0.82f), Color.black));
            Part("CeramicCentre", MeshFactory.Cylinder(0.035f, 0.0035f), _gauzeGlow, new Vector3(0, GaugeY + 0.0002f, 0));

            // Colliders: base, barrel, and a thin platform at gauze height so items rest on it.
            AddBoxCollider(new Vector3(0.09f, 0.014f, 0.09f), new Vector3(0, 0.007f, 0));
            AddBoxCollider(new Vector3(0.02f, BarrelTop, 0.02f), new Vector3(0, BarrelTop * 0.5f, 0));
            AddBoxCollider(new Vector3(0.14f, 0.004f, 0.14f), new Vector3(0, GaugeY + 0.001f, 0));

            // Flame: two nested additive cones + light.
            _outerMaterial = MaterialFactory.Additive(new Color(0.25f, 0.45f, 1f, 0.55f));
            _innerMaterial = MaterialFactory.Additive(new Color(0.55f, 0.85f, 1f, 0.9f));
            _outerFlame = Part("FlameOuter", MeshFactory.Cone(0.008f, 1f), _outerMaterial, new Vector3(0, BarrelTop, 0)).transform;
            _innerFlame = Part("FlameInner", MeshFactory.Cone(0.0055f, 1f), _innerMaterial, new Vector3(0, BarrelTop, 0)).transform;
            foreach (var r in new[] { _outerFlame, _innerFlame })
            {
                r.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var lightGo = new GameObject("FlameLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0, BarrelTop + 0.03f, 0);
            _flameLight = lightGo.AddComponent<Light>();
            _flameLight.type = LightType.Point;
            _flameLight.range = 0.6f;
            _flameLight.color = new Color(0.55f, 0.65f, 1f);
            _flameLight.shadows = LightShadows.None;
        }

        public override void SyncVisuals(float deltaTime)
        {
            float valve = (float)_source.GasValve;
            bool lit = _source.IsLit;
            _flicker += deltaTime * 14f;
            float noise = 0.9f + 0.2f * Mathf.PerlinNoise(_flicker, Instance.Id * 3.1f);

            _outerFlame.gameObject.SetActive(lit);
            _innerFlame.gameObject.SetActive(lit);
            _flameLight.enabled = lit;
            if (lit)
            {
                float outerHeight = Mathf.Lerp(0.025f, 0.075f, valve) * noise;
                _outerFlame.localScale = new Vector3(1f + valve * 0.6f, outerHeight, 1f + valve * 0.6f);
                _innerFlame.localScale = new Vector3(1f, outerHeight * 0.45f, 1f);
                _flameLight.intensity = (0.4f + valve * 1.6f) * noise;
            }

            // Knob pointer shows the valve position (quarter turn = fully open).
            _knob.localRotation = Quaternion.Euler(-90, 0, 0) * Quaternion.Euler(0, valve * 90f, 0);

            // Ceramic centre of the gauze glows when heated.
            float glow = lit ? valve * valve * 0.9f : 0f;
            _gauzeGlow.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.05f) * glow);
        }

        public override void Describe(StringBuilder sb)
        {
            sb.Append($"Gas valve  {_source.GasValve * 100:0}%\n");
            sb.Append($"Flame      {(_source.IsLit ? $"{_source.FlamePowerW:0} W" : "off")}\n");
            var heated = _source.HeatedContainer;
            sb.Append(heated != null
                ? $"Heating    {heated.Owner.Definition.DisplayName} (+{_source.DeliveredPowerW:0} W)\n"
                : "Heating    nothing — place a container on the gauze\n");
        }
    }
}
