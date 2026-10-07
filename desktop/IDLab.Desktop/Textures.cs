using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Every texture is generated at start-up (no image files): lab surfaces, wall posters (a real periodic table,
    /// whiteboard, safety rules), safety signs, GHS hazard pictograms and printed reagent-bottle labels.
    /// </summary>
    internal static class Textures
    {
        public static Texture2D Floor, WallTiles, Epoxy, Poster, Whiteboard, Rules, EyeWash, Extinguisher, Exit, Window, Steel;
        private static readonly Dictionary<Hazard, Texture2D> PictogramTextures = new Dictionary<Hazard, Texture2D>();
        private static readonly Dictionary<string, Texture2D> Labels = new Dictionary<string, Texture2D>();
        private static readonly Random Rng = new Random(11);

        public static readonly Hazard[] AllHazards =
        {
            Hazard.Corrosive, Hazard.Toxic, Hazard.Flammable, Hazard.Oxidizer, Hazard.Irritant, Hazard.Environment, Hazard.WaterReactive,
        };

        public static void Init()
        {
            Floor = Upload(Tiles(512, 4, new Color(206, 209, 213, 255), new Color(170, 174, 180, 255), 3, 10), true);
            WallTiles = Upload(Tiles(256, 4, new Color(240, 242, 244, 255), new Color(196, 200, 206, 255), 2, 3), true);
            Epoxy = Upload(Speckle(256, new Color(48, 53, 61, 255), 14), true);
            Steel = Upload(Brushed(256), true);
            Poster = Upload(PeriodicPoster(), false);
            Whiteboard = Upload(WhiteboardImage(), false);
            Rules = Upload(RulesPoster(), false);
            EyeWash = Upload(Sign(new Color(0, 140, 70, 255), "GÖZ DUŞU", "Acil durumda 15 dk yıkayın"), false);
            Extinguisher = Upload(Sign(new Color(200, 30, 30, 255), "YANGIN TÜPÜ", "Yangın anında kullanın"), false);
            Exit = Upload(Sign(new Color(0, 140, 70, 255), "ACİL ÇIKIŞ", "→"), false);
            Window = Upload(WindowImage(), false);
            foreach (var h in AllHazards) PictogramTextures[h] = Upload(Pictogram(h, 128), false);
        }

        public static Texture2D Pictogram(Hazard h) => PictogramTextures[h];

        private static Texture2D Upload(Image img, bool repeat)
        {
            var tex = LoadTextureFromImage(img);
            UnloadImage(img);
            GenTextureMipmaps(ref tex);
            SetTextureFilter(tex, TextureFilter.Trilinear);
            SetTextureWrap(tex, repeat ? TextureWrap.Repeat : TextureWrap.Clamp);
            return tex;
        }

        // ------------------------------------------------------------------ surfaces

        private static Image Tiles(int size, int cells, Color tile, Color grout, int groutWidth, int noise)
        {
            var img = GenImageColor(size, size, tile);
            for (int i = 0; i < size * size / 12; i++)
            {
                int x = Rng.Next(size), y = Rng.Next(size), d = Rng.Next(-noise, noise + 1);
                ImageDrawPixel(ref img, x, y, Shift(tile, d));
            }

            int cell = size / cells;
            for (int i = 0; i < cells; i++)
            {
                ImageDrawRectangle(ref img, i * cell, 0, groutWidth, size, grout);
                ImageDrawRectangle(ref img, 0, i * cell, size, groutWidth, grout);
            }

            return img;
        }

        private static Image Speckle(int size, Color baseColor, int noise)
        {
            var img = GenImageColor(size, size, baseColor);
            for (int i = 0; i < size * size / 6; i++)
            {
                ImageDrawPixel(ref img, Rng.Next(size), Rng.Next(size), Shift(baseColor, Rng.Next(-noise, noise + 1)));
            }

            return img;
        }

        private static Image Brushed(int size)
        {
            var img = GenImageColor(size, size, new Color(176, 182, 190, 255));
            for (int y = 0; y < size; y++)
            {
                ImageDrawLine(ref img, 0, y, size, y, Shift(new Color(176, 182, 190, 255), Rng.Next(-14, 15)));
            }

            return img;
        }

        private static Color Shift(Color c, int d) =>
            new Color(Math.Clamp(c.R + d, 0, 255), Math.Clamp(c.G + d, 0, 255), Math.Clamp(c.B + d, 0, 255), (int)c.A);

        private static void Text(ref Image img, string text, float x, float y, int size, Color color, bool bold = false) =>
            ImageDrawTextEx(ref img, bold ? Ui.BoldFont : Ui.RegularFont, text, new Vector2(x, y), size, 0, color);

        private static float TextWidth(string text, int size, bool bold = false) =>
            MeasureTextEx(bold ? Ui.BoldFont : Ui.RegularFont, text, size, 0).X;

        // ------------------------------------------------------------------ wall posters

        private static Image PeriodicPoster()
        {
            const int w = 1152, h = 680;
            var img = GenImageColor(w, h, new Color(248, 249, 252, 255));
            ImageDrawRectangle(ref img, 0, 0, w, 70, new Color(20, 36, 64, 255));
            Text(ref img, "ELEMENTLERİN PERİYODİK TABLOSU", 30, 14, 40, Color.White, true);
            const int cell = 60, ox = 36, oy = 96;
            foreach (var e in Elements.All)
            {
                int x = ox + (e.Column - 1) * cell;
                int y = oy + (e.Row - 1) * cell + (e.Row >= 9 ? 16 : 0);
                var c = Elements.CategoryColor(e.Category);
                ImageDrawRectangle(ref img, x + 1, y + 1, cell - 2, cell - 2, c);
                Text(ref img, e.Z.ToString(), x + 4, y + 3, 13, new Color(30, 30, 40, 255));
                float sw = TextWidth(e.Symbol, 26, true);
                Text(ref img, e.Symbol, x + (cell - sw) / 2, y + 20, 26, new Color(20, 20, 30, 255), true);
            }

            int lx = 36 + 2 * cell + 20, ly = oy + 8;
            foreach (ElementCategory cat in Enum.GetValues(typeof(ElementCategory)))
            {
                ImageDrawRectangle(ref img, lx, ly, 18, 18, Elements.CategoryColor(cat));
                Text(ref img, Elements.CategoryName(cat), lx + 24, ly - 1, 17, new Color(40, 40, 50, 255));
                ly += 22;
                if (ly > oy + 8 + 22 * 4) { ly = oy + 8; lx += 210; }
            }

            return img;
        }

        private static Image WhiteboardImage()
        {
            const int w = 1024, h = 512;
            var img = GenImageColor(w, h, new Color(250, 251, 252, 255));
            var blue = new Color(30, 70, 190, 255);
            var red = new Color(200, 40, 40, 255);
            var green = new Color(20, 130, 60, 255);
            Text(ref img, "Bugünün konusu: Asitler ve Bazlar", 40, 28, 38, blue, true);
            Text(ref img, "HCl + NaOH → NaCl + H₂O   (ΔH = −57 kJ)", 40, 100, 34, Color.Black);
            Text(ref img, "pH = −log[H₃O⁺]", 40, 160, 34, red);
            Text(ref img, "n = m / M        M = n / V", 40, 220, 34, Color.Black);
            Text(ref img, "M₁·V₁ = M₂·V₂", 40, 280, 34, green);
            Text(ref img, "2 H₂ + O₂ → 2 H₂O", 560, 220, 30, blue);
            Text(ref img, "Mg + 2 HCl → MgCl₂ + H₂↑", 560, 280, 30, Color.Black);
            // pH scale sketch
            for (int i = 0; i <= 14; i++)
            {
                var c = Gfx.FromCore(IndicatorColors.UniversalIndicator(i));
                c.A = 255;
                ImageDrawRectangle(ref img, 40 + i * 62, 380, 60, 44, c);
                Text(ref img, i.ToString(), 58 + i * 62 - (i >= 10 ? 8 : 0), 432, 26, Color.Black);
            }

            Text(ref img, "asidik", 40, 340, 26, red);
            Text(ref img, "nötr", 460, 340, 26, green);
            Text(ref img, "bazik", 880, 340, 26, blue);
            return img;
        }

        private static Image RulesPoster()
        {
            const int w = 512, h = 720;
            var img = GenImageColor(w, h, new Color(255, 248, 225, 255));
            ImageDrawRectangle(ref img, 0, 0, w, 90, new Color(230, 140, 20, 255));
            Text(ref img, "LABORATUVAR", 30, 8, 40, Color.White, true);
            Text(ref img, "KURALLARI", 30, 48, 36, Color.White, true);
            string[] rules =
            {
                "1. Önlük, gözlük ve eldiven tak.",
                "2. Laboratuvarda yiyip içme.",
                "3. Kimyasalları koklama, tatma.",
                "4. Asidi suya ekle, suyu asite değil!",
                "5. Yanıcıları alevden uzak tut.",
                "6. Etiketsiz kabı kullanma.",
                "7. Kazayı hemen öğretmene bildir.",
                "8. Atıkları lavaboya dökme.",
                "9. Çalışma alanını temiz bırak.",
            };
            for (int i = 0; i < rules.Length; i++)
            {
                Text(ref img, rules[i], 26, 120 + i * 64, 25, new Color(50, 40, 30, 255));
            }

            return img;
        }

        private static Image Sign(Color color, string title, string subtitle)
        {
            const int w = 512, h = 256;
            var img = GenImageColor(w, h, color);
            ImageDrawRectangleLines(ref img, new Rectangle(10, 10, w - 20, h - 20), 6, Color.White);
            float tw = TextWidth(title, 56, true);
            Text(ref img, title, (w - tw) / 2, 60, 56, Color.White, true);
            float sw = TextWidth(subtitle, 30);
            Text(ref img, subtitle, (w - sw) / 2, 150, 30, Color.White);
            return img;
        }

        private static Image WindowImage()
        {
            var img = GenImageGradientLinear(512, 512, 0, new Color(150, 200, 245, 255), new Color(230, 242, 252, 255));
            for (int y = 20; y < 512; y += 34) ImageDrawRectangle(ref img, 0, y, 512, 6, new Color(240, 244, 248, 255)); // blinds
            ImageDrawRectangle(ref img, 250, 0, 12, 512, new Color(235, 238, 242, 255));
            return img;
        }

        // ------------------------------------------------------------------ GHS pictograms

        public static Image Pictogram(Hazard h, int size)
        {
            var img = GenImageColor(size, size, new Color(0, 0, 0, 0));
            float s = size / 128f;
            Vector2 P(float x, float y) => new Vector2(x * s, y * s);
            var red = new Color(220, 20, 30, 255);
            var black = new Color(15, 15, 15, 255);
            ImageDrawTriangle(ref img, P(64, 2), P(2, 64), P(126, 64), red);
            ImageDrawTriangle(ref img, P(2, 64), P(64, 126), P(126, 64), red);
            ImageDrawTriangle(ref img, P(64, 12), P(12, 64), P(116, 64), Color.White);
            ImageDrawTriangle(ref img, P(12, 64), P(64, 116), P(116, 64), Color.White);
            int R(float r) => Math.Max(1, (int)(r * s));
            switch (h)
            {
                case Hazard.Flammable:
                    ImageDrawCircle(ref img, R(64), R(78), R(17), black);
                    ImageDrawTriangle(ref img, P(64, 34), P(47, 74), P(81, 74), black);
                    ImageDrawTriangle(ref img, P(74, 46), P(66, 70), P(84, 70), black);
                    ImageDrawRectangle(ref img, R(42), R(98), R(44), R(6), black);
                    break;
                case Hazard.Corrosive:
                    ImageDrawLineEx(ref img, P(40, 36), P(56, 58), R(7), black);
                    ImageDrawLineEx(ref img, P(88, 36), P(72, 58), R(7), black);
                    ImageDrawCircle(ref img, R(58), R(70), R(5), black);
                    ImageDrawCircle(ref img, R(70), R(70), R(5), black);
                    ImageDrawRectangle(ref img, R(36), R(86), R(56), R(12), black);
                    ImageDrawRectangle(ref img, R(44), R(80), R(10), R(8), Color.White);
                    break;
                case Hazard.Toxic:
                    ImageDrawCircle(ref img, R(64), R(56), R(20), black);
                    ImageDrawRectangle(ref img, R(54), R(68), R(20), R(12), black);
                    ImageDrawCircle(ref img, R(56), R(54), R(6), Color.White);
                    ImageDrawCircle(ref img, R(72), R(54), R(6), Color.White);
                    ImageDrawLineEx(ref img, P(40, 88), P(88, 104), R(6), black);
                    ImageDrawLineEx(ref img, P(40, 104), P(88, 88), R(6), black);
                    break;
                case Hazard.Oxidizer:
                    ImageDrawCircle(ref img, R(64), R(80), R(16), black);
                    ImageDrawCircle(ref img, R(64), R(80), R(9), Color.White);
                    ImageDrawTriangle(ref img, P(64, 30), P(50, 66), P(78, 66), black);
                    ImageDrawRectangle(ref img, R(42), R(100), R(44), R(6), black);
                    break;
                case Hazard.Irritant:
                    ImageDrawRectangle(ref img, R(58), R(34), R(12), R(44), black);
                    ImageDrawCircle(ref img, R(64), R(92), R(7), black);
                    break;
                case Hazard.Environment:
                    ImageDrawLineEx(ref img, P(50, 40), P(50, 98), R(5), black);
                    ImageDrawLineEx(ref img, P(50, 60), P(36, 46), R(4), black);
                    ImageDrawLineEx(ref img, P(50, 70), P(66, 54), R(4), black);
                    ImageDrawCircle(ref img, R(80), R(90), R(10), black);
                    ImageDrawTriangle(ref img, P(90, 90), P(100, 82), P(100, 98), black);
                    ImageDrawRectangle(ref img, R(30), R(100), R(70), R(4), black);
                    break;
                case Hazard.WaterReactive:
                    ImageDrawTriangle(ref img, P(64, 30), P(48, 70), P(80, 70), black);
                    ImageDrawCircle(ref img, R(64), R(72), R(13), black);
                    for (int i = 0; i < 2; i++)
                    {
                        ImageDrawLineEx(ref img, P(38, 92 + i * 9), P(90, 92 + i * 9), R(4), black);
                    }

                    break;
            }

            return img;
        }

        // ------------------------------------------------------------------ reagent labels

        /// <summary>Printed label for a bottle / dropper / jar, made from its initial contents (formula, name, hazards).</summary>
        public static Texture2D LabelFor(Item item)
        {
            if (Labels.TryGetValue(item.Def.Id, out var tex)) return tex;
            const int w = 1024, h = 256;
            var img = GenImageColor(w, h, new Color(250, 248, 240, 255));
            var accent = Gfx.FromCore(item.Def.AccentColor);
            accent.A = 255;
            var main = MainSubstance(item.Container);
            string formula = main != null ? main.Formula : "";
            string title = Tr.Name(item.Def);
            const float cx = w * 0.25f, half = 230;
            ImageDrawRectangle(ref img, (int)(cx - half), 14, (int)(half * 2), 56, accent);
            Fit(ref img, title, cx, 22, 34, Color.White, half * 2 - 20, true);
            Fit(ref img, formula, cx, 82, 64, new Color(20, 20, 26, 255), half * 2 - 20, true);
            var hazards = main == null ? new List<Hazard>() : AllHazards.Where(x => (main.Hazards & x) != 0).Take(3).ToList();
            float px = cx - hazards.Count * 34;
            foreach (var hz in hazards)
            {
                var pict = Pictogram(hz, 64);
                ImageDraw(ref img, pict, new Rectangle(0, 0, 64, 64), new Rectangle(px, 166, 64, 64), Color.White);
                UnloadImage(pict);
                px += 68;
            }

            if (hazards.Count == 0)
            {
                Fit(ref img, "ID LAB · Laboratuvar", cx, 180, 26, new Color(110, 110, 120, 255), half * 2 - 20);
            }

            ImageDrawRectangle(ref img, 0, h - 8, w, 8, accent);
            ImageFlipHorizontal(ref img); // u grows to the viewer's left on the lathe band
            ImageFlipVertical(ref img);
            tex = Upload(img, false);
            Labels[item.Def.Id] = tex;
            return tex;
        }

        private static void Fit(ref Image img, string text, float cx, float y, int size, Color color, float maxWidth, bool bold = false)
        {
            float width = TextWidth(text, size, bold);
            while (width > maxWidth && size > 12)
            {
                size -= 2;
                width = TextWidth(text, size, bold);
            }

            Text(ref img, text, cx - width / 2, y, size, color, bold);
        }

        /// <summary>The substance a reagent container is "about" (largest non-water component).</summary>
        public static Substance MainSubstance(LiquidContainer c)
        {
            if (c == null) return null;
            Substance best = null;
            double most = 0;
            foreach (var kv in c.Contents.Components)
            {
                if (kv.Key == SubstanceIds.Water) continue;
                if (kv.Value > most)
                {
                    most = kv.Value;
                    best = c.Contents.Database.Get(kv.Key);
                }
            }

            return best ?? (c.Contents.Contains(SubstanceIds.Water) ? c.Contents.Database.Get(SubstanceIds.Water) : null);
        }
    }
}
