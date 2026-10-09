using System;
using System.Collections.Generic;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using Raylib_cs;

namespace IDLab.Desktop
{
    /// <summary>
    /// The real inner shape of a vessel (beaker, conical flask, round flask, test tube, bottle...), used for the glass
    /// mesh, for turning a volume into a liquid height, and for clipping the liquid to a level surface when tilted.
    /// Coordinates are metres relative to the vessel's base centre (the container's LocalOffset).
    /// </summary>
    internal sealed class VesselShape
    {
        private const int Samples = 96;
        private readonly float[] _ys = new float[Samples + 1];
        private readonly double[] _cumulative = new double[Samples + 1];

        public Vector2[] Inner { get; }
        public float Wall { get; }
        public float RimY => Inner[Inner.Length - 1].Y;
        public float Floor => Inner[0].Y;
        public Mesh Glass { get; }
        public double CapacityLiters => _cumulative[Samples] * 1000.0;

        public VesselShape(Vector2[] inner, float wall, bool lipFlare = true)
        {
            Inner = inner;
            Wall = wall;

            // Outer profile = inner pushed outwards by the wall thickness, plus a rolled lip.
            var p = new List<Vector2> { new Vector2(0, 0) };
            for (int i = 1; i < inner.Length; i++)
            {
                p.Add(new Vector2(inner[i].X + wall, Math.Max(0, inner[i].Y - (i == 1 ? wall : 0))));
            }

            var rim = inner[inner.Length - 1];
            if (lipFlare)
            {
                p[p.Count - 1] = new Vector2(rim.X + wall, rim.Y - 0.002f);
                p.Add(new Vector2(rim.X + wall + 0.0012f, rim.Y));
            }

            for (int i = inner.Length - 1; i >= 0; i--) p.Add(inner[i]);
            Glass = Gfx.Lathe(p.ToArray(), 96);

            // Volume table: V(y) = ∫ π r(y)² dy from the inner floor up.
            float y0 = Floor, y1 = RimY;
            double v = 0;
            for (int i = 0; i <= Samples; i++)
            {
                float y = y0 + (y1 - y0) * i / Samples;
                _ys[i] = y;
                if (i > 0)
                {
                    float rm = RadiusAt((y + _ys[i - 1]) / 2);
                    v += Math.PI * rm * rm * (y - _ys[i - 1]);
                }

                _cumulative[i] = v;
            }
        }

        /// <summary>Inner radius at height y (interpolated along the profile).</summary>
        public float RadiusAt(float y)
        {
            for (int i = 1; i < Inner.Length; i++)
            {
                var a = Inner[i - 1];
                var b = Inner[i];
                if (y <= b.Y)
                {
                    if (b.Y - a.Y < 1e-6f) return b.X;
                    return a.X + (b.X - a.X) * Math.Clamp((y - a.Y) / (b.Y - a.Y), 0f, 1f);
                }
            }

            return Inner[Inner.Length - 1].X;
        }

        /// <summary>Height of a liquid surface holding <paramref name="liters"/> when upright.</summary>
        public float HeightForVolume(double liters)
        {
            double m3 = liters / 1000.0;
            if (m3 <= 0) return Floor;
            if (m3 >= _cumulative[Samples]) return RimY;
            int lo = 0, hi = Samples;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_cumulative[mid] < m3) lo = mid;
                else hi = mid;
            }

