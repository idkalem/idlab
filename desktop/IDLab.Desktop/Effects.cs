using System;
using System.Collections.Generic;
using System.Numerics;
using IDLab.Core.Events;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>Lightweight particle effects: steam, smoke, sparks, fireball, chlorine gas, splashes, flashes.</summary>
    internal sealed class Effects
    {
        private struct Particle
        {
            public Vector3 Position, Velocity;
            public float Age, Life, Size, Growth, Gravity;
            public Color Color;
            public bool Additive;
        }

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Random _rng = new Random(7);
        private readonly List<(Vector3 pos, Color color, float rate, float until, float size, float speed, float life, float gravity)> _emitters =
            new List<(Vector3, Color, float, float, float, float, float, float)>();
        private float _time;

        /// <summary>Current flash light colour (added to ambient light by the renderer).</summary>
        public Vector3 Flash { get; private set; }
        private float _flashLevel;
        private Vector3 _flashColor;

        public void Clear()
        {
            _particles.Clear();
            _emitters.Clear();
            _flashLevel = 0;
        }

        public void OnEvent(LabEvent e, Lab lab)
        {
            var p = e.Position;
            float s = (float)Math.Clamp(e.Severity, 0, 1);
            switch (e.Type)
            {
                case LabEventType.Explosion:
                    Burst(p, Gfx.Rgba(0xFF8C26), 90, 1.4f, 0.03f, 0.55f, 0.3f, additive: true, growth: 0.4f);
                    Burst(p, Gfx.Rgba(0xFFD966), 60, 2.4f, 0.006f, 0.9f, 1.2f, additive: true, growth: 0.2f);
                    Emit(p, Gfx.Rgba(0x3A3A40, 170), 40, 2.0f, 0.06f, 0.25f, 4f, -0.03f);
                    FlashLight(new Vector3(1.4f, 0.7f, 0.25f) * (0.8f + s));
                    break;
                case LabEventType.ToxicGas:
                    Emit(p, Gfx.Rgba(0xB8E03A, 120), 30, 3f + 3f * s, 0.06f, 0.12f, 6f, -0.015f);
                    break;
                case LabEventType.Smoke:
                    Emit(p, Gfx.Rgba(0x4A4A50, 150), 20, 2f, 0.05f, 0.15f, 4f, -0.02f);
                    break;
                case LabEventType.Spark:
                case LabEventType.FuseBlown:
                    Burst(p, Gfx.Rgba(0xFFF0A0), 50, 1.8f, 0.005f, 0.5f, 1f, additive: true, growth: 0.2f);
                    FlashLight(new Vector3(0.8f, 0.85f, 1f));
                    break;
                case LabEventType.Fizz:
                    Emit(p, Gfx.Rgba(0xFFFFFF, 160), 25, 1.5f, 0.005f, 0.2f, 1f, -0.05f);
                    break;
                case LabEventType.Spill:
                    Burst(p, Gfx.Rgba(0x9CCBFF, 190), 20, 0.5f, 0.005f, 0.6f, 1f, additive: false, growth: 0.5f);
                    break;
            }
        }

        public void Steam(Vector3 mouth, float radius, float rate, float dt)
        {
            float n = rate * dt;
            while (n > 0)
            {
                if (_rng.NextDouble() < n)
                {
                    float a = (float)(_rng.NextDouble() * Math.PI * 2);
                    float r = (float)_rng.NextDouble() * radius;
                    Add(mouth + new Vector3(MathF.Cos(a) * r, 0, MathF.Sin(a) * r), new Vector3(Rand() * 0.01f, 0.05f + (float)_rng.NextDouble() * 0.04f, Rand() * 0.01f),
                        Gfx.Rgba(0xFFFFFF, 60), 0.012f, 2.2f, 1.6f, -0.01f, false);
                }

                n -= 1;
            }
        }

        private void Burst(Vector3 p, Color c, int count, float speed, float size, float life, float gravity, bool additive, float growth)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = Vector3.Normalize(new Vector3(Rand(), Rand() * 0.6f + 0.5f, Rand()));
                Add(p, dir * speed * (0.4f + (float)_rng.NextDouble() * 0.6f), c, size, growth, life * (0.6f + (float)_rng.NextDouble() * 0.4f), gravity, additive);
            }
        }

        private void Emit(Vector3 p, Color c, float rate, float duration, float size, float speed, float life, float gravity) =>
            _emitters.Add((p, c, rate, _time + duration, size, speed, life, gravity));

        private void FlashLight(Vector3 color)
        {
            _flashColor = color;
            _flashLevel = 1f;
        }

        private void Add(Vector3 p, Vector3 v, Color c, float size, float growth, float life, float gravity, bool additive)
        {
            if (_particles.Count > 1500) return;
            _particles.Add(new Particle { Position = p, Velocity = v, Color = c, Size = size, Growth = growth, Life = life, Gravity = gravity, Additive = additive });
        }

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
                    Add(e.pos, dir * e.speed, e.color, e.size, 3f, e.life, e.gravity, false);
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
                p.Velocity *= 1f - Math.Min(1f, dt * 0.8f);
                p.Position += p.Velocity * dt;
                if (p.Position.Y < 0 && p.Gravity > 0)
                {
                    p.Position.Y = 0;
                    p.Velocity = Vector3.Zero;
                }

                _particles[i] = p;
            }

            _flashLevel = Math.Max(0, _flashLevel - dt * 2.5f);
            Flash = _flashColor * _flashLevel;
        }

        public void Draw()
        {
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthMask();
            foreach (bool additive in new[] { false, true })
            {
                BeginBlendMode(additive ? BlendMode.Additive : BlendMode.Alpha);
                foreach (var p in _particles)
                {
                    if (p.Additive != additive) continue;
                    float t = p.Age / p.Life;
                    float alpha = t < 0.1f ? t / 0.1f : 1f - (t - 0.1f) / 0.9f;
                    float size = p.Size * (1f + (p.Growth - 1f) * t);
                    DrawSphereEx(p.Position, Math.Max(0.001f, size), 4, 6, Gfx.WithAlpha(p.Color, p.Color.A / 255f * alpha));
                }

                EndBlendMode();
            }

            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
        }

        private float Rand() => (float)(_rng.NextDouble() * 2 - 1);
    }
}
