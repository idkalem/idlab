using System;
using System.IO;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// The ID Lab identity: the logo (purple–pink tile with the "ID" and a test tube), the start-up splash screen, and
    /// the "Hakkında" (about) card.
    /// </summary>
    internal static class Brand
    {
        public const string Version = "1.0";

        // Logo colours (deep violet → pink, orange liquid).
        public static readonly Color Violet = Gfx.Rgba(0x6D2EE0);
        public static readonly Color DeepViolet = Gfx.Rgba(0x2C1580);
        public static readonly Color Pink = Gfx.Rgba(0xEE8ADB);
        public static readonly Color Orange = Gfx.Rgba(0xFFA51F);

        private static Texture2D _logo;
        private static bool _loaded;

        public static Texture2D Logo
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    using var stream = typeof(Brand).Assembly.GetManifestResourceStream("icon.png");
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    var image = LoadImageFromMemory(".png", ms.ToArray());
                    _logo = LoadTextureFromImage(image);
                    UnloadImage(image);
                    GenTextureMipmaps(ref _logo);
                    SetTextureFilter(_logo, TextureFilter.Trilinear);
                }

                return _logo;
            }
        }

        public static void DrawLogo(Rectangle r, float alpha = 1f)
        {
            var t = Logo;
            DrawTexturePro(t, new Rectangle(0, 0, t.Width, t.Height), r, Vector2.Zero, 0, Gfx.WithAlpha(Color.White, alpha));
        }

        /// <summary>A diagonal violet → pink gradient like the logo tile.</summary>
        public static void DrawGradient(Rectangle r, float alpha = 1f)
        {
            DrawRectangleGradientEx(r, Gfx.WithAlpha(Gfx.Rgba(0x7B4FE6), alpha), Gfx.WithAlpha(Gfx.Rgba(0x5F2BD9), alpha),
                Gfx.WithAlpha(Pink, alpha), Gfx.WithAlpha(Gfx.Rgba(0x8E44E6), alpha));
        }

        /// <summary>One frame of the start-up screen: logo, name and a progress bar only (fonts must be loaded).</summary>
        public static void Splash(float progress, string step)
        {
            BeginDrawing();
            float w = GetScreenWidth(), h = GetScreenHeight(), s = Math.Clamp(h / 1080f, 0.55f, 3f);
            DrawGradient(new Rectangle(0, 0, w, h));
            // Soft light blobs for depth.
            DrawCircleGradient((int)(w * 0.22f), (int)(h * 0.2f), h * 0.45f, Gfx.WithAlpha(Color.White, 0.18f), Gfx.WithAlpha(Color.White, 0f));
            DrawCircleGradient((int)(w * 0.85f), (int)(h * 0.9f), h * 0.5f, Gfx.WithAlpha(Pink, 0.35f), Gfx.WithAlpha(Pink, 0f));
            float size = 300 * s;
            var logo = new Rectangle((w - size) / 2, h * 0.42f - size / 2, size, size);
            for (int i = 0; i < 6; i++) DrawRectangleRounded(new Rectangle(logo.X + (4 + i * 3) * s, logo.Y + (8 + i * 4) * s, size, size), 0.32f, 16, Gfx.WithAlpha(DeepViolet, 0.05f));
            DrawLogo(logo);
            string title = "ID Lab";
            var tm = Ui.Measure(title, 64, true);
            Ui.Text(title, (w - tm.X) / 2, logo.Y + size + 30 * s, 64, Color.White, true);
            float bw = Math.Min(520 * s, w * 0.6f), by = h * 0.86f;
            DrawRectangleRounded(new Rectangle((w - bw) / 2, by, bw, 10 * s), 1f, 8, Gfx.WithAlpha(Color.White, 0.25f));
            DrawRectangleRounded(new Rectangle((w - bw) / 2, by, Math.Max(10 * s, bw * Math.Clamp(progress, 0, 1)), 10 * s), 1f, 8, Orange);
            EndDrawing();
        }

        /// <summary>The about card (modal). Returns false when it should close.</summary>
        public static bool DrawAbout(int chemicals, int experiments)
        {
            float s = Ui.S, w = GetScreenWidth(), h = GetScreenHeight();
            DrawRectangle(0, 0, (int)w, (int)h, new Color(10, 6, 24, 170));
            var card = new Rectangle((w - 760 * s) / 2, (h - 640 * s) / 2, 760 * s, 640 * s);
            Ui.Panel(card, Theme.Panel);
            var head = new Rectangle(card.X, card.Y, card.Width, 170 * s);
            BeginScissorMode((int)head.X, (int)head.Y, (int)head.Width, (int)head.Height);
            DrawRectangleRounded(new Rectangle(head.X, head.Y, head.Width, head.Height + 40 * s), 0.12f, 10, Violet);
            DrawGradient(new Rectangle(head.X, head.Y + 14 * s, head.Width, head.Height));
            EndScissorMode();
            DrawLogo(new Rectangle(card.X + 34 * s, card.Y + 25 * s, 120 * s, 120 * s));
            Ui.Text("ID Lab " + Version, card.X + 180 * s, card.Y + 42 * s, 40, Color.White, true);
            Ui.Text("3B Fen Laboratuvarı · akıllı tahtalar için", card.X + 182 * s, card.Y + 100 * s, 19, Gfx.WithAlpha(Color.White, 0.9f));

            float y = card.Y + 196 * s, x = card.X + 36 * s, tw = card.Width - 72 * s;
            y += Ui.Wrapped($"{chemicals:N0} kimyasal · 118 element · {experiments} hazır deney · gerçek zamanlı tepkime, ısı, pH ve dökme simülasyonu.",
                x, y, tw, 17, Theme.Text, true, 1.35f) + 14 * s;
            y += Ui.Wrapped("Kullanım: malzemeyi soldan tezgâha sürükleyin; seçmek için dokunun; dökmek için kabı seçip \"Dök\" düğmelerine basın ya da " +
                            "şişeyi başka bir kabın üstüne bırakın. Boş alanı sürükleyerek açıyı çevirin. Klavye: Q/E eğ, Sil kaldır, F11 tam ekran.",
                x, y, tw, 16, Theme.Muted, false, 1.4f) + 14 * s;
            y += Ui.Wrapped("Veriler: CRC Handbook of Chemistry and Physics ve PubChem tabloları (chemicals paketi), GHS piktogramları (BM UNECE), " +
                            "DejaVu yazı tipleri. Simülasyon öğretim amaçlıdır; gerçek laboratuvarda öğretmeninizin talimatlarına uyun.",
                x, y, tw, 15, Theme.Muted, false, 1.4f);

            var btn = new Rectangle(card.Right() - 200 * s, card.Bottom() - 74 * s, 170 * s, 50 * s);
            bool close = Ui.Button("about-close", btn, "Kapat", Theme.AccentDim, 17);
            // Tapping outside the card closes it too.
            if (Ui.Released && !CheckCollisionPointRec(Ui.Mouse, card) && !CheckCollisionPointRec(Ui.PressPosition, card)) close = true;
            Ui.Block(new Rectangle(0, 0, w, h));
            return !close;
        }
    }
}
