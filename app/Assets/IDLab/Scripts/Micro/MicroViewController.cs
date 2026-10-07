using System.Collections.Generic;
using System.Text;
using IDLab.App.Rendering;
using IDLab.App.UI;
using IDLab.App.Views;
using IDLab.Core.Components;
using IDLab.Core.Micro;
using UnityEngine;
using UnityEngine.UI;

namespace IDLab.App.Micro
{
    /// <summary>
    /// The atomic-scale inset ("dual viewport"). A second camera renders a particle box, placed far below the
    /// lab so the main camera never sees it, into a RenderTexture displayed in the HUD. Composition, speed and
    /// phase come from <see cref="MicroScaleModel"/>; this class only animates them.
    /// </summary>
    public sealed class MicroViewController : MonoBehaviour
    {
        private const int Budget = 60;
        private static readonly Vector3 Origin = new Vector3(0, -500, 0);
        private static readonly Vector3 HalfBox = new Vector3(1.6f, 1.15f, 0.9f);

        private sealed class Particle
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public string SpeciesId;
            public float Radius;
            public bool Escaping;
        }

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private LabApp _app;
        private Camera _camera;
        private RenderTexture _texture;
        private Mesh _sphere;
        private MicroViewState _state = MicroScaleModel.Idle();
        private float _refreshTimer;
        private string _signature = "";
        private Text _title, _caption, _legend;
        private LiquidContainer _target;

        public RenderTexture Texture => _texture;

        public void Initialize(LabApp app)
        {
            _app = app;
            transform.position = Origin;

            _texture = new RenderTexture(640, 480, 16) { name = "MicroView", antiAliasing = 2 };
            var camGo = new GameObject("MicroCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0, 0.3f, -5.2f);
            camGo.transform.localRotation = Quaternion.Euler(4f, 0, 0);
            _camera = camGo.AddComponent<Camera>();
            _camera.targetTexture = _texture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.03f, 0.05f, 0.09f);
            _camera.fieldOfView = 38f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 12f;
            _camera.depth = -10;

            var light = new GameObject("MicroLight").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = new Vector3(-2f, 3f, -4f);
            light.type = LightType.Point;
            light.range = 15f;
            light.intensity = 1.6f;

            BuildCellOutline();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }

        /// <summary>Builds the inset's HUD card (called by the HUD).</summary>
        public void BuildPanel(RectTransform panel)
        {
            _title = UIFactory.Label(panel, "MICRO VIEW", UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(_title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -4), new Vector2(316, 26));
            var image = UIFactory.Rect("Viewport", panel).gameObject.AddComponent<RawImage>();
            image.texture = _texture;
            image.raycastTarget = false;
            UIFactory.Place(image.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(320, 210));
            _legend = UIFactory.Label(panel, "", 12, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Place(_legend.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 46), new Vector2(316, 36));
            _caption = UIFactory.Label(panel, "", 12, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIFactory.Place(_caption.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 6), new Vector2(316, 40));
        }

        private void BuildCellOutline()
        {
            var mat = MaterialFactory.Emissive(new Color(0.1f, 0.3f, 0.4f), new Color(0.1f, 0.5f, 0.7f));
            var corners = new[] { -1f, 1f };
            const float t = 0.015f;
            foreach (float a in corners)
            {
                foreach (float b in corners)
                {
                    Edge(new Vector3(0, a * HalfBox.y, b * HalfBox.z), new Vector3(HalfBox.x * 2, t, t), mat);
                    Edge(new Vector3(a * HalfBox.x, 0, b * HalfBox.z), new Vector3(t, HalfBox.y * 2, t), mat);
                    Edge(new Vector3(a * HalfBox.x, b * HalfBox.y, 0), new Vector3(t, t, HalfBox.z * 2), mat);
                }
            }
        }

        private void Edge(Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.25f;
                Refresh();
            }

