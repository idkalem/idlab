using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IDLab.App.Rendering
{
    /// <summary>
    /// Creates materials at runtime for both the Built-in pipeline and URP, so the prototype needs no
    /// authored material assets. (For player builds the shaders used here are added to "Always Included
    /// Shaders" by Editor/IDLabBuildSetup.cs.)
    /// </summary>
    public static class MaterialFactory
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Texture2D _softParticle;

        public static bool IsUrp => GraphicsSettings.currentRenderPipeline != null;

        public static Material Opaque(Color color, float smoothness = 0.4f, float metallic = 0f)
        {
            string key = $"opaque{color}{smoothness}{metallic}";
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var m = new Material(LitShader()) { name = "IDLab Opaque" };
            SetColor(m, color);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        /// <summary>Unshared transparent lit material (glass, liquids). Caller may mutate its colour.</summary>
        public static Material Transparent(Color color, float smoothness = 0.9f, int queueOffset = 0)
        {
            var m = new Material(LitShader()) { name = "IDLab Transparent" };
            if (IsUrp)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                m.SetFloat("_Mode", 3); // Standard shader "Transparent"
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            m.renderQueue = (int)RenderQueue.Transparent + queueOffset;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Smoothness", smoothness);
            SetColor(m, color);
            return m;
        }

        public static Material Emissive(Color baseColor, Color emission)
        {
            string key = $"emissive{baseColor}{emission}";
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var m = new Material(LitShader()) { name = "IDLab Emissive" };
            SetColor(m, baseColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Cache[key] = m;
            return m;
        }

        /// <summary>Unshared additive, unlit material for flames and glows.</summary>
        public static Material Additive(Color color)
        {
            Material m;
            if (IsUrp)
            {
                m = new Material(Find("Universal Render Pipeline/Particles/Unlit", "Sprites/Default"));
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 2); // additive
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.One);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent + 10;
            }
            else
            {
                m = new Material(Find("Legacy Shaders/Particles/Additive", "Sprites/Default"));
                m.SetTexture("_MainTex", Texture2D.whiteTexture);
            }

            m.name = "IDLab Additive";
            SetColor(m, color);
            m.SetColor("_TintColor", color);
            return m;
        }

        /// <summary>Alpha-blended particle material with a soft round sprite (smoke, steam, gas).</summary>
        public static Material Particle(Color tint)
        {
            string key = $"particle{tint}";
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var shader = IsUrp
                ? Find("Universal Render Pipeline/Particles/Unlit", "Sprites/Default")
                : Find("Legacy Shaders/Particles/Alpha Blended", "Sprites/Default");
            var m = new Material(shader) { name = "IDLab Particle" };
            m.mainTexture = SoftParticleTexture;
            m.SetTexture("_BaseMap", SoftParticleTexture);
            if (IsUrp)
            {
                m.SetFloat("_Surface", 1);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }

            SetColor(m, tint);
            m.SetColor("_TintColor", tint);
            Cache[key] = m;
            return m;
        }

        /// <summary>Vertex-coloured unlit material for line renderers (pour streams, wires).</summary>
        public static Material Line()
        {
            if (!Cache.TryGetValue("line", out var m))
            {
                m = new Material(Find("Sprites/Default", "Unlit/Color")) { name = "IDLab Line" };
                Cache["line"] = m;
            }

            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", c);
            }

            if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", c);
            }
        }

        public static Texture2D SoftParticleTexture
        {
            get
            {
                if (_softParticle == null)
                {
                    const int size = 64;
                    _softParticle = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "IDLab Soft Particle", wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x + 0.5f) / size * 2 - 1;
                            float dy = (y + 0.5f) / size * 2 - 1;
                            float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                            _softParticle.SetPixel(x, y, new Color(1, 1, 1, a * a));
                        }
                    }

                    _softParticle.Apply();
                }

                return _softParticle;
            }
        }

        /// <summary>Procedural square grid texture (bench top 5 cm grid, wire gauze).</summary>
        public static Texture2D GridTexture(Color background, Color line, int size = 128, int cells = 4, int lineWidth = 1)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "IDLab Grid", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            int cell = size / cells;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isLine = x % cell < lineWidth || y % cell < lineWidth;
                    tex.SetPixel(x, y, isLine ? line : background);
                }
            }

            tex.Apply();
            return tex;
        }

        public static Shader LitShader() => IsUrp
            ? Find("Universal Render Pipeline/Lit", "Standard")
            : Find("Standard", "Diffuse");

        private static Shader Find(string name, string fallback)
        {
            var s = Shader.Find(name);
            if (s == null)
            {
                s = Shader.Find(fallback);
            }

            if (s == null)
            {
                Debug.LogWarning($"[IDLab] Shader '{name}' not found; add it to Always Included Shaders.");
                s = Shader.Find("Hidden/InternalErrorShader");
            }

            return s;
        }
    }
}
