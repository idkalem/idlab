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
            public bool Additive, Solid, Splashes;
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

        public void Clear()
        {
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
                    Burst(p, Gfx.Rgba(0xFF8C26), 90, 1.4f, 0.03f, 0.55f, 0.3f, true, 0.4f);
                    Burst(p, Gfx.Rgba(0xFFD966), 60, 2.4f, 0.006f, 0.9f, 1.2f, true, 0.2f);
                    Emit(p, Gfx.Rgba(0x3A3A40, 170), 40, 2.0f, 0.06f, 0.25f, 4f, -0.03f, false);
                    FlashLight(new Vector3(1.4f, 0.7f, 0.25f) * (0.8f + s));
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

        /// <summary>Flames dancing above a burning liquid.</summary>
        public void Fire(Vector3 mouth, float radius, float dt)
        {
            for (float n = 70 * dt; n > 0; n -= 1)
            {
                if (_rng.NextDouble() >= n) continue;
                float a = (float)(_rng.NextDouble() * Math.PI * 2);
                float r = (float)_rng.NextDouble() * radius;
                bool blue = _rng.NextDouble() < 0.25;
                Add(new Particle
                {
                    Position = mouth + new Vector3(MathF.Cos(a) * r, 0.003f, MathF.Sin(a) * r),
                    Velocity = new Vector3(Rand() * 0.02f, 0.18f + (float)_rng.NextDouble() * 0.12f, Rand() * 0.02f),
                    Color = blue ? Gfx.Rgba(0x5A8CFF, 170) : (_rng.NextDouble() < 0.5 ? Gfx.Rgba(0xFFB030, 200) : Gfx.Rgba(0xFF6A20, 190)),
                    Size = radius * 0.45f, Growth = 0.2f, Life = 0.45f, Gravity = -0.05f, Additive = true, LandY = float.NaN,
                });
            }

            _flashLevel = Math.Max(_flashLevel, 0.25f + 0.1f * (float)_rng.NextDouble());
            _flashColor = new Vector3(1.0f, 0.55f, 0.2f);
        }

        /// <summary>A falling drop that splashes where it lands.</summary>
        public void Drop(Vector3 from, Vector3 velocity, Color color, float landY)
        {
            color.A = (byte)Math.Max(170, (int)color.A);
            Add(new Particle { Position = from, Velocity = velocity, Color = color, Size = 0.0018f, Growth = 1, Life = 3, Gravity = 1, LandY = landY, Splashes = true });
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
                    if (p.Solid) p.Life = Math.Min(p.Life, p.Age + (p.LandY > 0.001f ? 0.15f : 2.5f)); // sinks into liquid / lies on the bench
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

            _flashLevel = Math.Max(0, _flashLevel - dt * 2.5f);
            Flash = _flashColor * _flashLevel;
        }

        /// <summary>Solid pieces are lit like any other object (opaque pass).</summary>
        public void DrawSolids()
        {
            foreach (var p in _particles)
            {
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

            foreach (bool additive in new[] { false, true })
            {
                BeginBlendMode(additive ? BlendMode.Additive : BlendMode.Alpha);
                foreach (var p in _particles)
                {
                    if (p.Additive != additive || p.Solid) continue;
                    float t = p.Age / p.Life;
                    float alpha = p.Splashes ? 1f : t < 0.1f ? t / 0.1f : 1f - (t - 0.1f) / 0.9f;
                    float size = p.Size * (1f + (p.Growth - 1f) * t);
                    DrawSphereEx(p.Position, Math.Max(0.0008f, size), 4, 6, Gfx.WithAlpha(p.Color, p.Color.A / 255f * alpha));
                }

                EndBlendMode();
            }

            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
        }

        private float Rand() => (float)(_rng.NextDouble() * 2 - 1);
    }
}
