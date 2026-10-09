using System;
using System.Collections.Generic;
using System.Numerics;
using IDLab.Core.Components;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Animated liquid streams. A stream is a lit, slightly transparent tube along the ballistic path from the lip
    /// (or a burette tip) to the surface below. When pouring starts its front falls from the lip at the speed of
    /// free fall; it thins as it speeds up, ripples along its length, and when pouring stops the tail detaches
    /// and falls the rest of the way instead of vanishing.
    /// </summary>
    internal static class Streams
    {
        private const int Rings = 22, Sides = 12;
        private const float G = 9.81f;

        private sealed class Stream
        {
            public bool Active;
            public float Age, StopAge;
            public Vector3 Lip, V0;
            public float LandY, Radius;
            public Color Color;
            public Mesh Mesh;
            public bool HasMesh;
            public float Seed;
        }

        private static readonly Dictionary<int, Stream> All = new Dictionary<int, Stream>();
        private static readonly Vector3[] Verts = new Vector3[Rings * Sides];
        private static readonly Vector3[] Normals = new Vector3[Rings * Sides];
        private static ushort[] _indices;
        private static float _time;

        /// <summary>Feeds the current state of one stream source (key: item id, negative for burette taps).</summary>
        public static void Feed(int key, bool flowing, Vector3 lip, Vector3 v0, float landY, double litersPerSecond, Color color, float dt)
        {
            if (!All.TryGetValue(key, out var s))
            {
                if (!flowing) return;
                s = new Stream { Seed = key * 1.37f };
                All[key] = s;
            }

            if (flowing)
            {
                if (!s.Active)
                {
                    s.Active = true;
                    s.Age = 0;
                }

                s.Age += dt;
                s.StopAge = 0;
                s.Lip = lip;
                s.V0 = v0;
                s.LandY = landY;
                // Continuity: Q = π r² v at the lip (v ≈ outflow speed); clamp to something visible.
                float speed = Math.Max(0.15f, v0.Length());
                float r = MathF.Sqrt((float)(litersPerSecond / 1000.0) / (MathF.PI * speed));
                s.Radius = s.Radius <= 0 ? r : s.Radius + (r - s.Radius) * Math.Min(1f, dt * 6f);
                s.Radius = Math.Clamp(s.Radius, 0.0009f, 0.009f);
                var c = color;
                c.A = (byte)Math.Clamp(Math.Max((int)color.A, 150), 0, 235);
                s.Color = c;
            }
            else if (s.Active)
            {
                s.Active = false;
                s.StopAge = 0;
            }
        }

        /// <summary>Ages detached tails and forgets streams that have fully landed.</summary>
        public static void Update(float dt)
        {
            _time += dt;
            foreach (var s in All.Values)
            {
                if (!s.Active) s.StopAge += dt;
            }
        }

        /// <summary>True while the front of a stream has reached the surface (splashes start then, not before).</summary>
        public static bool HasLanded(int key) => All.TryGetValue(key, out var s) && s.Active && s.Age >= FallTime(s);

        private static float FallTime(Stream s)
        {
            float drop = Math.Max(0.001f, s.Lip.Y - s.LandY);
            // Solve y(t) = lip.y + v0.y t − ½ g t² = landY.
            float vy = s.V0.Y;
            return (vy + MathF.Sqrt(vy * vy + 2 * G * drop)) / G;
        }

        public static void Draw()
        {
            foreach (var s in All.Values)
            {
                float total = FallTime(s);
                float top = s.Active ? 0 : s.StopAge;            // the tail that left the lip when pouring stopped
                float bottom = Math.Min(s.Age, total);           // the falling front
                if (!s.Active && top >= total) continue;
                if (bottom - top < 0.004f) continue;
                Build(s, top, bottom, total);
                Gfx.Env = 0.55f;
                Gfx.Draw(s.Mesh, Matrix4x4.Identity, s.Color, new Vector3(0.05f, 0.06f, 0.07f), Gfx.White, 0.9f);

                // Rounded ends: the falling front, and a drop forming at a detached tail.
                var tipCol = s.Color;
                if (s.Age < total) Gfx.Draw(Gfx.Sphere, Gfx.Mul(Gfx.Scale(RadiusAt(s, bottom) * 1.35f), Gfx.Translate(Point(s, bottom))), tipCol);
                if (!s.Active) Gfx.Draw(Gfx.Sphere, Gfx.Mul(Gfx.Scale(RadiusAt(s, top) * 1.2f), Gfx.Translate(Point(s, top))), tipCol);
                Gfx.Env = -1f;
            }
        }

        private static Vector3 Point(Stream s, float t) => s.Lip + s.V0 * t + new Vector3(0, -0.5f * G * t * t, 0);

        private static float RadiusAt(Stream s, float t)
        {
            float v0 = Math.Max(0.15f, s.V0.Length());
            var v = s.V0 + new Vector3(0, -G * t, 0);
            float thin = MathF.Sqrt(v0 / Math.Max(v0, v.Length()));          // faster → thinner (continuity)
            float ripple = 1f + 0.13f * MathF.Sin(t * 90f - _time * 26f + s.Seed) + 0.06f * MathF.Sin(t * 210f - _time * 47f);
            return s.Radius * Math.Max(0.45f, thin) * ripple;
        }

        private static void Build(Stream s, float top, float bottom, float total)
        {
            if (!s.HasMesh)
            {
                if (_indices == null)
                {
                    var list = new List<ushort>();
                    for (int i = 0; i < Rings - 1; i++)
                    {
                        for (int j = 0; j < Sides; j++)
                        {
                            int a = i * Sides + j, b = i * Sides + (j + 1) % Sides, c = a + Sides, d = b + Sides;
                            list.Add((ushort)a); list.Add((ushort)c); list.Add((ushort)b);
                            list.Add((ushort)b); list.Add((ushort)c); list.Add((ushort)d);
                        }
                    }

                    _indices = list.ToArray();
                }

                s.Mesh = Gfx.DynamicMesh(Rings * Sides, _indices);
                s.HasMesh = true;
            }

            for (int i = 0; i < Rings; i++)
            {
                float t = top + (bottom - top) * i / (Rings - 1);
                var p = Point(s, t);
                var v = s.V0 + new Vector3(0, -G * t, 0);
                var tangent = v.LengthSquared() > 1e-8f ? Vector3.Normalize(v) : -Vector3.UnitY;
                var side = Vector3.Cross(tangent, MathF.Abs(tangent.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY);
                side = Vector3.Normalize(side);
                var up = Vector3.Cross(side, tangent);
                // The stream sways a little as it falls (surface tension wobble).
                float sway = 0.0006f * MathF.Sin(t * 35f - _time * 9f + s.Seed) * Math.Min(1f, t * 20f);
                p += side * sway;
                float r = RadiusAt(s, t);
                for (int j = 0; j < Sides; j++)
                {
                    float a = j * MathF.PI * 2 / Sides;
                    var n = side * MathF.Cos(a) + up * MathF.Sin(a);
                    Verts[i * Sides + j] = p + n * r;
                    Normals[i * Sides + j] = n;
                }
            }

            Gfx.UpdateDynamic(s.Mesh, Verts, Normals);
        }
    }
}
