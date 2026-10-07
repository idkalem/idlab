using System.Collections.Generic;
using System.Linq;
using System.Text;
using IDLab.App.Rendering;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using UnityEngine;

namespace IDLab.App.Views
{
    /// <summary>
    /// Beakers, reagent bottles and jars: glass shell, a liquid body whose surface stays level when tilted,
    /// a pour stream, and steam while boiling. All quantities come from the core <see cref="LiquidContainer"/>.
    /// </summary>
    public sealed class ContainerView : EquipmentView
    {
        public enum Shape
        {
            Beaker,
            Bottle,
            Jar,
        }

        private const float Wall = 0.0025f;

        private Shape _shape;
        private LiquidContainer _container;
        private LiquidMeshBuilder _liquid;
        private Material _liquidMaterial;
        private MeshRenderer _liquidRenderer;
        private LineRenderer _stream;
        private ParticleSystem _steam;
        private Color _displayColor;

        public LiquidContainer Container => _container;
        public override float MaxTilt => 130f;
        protected override float MassKg => (float)(_container.GlassMassKg + _container.Contents.MassKg);

        public void Configure(Shape shape) => _shape = shape;

        protected override void BuildVisuals()
        {
            _container = Instance.Get<LiquidContainer>();
            float r = (float)_container.RadiusM;
            float h = (float)_container.InnerHeightM;
            float outerR = r + Wall;
            var glass = MaterialFactory.Transparent(new Color(0.78f, 0.88f, 1f, 0.16f), 0.95f, 1);
            Color accent = Instance.Definition.AccentColor.ToUnity();

            switch (_shape)
            {
                case Shape.Bottle:
                    Part("Glass", MeshFactory.Vessel(outerR, h + 0.012f, Wall, 0.016f, h * 0.72f), glass, Vector3.zero);
                    Part("Label", MeshFactory.Cylinder(outerR + 0.0008f, h * 0.32f), Plastic(Color.Lerp(accent, Color.white, 0.25f)), new Vector3(0, h * 0.22f, 0));
                    Part("Collar", MeshFactory.Cylinder(0.0175f, 0.01f), Plastic(accent), new Vector3(0, h + 0.004f, 0));
                    FloatingLabel(Instance.Definition.DisplayName, new Vector3(0, h + 0.03f, 0), 0.0016f, Color.white);
                    break;
                case Shape.Jar:
                    Part("Glass", MeshFactory.Vessel(outerR, h, Wall), glass, Vector3.zero);
                    Part("Band", MeshFactory.Cylinder(outerR + 0.0008f, 0.012f), Plastic(accent), new Vector3(0, h * 0.35f, 0));
                    FloatingLabel(Instance.Definition.DisplayName, new Vector3(0, h + 0.02f, 0), 0.0016f, new Color(1f, 0.75f, 0.45f));
                    break;
                default:
                    Part("Glass", MeshFactory.Vessel(outerR, h, Wall), glass, Vector3.zero);
                    BuildGraduations(outerR, h);
                    break;
            }

            AddBoxCollider(new Vector3(outerR * 2f, h, outerR * 2f), new Vector3(0, h * 0.5f, 0));

            _liquid = new LiquidMeshBuilder();
            _liquidMaterial = MaterialFactory.Transparent(Color.clear, 0.85f, 0);
            var liquidGo = Part("Liquid", _liquid.Mesh, _liquidMaterial, Vector3.zero);
            _liquidRenderer = liquidGo.GetComponent<MeshRenderer>();
            _liquidRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildStream();
            _steam = Vfx.ParticleBuilder.Steam(transform, new Vector3(0, h, 0), r);
            SyncVisuals(0f);
        }

        private void BuildGraduations(float outerR, float h)
        {
            var mark = MaterialFactory.Opaque(new Color(0.92f, 0.95f, 1f), 0.2f);
            // Every 50 mL on the front face.
            for (int ml = 50; ml <= 250; ml += 50)
            {
                float y = ml / 1000f / (Mathf.PI * (float)(_container.RadiusM * _container.RadiusM));
                if (y > h)
                {
                    break;
                }

                Box($"Mark {ml}", new Vector3(ml % 100 == 0 ? 0.014f : 0.008f, 0.0009f, 0.0006f), mark,
                    new Vector3(0, y, -outerR - 0.0003f), collider: false);
            }
        }

