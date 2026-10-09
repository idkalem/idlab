using System;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    internal enum QualityMode { Auto, High, Balanced, Fast }

    /// <summary>
    /// Keeps the lab smooth on smart boards (large 4K panels driven by modest integrated graphics). The 3D scene can be
    /// drawn into an off-screen target at a fraction of the screen resolution and stretched up, while the UI stays at
    /// full resolution and sharp. In Auto mode the fraction follows the measured frame time: it drops when frames
    /// take too long and creeps back up when there is headroom.
    /// </summary>
    internal static class Quality
    {
        // FXAA 3.11-style edge smoothing for the off-screen scene (the window has no MSAA outside High mode).
        private const string FxaaFragment = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
uniform sampler2D texture0;
uniform vec2 texel;
out vec4 finalColor;
float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }
void main()
{
    vec2 uv = fragTexCoord;
    vec3 rgbM = texture(texture0, uv).rgb;
    vec3 rgbNW = texture(texture0, uv + vec2(-1.0, -1.0) * texel).rgb;
    vec3 rgbNE = texture(texture0, uv + vec2(1.0, -1.0) * texel).rgb;
    vec3 rgbSW = texture(texture0, uv + vec2(-1.0, 1.0) * texel).rgb;
    vec3 rgbSE = texture(texture0, uv + vec2(1.0, 1.0) * texel).rgb;
    float lM = luma(rgbM), lNW = luma(rgbNW), lNE = luma(rgbNE), lSW = luma(rgbSW), lSE = luma(rgbSE);
    float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
    float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
    if (lMax - lMin < max(0.0312, lMax * 0.125)) { finalColor = vec4(rgbM, 1.0) * fragColor; return; }
    vec2 dir = vec2(-((lNW + lNE) - (lSW + lSE)), (lNW + lSW) - (lNE + lSE));
    float reduce = max((lNW + lNE + lSW + lSE) * 0.03125, 0.0078125);
    float rcp = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
    dir = clamp(dir * rcp, vec2(-8.0), vec2(8.0)) * texel;
    vec3 a = 0.5 * (texture(texture0, uv + dir * (1.0 / 3.0 - 0.5)).rgb + texture(texture0, uv + dir * (2.0 / 3.0 - 0.5)).rgb);
    vec3 b = a * 0.5 + 0.25 * (texture(texture0, uv - dir * 0.5).rgb + texture(texture0, uv + dir * 0.5).rgb);
    float lB = luma(b);
    finalColor = vec4((lB < lMin || lB > lMax) ? a : b, 1.0) * fragColor;
}";

        private static Shader _fxaa;
        private static bool _fxaaLoaded;
        private static int _texelLoc;

        /// <summary>The window is created with 4× MSAA only in High mode (it multiplies the cost of every pixel).</summary>
        public static bool WantsMsaa => Settings.Get("grafik", "otomatik") == "yuksek";

        private static RenderTexture2D _target;
        private static int _targetW, _targetH;
        private static float _autoScale = 1f, _avgFrame = 1f / 60f, _calm, _sinceDrop = 99f;

        public static QualityMode Mode { get; private set; } = QualityMode.Auto;

        private static bool _msaaNotice;

        /// <summary>True once after switching to High while the window has no MSAA (it takes effect after a restart).</summary>
        public static bool TakeMsaaNotice()
        {
            bool n = _msaaNotice;
            _msaaNotice = false;
            return n;
        }

        /// <summary>Fraction of the screen resolution the 3D scene is rendered at (1 = directly, with MSAA).</summary>
        public static float RenderScale { get; private set; } = 1f;

        /// <summary>Lower-cost effects (fewer particles, slower micro-view refresh) in the cheaper modes.</summary>
        public static bool Economy => RenderScale < 0.7f;

        /// <summary>Compact top-bar label.</summary>
        public static string ShortLabel =>
            Mode == QualityMode.High ? "Kalite: Yüksek" : Mode == QualityMode.Balanced ? "Kalite: Orta" : Mode == QualityMode.Fast ? "Kalite: Hızlı" : "Kalite: Oto";

        public static string Label
        {
            get
            {
                switch (Mode)
                {
                    case QualityMode.High: return "Grafik: Yüksek";
                    case QualityMode.Balanced: return "Grafik: Dengeli";
                    case QualityMode.Fast: return "Grafik: Hızlı";
                    default: return "Grafik: Otomatik";
                }
            }
        }

        public static void Load()
        {
            switch (Settings.Get("grafik", "otomatik"))
            {
                case "yuksek": Mode = QualityMode.High; break;
                case "dengeli": Mode = QualityMode.Balanced; break;
                case "hizli": Mode = QualityMode.Fast; break;
                default: Mode = QualityMode.Auto; break;
            }

            _autoScale = MaxAutoScale();
        }

        public static void Cycle()
        {
            Mode = (QualityMode)(((int)Mode + 1) % 4);
            if (Mode == QualityMode.High && !IsWindowState(ConfigFlags.Msaa4xHint)) _msaaNotice = true;
            Settings.Set("grafik", Mode == QualityMode.High ? "yuksek" : Mode == QualityMode.Balanced ? "dengeli" : Mode == QualityMode.Fast ? "hizli" : "otomatik");
            _autoScale = MaxAutoScale();
            _sinceDrop = 0;
        }

        /// <summary>Auto renders the 3D scene at up to 1440 lines (sharp on a 4K board, still ~2× cheaper than native) and drops lower if frames run late.</summary>
        private static float MaxAutoScale() => Math.Min(1f, 1440f / Math.Max(1, GetScreenHeight()));

        /// <summary>Updates the render scale from this frame's duration (seconds).</summary>
        public static void Update(float frameSeconds)
        {
            float h = Math.Max(1, GetScreenHeight());
            switch (Mode)
            {
                // Without MSAA (window opened in another mode), High still goes through the FXAA target at full size.
                case QualityMode.High: RenderScale = IsWindowState(ConfigFlags.Msaa4xHint) ? 1f : 0.97f; return;
                case QualityMode.Balanced: RenderScale = Math.Min(1f, 900f / h); return;
                case QualityMode.Fast: RenderScale = Math.Min(1f, 600f / h); return;
            }

            _avgFrame += (Math.Min(frameSeconds, 0.2f) - _avgFrame) * 0.05f;
            _sinceDrop += frameSeconds;
            if (_avgFrame > 0.0215f && _sinceDrop > 1.0f)
            {
                // Missing 60 fps (and possibly falling to 30 with vsync): render fewer pixels.
                _autoScale = Math.Max(0.45f, _autoScale * 0.85f);
                _sinceDrop = 0;
                _calm = 0;
            }
            else if (_avgFrame < 0.0178f)
            {
                _calm += frameSeconds;
                if (_calm > 4f && _sinceDrop > 8f && _autoScale < MaxAutoScale())
                {
                    _autoScale = Math.Min(MaxAutoScale(), _autoScale * 1.06f);
                    _calm = 0;
                }
            }
            else
            {
                _calm = 0;
            }

            RenderScale = _autoScale;
        }

        /// <summary>
        /// Starts drawing the 3D scene. Returns true when it goes to the off-screen target (call
        /// <see cref="EndScene"/> after EndMode3D, before BeginDrawing).
        /// </summary>
        public static bool BeginScene(Color clear)
        {
            if (RenderScale >= 0.98f && IsWindowState(ConfigFlags.Msaa4xHint)) return false;
            int w = Math.Max(64, (int)(GetScreenWidth() * RenderScale)), h = Math.Max(64, (int)(GetScreenHeight() * RenderScale));
            if (w != _targetW || h != _targetH)
            {
                if (_targetW > 0) UnloadRenderTexture(_target);
                _target = LoadRenderTexture(w, h);
                SetTextureFilter(_target.Texture, TextureFilter.Bilinear);
                _targetW = w;
                _targetH = h;
            }

            BeginTextureMode(_target);
            ClearBackground(clear);
            return true;
        }

        public static void EndScene() => EndTextureMode();

        /// <summary>Stretches the off-screen scene over the window (inside BeginDrawing).</summary>
        public static void Present()
        {
            if (!_fxaaLoaded)
            {
                _fxaaLoaded = true;
                _fxaa = LoadShaderFromMemory(null, FxaaFragment);
                _texelLoc = GetShaderLocation(_fxaa, "texel");
            }

            SetShaderValue(_fxaa, _texelLoc, new System.Numerics.Vector2(1f / _targetW, 1f / _targetH), ShaderUniformDataType.Vec2);
            BeginShaderMode(_fxaa);
            DrawTexturePro(_target.Texture, new Rectangle(0, 0, _targetW, -_targetH), new Rectangle(0, 0, GetScreenWidth(), GetScreenHeight()),
                System.Numerics.Vector2.Zero, 0, Color.White);
            EndShaderMode();
        }
    }
}
