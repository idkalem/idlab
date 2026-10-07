using System.Collections.Generic;
using UnityEngine;

namespace IDLab.App.Rendering
{
    /// <summary>Procedural meshes: glassware and burner parts are surfaces of revolution ("lathe").</summary>
    public static class MeshFactory
    {
        /// <summary>
        /// Revolves a 2D profile (x = radius, y = height) around the Y axis. Consecutive profile points form
        /// rings; set <paramref name="hardEdges"/> to duplicate rings so each profile segment gets flat normals.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments = 40, bool hardEdges = true)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int p = 0; p < profile.Count - 1; p++)
            {
                Vector2 a = profile[p];
                Vector2 b = profile[p + 1];
                Vector2 tangent = (b - a).normalized;
                Vector2 n2 = new Vector2(tangent.y, -tangent.x); // outward when profile runs bottom→top on the outside
                int baseIndex = verts.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s / (float)segments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                    verts.Add(new Vector3(a.x * cos, a.y, a.x * sin));
                    verts.Add(new Vector3(b.x * cos, b.y, b.x * sin));
                    Vector3 n = new Vector3(n2.x * cos, n2.y, n2.x * sin).normalized;
                    normals.Add(n);
                    normals.Add(n);
                    uvs.Add(new Vector2(s / (float)segments, p / (float)(profile.Count - 1)));
                    uvs.Add(new Vector2(s / (float)segments, (p + 1) / (float)(profile.Count - 1)));
                }

                for (int s = 0; s < segments; s++)
                {
                    int i0 = baseIndex + s * 2;
                    int i1 = i0 + 1;
                    int i2 = i0 + 2;
                    int i3 = i0 + 3;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i2); tris.Add(i1); tris.Add(i3);
                }
            }

            var mesh = new Mesh { name = "Lathe" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            if (!hardEdges)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Cylinder(float radius, float height, int segments = 32)
        {
            return Lathe(new[]
            {
                new Vector2(0, 0), new Vector2(radius, 0), new Vector2(radius, height), new Vector2(0, height),
            }, segments);
        }

        public static Mesh Cone(float radius, float height, int segments = 24)
        {
            return Lathe(new[] { new Vector2(0, 0), new Vector2(radius, 0), new Vector2(0, height) }, segments, false);
        }

        /// <summary>Open glass vessel with wall thickness: outside up, rim, inside down, inner floor.</summary>
        public static Mesh Vessel(float outerRadius, float height, float wall, float neckRadius = -1f, float shoulderHeight = -1f)
        {
            var p = new List<Vector2> { new Vector2(0, 0), new Vector2(outerRadius, 0) };
            if (neckRadius > 0 && shoulderHeight > 0)
            {
                p.Add(new Vector2(outerRadius, shoulderHeight));
                p.Add(new Vector2(neckRadius, shoulderHeight + (outerRadius - neckRadius) * 0.8f));
                p.Add(new Vector2(neckRadius, height));
                p.Add(new Vector2(neckRadius - wall, height));
                p.Add(new Vector2(neckRadius - wall, shoulderHeight + (outerRadius - neckRadius) * 0.8f));
                p.Add(new Vector2(outerRadius - wall, shoulderHeight));
            }
            else
            {
                p.Add(new Vector2(outerRadius, height));
                p.Add(new Vector2(outerRadius - wall, height));
            }

            p.Add(new Vector2(outerRadius - wall, wall));
            p.Add(new Vector2(0, wall));
            return Lathe(p, 48);
        }

        /// <summary>Torus-like flat ring (selection / snap markers).</summary>
        public static Mesh Ring(float innerRadius, float outerRadius, float thickness = 0.002f, int segments = 48)
        {
            return Lathe(new[]
            {
                new Vector2(innerRadius, 0), new Vector2(outerRadius, 0),
                new Vector2(outerRadius, thickness), new Vector2(innerRadius, thickness), new Vector2(innerRadius, 0),
            }, segments);
        }
    }

    /// <summary>
    /// Liquid body inside a cylindrical vessel whose free surface stays horizontal in world space as the
    /// vessel tilts. The surface plane passes through the axis at height h = V / (πr²) (volume conserved
    /// while the surface does not reach the floor or rim).
    /// </summary>
    public sealed class LiquidMeshBuilder
    {
        private readonly int _segments;
        private readonly Vector3[] _verts;
        private readonly Vector3[] _normals;
        private readonly int[] _tris;
        private bool _trianglesAssigned;

        public Mesh Mesh { get; }

        public LiquidMeshBuilder(int segments = 32)
        {
            _segments = segments;
            // bottom: centre + ring, wall: 2 rings, top: centre + ring
            int vertexCount = (1 + segments) + (2 * segments) + (1 + segments);
            _verts = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            _tris = new int[segments * 3 * 2 + segments * 6];
            Mesh = new Mesh { name = "Liquid" };
            Mesh.MarkDynamic();
            BuildTriangles();
        }

        /// <param name="upLocal">World up expressed in the vessel's local space.</param>
        public void Update(float radius, float height, float maxHeight, float floor, Vector3 upLocal)
        {
            upLocal = upLocal.normalized;
            float uy = Mathf.Max(0.05f, upLocal.y);
            int n = _segments;
            int bottomCentre = 0, bottomRing = 1, wallLow = 1 + n, wallHigh = 1 + 2 * n, topCentre = 1 + 3 * n, topRing = 2 + 3 * n;

            _verts[bottomCentre] = new Vector3(0, floor, 0);
            _normals[bottomCentre] = Vector3.down;
            _verts[topCentre] = new Vector3(0, floor + height, 0);
            _normals[topCentre] = upLocal;

            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * radius, z = Mathf.Sin(a) * radius;
                float y = floor + Mathf.Clamp(height - (upLocal.x * x + upLocal.z * z) / uy, 0f, maxHeight);
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));

                _verts[bottomRing + i] = new Vector3(x, floor, z);
                _normals[bottomRing + i] = Vector3.down;
                _verts[wallLow + i] = new Vector3(x, floor, z);
                _normals[wallLow + i] = radial;
                _verts[wallHigh + i] = new Vector3(x, y, z);
                _normals[wallHigh + i] = radial;
                _verts[topRing + i] = new Vector3(x, y, z);
                _normals[topRing + i] = upLocal;
            }

            Mesh.vertices = _verts;
            Mesh.normals = _normals;
            if (!_trianglesAssigned)
            {
                Mesh.triangles = _tris;
                _trianglesAssigned = true;
            }

            Mesh.RecalculateBounds();
        }

        private void BuildTriangles()
        {
            int n = _segments;
            int bottomRing = 1, wallLow = 1 + n, wallHigh = 1 + 2 * n, topCentre = 1 + 3 * n, topRing = 2 + 3 * n;
            int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                // bottom (facing down)
                _tris[t++] = 0; _tris[t++] = bottomRing + i; _tris[t++] = bottomRing + j;
                // top (facing up)
                _tris[t++] = topCentre; _tris[t++] = topRing + j; _tris[t++] = topRing + i;
                // wall (facing out)
                _tris[t++] = wallLow + i; _tris[t++] = wallHigh + i; _tris[t++] = wallLow + j;
                _tris[t++] = wallLow + j; _tris[t++] = wallHigh + i; _tris[t++] = wallHigh + j;
            }
        }
    }
}