            double t = (m3 - _cumulative[lo]) / Math.Max(1e-15, _cumulative[hi] - _cumulative[lo]);
            return _ys[lo] + (float)t * (_ys[hi] - _ys[lo]);
        }

        // ------------------------------------------------------------------ catalogue of shapes

        private static readonly Dictionary<string, VesselShape> Cache = new Dictionary<string, VesselShape>();

        public static VesselShape For(Item item)
        {
            var c = item.Container;
            string key = item.Def.Id;
            if (Cache.TryGetValue(key, out var shape)) return shape;

            float r = (float)c.RadiusM, h = (float)c.InnerHeightM;
            switch (item.Def.VisualKey)
            {
                case "erlenmeyer":
                    shape = new VesselShape(new[]
                    {
                        new Vector2(0, 0.0025f), new Vector2(0.036f, 0.0025f), new Vector2(0.0385f, 0.008f), new Vector2(0.038f, 0.016f),
                        new Vector2(0.0155f, 0.096f), new Vector2(0.015f, 0.13f),
                    }, 0.0022f);
                    break;
                case "round_flask":
                {
                    var pts = new List<Vector2>();
                    const float R = 0.04f, cy = 0.045f, neck = 0.013f;
                    for (int a = -90; a <= 70; a += 10)
                    {
                        float rad = a * MathF.PI / 180f;
                        pts.Add(new Vector2(Math.Max(0, R * MathF.Cos(rad)), cy + R * MathF.Sin(rad)));
                    }

                    pts[0] = new Vector2(0, cy - R);
                    pts.Add(new Vector2(neck, cy + R * MathF.Sin(71f * MathF.PI / 180f)));
                    pts.Add(new Vector2(neck, 0.12f));
                    shape = new VesselShape(pts.ToArray(), 0.0022f);
                    break;
                }

                case "test_tube":
                {
                    var pts = new List<Vector2> { new Vector2(0, 0.0012f) };
                    for (int a = 15; a <= 90; a += 15)
                    {
                        float rad = a * MathF.PI / 180f;
                        pts.Add(new Vector2(r * MathF.Sin(rad), 0.0012f + r * (1 - MathF.Cos(rad))));
                    }

                    pts.Add(new Vector2(r, h));
                    shape = new VesselShape(pts.ToArray(), 0.0012f);
                    break;
                }

                case "bottle":
                    shape = new VesselShape(new[]
                    {
                        new Vector2(0, 0.004f), new Vector2(0.034f, 0.004f), new Vector2(0.035f, 0.01f), new Vector2(0.035f, 0.1f),
                        new Vector2(0.0145f, 0.122f), new Vector2(0.0135f, 0.142f),
                    }, 0.0028f, lipFlare: false);
                    break;
                case "dropper":
                    shape = new VesselShape(new[]
                    {
                        new Vector2(0, 0.003f), new Vector2(0.015f, 0.003f), new Vector2(0.015f, 0.045f), new Vector2(0.0065f, 0.053f),
                        new Vector2(0.006f, 0.06f),
                    }, 0.002f, lipFlare: false);
                    break;
                case "jar":
                    shape = new VesselShape(new[]
                    {
                        new Vector2(0, 0.004f), new Vector2(0.024f, 0.004f), new Vector2(0.025f, 0.008f), new Vector2(0.025f, 0.048f),
                        new Vector2(0.022f, 0.051f), new Vector2(0.022f, 0.055f),
                    }, 0.003f, lipFlare: false);
                    break;
                case "burette":
                    shape = new VesselShape(new[] { new Vector2(0, 0.0f), new Vector2(r, 0.004f), new Vector2(r, h) }, 0.0016f, lipFlare: false);
                    break;
                default: // beaker, cylinder, petri
                    shape = new VesselShape(new[] { new Vector2(0, 0.002f), new Vector2(r, 0.002f), new Vector2(r, h) },
                        item.Def.VisualKey == "petri" ? 0.0015f : 0.0022f);
                    break;
            }

            Cache[key] = shape;
            return shape;
        }
    }

    /// <summary>
    /// Mesh of a liquid (or powder pile) inside a vessel whose free surface stays horizontal in world space.
    /// The inner profile is swept around the axis and clipped by the plane through (0, level, 0) whose normal is
    /// world-up expressed in the vessel's frame — exact for straight walls, a close approximation for flasks.
    /// </summary>
    internal sealed class LiquidSurface
    {
        private const int Segments = 28;
        private const int ProfileSamples = 14;
        private readonly Vector3[] _verts;
        private readonly Vector3[] _normals;
        private readonly Vector2[] _profile = new Vector2[ProfileSamples];
        private readonly Vector2[] _profileNormal = new Vector2[ProfileSamples];
        private VesselShape _shape;

        public Mesh Mesh { get; }

        public LiquidSurface()
        {
            int vertexCount = Segments * ProfileSamples + Segments + 1;
            _verts = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            var idx = new List<ushort>();
            for (int i = 0; i < Segments; i++)
            {
                int j = (i + 1) % Segments;
                for (int k = 0; k < ProfileSamples - 1; k++)
                {
                    ushort a = (ushort)(i * ProfileSamples + k), b = (ushort)(i * ProfileSamples + k + 1);
                    ushort c = (ushort)(j * ProfileSamples + k), d = (ushort)(j * ProfileSamples + k + 1);
                    idx.Add(a); idx.Add(b); idx.Add(c);
                    idx.Add(c); idx.Add(b); idx.Add(d);
                }
            }

            int ring = Segments * ProfileSamples, centre = ring + Segments;
            for (int i = 0; i < Segments; i++)
            {
                idx.Add((ushort)(ring + i));
                idx.Add((ushort)centre);
                idx.Add((ushort)(ring + (i + 1) % Segments));
            }

            Mesh = Gfx.DynamicMesh(vertexCount, idx.ToArray());
        }

        private void Resample(VesselShape shape, float inset)
        {
            if (_shape == shape) return;
            _shape = shape;
            var inner = shape.Inner;
            var lengths = new float[inner.Length];
            for (int i = 1; i < inner.Length; i++) lengths[i] = lengths[i - 1] + Vector2.Distance(inner[i], inner[i - 1]);
            float total = lengths[inner.Length - 1];
            for (int k = 0; k < ProfileSamples; k++)
            {
                float s = total * k / (ProfileSamples - 1);
                int seg = 1;
                while (seg < inner.Length - 1 && lengths[seg] < s) seg++;
                var a = inner[seg - 1];
                var b = inner[seg];
                float t = Math.Clamp((s - lengths[seg - 1]) / Math.Max(1e-6f, lengths[seg] - lengths[seg - 1]), 0, 1);
                var p = Vector2.Lerp(a, b, t);
                p.X = Math.Max(0, p.X - inset);
                _profile[k] = p;
                var tangent = Vector2.Normalize(b - a);
                _profileNormal[k] = new Vector2(tangent.Y, -tangent.X);
            }
        }

        /// <summary>Rebuilds the mesh for a fill level (vessel frame) and the world-up direction in that frame.</summary>
        public void Update(VesselShape shape, float level, Vector3 upLocal)
        {
            Resample(shape, 0.0005f);
            var n = Vector3.Normalize(upLocal);
            var planePoint = new Vector3(0, level, 0);
            int ring = Segments * ProfileSamples;
            var centre = Vector3.Zero;
            for (int i = 0; i < Segments; i++)
            {
                float ang = i / (float)Segments * MathF.PI * 2f;
                float cos = MathF.Cos(ang), sin = MathF.Sin(ang);
                Vector3 previous = default;
                float previousSide = 0;
                Vector3 surfacePoint = default;
                bool clipped = false;
                for (int k = 0; k < ProfileSamples; k++)
                {
                    var p = new Vector3(_profile[k].X * cos, _profile[k].Y, _profile[k].X * sin);
                    float side = Vector3.Dot(n, p - planePoint);
                    if (!clipped && side > 0)
                    {
                        clipped = true;
                        surfacePoint = k == 0 ? p : Vector3.Lerp(previous, p, previousSide / (previousSide - side));
                    }

                    _verts[i * ProfileSamples + k] = clipped ? surfacePoint : p;
                    _normals[i * ProfileSamples + k] = Vector3.Normalize(new Vector3(_profileNormal[k].X * cos, _profileNormal[k].Y, _profileNormal[k].X * sin));
                    previous = p;
                    previousSide = side;
                }

                if (!clipped) surfacePoint = previous; // full to the brim on this side
                _verts[ring + i] = surfacePoint;
                _normals[ring + i] = n;
                centre += surfacePoint;
            }

            _verts[ring + Segments] = centre / Segments;
            _normals[ring + Segments] = n;
            Gfx.UpdateDynamic(Mesh, _verts, _normals);
        }
    }

    /// <summary>Per-item render state that persists between frames (liquid meshes, smoothed colours, solid pieces).</summary>
    internal sealed class VesselState
    {
        public readonly LiquidSurface Liquid = new LiquidSurface();
        public readonly LiquidSurface Pile = new LiquidSurface();
        public Color LiquidColor;
        public bool HasColor;
        public readonly Vector4[] Pieces; // unit-space seeds: x = radius fraction, y = height fraction, z = angle, w = random

        public VesselState(int seed)
        {
            var rng = new Random(seed * 7919 + 17);
            Pieces = new Vector4[80];
            for (int i = 0; i < Pieces.Length; i++)
            {
                Pieces[i] = new Vector4(MathF.Sqrt((float)rng.NextDouble()), (float)rng.NextDouble(), (float)(rng.NextDouble() * Math.PI * 2), (float)rng.NextDouble());
            }
        }

        private static readonly Dictionary<IDLab.Core.Model.EquipmentInstance, VesselState> States = new Dictionary<IDLab.Core.Model.EquipmentInstance, VesselState>();

        public static VesselState For(Item item)
        {
            if (!States.TryGetValue(item.Inst, out var s))
            {
                s = new VesselState(item.Inst.Id);
                States[item.Inst] = s;
            }

            return s;
        }

        public static void Forget(IDLab.Core.Model.EquipmentInstance inst) => States.Remove(inst);

        /// <summary>How many pieces to draw per mL of a solid, and how big they are.</summary>
        public static (float perMl, float size) PieceStyle(SolidForm form)
        {
            switch (form)
            {
                case SolidForm.Ribbon: return (5f, 0.016f);
                case SolidForm.Granules: return (16f, 0.0026f);
                case SolidForm.Turnings: return (18f, 0.008f);
                case SolidForm.Chunks: return (3.5f, 0.0065f);
                case SolidForm.Crystals: return (6f, 0.0022f);
                default: return (0f, 0f); // powders/precipitates are drawn as a pile surface
            }
        }
    }
}