            Animate(Time.deltaTime);
        }

        /// <summary>Picks what to look at: the selection, what a selected probe/burner touches, else the hottest container.</summary>
        private LiquidContainer ChooseTarget()
        {
            var selected = _app.Selected;
            switch (selected)
            {
                case ContainerView c: return c.Container;
                case ProbeView p when p.Probe.ImmersedIn != null: return p.Probe.ImmersedIn;
                case BurnerView b when b.Source.HeatedContainer != null: return b.Source.HeatedContainer;
            }

            LiquidContainer best = null;
            foreach (var view in ContainerView.All(_app))
            {
                var c = view.Container;
                if (c.Contents.IsEmpty || view.Instance.Definition.Category != Core.Model.EquipmentCategory.Glassware)
                {
                    continue;
                }

                if (best == null || c.TemperatureC > best.TemperatureC)
                {
                    best = c;
                }
            }

            return best;
        }

        private void Refresh()
        {
            _target = ChooseTarget();
            _state = MicroScaleModel.Describe(_target, Budget);
            if (_title != null)
            {
                _title.text = _target != null ? $"MICRO VIEW  ·  {_target.Owner.Definition.DisplayName} #{_target.Owner.Id}" : "MICRO VIEW";
                _caption.text = _state.Caption;
                _legend.text = BuildLegend();
            }

            var sb = new StringBuilder();
            foreach (var s in _state.Species)
            {
                sb.Append(s.Id).Append(':').Append(s.Count).Append(';');
            }

            string signature = sb.ToString();
            if (signature != _signature)
            {
                _signature = signature;
                RebuildParticles();
            }
        }

        private string BuildLegend()
        {
            var sb = new StringBuilder();
            foreach (var s in _state.Species)
            {
                Color c = s.Color.ToUnity();
                sb.Append($"<color=#{c.ToHex()}><b>{s.Id}</b></color> ×{s.Count}   ");
            }

            return sb.ToString();
        }

        private void RebuildParticles()
        {
            var wanted = new List<(string id, float radius, Color color)>();
            foreach (var s in _state.Species)
            {
                for (int i = 0; i < s.Count; i++)
                {
                    wanted.Add((s.Id, s.Radius, s.Color.ToUnity()));
                }
            }

            while (_particles.Count < wanted.Count)
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _sphere;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _particles.Add(new Particle { Transform = go.transform, Renderer = r });
            }

            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                bool active = i < wanted.Count;
                p.Transform.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var (id, radius, color) = wanted[i];
                if (p.SpeciesId != id)
                {
                    p.SpeciesId = id;
                    p.Radius = 0.1f * radius;
                    p.Renderer.sharedMaterial = MaterialFor(id, color);
                    p.Transform.localScale = Vector3.one * p.Radius * 2f;
                    p.Transform.localPosition = RandomInBox(p.Radius);
                    p.Velocity = Random.onUnitSphere;
                    p.Escaping = false;
                }
            }
        }

        private Material MaterialFor(string id, Color color)
        {
            if (!_materials.TryGetValue(id, out var m))
            {
                m = MaterialFactory.Emissive(color, color * 0.35f);
                _materials[id] = m;
            }

            return m;
        }

        private void Animate(float dt)
        {
            if (_state.Mode == MicroViewMode.Idle)
            {
                return;
            }

            // Exaggerated so the change between 20 °C and 100 °C is obvious (real v_rms rises only ~13%).
            float speed = 0.45f * Mathf.Pow((float)_state.SpeedFactor, 6f);
            bool boiling = _state.Phase == MatterPhase.Boiling;

            foreach (var p in _particles)
            {
                if (!p.Transform.gameObject.activeSelf)
                {
                    continue;
                }

                // Brownian kick + ballistic motion; ions drift slower (heavier, hydrated).
                p.Velocity += Random.insideUnitSphere * (dt * 6f);
                p.Velocity = p.Velocity.normalized;
                Vector3 pos = p.Transform.localPosition + p.Velocity * (speed * dt / Mathf.Max(0.6f, p.Radius * 8f));

                if (p.Escaping)
                {
                    pos += Vector3.up * (dt * 1.5f);
                    if (pos.y > HalfBox.y + 0.8f)
                    {
                        p.Escaping = false;
                        pos = new Vector3(Random.Range(-HalfBox.x, HalfBox.x), -HalfBox.y + p.Radius, Random.Range(-HalfBox.z, HalfBox.z));
                    }
                }
                else
                {
                    if (boiling && p.SpeciesId == "H2O" && pos.y > HalfBox.y - 0.3f && Random.value < dt * 0.8f)
                    {
                        p.Escaping = true; // vapour leaving the surface
                    }

                    Bounce(ref pos, ref p.Velocity, p.Radius);
                }

                p.Transform.localPosition = pos;
            }
        }

        private static void Bounce(ref Vector3 pos, ref Vector3 vel, float r)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                float limit = HalfBox[axis] - r;
                if (pos[axis] > limit)
                {
                    pos[axis] = limit;
                    vel[axis] = -Mathf.Abs(vel[axis]);
                }
                else if (pos[axis] < -limit)
                {
                    pos[axis] = -limit;
                    vel[axis] = Mathf.Abs(vel[axis]);
                }
            }
        }

        private static Vector3 RandomInBox(float r) => new Vector3(
            Random.Range(-HalfBox.x + r, HalfBox.x - r),
            Random.Range(-HalfBox.y + r, HalfBox.y - r),
            Random.Range(-HalfBox.z + r, HalfBox.z - r));

        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
            }
        }
    }
}
