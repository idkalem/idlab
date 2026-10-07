using IDLab.App.Rendering;
using IDLab.App.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace IDLab.App
{
    /// <summary>
    /// Builds the dark, modern lab room procedurally: black epoxy bench with a 5 cm grid (gives scale),
    /// back wall with cyan accent strips, soft key light with tuned shadows for centimetre-scale objects.
    /// The bench top is the world plane y = 0, so core coordinates read naturally.
    /// </summary>
    public static class LabEnvironment
    {
        public const float BenchWidth = 1.8f;
        public const float BenchDepth = 0.9f;
        public const float BenchHeight = 0.9f;

        public static void Build(Transform parent)
        {
            var root = new GameObject("Environment").transform;
            root.SetParent(parent, false);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.26f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.14f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = UITheme.SceneBackground;
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 9f;
            QualitySettings.shadowDistance = 4f;
            QualitySettings.shadowCascades = 2;

            // Bench top: black epoxy resin with a faint grid.
            var benchMat = new Material(MaterialFactory.Opaque(Color.white, 0.55f))
            {
                mainTexture = MaterialFactory.GridTexture(new Color(0.07f, 0.08f, 0.1f), new Color(0.13f, 0.16f, 0.2f), 128, 4, 2),
            };
            benchMat.mainTextureScale = new Vector2(BenchWidth / 0.2f, BenchDepth / 0.2f); // 4 cells per 20 cm = 5 cm grid
            Block(root, "BenchTop", new Vector3(BenchWidth, 0.04f, BenchDepth), new Vector3(0, -0.02f, 0), benchMat);
            var trim = MaterialFactory.Emissive(new Color(0.05f, 0.2f, 0.3f), new Color(0.1f, 0.6f, 0.9f) * 0.6f);
            Block(root, "BenchEdgeGlow", new Vector3(BenchWidth, 0.004f, 0.004f), new Vector3(0, -0.002f, -BenchDepth / 2 - 0.002f), trim, false);

            var cabinet = MaterialFactory.Opaque(new Color(0.11f, 0.12f, 0.15f), 0.3f);
            Block(root, "Cabinet", new Vector3(BenchWidth - 0.06f, BenchHeight - 0.04f, BenchDepth - 0.08f),
                new Vector3(0, -0.04f - (BenchHeight - 0.04f) / 2f, 0.02f), cabinet);
            Block(root, "Floor", new Vector3(12f, 0.02f, 12f), new Vector3(0, -BenchHeight - 0.01f, 0),
                MaterialFactory.Opaque(new Color(0.06f, 0.065f, 0.08f), 0.25f));

            // Back wall with shelf and light strips.
            var wall = MaterialFactory.Opaque(new Color(0.09f, 0.1f, 0.13f), 0.2f);
            Block(root, "BackWall", new Vector3(6f, 3f, 0.05f), new Vector3(0, 0.6f, BenchDepth / 2 + 0.35f), wall);
            Block(root, "Shelf", new Vector3(BenchWidth, 0.02f, 0.18f), new Vector3(0, 0.45f, BenchDepth / 2 + 0.24f), cabinet);
            var strip = MaterialFactory.Emissive(new Color(0.05f, 0.15f, 0.2f), new Color(0.15f, 0.75f, 1f));
            Block(root, "LightStripLow", new Vector3(BenchWidth, 0.008f, 0.01f), new Vector3(0, 0.25f, BenchDepth / 2 + 0.32f), strip, false);
            Block(root, "LightStripHigh", new Vector3(BenchWidth, 0.008f, 0.01f), new Vector3(0, 0.75f, BenchDepth / 2 + 0.32f), strip, false);

            // Lights
            var key = Object.FindFirstObjectByType<Light>();
            if (key == null || key.type != LightType.Directional)
            {
                key = new GameObject("Key Light").AddComponent<Light>();
                key.type = LightType.Directional;
            }

            key.transform.SetParent(root, true);
            key.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            key.color = new Color(1f, 0.97f, 0.92f);
            key.intensity = 1.05f;
            key.shadows = LightShadows.Soft;
            key.shadowBias = 0.01f;
            key.shadowNormalBias = 0.15f;
            key.shadowStrength = 0.85f;

            AddPointLight(root, "Fill", new Vector3(-0.8f, 0.8f, -0.6f), new Color(0.55f, 0.7f, 1f), 0.6f, 3f);
            AddPointLight(root, "Rim", new Vector3(0.7f, 0.6f, 0.7f), new Color(0.3f, 0.85f, 1f), 0.8f, 2.5f);
        }

        private static GameObject Block(Transform parent, string name, Vector3 size, Vector3 centre, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
            {
                Object.Destroy(go.GetComponent<Collider>());
            }

            return go;
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
