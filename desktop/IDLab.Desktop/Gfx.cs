using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Rendering primitives: one lit shader (key + fill + rim light, glass Fresnel), procedural meshes
    /// (lathe = surface of revolution for glassware and burner parts) and transform helpers.
    /// Transforms use raylib's convention: Mul(first, then).
    /// </summary>
    internal static class Gfx
    {
        private const string VertexShader = @"#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
uniform mat4 mvp;
uniform mat4 matModel;
uniform mat4 matNormal;
out vec3 fragPosition;
out vec2 fragTexCoord;
out vec3 fragNormal;
void main() {
    fragTexCoord = vertexTexCoord;
    fragPosition = vec3(matModel * vec4(vertexPosition, 1.0));
    fragNormal = normalize(vec3(matNormal * vec4(vertexNormal, 0.0)));
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}";

        private const string FragmentShader = @"#version 330
in vec3 fragPosition;
in vec2 fragTexCoord;
in vec3 fragNormal;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 viewPos;
uniform vec3 ambient;
uniform vec3 emission;
uniform float specular;
uniform vec3 fogColor;
uniform vec2 fogRange;
out vec4 finalColor;
void main() {
    vec3 n = normalize(fragNormal);
    if (!gl_FrontFacing) n = -n;
    vec3 v = normalize(viewPos - fragPosition);
    vec3 l = normalize(vec3(-0.45, 0.85, -0.35));
    float diff = max(dot(n, l), 0.0);
    float fill = max(dot(n, normalize(vec3(0.7, 0.3, 0.6))), 0.0) * 0.22;
    vec3 h = normalize(l + v);
    float spec = pow(max(dot(n, h), 0.0), 56.0) * 0.55;
    float rim = pow(1.0 - max(dot(n, v), 0.0), 3.0);
    vec4 tex = texture(texture0, fragTexCoord);
    vec3 base = colDiffuse.rgb * tex.rgb;
    vec3 c = base * (ambient + diff * 0.8 + fill) + vec3(spec) * specular + rim * vec3(0.25, 0.6, 0.9) * 0.25 + emission;
    float a = colDiffuse.a * tex.a;
    if (a < 0.99) a = clamp(a + rim * 0.55 + spec * 0.6, 0.0, 1.0);
    float dist = length(viewPos - fragPosition);
    float fog = clamp((dist - fogRange.x) / fogRange.y, 0.0, 1.0);
    c = mix(c, fogColor, fog);
    finalColor = vec4(c, a);
}";

        private static Shader _shader;
        private static Material _material;
        private static int _locViewPos, _locAmbient, _locEmission, _locSpecular, _locFog, _locFogRange;
        private static Texture2D _white;
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static Mesh Cube, Sphere, Cylinder, Cone, Disc;
        public static Vector3 Ambient = new Vector3(0.32f, 0.35f, 0.42f);
        public static Vector3 FogColor = new Vector3(0.62f, 0.66f, 0.70f);

        /// <summary>Fog start distance and length in metres (the lab room uses 4 + 8 m; other views push it away).</summary>
        public static Vector2 FogRange = new Vector2(4f, 8f);

        public static void Init()
        {
            _shader = LoadShaderFromMemory(VertexShader, FragmentShader);
            _locViewPos = GetShaderLocation(_shader, "viewPos");
            _locAmbient = GetShaderLocation(_shader, "ambient");
            _locEmission = GetShaderLocation(_shader, "emission");
            _locSpecular = GetShaderLocation(_shader, "specular");
            _locFog = GetShaderLocation(_shader, "fogColor");
            _locFogRange = GetShaderLocation(_shader, "fogRange");
            _material = LoadMaterialDefault();
            _material.Shader = _shader;
            unsafe { _white = _material.Maps[(int)MaterialMapIndex.Albedo].Texture; }

            Cube = GenMeshCube(1, 1, 1);
            Sphere = GenMeshSphere(1, 12, 18);
            Cylinder = Lathe(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, 32);
            Cone = Lathe(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) }, 32, smooth: true);
            Disc = Lathe(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0.001f), new Vector2(0, 0.001f) }, 40);
        }

        public static void BeginFrame(Vector3 cameraPosition)
        {
            SetShaderValue(_shader, _locViewPos, cameraPosition, ShaderUniformDataType.Vec3);
            SetShaderValue(_shader, _locAmbient, Ambient, ShaderUniformDataType.Vec3);
            SetShaderValue(_shader, _locFog, FogColor, ShaderUniformDataType.Vec3);
            SetShaderValue(_shader, _locFogRange, FogRange, ShaderUniformDataType.Vec2);
        }

        public static void Draw(Mesh mesh, Matrix4x4 transform, Color color, Vector3 emission = default) =>
            Draw(mesh, transform, color, emission, _white, 1f);

        public static void DrawTextured(Mesh mesh, Matrix4x4 transform, Texture2D texture, Color tint, Vector3 emission = default, float specular = 0.4f) =>
            Draw(mesh, transform, tint, emission, texture, specular);

        public static unsafe void Draw(Mesh mesh, Matrix4x4 transform, Color color, Vector3 emission, Texture2D texture, float specular)
        {
            _material.Maps[(int)MaterialMapIndex.Albedo].Color = color;
            _material.Maps[(int)MaterialMapIndex.Albedo].Texture = texture;
            SetShaderValue(_shader, _locEmission, emission, ShaderUniformDataType.Vec3);
            SetShaderValue(_shader, _locSpecular, specular, ShaderUniformDataType.Float);
            DrawMesh(mesh, _material, transform);
            _material.Maps[(int)MaterialMapIndex.Albedo].Texture = _white;
        }

        /// <summary>Flat quad in the XZ plane (normal +Y), centred, with repeated UVs for tiled textures.</summary>
        public static Mesh Quad(float width, float depth, float uRepeat, float vRepeat, bool flipV = false)
        {
            var mesh = new Mesh(4, 2);
            mesh.AllocVertices();
            mesh.AllocNormals();
            mesh.AllocTexCoords();
            mesh.AllocIndices();
            var v = mesh.VerticesAs<Vector3>();
            var n = mesh.NormalsAs<Vector3>();
            var uv = mesh.TexCoordsAs<Vector2>();
            var idx = mesh.IndicesAs<ushort>();
            float hw = width / 2, hd = depth / 2;
            float vNear = flipV ? 0 : vRepeat, vFar = flipV ? vRepeat : 0;
            v[0] = new Vector3(-hw, 0, -hd); uv[0] = new Vector2(0, vNear);
            v[1] = new Vector3(hw, 0, -hd); uv[1] = new Vector2(uRepeat, vNear);
            v[2] = new Vector3(hw, 0, hd); uv[2] = new Vector2(uRepeat, vFar);
            v[3] = new Vector3(-hw, 0, hd); uv[3] = new Vector2(0, vFar);
            for (int i = 0; i < 4; i++) n[i] = Vector3.UnitY;
            idx[0] = 0; idx[1] = 2; idx[2] = 1; idx[3] = 0; idx[4] = 3; idx[5] = 2;
            UploadMesh(ref mesh, false);
            return mesh;
        }

        /// <summary>A mesh with a fixed topology whose vertices/normals are rewritten every frame (liquids).</summary>
        public static Mesh DynamicMesh(int vertexCount, ReadOnlySpan<ushort> indices)
        {
            var mesh = new Mesh(vertexCount, indices.Length / 3);
            mesh.AllocVertices();
            mesh.AllocNormals();
            mesh.AllocTexCoords();
            mesh.AllocIndices();
            indices.CopyTo(mesh.IndicesAs<ushort>());
            UploadMesh(ref mesh, true);
            return mesh;
        }

        public static unsafe void UpdateDynamic(Mesh mesh, Vector3[] vertices, Vector3[] normals)
        {
            fixed (Vector3* p = vertices) UpdateMeshBuffer(mesh, 0, p, vertices.Length * sizeof(Vector3), 0);
            fixed (Vector3* p = normals) UpdateMeshBuffer(mesh, 2, p, normals.Length * sizeof(Vector3), 0);
        }

        // ------------------------------------------------------------------ transforms

        public static Matrix4x4 Mul(Matrix4x4 first, Matrix4x4 then) => Raymath.MatrixMultiply(first, then);
        public static Matrix4x4 Scale(float x, float y, float z) => Raymath.MatrixScale(x, y, z);
        public static Matrix4x4 Scale(float s) => Raymath.MatrixScale(s, s, s);
        public static Matrix4x4 Translate(Vector3 p) => Raymath.MatrixTranslate(p.X, p.Y, p.Z);
        public static Matrix4x4 Translate(float x, float y, float z) => Raymath.MatrixTranslate(x, y, z);
        public static Matrix4x4 Rotate(Quaternion q) => Raymath.QuaternionToMatrix(q);
        public static Matrix4x4 Pose(Vector3 position, Quaternion rotation) => Mul(Rotate(rotation), Translate(position));

        /// <summary>Part placed in an object's local frame, then the object's pose.</summary>
        public static Matrix4x4 Part(Matrix4x4 local, Matrix4x4 pose) => Mul(local, pose);

        // ------------------------------------------------------------------ meshes

        /// <summary>Open glass vessel with wall thickness (outside, rim, inside, inner floor).</summary>
        public static Mesh Vessel(float outerRadius, float height, float wall, float neckRadius = 0, float shoulder = 0)
        {
            string key = $"vessel{outerRadius}{height}{wall}{neckRadius}{shoulder}";
            if (Cache.TryGetValue(key, out var m))
            {
                return m;
            }

            var p = new List<Vector2> { new Vector2(0, 0), new Vector2(outerRadius, 0) };
            if (neckRadius > 0)
            {
                float neckStart = shoulder + (outerRadius - neckRadius) * 0.8f;
                p.Add(new Vector2(outerRadius, shoulder));
                p.Add(new Vector2(neckRadius, neckStart));
                p.Add(new Vector2(neckRadius, height));
                p.Add(new Vector2(neckRadius - wall, height));
                p.Add(new Vector2(neckRadius - wall, neckStart));
                p.Add(new Vector2(outerRadius - wall, shoulder));
            }
            else
            {
                p.Add(new Vector2(outerRadius, height));
                p.Add(new Vector2(outerRadius - wall, height));
            }

            p.Add(new Vector2(outerRadius - wall, wall));
            p.Add(new Vector2(0, wall));
            m = Lathe(p.ToArray(), 48);
            Cache[key] = m;
            return m;
        }

        public static Mesh CachedLathe(string key, Vector2[] profile, int segments = 32)
        {
            if (!Cache.TryGetValue(key, out var m))
            {
                m = Lathe(profile, segments);
                Cache[key] = m;
            }

            return m;
        }

        /// <summary>Revolves a profile (x = radius, y = height) about +Y. Outward normals for bottom→top outside profiles.</summary>
        public static Mesh Lathe(Vector2[] profile, int segments, bool smooth = false)
        {
            int ringVerts = (segments + 1) * 2;
            int vertexCount = (profile.Length - 1) * ringVerts;
            int triangleCount = (profile.Length - 1) * segments * 2;
            var mesh = new Mesh(vertexCount, triangleCount);
            mesh.AllocVertices();
            mesh.AllocNormals();
            mesh.AllocTexCoords();
            mesh.AllocIndices();
            var verts = mesh.VerticesAs<Vector3>();
            var normals = mesh.NormalsAs<Vector3>();
            var uvs = mesh.TexCoordsAs<Vector2>();
            var indices = mesh.IndicesAs<ushort>();

            int v = 0, t = 0;
            for (int p = 0; p < profile.Length - 1; p++)
            {
                Vector2 a = profile[p];
                Vector2 b = profile[p + 1];
                Vector2 tangent = Vector2.Normalize(b - a);
                var n2 = new Vector2(tangent.Y, -tangent.X);
                int baseIndex = v;
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s / (float)segments * MathF.PI * 2f;
                    float cos = MathF.Cos(ang), sin = MathF.Sin(ang);
                    verts[v] = new Vector3(a.X * cos, a.Y, a.X * sin);
                    verts[v + 1] = new Vector3(b.X * cos, b.Y, b.X * sin);
                    Vector3 n = Vector3.Normalize(new Vector3(n2.X * cos, n2.Y, n2.X * sin));
                    if (smooth && b.X == 0 && a.X > 0)
                    {
                        n = Vector3.Normalize(new Vector3(n2.X * cos, n2.Y * 0.6f, n2.X * sin));
                    }

                    normals[v] = n;
                    normals[v + 1] = n;
                    uvs[v] = new Vector2(s / (float)segments, 0);
                    uvs[v + 1] = new Vector2(s / (float)segments, 1);
                    v += 2;
                }

                for (int s = 0; s < segments; s++)
                {
                    int i0 = baseIndex + s * 2;
                    indices[t++] = (ushort)i0;
                    indices[t++] = (ushort)(i0 + 1);
                    indices[t++] = (ushort)(i0 + 2);
                    indices[t++] = (ushort)(i0 + 2);
                    indices[t++] = (ushort)(i0 + 1);
                    indices[t++] = (ushort)(i0 + 3);
                }
            }

            UploadMesh(ref mesh, false);
            return mesh;
        }

        public static Color Rgba(uint rgb, byte a = 255) => new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

        public static Color WithAlpha(Color c, float a) => new Color(c.R, c.G, c.B, (byte)Math.Clamp(a * 255f, 0f, 255f));

        public static Color FromCore(IDLab.Core.ColorRGBA c) =>
            new Color((byte)(Math.Clamp(c.R, 0f, 1f) * 255), (byte)(Math.Clamp(c.G, 0f, 1f) * 255), (byte)(Math.Clamp(c.B, 0f, 1f) * 255), (byte)(Math.Clamp(c.A, 0f, 1f) * 255));

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Color((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));
        }
    }
}