namespace IDLab.Desktop
{
    /// <summary>Gas bubbles rising through a liquid (reaction gas, boiling), simulated in the vessel's frame.</summary>
    internal static class Bubbles
    {
        private struct Bubble
        {
            public Vector3 Position;
            public float Size, Speed, Phase;
        }

        private static readonly Dictionary<IDLab.Core.Model.EquipmentInstance, List<Bubble>> All = new Dictionary<IDLab.Core.Model.EquipmentInstance, List<Bubble>>();
        private static readonly Dictionary<IDLab.Core.Model.EquipmentInstance, float> Accumulators = new Dictionary<IDLab.Core.Model.EquipmentInstance, float>();
        private static readonly Random Rng = new Random(5);

        public static void Update(Item item, float dt)
        {
            var c = item.Container;
            if (c == null) return;
            if (!All.TryGetValue(item.Inst, out var list))
            {
                list = new List<Bubble>();
                All[item.Inst] = list;
            }

            var shape = VesselShape.For(item);
            float level = c.Contents.HasLiquid ? shape.HeightForVolume(c.Contents.VolumeLiters) : shape.Floor;
            float rate = (float)Math.Min(90, c.GasRateMolPerS * 2500);
            if (c.IsBoiling) rate += 45;
            else if (c.TemperatureC > 75) rate += (float)(c.TemperatureC - 75) * 0.6f;
            if (!c.Contents.HasLiquid) rate = 0;

            Accumulators.TryGetValue(item.Inst, out float acc);
            acc += rate * dt;
            while (acc >= 1 && list.Count < 160)
            {
                acc -= 1;
                float y = shape.Floor + 0.002f + (float)Rng.NextDouble() * Math.Max(0.001f, (level - shape.Floor) * 0.25f);
                float r = shape.RadiusAt(y) * 0.8f * MathF.Sqrt((float)Rng.NextDouble());
                float a = (float)(Rng.NextDouble() * Math.PI * 2);
                float size = c.IsBoiling ? 0.0012f + (float)Rng.NextDouble() * 0.0025f : 0.0006f + (float)Rng.NextDouble() * 0.0012f;
                list.Add(new Bubble { Position = new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r), Size = size, Speed = 0.04f + size * 18, Phase = a });
            }

            Accumulators[item.Inst] = acc;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var b = list[i];
                b.Position.Y += b.Speed * dt;
                b.Position.X += MathF.Sin(b.Phase + b.Position.Y * 400) * 0.0004f;
                if (b.Position.Y >= level - b.Size)
                {
                    list.RemoveAt(i);
                    continue;
                }

                list[i] = b;
            }
        }

        public static void Draw(Item item, VesselShape shape, float level)
        {
            if (!All.TryGetValue(item.Inst, out var list) || list.Count == 0) return;
            var pose = Props.VesselPose(item);
            foreach (var b in list)
            {
                Gfx.Draw(Gfx.Sphere, Gfx.Mul(Gfx.Mul(Gfx.Scale(b.Size), Gfx.Translate(b.Position)), pose), new Color(255, 255, 255, 120));
            }
        }

        public static void Forget(IDLab.Core.Model.EquipmentInstance inst)
        {
            All.Remove(inst);
            Accumulators.Remove(inst);
            VesselState.Forget(inst);
        }
    }
}