        private void BuildStream()
        {
            var go = new GameObject("PourStream");
            go.transform.SetParent(transform, false);
            _stream = go.AddComponent<LineRenderer>();
            _stream.sharedMaterial = MaterialFactory.Line();
            _stream.positionCount = 8;
            _stream.useWorldSpace = true;
            _stream.numCapVertices = 4;
            _stream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _stream.enabled = false;
        }

        public override void SyncVisuals(float deltaTime)
        {
            float r = (float)_container.RadiusM - 0.0004f;
            float h = (float)_container.InnerHeightM;
            bool hasLiquid = !_container.Contents.IsEmpty && _container.LiquidHeightM > 0.0004;
            _liquidRenderer.enabled = hasLiquid;
            if (hasLiquid)
            {
                Vector3 upLocal = transform.InverseTransformDirection(Vector3.up);
                _liquid.Update(r, (float)_container.LiquidHeightM, h - Wall, Wall, upLocal);

                Color target = _container.LiquidColor.ToUnity();
                target.a = Mathf.Max(target.a, 0.35f);
                _displayColor = deltaTime > 0 ? Color.Lerp(_displayColor, target, 1f - Mathf.Exp(-deltaTime * 4f)) : target;
                MaterialFactory.SetColor(_liquidMaterial, _displayColor);
            }

            UpdateStream();

            var emission = _steam.emission;
            emission.rateOverTime = _container.IsBoiling ? 18f : (_container.TemperatureC > 70 ? 3f : 0f);
        }

        private void UpdateStream()
        {
            if (!_container.IsPouring)
            {
                _stream.enabled = false;
                return;
            }

            Vector3 lip = _container.PourLipPosition.ToUnity();
            float endY;
            var target = _container.PourTarget?.Get<LiquidContainer>();
            if (target != null)
            {
                endY = (float)(target.Owner.Pose.Position.Y + target.LiquidHeightM) + 0.002f;
            }
            else
            {
                endY = 0f; // bench top
            }

            _stream.enabled = true;
            float width = Mathf.Clamp(Mathf.Sqrt((float)_container.PourRateLitersPerSecond) * 0.03f, 0.002f, 0.012f);
            _stream.startWidth = width;
            _stream.endWidth = width * 0.6f;
            Color c = _displayColor;
            c.a = Mathf.Max(0.55f, c.a);
            _stream.startColor = c;
            _stream.endColor = c;

            // Short ballistic arc away from the lip, then straight down.
            Vector3 outward = lip - transform.position;
            outward.y = 0;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized * 0.012f : Vector3.zero;
            for (int i = 0; i < _stream.positionCount; i++)
            {
                float t = i / (float)(_stream.positionCount - 1);
                Vector3 p = lip + outward * Mathf.Sqrt(t);
                p.y = Mathf.Lerp(lip.y, endY, t * t);
                _stream.SetPosition(i, p);
            }
        }

        public override void Describe(StringBuilder sb)
        {
            var c = _container;
            sb.Append($"Volume  {c.VolumeMilliliters:0.0} mL / {c.CapacityLiters * 1000:0} mL\n");
            sb.Append($"Temp    {c.TemperatureC:0.0} °C{(c.IsBoiling ? "  (boiling)" : "")}\n");
            double ph = c.PH;
            sb.Append(double.IsNaN(ph) ? "pH      --\n" : $"pH      {ph:0.00}\n");

            var parts = c.Contents.Components
                .Where(kv => kv.Key != SubstanceIds.Water)
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => $"{c.Contents.Database.Get(kv.Key).Formula} {kv.Value * 1000:0.##} mmol")
                .ToList();
            if (parts.Count > 0)
            {
                sb.Append("Solutes ").Append(string.Join(", ", parts)).Append('\n');
            }

            if (c.IsPouring)
            {
                sb.Append($"Pouring {c.PourRateLitersPerSecond * 1000:0} mL/s → {(c.PourTarget != null ? c.PourTarget.Definition.DisplayName : "bench (spill!)")}\n");
            }
        }

        public static IEnumerable<ContainerView> All(LabApp app) => app.Views.OfType<ContainerView>();
    }
}
