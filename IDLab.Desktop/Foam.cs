using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Elephant-toothpaste foam as one continuous, soft body instead of loose balls: the foam fills the vessel from the
    /// liquid surface, is pushed out of the mouth as a column (speed = volume of oxygen per second ÷ mouth area), bends
    /// over under its own weight, drapes down the glass and coils into a heap on the bench. Physically it is a chain of
    /// nodes (Verlet rope with stretch, bending, ground, vessel, bench-item and self collisions); visually a lumpy tube
    /// with a bubbly foam texture. Old foam slowly collapses and disappears.
    /// </summary>
    internal sealed class Foam
    {
        private const int MaxNodes = 420, Sides = 22, CapRings = 4, MaxRopes = 8;
        private const int Rings = MaxNodes + 2 * CapRings, Columns = Sides + 1;

        private sealed class Node
        {
            public Vector3 Pos, Prev;
            public float R, R0, Age, SeedA, SeedB, Axial;
            public bool Inside; // still inside the vessel: moves along its axis with the column
        }

        private sealed class Rope
        {
            public readonly List<Node> Nodes = new List<Node>(); // [0] = tip (oldest) ... [^1] = newest, at the liquid surface
            public Item Source;
            public Vector3 Anchor;
            public float Wobble, LastEmit = -999, Speed, Spacing, MouthR, Still, Born, Fade = 1f, RebuildTimer;
            public bool Frozen, Retired; // retired: full, keeps its place while a new rope continues the column
            public Color Color = new Color(250, 250, 246, 255);
            public Mesh Mesh;
            public bool HasMesh;
        }

        private readonly List<Rope> _ropes = new List<Rope>();
        private readonly Stack<Mesh> _pool = new Stack<Mesh>();
        private readonly Random _rng = new Random(11);
        private float _time;

        private static ushort[] _indices;
        private static Texture2D _texture;
        private static bool _textureReady;
        private readonly Vector3[] _v = new Vector3[Rings * Columns];
        private readonly Vector3[] _n = new Vector3[Rings * Columns];
        private readonly Vector2[] _uv = new Vector2[Rings * Columns];
        private readonly Vector3[] _centre = new Vector3[Rings];
        private readonly Vector3[] _tangent = new Vector3[Rings];
        private readonly float[] _radius = new float[Rings];
        private readonly float[] _seedA = new float[Rings], _seedB = new float[Rings];

        public int NodeCount => _ropes.Sum(r => r.Nodes.Count);

        public void Clear()
        {
            foreach (var r in _ropes) Release(r);
            _ropes.Clear();
        }

        /// <summary>Foam is being produced in <paramref name="item"/> this frame, rising at <paramref name="speed"/> m/s through the mouth.</summary>
        public void Extrude(Item item, float speed, Color color)
        {
            var rope = _ropes.FirstOrDefault(r => r.Source == item && !r.Retired);
            if (rope != null && rope.Nodes.Count >= MaxNodes - 2)
            {
                rope.Retired = true;
                rope = null;
            }

            if (rope == null)
            {
                if (_ropes.Count >= MaxRopes)
                {
                    var oldest = _ropes.OrderBy(r => r.Born).First();
                    Release(oldest);
                    _ropes.Remove(oldest);
                }

                float mouthR = (float)item.Container.MouthRadiusM;
                rope = new Rope { Source = item, Anchor = item.Position, MouthR = mouthR, Spacing = mouthR * 0.75f, Born = _time, Color = color };
                _ropes.Add(rope);
            }

            rope.LastEmit = _time;
            rope.Speed = speed;
            rope.Color = Gfx.Lerp(rope.Color, color, 0.05f);
            rope.Frozen = false;
            rope.Still = 0;
        }

        private void Release(Rope r)
        {
            if (r.HasMesh) _pool.Push(r.Mesh);
            r.HasMesh = false;
        }

        // ------------------------------------------------------------------ simulation

        public void Update(float dt, Lab lab)
        {
            if (dt <= 0) return;
            _time += dt;
            dt = Math.Min(dt, 1f / 20f);

            for (int i = _ropes.Count - 1; i >= 0; i--)
            {
                var rope = _ropes[i];
                // Old foam collapses: after ~1.5 min without new foam it shrinks away.
                if (_time - rope.LastEmit > 100f) rope.Fade -= dt / 8f;
                if (rope.Fade <= 0 || rope.Nodes.Count == 0 && _time - rope.LastEmit > 1f)
                {
                    Release(rope);
                    _ropes.RemoveAt(i);
                    continue;
                }

                if (rope.Source != null && (!lab.Items.Contains(rope.Source) || Vector3.Distance(rope.Source.Position, rope.Anchor) > 0.015f ||
                                            rope.Source.Container == null))
                {
                    // The vessel was moved or removed: the foam it held falls out with the rest.
                    rope.Source = null;
                    foreach (var n in rope.Nodes) n.Inside = false;
                    rope.Frozen = false;
                }
            }

            var boxes = lab.Items.Where(it => it.Host == null || it.Container == null).Select(it => (it, box: lab.WorldBox(it))).ToList();
            int steps = Math.Clamp((int)MathF.Ceiling(dt / 0.006f), 1, 6);
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                foreach (var rope in _ropes)
                {
                    if (!rope.Frozen) Step(rope, h, s == 0 ? dt : 0, boxes);
                }

                Collide();
            }

            foreach (var rope in _ropes)
            {
                if (rope.Frozen) continue;
                bool emitting = !rope.Retired && _time - rope.LastEmit < 0.2f;
                float maxV = 0;
                foreach (var n in rope.Nodes)
                {
                    if (!n.Inside) maxV = Math.Max(maxV, (n.Pos - n.Prev).Length() / h);
                }

                rope.Still = !emitting && maxV < 0.004f ? rope.Still + dt : 0;
                if (rope.Still > 1.2f) rope.Frozen = true; // settled: stop simulating, keep drawing
            }
        }

        private void Step(Rope rope, float h, float frameDt, List<(Item it, BoundingBox box)> boxes)
        {
            var nodes = rope.Nodes;
            bool emitting = !rope.Retired && _time - rope.LastEmit < 0.2f;
            VesselShape shape = null;
            Vector3 basePos = default, up = Vector3.UnitY;
            float rim = 0, start = 0;
            var source = rope.Source;
            if (source != null)
            {
                var c = source.Container;
                shape = VesselShape.For(source);
                basePos = source.Inst.Pose.TransformPoint(c.LocalOffset);
                up = Vector3.Transform(Vector3.UnitY, source.Rotation);
                rim = shape.RimY;
                start = Math.Max(shape.Floor + 0.002f, shape.HeightForVolume(c.Contents.VolumeLiters) - 0.003f);
            }

            // Push the column: the part inside the vessel slides up the axis; new foam forms at the liquid surface.
            if (source != null && emitting)
            {
                float adv = rope.Speed * h;
                foreach (var n in nodes)
                {
                    if (n.Inside) n.Axial += adv;
                }

                while (nodes.Count == 0 || nodes[nodes.Count - 1].Axial - start >= rope.Spacing && nodes.Count < MaxNodes)
                {
                    float axial = nodes.Count == 0 ? start : nodes[nodes.Count - 1].Axial - rope.Spacing;
                    float r0 = rope.MouthR * (1.12f + 0.16f * (float)_rng.NextDouble());
                    var p = basePos + up * axial;
                    nodes.Add(new Node
                    {
                        Pos = p, Prev = p, Axial = axial, Inside = true, R = shape.RadiusAt(axial) * 0.96f, R0 = r0,
                        SeedA = (float)(_rng.NextDouble() * 100), SeedB = (float)(_rng.NextDouble() * 100),
                    });
                    if (nodes.Count >= MaxNodes) break;
                }
            }

            var g = new Vector3(0, -9.81f * 0.55f, 0) * h * h;
            float drag = 1f - Math.Min(0.5f, 2.2f * h);
            foreach (var n in nodes)
            {
                n.Age += h;
                if (n.Inside)
                {
                    var target = basePos + up * n.Axial;
                    n.Prev = n.Pos;
                    n.Pos = target;
                    n.R = Math.Max(0.004f, shape.RadiusAt(Math.Min(n.Axial, rim)) * 0.96f);
                    if (n.Axial > rim + rope.Spacing * 0.3f && emitting)
                    {
                        // Leaves the mouth: free from now on, carrying the column's speed.
                        n.Inside = false;
                        // The column never comes out perfectly straight: a slowly wandering sideways push makes it lean,
                        // buckle and topple like real foam.
                        rope.Wobble += (float)(_rng.NextDouble() - 0.5) * 1.6f;
                        var side = Vector3.Normalize(Vector3.Cross(up, new Vector3(MathF.Cos(rope.Wobble), 0.01f, MathF.Sin(rope.Wobble))) + new Vector3(0.0001f, 0, 0));
                        n.Prev = n.Pos - (up * rope.Speed + side * (0.02f + 0.25f * rope.Speed)) * h;
                        n.R = Math.Min(n.R, rope.MouthR);
                    }

                    continue;
                }

                // Expands a little after leaving the neck; collapses slowly as the bubbles burst.
                float collapse = n.Age < 30 ? 1f : Math.Max(0.6f, 1f - (n.Age - 30) / 150f);
                n.R += (n.R0 * collapse - n.R) * Math.Min(1f, h * 2.5f);
                var v = (n.Pos - n.Prev) * drag;
                v += new Vector3((float)_rng.NextDouble() - 0.5f, 0, (float)_rng.NextDouble() - 0.5f) * 2e-6f; // breaks perfect symmetry
                n.Prev = n.Pos;
                n.Pos += v + g;
            }

            float d = rope.Spacing;
            for (int it = 0; it < 6; it++)
            {
                for (int i = 0; i + 1 < nodes.Count; i++)
                {
                    Satisfy(nodes[i], nodes[i + 1], d, 1f, false);
                }

                for (int i = 0; i + 2 < nodes.Count; i++)
                {
                    // Bending resistance: fresh foam is stiff enough to stand as a column, old foam is soggy.
                    float stiff = nodes[i + 1].Inside ? 0.3f : nodes[i + 1].Age < 0.6f ? 0.18f : 0.03f;
                    Satisfy(nodes[i], nodes[i + 2], d * 1.75f, stiff, true);
                }

                foreach (var n in nodes)
                {
                    if (n.Inside) continue;
                    float floor = n.R * 0.56f;
                    if (n.Pos.Y < floor) n.Pos.Y = floor;
                    if (source != null) PushOutOfVessel(n, shape, basePos, up, rim);
                    foreach (var (item, box) in boxes)
                    {
                        if (item == source) continue;
                        PushOutOfBox(n, box);
                    }
                }
            }

            // Friction: foam resting on a surface sticks rather than sliding.
            foreach (var n in nodes)
            {
                if (n.Inside) continue;
                if (n.Pos.Y <= n.R * 0.56f + 0.0005f)
                {
                    n.Prev.X += (n.Pos.X - n.Prev.X) * 0.6f;
                    n.Prev.Z += (n.Pos.Z - n.Prev.Z) * 0.6f;
                }
            }
        }

        private static void Satisfy(Node a, Node b, float rest, float stiffness, bool minOnly)
        {
            float wa = a.Inside ? 0 : 1, wb = b.Inside ? 0 : 1;
            if (wa + wb == 0) return;
            var delta = b.Pos - a.Pos;
            float len = delta.Length();
            if (len < 1e-7f) return;
            if (minOnly && len >= rest) return;
            var corr = delta * ((len - rest) / len * stiffness / (wa + wb));
            a.Pos += corr * wa;
            b.Pos -= corr * wb;
        }

        private static void PushOutOfVessel(Node n, VesselShape shape, Vector3 basePos, Vector3 up, float rim)
        {
            var rel = n.Pos - basePos;
            float y = Vector3.Dot(rel, up);
            if (y < -n.R || y > rim + n.R * 0.4f) return;
            var radial = rel - up * y;
            float dist = radial.Length();
            float mouth = shape.RadiusAt(rim);
            if (dist < mouth && y > rim - n.R * 1.2f)
            {
                // Over the opening: sits on the column coming out.
                n.Pos += up * (rim + n.R * 0.4f - y);
                return;
            }

            float wall = shape.RadiusAt(Math.Clamp(y, shape.Floor, rim)) + shape.Wall + n.R * 0.7f;
            if (dist >= wall) return;
            var dir = dist > 1e-5f ? radial / dist : Vector3.Normalize(new Vector3(MathF.Cos(n.SeedA), 0, MathF.Sin(n.SeedA)));
            n.Pos += dir * (wall - dist);
        }

        private static void PushOutOfBox(Node n, BoundingBox box)
        {
            float pad = n.R * 0.6f;
            var p = n.Pos;
            if (p.X < box.Min.X - pad || p.X > box.Max.X + pad || p.Y < box.Min.Y - pad || p.Y > box.Max.Y + pad ||
                p.Z < box.Min.Z - pad || p.Z > box.Max.Z + pad) return;
            // Smallest way out (mostly: onto the top of a hot plate / balance pan).
            float up = box.Max.Y + pad - p.Y;
            float[] outs = { up, p.X - (box.Min.X - pad), box.Max.X + pad - p.X, p.Z - (box.Min.Z - pad), box.Max.Z + pad - p.Z };
            int best = 0;
            for (int i = 1; i < outs.Length; i++)
            {
                if (outs[i] < outs[best]) best = i;
            }

            switch (best)
            {
                case 0: n.Pos.Y += up; break;
                case 1: n.Pos.X -= outs[1]; break;
                case 2: n.Pos.X += outs[2]; break;
                case 3: n.Pos.Z -= outs[3]; break;
                default: n.Pos.Z += outs[4]; break;
            }
        }

        // Self collision so the foam piles up into a heap instead of passing through itself.
        private readonly Dictionary<long, List<(Rope rope, int index)>> _grid = new Dictionary<long, List<(Rope, int)>>();
        private readonly Stack<List<(Rope, int)>> _cellPool = new Stack<List<(Rope, int)>>();

        private void Collide()
        {
            foreach (var cell in _grid.Values)
            {
                cell.Clear();
                _cellPool.Push(cell);
            }

            _grid.Clear();
            const float cellSize = 0.03f;
            foreach (var rope in _ropes)
            {
                for (int i = 0; i < rope.Nodes.Count; i++)
                {
                    var n = rope.Nodes[i];
                    if (n.Inside) continue;
                    long key = Key((int)MathF.Floor(n.Pos.X / cellSize), (int)MathF.Floor(n.Pos.Y / cellSize), (int)MathF.Floor(n.Pos.Z / cellSize));
                    if (!_grid.TryGetValue(key, out var list))
                    {
                        list = _cellPool.Count > 0 ? _cellPool.Pop() : new List<(Rope, int)>();
                        _grid[key] = list;
                    }

                    list.Add((rope, i));
                }
            }

            foreach (var rope in _ropes)
            {
                if (rope.Frozen) continue;
                for (int i = 0; i < rope.Nodes.Count; i++)
                {
                    var a = rope.Nodes[i];
                    if (a.Inside) continue;
                    int cx = (int)MathF.Floor(a.Pos.X / cellSize), cy = (int)MathF.Floor(a.Pos.Y / cellSize), cz = (int)MathF.Floor(a.Pos.Z / cellSize);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!_grid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out var list)) continue;
                        foreach (var (other, j) in list)
                        {
                            if (other == rope && Math.Abs(i - j) <= 3) continue;
                            if (other == rope && j < i) continue; // each pair once
                            var b = other.Nodes[j];
                            float min = (a.R + b.R) * 0.66f; // overlapping a little: the coils merge into one heap
                            var delta = b.Pos - a.Pos;
                            float len2 = delta.LengthSquared();
                            if (len2 >= min * min || len2 < 1e-12f) continue;
                            float len = MathF.Sqrt(len2);
                            float wb = other.Frozen ? 0 : 1;
                            var corr = delta * ((min - len) / len / (1 + wb) * 0.8f);
                            a.Pos -= corr;
                            b.Pos += corr * wb;
                        }
                    }
                }
            }
        }

        private static long Key(int x, int y, int z) => ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);

        // ------------------------------------------------------------------ drawing

        public void Draw()
        {
            if (_ropes.Count == 0) return;
            EnsureTexture();
            float prevEnv = Gfx.Env;
            Gfx.Env = 0.05f;
            foreach (var rope in _ropes)
            {
                if (rope.Nodes.Count < 2) continue;
                if (!rope.HasMesh)
                {
                    rope.Mesh = _pool.Count > 0 ? _pool.Pop() : CreateMesh();
                    rope.HasMesh = true;
                    rope.RebuildTimer = 0;
                }

                // Settled foam only changes slowly (collapse): rebuild it a few times a second.
                rope.RebuildTimer -= GetFrameTime();
                if (!rope.Frozen || rope.RebuildTimer <= 0)
                {
                    Build(rope);
                    rope.RebuildTimer = 0.4f;
                }

                var c = rope.Color;
                var glow = new Vector3(c.R, c.G, c.B) / 255f * 0.10f;
                Gfx.Draw(rope.Mesh, Matrix4x4.Identity, c, glow, _texture, 0.22f);
            }

            Gfx.Env = prevEnv;
        }

        private Mesh CreateMesh()
        {
            if (_indices == null)
            {
                var idx = new List<ushort>();
                for (int i = 0; i + 1 < Rings; i++)
                for (int j = 0; j < Sides; j++)
                {
                    int a = i * Columns + j, b = a + 1, c = a + Columns, e = c + 1;
                    idx.Add((ushort)a); idx.Add((ushort)b); idx.Add((ushort)c);
                    idx.Add((ushort)b); idx.Add((ushort)e); idx.Add((ushort)c);
                }

                _indices = idx.ToArray();
            }

            return Gfx.DynamicMesh(Rings * Columns, _indices);
        }

        private unsafe void Build(Rope rope)
        {
            var nodes = rope.Nodes;
            int n = nodes.Count;
            float fade = Math.Clamp(rope.Fade, 0, 1);

            // Ring centres along the chain plus a rounded cap at both ends.
            int rings = 0;
            Vector3 TangentAt(int i)
            {
                var a = nodes[Math.Max(0, i - 1)].Pos;
                var b = nodes[Math.Min(n - 1, i + 1)].Pos;
                var t = b - a;
                return t.LengthSquared() > 1e-12f ? Vector3.Normalize(t) : Vector3.UnitY;
            }

            var t0 = TangentAt(0);
            for (int k = CapRings; k >= 1; k--)
            {
                float ang = k / (float)CapRings * MathF.PI / 2;
                AddRing(ref rings, nodes[0].Pos - t0 * nodes[0].R * fade * MathF.Sin(ang) * 0.9f, t0, nodes[0].R * fade * MathF.Cos(ang), nodes[0]);
            }

            for (int i = 0; i < n; i++) AddRing(ref rings, nodes[i].Pos, TangentAt(i), nodes[i].R * fade, nodes[i]);
            var tn = TangentAt(n - 1);
            for (int k = 1; k <= CapRings; k++)
            {
                float ang = k / (float)CapRings * MathF.PI / 2;
                AddRing(ref rings, nodes[n - 1].Pos + tn * nodes[n - 1].R * fade * MathF.Sin(ang) * 0.9f, tn, nodes[n - 1].R * fade * MathF.Cos(ang), nodes[n - 1]);
            }

            // Parallel-transported frames → lumpy rings of vertices.
            var normal = Vector3.Cross(_tangent[0], MathF.Abs(_tangent[0].Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
            normal = Vector3.Normalize(normal);
            float along = 0;
            for (int i = 0; i < rings; i++)
            {
                var t = _tangent[i];
                normal -= t * Vector3.Dot(normal, t);
                if (normal.LengthSquared() < 1e-8f) normal = Vector3.Cross(t, MathF.Abs(t.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
                normal = Vector3.Normalize(normal);
                var bin = Vector3.Cross(t, normal);
                if (i > 0) along += Vector3.Distance(_centre[i], _centre[i - 1]);
                for (int j = 0; j <= Sides; j++)
                {
                    float th = j / (float)Sides * MathF.PI * 2;
                    float lump = 1f + 0.10f * MathF.Sin(3 * th + _seedA[i]) + 0.06f * MathF.Sin(5 * th + _seedB[i]) + 0.035f * MathF.Sin(8 * th + _seedA[i] * 1.7f);
                    var radial = normal * MathF.Cos(th) + bin * MathF.Sin(th);
                    int v = i * Columns + j;
                    var off = radial * (_radius[i] * lump);
                    // Foam lying on the bench sags under its own weight.
                    if (_centre[i].Y < _radius[i] * 1.3f && off.Y < 0) off.Y *= 0.72f;
                    _v[v] = _centre[i] + off;
                    _n[v] = radial;
                    _uv[v] = new Vector2(j / (float)Sides * 3f, along / 0.045f);
                }
            }

            // Unused capacity collapses onto the last centre (degenerate triangles).
            for (int v = rings * Columns; v < Rings * Columns; v++)
            {
                _v[v] = _centre[rings - 1];
                _n[v] = Vector3.UnitY;
                _uv[v] = Vector2.Zero;
            }

            // Shading normals from the actual lumpy surface.
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < Sides; j++)
                {
                    int v = i * Columns + j;
                    var around = _v[i * Columns + (j + 1) % Sides] - _v[i * Columns + (j + Sides - 1) % Sides];
                    var lengthwise = _v[Math.Min(rings - 1, i + 1) * Columns + j] - _v[Math.Max(0, i - 1) * Columns + j];
                    var nn = Vector3.Cross(around, lengthwise);
                    if (nn.LengthSquared() > 1e-14f)
                    {
                        nn = Vector3.Normalize(nn);
                        if (Vector3.Dot(nn, _n[v]) < 0) nn = -nn;
                        _n[v] = Vector3.Normalize(nn * 0.55f + _n[v] * 0.45f);
                    }
                }

                _n[i * Columns + Sides] = _n[i * Columns];
            }

            fixed (Vector3* p = _v) UpdateMeshBuffer(rope.Mesh, 0, p, _v.Length * sizeof(Vector3), 0);
            fixed (Vector2* p = _uv) UpdateMeshBuffer(rope.Mesh, 1, p, _uv.Length * sizeof(Vector2), 0);
            fixed (Vector3* p = _n) UpdateMeshBuffer(rope.Mesh, 2, p, _n.Length * sizeof(Vector3), 0);
        }

        private void AddRing(ref int rings, Vector3 centre, Vector3 tangent, float radius, Node node)
        {
            if (rings >= Rings) return;
            _centre[rings] = centre;
            _tangent[rings] = tangent;
            _radius[rings] = Math.Max(0, radius);
            _seedA[rings] = node.SeedA + rings * 0.37f;
            _seedB[rings] = node.SeedB;
            rings++;
        }

        // ------------------------------------------------------------------ texture

        /// <summary>Tileable foam: packed polyhedral bubbles (two sizes) with thin dark films and tiny highlights.</summary>
        public static Texture2D Texture
        {
            get
            {
                EnsureTexture();
                return _texture;
            }
        }

        private static unsafe void EnsureTexture()
        {
            if (_textureReady) return;
            _textureReady = true;
            const int size = 512;
            var img = GenImageColor(size, size, Color.White);
            ImageFormat(ref img, PixelFormat.UncompressedR8G8B8A8);
            var data = (byte*)img.Data;
            var rng = new Random(5);
            var big = Cells(16, rng);
            var small = Cells(44, rng);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var (b1, b2, bc) = Worley(big, 16, x / (float)size, y / (float)size);
                var (s1, s2, _) = Worley(small, 44, x / (float)size, y / (float)size);
                float cellB = 1f / 16, cellS = 1f / 44;
                float film = Smooth((b2 - b1) / cellB, 0.0f, 0.09f);
                float filmS = Smooth((s2 - s1) / cellS, 0.0f, 0.12f);
                float dome = 1f - 0.10f * MathF.Min(1, b1 / cellB);
                float shade = 0.955f * dome;
                shade *= 0.80f + 0.20f * film;     // films between the big bubbles
                shade *= 0.93f + 0.07f * filmS;    // finer bubbles everywhere
                // A small window highlight in each big bubble.
                var hl = new Vector2(x / (float)size, y / (float)size) - (bc + new Vector2(-0.22f, -0.22f) * cellB);
                hl.X -= MathF.Round(hl.X);
                hl.Y -= MathF.Round(hl.Y);
                float spec = 1f - Smooth(hl.Length() / cellB, 0.05f, 0.13f);
                float value = Math.Clamp(shade + spec * 0.12f, 0, 1);
                byte g = (byte)(value * 255);
                int o = (y * size + x) * 4;
                data[o] = g;
                data[o + 1] = g;
                data[o + 2] = (byte)Math.Min(255, g + 3);
                data[o + 3] = 255;
            }

            _texture = LoadTextureFromImage(img);
            UnloadImage(img);
            GenTextureMipmaps(ref _texture);
            SetTextureFilter(_texture, TextureFilter.Trilinear);
            SetTextureWrap(_texture, TextureWrap.Repeat);
        }

        private static float Smooth(float x, float a, float b)
        {
            float t = Math.Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }

        private static Vector2[] Cells(int n, Random rng)
        {
            var pts = new Vector2[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                pts[y * n + x] = new Vector2((x + 0.15f + 0.7f * (float)rng.NextDouble()) / n, (y + 0.15f + 0.7f * (float)rng.NextDouble()) / n);
            }

            return pts;
        }

        /// <summary>Distances to the nearest and second-nearest feature point on a tiling grid, and the nearest point.</summary>
        private static (float f1, float f2, Vector2 nearest) Worley(Vector2[] pts, int n, float u, float v)
        {
            int cx = (int)(u * n), cy = (int)(v * n);
            float f1 = 9, f2 = 9;
            var best = Vector2.Zero;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy;
                int wx = (gx % n + n) % n, wy = (gy % n + n) % n;
                var p = pts[wy * n + wx] + new Vector2((gx - wx) / (float)n, (gy - wy) / (float)n);
                float d = Vector2.Distance(p, new Vector2(u, v));
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                    best = p;
                }
                else if (d < f2)
                {
                    f2 = d;
                }
            }

            return (f1, f2, best);
        }
    }
}
