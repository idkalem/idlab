using System;
using System.Collections.Generic;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Events;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Particle effects: steam, smoke, sparks, fireball, toxic gas, foam, flames, falling drops and solid pieces
    /// (which splash or settle where they land), and ripples on liquid surfaces.
    /// </summary>
    internal sealed class Effects
    {
        private struct Particle
        {
            public Vector3 Position, Velocity, Spin;
            public float Age, Life, Size, Growth, Gravity, LandY;
            public Color Color;
            public bool Additive, Solid, Splashes, Foam;
        }

        private struct Ripple
        {
            public Vector3 Centre;
            public Color Color;
            public float Age;
        }

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<Ripple> _ripples = new List<Ripple>();
        private readonly Random _rng = new Random(7);
        private readonly List<(Vector3 pos, Color color, float rate, float until, float size, float speed, float life, float gravity, bool additive)> _emitters =
            new List<(Vector3, Color, float, float, float, float, float, float, bool)>();
        private float _time;
        private float _flashLevel;
        private Vector3 _flashColor;

        /// <summary>Current flash light colour (added to ambient light by the renderer).</summary>
        public Vector3 Flash { get; private set; }

        /// <summary>Elephant-toothpaste foam bodies.</summary>
        public readonly Foam Foam = new Foam();

        public void Clear()
        {
            Foam.Clear();
            _fireballs.Clear();
            _shockwaves.Clear();
            ScreenFlash = 0;
            _particles.Clear();
            _emitters.Clear();
            _ripples.Clear();
            _flashLevel = 0;
        }

        public void OnEvent(LabEvent e, Lab lab)
        {
            var p = e.Position;
            float s = (float)Math.Clamp(e.Severity, 0, 1);
            switch (e.Type)
            {
                case LabEventType.Explosion:
                    Explode(p, s, e, lab);
                    break;
                case LabEventType.ToxicGas:
                    Emit(p, e.Code == "hazard.chloramine" ? Gfx.Rgba(0xD8E6C0, 120) : Gfx.Rgba(0xB8E03A, 120), 30, 3f + 3f * s, 0.06f, 0.12f, 6f, -0.015f, false);
                    break;
                case LabEventType.Smoke:
                    Emit(p, Gfx.Rgba(0x4A4A50, 150), 20, 2f, 0.05f, 0.15f, 4f, -0.02f, false);
                    break;
                case LabEventType.Spark:
                case LabEventType.FuseBlown:
                    Burst(p, Gfx.Rgba(0xFFF0A0), 50, 1.8f, 0.005f, 0.5f, 1f, true, 0.2f);
                    FlashLight(new Vector3(0.8f, 0.85f, 1f));
                    break;
                case LabEventType.Fizz:
                    Emit(p, Gfx.Rgba(0xFFFFFF, 110), 14, 1.2f, 0.003f, 0.06f, 0.7f, -0.05f, false);
                    break;
                case LabEventType.Foam:
                    Emit(p, Gfx.Rgba(0xFAFAF2, 235), 70, 3.5f, 0.007f, 0.11f, 3.5f, 0.25f, false);
                    break;
                case LabEventType.Fire:
                    FlashLight(new Vector3(1.0f, 0.55f, 0.2f));
                    break;
            }
        }

        // ------------------------------------------------------------------ emitters used every frame

        public void Steam(Vector3 mouth, float radius, float rate, float dt)
        {
            for (float n = rate * dt; n > 0; n -= 1)
            {
                if (_rng.NextDouble() >= n) continue;
                float a = (float)(_rng.NextDouble() * Math.PI * 2);
                float r = (float)_rng.NextDouble() * radius;
                Add(new Particle
                {
                    Position = mouth + new Vector3(MathF.Cos(a) * r, 0, MathF.Sin(a) * r),
                    Velocity = new Vector3(Rand() * 0.01f, 0.05f + (float)_rng.NextDouble() * 0.04f, Rand() * 0.01f),
                    Color = Gfx.Rgba(0xFFFFFF, 60), Size = 0.012f, Growth = 2.2f, Life = 1.6f, Gravity = -0.01f, LandY = float.NaN,
                });
            }
        }

        /// <summary>Flames dancing above a burning liquid (default colours), or a coloured flame for a burning element.</summary>
        public void Fire(Vector3 mouth, float radius, float dt, Color? tint = null, bool dazzling = false, bool smoke = false)
        {
            for (float n = (dazzling ? 110 : 70) * dt; n > 0; n -= 1)
            {
                if (_rng.NextDouble() >= n) continue;
                float a = (float)(_rng.NextDouble() * Math.PI * 2);
                float r = (float)_rng.NextDouble() * radius;
                Color color;
                if (tint is Color t)
                {
                    color = _rng.NextDouble() < 0.35 ? Gfx.Lerp(t, Color.White, 0.55f) : t;
                    color.A = 200;
                }
                else
                {
                    bool blue = _rng.NextDouble() < 0.25;
                    color = blue ? Gfx.Rgba(0x5A8CFF, 170) : (_rng.NextDouble() < 0.5 ? Gfx.Rgba(0xFFB030, 200) : Gfx.Rgba(0xFF6A20, 190));
                }

                Add(new Particle
                {
                    Position = mouth + new Vector3(MathF.Cos(a) * r, 0.003f, MathF.Sin(a) * r),
                    Velocity = new Vector3(Rand() * 0.02f, 0.18f + (float)_rng.NextDouble() * 0.12f, Rand() * 0.02f),
                    Color = color, Size = radius * (dazzling ? 0.6f : 0.45f), Growth = 0.2f, Life = 0.45f, Gravity = -0.05f, Additive = true, LandY = float.NaN,
                });
            }

            if (smoke && _rng.NextDouble() < 12 * dt)
            {
                // White oxide smoke (MgO, P₄O₁₀) rising from the burning solid.
                Emit(mouth + new Vector3(0, 0.03f, 0), Gfx.Rgba(0xF2F2F2, 120), 2, 0.4f, 0.01f, 0.05f, 3f, -0.02f, false);
            }

            var light = tint is Color c ? new Vector3(c.R, c.G, c.B) / 255f : new Vector3(1.0f, 0.55f, 0.2f);
            _flashLevel = Math.Max(_flashLevel, (dazzling ? 0.9f : 0.25f) + 0.1f * (float)_rng.NextDouble());
            _flashColor = dazzling ? Vector3.Lerp(light, Vector3.One, 0.6f) * 1.3f : light;
        }

        /// <summary>A falling drop that splashes where it lands.</summary>
        public void Drop(Vector3 from, Vector3 velocity, Color color, float landY)
        {
            color.A = (byte)Math.Max(170, (int)color.A);
            Add(new Particle { Position = from, Velocity = velocity, Color = color, Size = 0.0018f, Growth = 1, Life = 3, Gravity = 1, LandY = landY, Splashes = true });
        }

        private int _foamCount;

        /// <summary>
        /// A blob of soap foam pushed out of a vessel (elephant toothpaste): it rises, arcs over, lands on the bench and
        /// lies there for a while before slowly collapsing.
        /// </summary>
        public void FoamBlob(Vector3 from, Vector3 velocity, Color color, float size)
        {
            if (_foamCount > 650) return;
            color.A = 255;
            Add(new Particle
            {
                Position = from, Velocity = velocity, Color = color, Size = size, Growth = 1.35f, Life = 14f, Gravity = 0.32f, LandY = 0.004f,
                Foam = true, Solid = true,
            });
        }

        /// <summary>A falling solid piece (metal granule, marble chip...).</summary>
        public void Chunk(Vector3 from, Vector3 velocity, Color color, float size, float landY)
        {
            color.A = 255;
            Add(new Particle
            {
                Position = from, Velocity = velocity, Color = color, Size = size, Growth = 1, Life = 4, Gravity = 1, LandY = landY, Solid = true,
                Spin = new Vector3(Rand(), Rand(), Rand()) * 8,
            });
        }

        public void Splash(Vector3 at, Color color, int count = 5)
        {
            color.A = (byte)Math.Max(160, (int)color.A);
            for (int i = 0; i < count; i++)
            {
                var v = new Vector3(Rand() * 0.25f, 0.25f + (float)_rng.NextDouble() * 0.3f, Rand() * 0.25f);
                Add(new Particle { Position = at, Velocity = v, Color = color, Size = 0.0012f, Growth = 1, Life = 0.35f, Gravity = 1, LandY = float.NaN });
            }

            if (_ripples.Count < 40) _ripples.Add(new Ripple { Centre = at, Color = color, Age = 0 });
        }

        private struct Blast
        {
            public Vector3 Centre;
            public float Age, Life, Radius, Strength;
        }

        private readonly List<Blast> _fireballs = new List<Blast>();
        private readonly List<Blast> _shockwaves = new List<Blast>();

        /// <summary>Full-screen flash left by a blast (0–1), drawn over the scene by the front-end.</summary>
        public float ScreenFlash { get; private set; }

        /// <summary>
        /// A bang: a white-hot core and an orange fireball that swell and fade, a shock ring racing out over the bench,
        /// sparks and burning bits arcing away, the vessel's liquid splashed out as droplets, a smoke column, a light
        /// flash over the whole scene.
        /// </summary>
        private void Explode(Vector3 p, float s, LabEvent e, Lab lab)
        {
            float k = 0.5f + s;
            _fireballs.Add(new Blast { Centre = p + new Vector3(0, 0.02f, 0), Life = 0.45f + 0.3f * s, Radius = 0.035f + 0.07f * s, Strength = k });
            _shockwaves.Add(new Blast { Centre = new Vector3(p.X, Math.Max(0.002f, p.Y - 0.05f), p.Z), Life = 0.45f, Radius = 0.18f + 0.3f * s, Strength = k });

            // Sparks and glowing fragments that fall back under gravity.
            for (int i = 0; i < (int)(50 + 90 * s); i++)
            {
                var dir = Vector3.Normalize(new Vector3(Rand(), (float)_rng.NextDouble() * 1.3f + 0.2f, Rand()));
                bool ember = _rng.NextDouble() < 0.3;
                Add(new Particle
                {
                    Position = p, Velocity = dir * (0.8f + 1.8f * s) * (0.35f + (float)_rng.NextDouble() * 0.65f),
                    Color = ember ? Gfx.Rgba(0xFF6A1E) : Gfx.Rgba(0xFFE9A0), Size = ember ? 0.004f : 0.0022f, Growth = 0.6f,
                    Life = 0.5f + (float)_rng.NextDouble() * 0.9f, Gravity = 0.8f, Additive = true, LandY = 0f, Splashes = false,
                });
            }

            // Tongues of flame licking out of the fireball.
            Burst(p, Gfx.Rgba(0xFF8C26), (int)(16 + 24 * s), 0.7f + 0.6f * s, 0.012f + 0.01f * s, 0.4f, -0.1f, true, 1.6f);

            // The liquid in the vessel is thrown out and splashes down on the bench.
            var owner = lab.World.Find(e.InstanceId);
            var c = owner?.Get<IDLab.Core.Components.LiquidContainer>();
            if (c != null)
            {
                var col = Gfx.FromCore(c.LiquidColor);
                col.A = (byte)Math.Max(190, (int)col.A);
                for (int i = 0; i < (int)(20 + 60 * s); i++)
                {
                    float a = (float)(_rng.NextDouble() * Math.PI * 2);
                    float out_ = 0.3f + (float)_rng.NextDouble() * (0.6f + 0.8f * s);
                    Add(new Particle
                    {
                        Position = p, Velocity = new Vector3(MathF.Cos(a) * out_, 0.8f + (float)_rng.NextDouble() * (1.2f + 1.5f * s), MathF.Sin(a) * out_),
                        Color = col, Size = 0.0025f + (float)_rng.NextDouble() * 0.002f, Growth = 1, Life = 3, Gravity = 1, LandY = 0.001f, Splashes = true,
                    });
                }
            }

            // Thick smoke billowing up into a mushroom, and a dark haze around the vessel.
            Emit(p + new Vector3(0, 0.03f, 0), Gfx.Rgba(0x3A3A40, 150), 18 + 20 * s, 1.2f + 1.5f * s, 0.022f + 0.015f * s, 0.25f, 3.5f, -0.03f, false);
            Emit(p, Gfx.Rgba(0x6A6A70, 90), 12, 0.5f, 0.03f, 0.4f, 2f, -0.005f, false);
            FlashLight(new Vector3(1.4f, 0.75f, 0.3f) * (0.7f + 0.6f * s));
            ScreenFlash = Math.Max(ScreenFlash, 0.2f + 0.3f * s);
        }

        private void Burst(Vector3 p, Color c, int count, float speed, float size, float life, float gravity, bool additive, float growth)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = Vector3.Normalize(new Vector3(Rand(), Rand() * 0.6f + 0.5f, Rand()));
                Add(new Particle
                {
                    Position = p, Velocity = dir * speed * (0.4f + (float)_rng.NextDouble() * 0.6f), Color = c, Size = size, Growth = growth,
                    Life = life * (0.6f + (float)_rng.NextDouble() * 0.4f), Gravity = gravity, Additive = additive, LandY = float.NaN,
                });
            }
        }

        private void Emit(Vector3 p, Color c, float rate, float duration, float size, float speed, float life, float gravity, bool additive) =>
            _emitters.Add((p, c, rate, _time + duration, size, speed, life, gravity, additive));

        private void FlashLight(Vector3 color)
        {
            _flashColor = color;
            _flashLevel = 1f;
        }

        private void Add(Particle p)
        {
            if (_particles.Count > 2500) return;
            _particles.Add(p);
        }

        // ------------------------------------------------------------------ simulation

        public void Update(float dt, Lab lab)
        {
            _time += dt;
            Foam.Update(dt, lab);
            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                var e = _emitters[i];
                if (_time > e.until)
                {
                    _emitters.RemoveAt(i);
                    continue;
                }

                int n = (int)(e.rate * dt + _rng.NextDouble());
                for (int k = 0; k < n; k++)
                {
                    var dir = Vector3.Normalize(new Vector3(Rand(), 1.2f, Rand()));
                    Add(new Particle
                    {
                        Position = e.pos, Velocity = dir * e.speed, Color = e.color, Size = e.size, Growth = e.gravity > 0 ? 1.4f : 3f,
                        Life = e.life, Gravity = e.gravity, Additive = e.additive, LandY = e.gravity > 0 ? 0f : float.NaN,
                    });
                }
            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                p.Velocity.Y -= 9.81f * p.Gravity * dt;
                if (!p.Solid && !p.Splashes) p.Velocity *= 1f - Math.Min(1f, dt * 0.8f);
                if (p.Foam) p.Velocity *= 1f - Math.Min(1f, dt * 1.2f); // thick foam: slow, heavy, sticky
                p.Position += p.Velocity * dt;
                p.Spin += Vector3.Zero;
                if (!float.IsNaN(p.LandY) && p.Position.Y <= p.LandY)
                {
                    if (p.Splashes)
                    {
                        Splash(new Vector3(p.Position.X, p.LandY + 0.001f, p.Position.Z), p.Color, 3);
                        _particles.RemoveAt(i);
                        continue;
                    }

                    p.Position.Y = p.LandY;
                    p.Velocity = Vector3.Zero;
                    p.Gravity = 0;
                    if (p.Foam) p.Life = Math.Min(p.Life, p.Age + 9f); // a heap of foam on the bench
                    else if (p.Solid) p.Life = Math.Min(p.Life, p.Age + (p.LandY > 0.001f ? 0.15f : 2.5f)); // sinks into liquid / lies on the bench
                }

                _particles[i] = p;
            }

            for (int i = _ripples.Count - 1; i >= 0; i--)
            {
                var r = _ripples[i];
                r.Age += dt;
                if (r.Age > 0.6f) _ripples.RemoveAt(i);
                else _ripples[i] = r;
            }

            for (int i = _fireballs.Count - 1; i >= 0; i--)
            {
                var b = _fireballs[i];
                b.Age += dt;
                if (b.Age >= b.Life) _fireballs.RemoveAt(i);
                else _fireballs[i] = b;
            }

            for (int i = _shockwaves.Count - 1; i >= 0; i--)
            {
                var b = _shockwaves[i];
                b.Age += dt;
                if (b.Age >= b.Life) _shockwaves.RemoveAt(i);
                else _shockwaves[i] = b;
            }

            ScreenFlash = Math.Max(0, ScreenFlash - dt * 3.5f);
            _flashLevel = Math.Max(0, _flashLevel - dt * 2.5f);
            Flash = _flashColor * _flashLevel;
        }

        /// <summary>Solid pieces are lit like any other object (opaque pass).</summary>
        public void DrawSolids()
        {
            Foam.Draw();
            _foamCount = 0;
            foreach (var p in _particles)
            {
                if (p.Foam)
                {
                    // Swells as it leaves the flask, and slumps / shrinks in its last seconds on the bench.
                    _foamCount++;
                    float left = p.Life - p.Age;
                    float size = p.Size * Math.Min(p.Growth, 1f + p.Age * 1.5f) * Math.Clamp(left / 2.5f, 0.15f, 1f);
                    var squash = p.Gravity == 0 ? new Vector3(size * 1.15f, size * 0.7f, size * 1.15f) : new Vector3(size);
                    Gfx.Draw(Gfx.Sphere, Gfx.Mul(Gfx.Scale(squash.X, squash.Y, squash.Z), Gfx.Translate(p.Position)), p.Color, new Vector3(0.08f), Foam.Texture, 0.22f);
                    continue;
                }

                if (!p.Solid) continue;
                var rot = Quaternion.CreateFromYawPitchRoll(p.Spin.X * p.Age, p.Spin.Y * p.Age, p.Spin.Z * p.Age);
                Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Mul(Gfx.Scale(p.Size), Gfx.Rotate(rot)), Gfx.Translate(p.Position)), p.Color);
            }
        }

        public void Draw()
        {
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthMask();
            foreach (var r in _ripples)
            {
                float t = r.Age / 0.6f;
                DrawCircle3D(r.Centre, 0.003f + t * 0.02f, Vector3.UnitX, 90f, Gfx.WithAlpha(Gfx.Lerp(r.Color, Color.White, 0.5f), (1 - t) * 0.9f));
            }

            // Shock ring on the bench and a faint expanding shell of compressed air.
            foreach (var b in _shockwaves)
            {
                float t = b.Age / b.Life;
                float r = b.Radius * (1 - (1 - t) * (1 - t));
                for (int ring = 0; ring < 3; ring++)
                {
                    DrawCircle3D(b.Centre, r * (1 - ring * 0.06f), Vector3.UnitX, 90f, Gfx.WithAlpha(new Color(255, 236, 200, 255), (1 - t) * (0.9f - ring * 0.25f)));
                }

                DrawSphereEx(b.Centre, r * 0.8f, 10, 14, Gfx.WithAlpha(new Color(255, 240, 220, 255), (1 - t) * 0.12f));
            }

            BeginBlendMode(BlendMode.Additive);
            foreach (var b in _fireballs)
            {
                float t = b.Age / b.Life;
                float grow = 1 - (1 - Math.Min(1, t * 2.2f)) * (1 - Math.Min(1, t * 2.2f));
                float fade = 1 - t;
                var rise = new Vector3(0, b.Radius * 0.8f * t, 0);
                DrawSphereEx(b.Centre + rise, b.Radius * grow, 12, 16, Gfx.WithAlpha(new Color(255, 120, 30, 255), fade * 0.55f));
                DrawSphereEx(b.Centre + rise * 0.7f, b.Radius * 0.72f * grow, 12, 16, Gfx.WithAlpha(new Color(255, 200, 70, 255), fade * 0.7f));
                DrawSphereEx(b.Centre + rise * 0.4f, b.Radius * 0.42f * grow, 10, 14, Gfx.WithAlpha(new Color(255, 250, 225, 255), fade * fade * 0.95f));
            }

            EndBlendMode();

            foreach (bool additive in new[] { false, true })
            {
                BeginBlendMode(additive ? BlendMode.Additive : BlendMode.Alpha);
                foreach (var p in _particles)
                {
                    if (p.Additive != additive || p.Solid) continue;
                    float t = p.Age / p.Life;
                    float alpha = p.Splashes ? 1f : t < 0.1f ? t / 0.1f : 1f - (t - 0.1f) / 0.9f;
                    float size = p.Size * (1f + (p.Growth - 1f) * t);
                    int rings = size > 0.012f ? 7 : 4, slices = size > 0.012f ? 10 : 6; // big puffs need a rounder outline
                    DrawSphereEx(p.Position, Math.Max(0.0008f, size), rings, slices, Gfx.WithAlpha(p.Color, p.Color.A / 255f * alpha));
                }

                EndBlendMode();
            }

            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
        }

        private float Rand() => (float)(_rng.NextDouble() * 2 - 1);
    }
}
