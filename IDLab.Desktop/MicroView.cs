using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using IDLab.Core.Components;
using IDLab.Core.Micro;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Atomic-scale inset: renders a particle box into its own texture with a second camera. Composition,
    /// speed and phase come from the core's MicroScaleModel; this class animates them.
    /// </summary>
    internal sealed class MicroView
    {
        private const int Budget = 60;
        private static readonly Vector3 Half = new Vector3(1.6f, 1.15f, 0.9f);

        private sealed class P
        {
            public Vector3 Pos, Vel;
            public string Species;
            public float Radius;
            public Color Color;
            public bool Escaping;
        }

        private readonly List<P> _particles = new List<P>();
        private readonly Random _rng = new Random(3);
        private RenderTexture2D _target;
        private MicroViewState _state = MicroScaleModel.Idle();
        private string _signature = "";
        private float _refresh;
        private float _spin;

        public LiquidContainer Container { get; private set; }
        public MicroViewState State => _state;
        public Texture2D Texture => _target.Texture;

        public void Init() => _target = LoadRenderTexture(640, 460);

        public void Update(Lab lab, float dt)
        {
            _refresh -= dt;
            if (_refresh <= 0)
            {
                _refresh = 0.25f;
                Container = ChooseTarget(lab);
                _state = MicroScaleModel.Describe(Container, Budget);
                string sig = string.Join(";", _state.Species.Select(s => s.Id + s.Count));
                if (sig != _signature)
                {
                    _signature = sig;
                    Rebuild();
                }
            }

            Animate(dt);
        }

        private static LiquidContainer ChooseTarget(Lab lab)
        {
            var s = lab.Selected;
            if (s?.Container != null && s.Def.VisualKey == "beaker") return s.Container;
            if (s?.Container != null) return s.Container;
            if (s?.Probe?.ImmersedIn != null) return s.Probe.ImmersedIn;
            if (s?.Rod?.ImmersedIn != null) return s.Rod.ImmersedIn;
            if (s?.Heat?.HeatedContainer != null) return s.Heat.HeatedContainer;
            return lab.Items.Where(i => i.Def.VisualKey == "beaker" && i.Container != null && !i.Container.Contents.IsEmpty)
                            .Select(i => i.Container)
                            .OrderByDescending(c => c.TemperatureC)
                            .FirstOrDefault();
        }

        private void Rebuild()
        {
            var wanted = new List<MicroSpecies>();
            foreach (var s in _state.Species)
            {
                for (int i = 0; i < s.Count; i++) wanted.Add(s);
            }

            while (_particles.Count > wanted.Count) _particles.RemoveAt(_particles.Count - 1);
            for (int i = 0; i < wanted.Count; i++)
            {
                if (i >= _particles.Count) _particles.Add(new P { Pos = RandomInBox(0.1f), Vel = RandomDir() });
                var p = _particles[i];
                if (p.Species != wanted[i].Id)
                {
                    p.Species = wanted[i].Id;
                    p.Radius = 0.1f * wanted[i].Radius;
                    p.Color = Gfx.FromCore(wanted[i].Color);
                    p.Pos = RandomInBox(p.Radius);
                    p.Escaping = false;
                }
            }
        }

        private void Animate(float dt)
        {
            if (_state.Mode == MicroViewMode.Idle) return;
            _spin += dt * 4f;
            float speed = 0.45f * MathF.Pow((float)_state.SpeedFactor, 6f);
            bool boiling = _state.Phase == MatterPhase.Boiling;
            foreach (var p in _particles)
            {
                p.Vel = Vector3.Normalize(p.Vel + RandomDir() * dt * 6f);
                var pos = p.Pos + p.Vel * (speed * dt / Math.Max(0.6f, p.Radius * 8f));
                if (p.Escaping)
                {
                    pos.Y += dt * 1.5f;
                    if (pos.Y > Half.Y + 0.8f)
                    {
                        p.Escaping = false;
                        pos = new Vector3(Rand() * Half.X, -Half.Y + p.Radius, Rand() * Half.Z);
                    }
                }
                else
                {
                    if (boiling && p.Species == "H2O" && pos.Y > Half.Y - 0.3f && _rng.NextDouble() < dt * 0.8f) p.Escaping = true;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        float limit = (axis == 0 ? Half.X : axis == 1 ? Half.Y : Half.Z) - p.Radius;
                        float v = axis == 0 ? pos.X : axis == 1 ? pos.Y : pos.Z;
                        float vel = axis == 0 ? p.Vel.X : axis == 1 ? p.Vel.Y : p.Vel.Z;
                        if (v > limit) { v = limit; vel = -Math.Abs(vel); }
                        else if (v < -limit) { v = -limit; vel = Math.Abs(vel); }
                        if (axis == 0) { pos.X = v; p.Vel.X = vel; }
                        else if (axis == 1) { pos.Y = v; p.Vel.Y = vel; }
                        else { pos.Z = v; p.Vel.Z = vel; }
                    }
                }

                p.Pos = pos;
            }
        }

        /// <summary>Renders the particle box into the inset texture (call outside BeginDrawing's 3D mode).</summary>
        public void Render()
        {
            var cam = new Camera3D(new Vector3(0, 0.35f, -5.2f), Vector3.Zero, Vector3.UnitY, 38f, CameraProjection.Perspective);
            BeginTextureMode(_target);
            ClearBackground(new Color(8, 13, 23, 255));
            BeginMode3D(cam);
            Gfx.Ambient = new Vector3(0.35f, 0.38f, 0.45f);
            Gfx.FogRange = new Vector2(1000f, 1f);
            Gfx.BeginFrame(cam.Position);
            DrawCubeWiresV(Vector3.Zero, Half * 2, new Color(40, 130, 180, 255));
            if (_state.Mode != MicroViewMode.Idle)
            {
                foreach (var p in _particles)
                {
                    Gfx.Draw(Gfx.Sphere, Gfx.Mul(Gfx.Scale(p.Radius), Gfx.Translate(p.Pos)), p.Color, new Vector3(p.Color.R, p.Color.G, p.Color.B) / 255f * 0.25f);
                }
            }

            EndMode3D();
            EndTextureMode();
        }

        public string Caption()
        {
            var ci = CultureInfo.CurrentCulture;
            if (_state.Mode == MicroViewMode.Idle)
            {
                return Container == null ? "İçine bakmak için bir kap seçin." : "Kap boş.";
            }

            string t = string.Format(ci, "{0:0} °C", _state.TemperatureC);
            if (_state.Phase == MatterPhase.Boiling)
                return t + " · Kaynama: moleküller buhar olarak kaçacak kadar enerji kazanıyor.";
            if (_state.Mode == MicroViewMode.IonDissociation)
                return t + string.Format(ci, " · pH {0:0.0} · Çözeltideki iyonlar (ölçekli değildir).", Container.PH);
            return t + string.Format(ci, " · Moleküller 20 °C'ye göre ×{0:0.00} daha hızlı.", _state.SpeedFactor);
        }

        private Vector3 RandomInBox(float r) => new Vector3(Rand() * (Half.X - r), Rand() * (Half.Y - r), Rand() * (Half.Z - r));
        private Vector3 RandomDir() => Vector3.Normalize(new Vector3(Rand(), Rand(), Rand()) + new Vector3(1e-4f));
        private float Rand() => (float)(_rng.NextDouble() * 2 - 1);
    }
}
