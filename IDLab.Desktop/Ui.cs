using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>Theme tokens: a dark (default) and a light palette, switched from the top bar.</summary>
    internal static class Theme
    {
        public static Color Scene, Panel, Raised, Border, TopBar, Text, Muted, Accent, AccentDim, Button, ButtonHot, Danger, Warning, Success, Grid;
        public static Color[] Series;

        public static bool Dark { get; private set; }

        static Theme() => Apply(true);

        public static void Apply(bool dark)
        {
            Dark = dark;
            // Palette taken from the ID Lab logo: violet tile, pink glow, orange liquid.
            if (dark)
            {
                Scene = Gfx.Rgba(0x0D0A1A);
                Panel = Gfx.Rgba(0x15112A, 238);
                Raised = Gfx.Rgba(0x1F1939, 246);
                Border = Gfx.Rgba(0x3A2F66);
                TopBar = Gfx.Rgba(0x120E24, 250);
                Text = Gfx.Rgba(0xF2EFFB);
                Muted = Gfx.Rgba(0xA49CC6);
                Accent = Gfx.Rgba(0xA078FF);
                AccentDim = Gfx.Rgba(0x40287E);
                Button = Gfx.Rgba(0x261F47);
                ButtonHot = Gfx.Rgba(0x342A5E);
                Danger = Gfx.Rgba(0xFF5A5F);
                Warning = Gfx.Rgba(0xFFA51F);
                Success = Gfx.Rgba(0x43D68A);
                Grid = Gfx.Rgba(0x2D2650);
                Series = new[] { Gfx.Rgba(0xFFA51F), Gfx.Rgba(0xA078FF), Gfx.Rgba(0x43D68A), Gfx.Rgba(0xEE8ADB), Gfx.Rgba(0x4FC3F7) };
            }
            else
            {
                Scene = Gfx.Rgba(0xF1EEF8);
                Panel = Gfx.Rgba(0xFDFCFF, 244);
                Raised = Gfx.Rgba(0xF3EFFC, 250);
                Border = Gfx.Rgba(0xD2C8EE);
                TopBar = Gfx.Rgba(0xFFFFFF, 250);
                Text = Gfx.Rgba(0x1B1436);
                Muted = Gfx.Rgba(0x5F5783);
                Accent = Gfx.Rgba(0x6D2EE0);
                AccentDim = Gfx.Rgba(0xE3D7FC);
                Button = Gfx.Rgba(0xE9E3F7);
                ButtonHot = Gfx.Rgba(0xDCD3F1);
                Danger = Gfx.Rgba(0xE0453A);
                Warning = Gfx.Rgba(0xD9820F);
                Success = Gfx.Rgba(0x1E9A55);
                Grid = Gfx.Rgba(0xE2DBF3);
                Series = new[] { Gfx.Rgba(0xE8860C), Gfx.Rgba(0x6D2EE0), Gfx.Rgba(0x1E9A55), Gfx.Rgba(0xD45CB8), Gfx.Rgba(0x0A86C4) };
            }
        }
    }

    /// <summary>
    /// Tiny immediate-mode UI sized for touch: everything scales with screen height (1080p = 1.0), buttons
    /// fire on release inside (so a finger can slide off to cancel), sliders capture until release.
    /// </summary>
    internal static class Ui
    {
        private static Font _regular, _bold;
        private static string _active;
        private static readonly List<Rectangle> Blockers = new List<Rectangle>();

        public static Font RegularFont => _regular;
        public static Font BoldFont => _bold;

        public static float S { get; private set; } = 1f;
        public static Vector2 Mouse { get; private set; }
        public static bool Pressed { get; private set; }
        public static bool Down { get; private set; }
        public static bool Released { get; private set; }
        public static string Active => _active;

        /// <summary>Where the current (or last) press started.</summary>
        public static Vector2 PressPosition { get; private set; }

        /// <summary>How far a finger may wander before a tap counts as a drag (touch screens jitter a lot).</summary>
        public static float TapSlop => 18 * S;

        private static bool _synthRelease;

        /// <summary>True while the current press started on UI (the 3D scene must ignore it).</summary>
        public static bool Captured { get; private set; }

        public static void Init()
        {
            _regular = LoadEmbeddedFont("font.regular");
            _bold = LoadEmbeddedFont("font.bold");
        }

        private static Font LoadEmbeddedFont(string name)
        {
            using var stream = typeof(Ui).Assembly.GetManifestResourceStream(name);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var codepoints = new List<int>();
            void Range(int a, int b) { for (int c = a; c <= b; c++) codepoints.Add(c); }
            Range(32, 126);
            Range(160, 383);        // Latin-1 + Latin Extended-A (ç ğ ı İ ö ş ü °, ·, ×)
            Range(0x2010, 0x2027);  // dashes, bullets, ellipsis
            Range(0x2070, 0x209F);  // superscripts / subscripts (H₂O, H₃O⁺)
            Range(0x2190, 0x2199);  // arrows
            Range(0x0391, 0x03C9);  // Greek (Δ, ρ, λ, μ ...)
            codepoints.AddRange(new[] { 0x2212, 0x2248, 0x2264, 0x2265, 0x25B2, 0x25B6, 0x25BC, 0x25C0, 0x25CF, 0x26A0, 0x2622, 0x2600, 0x263E, 0x2715, 0x21BA });
            var font = LoadFontFromMemory(".ttf", ms.ToArray(), 64, codepoints.ToArray(), codepoints.Count);
            GenTextureMipmaps(ref font.Texture);
            SetTextureFilter(font.Texture, TextureFilter.Trilinear);
            return font;
        }

        public static void BeginFrame()
        {
            S = Math.Clamp(GetScreenHeight() / 1080f, 0.55f, 3f);
            Mouse = GetMousePosition();
            Pressed = IsMouseButtonPressed(MouseButton.Left);
            Down = IsMouseButtonDown(MouseButton.Left);
            Released = IsMouseButtonReleased(MouseButton.Left);
            bool tap = TouchInput.TakeTap();
            if (_synthRelease)
            {
                // Second half of a recovered tap.
                _synthRelease = false;
                if (!Pressed && !Down)
                {
                    Released = true;
                }
            }
            else if (tap && !Pressed && !Down && !Released)
            {
                // A touch tap whose down and up arrived in the same frame: replay it as press now, release next frame.
                Pressed = true;
                Down = true;
                _synthRelease = true;
            }

            if (Pressed) PressPosition = Mouse;
            if (Pressed)
            {
                Captured = false;
                foreach (var r in Blockers)
                {
                    if (CheckCollisionPointRec(Mouse, r))
                    {
                        Captured = true;
                    }
                }
            }

            Blockers.Clear();
        }

        public static void EndFrame()
        {
            if (!Down && !Released)
            {
                _active = null;
                Captured = false;
            }
        }

        /// <summary>Registers an area that swallows presses before the 3D scene sees them (next frame).</summary>
        public static void Block(Rectangle r) => Blockers.Add(r);

        /// <summary>While set (a modal card is open), everything except the modal ignores the pointer.</summary>
        public static bool Suspended { get; set; }

        public static bool IsOver(Rectangle r) => !Suspended && CheckCollisionPointRec(Mouse, r);

        public static void SetActive(string id) => _active = id;

        // ------------------------------------------------------------------ text

        public static void Text(string text, float x, float y, float size, Color color, bool bold = false) =>
            DrawTextEx(bold ? _bold : _regular, text, new Vector2(MathF.Round(x), MathF.Round(y)), size * S, 0, color);

        public static Vector2 Measure(string text, float size, bool bold = false) =>
            MeasureTextEx(bold ? _bold : _regular, text, size * S, 0);

        public static void TextCentered(string text, Rectangle r, float size, Color color, bool bold = false)
        {
            var m = Measure(text, size, bold);
            Text(text, r.X + (r.Width - m.X) / 2, r.Y + (r.Height - m.Y) / 2, size, color, bold);
        }

        private static readonly Dictionary<(string, float, bool, int), string[]> WrapCache = new Dictionary<(string, float, bool, int), string[]>();

        /// <summary>Line breaks of a text at a width (measuring every word each frame is costly, so layouts are cached).</summary>
        private static string[] WrapLines(string text, float width, float size, bool bold)
        {
            var key = (text, size * S, bold, (int)width);
            if (WrapCache.TryGetValue(key, out var cached)) return cached;
            if (WrapCache.Count > 6000) WrapCache.Clear();
            var lines = new List<string>();
            foreach (var paragraph in text.Split('\n'))
            {
                string line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (Measure(candidate, size, bold).X > width && line.Length > 0)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else
                    {
                        line = candidate;
                    }
                }

                lines.Add(line);
            }

            var result = lines.ToArray();
            WrapCache[key] = result;
            return result;
        }

        /// <summary>Word-wrapped text; returns the height used.</summary>
        public static float Wrapped(string text, float x, float y, float width, float size, Color color, bool bold = false, float lineGap = 1.25f, bool draw = true)
        {
            float lineHeight = size * S * lineGap;
            var lines = WrapLines(text, width, size, bold);
            if (draw)
            {
                float cy = y;
                foreach (var line in lines)
                {
                    Text(line, x, cy, size, color, bold);
                    cy += lineHeight;
                }
            }

            return lines.Length * lineHeight;
        }

        // ------------------------------------------------------------------ widgets

        public static void Panel(Rectangle r, Color color, bool block = true)
        {
            DrawRectangleRounded(r, Math.Min(0.5f, 14 * S / Math.Max(1, Math.Min(r.Width, r.Height))), 8, color);
            if (block)
            {
                Block(r);
            }
        }

        public static bool Button(string id, Rectangle r, string label, Color? background = null, float size = 17, bool selected = false)
        {
            bool hot = IsOver(r);
            if (Pressed && hot)
            {
                _active = id;
            }

            bool pressing = _active == id && Down && hot;
            Color bg = selected ? Gfx.Lerp(Theme.Accent, Theme.Button, 0.35f) : background ?? Theme.Button;
            if (pressing)
            {
                bg = Gfx.Lerp(bg, Color.White, 0.18f);
            }
            else if (hot)
            {
                bg = Gfx.Lerp(bg, Color.White, 0.07f);
            }

            DrawRectangleRounded(r, 0.35f, 8, bg);
            TextCentered(label, r, size, Theme.Text, bold: true);
            Block(r);
            return Released && _active == id && hot;
        }

        /// <summary>Repeating button (fires every frame while held) for camera controls.</summary>
        public static bool HoldButton(string id, Rectangle r, string label, float size = 22)
        {
            bool hot = IsOver(r);
            if (Pressed && hot)
            {
                _active = id;
            }

            bool held = _active == id && Down;
            DrawRectangleRounded(r, 0.5f, 10, held ? Gfx.Lerp(Theme.Accent, Theme.Button, 0.4f) : Gfx.WithAlpha(Theme.Button, 0.92f));
            TextCentered(label, r, size, Theme.Text, bold: true);
            Block(r);
            return held;
        }

        public static float Slider(string id, Rectangle r, float value, float min, float max, Color accent)
        {
            bool hot = IsOver(r);
            if (Pressed && hot)
            {
                _active = id;
            }

            if (_active == id && Down)
            {
                float t = Math.Clamp((Mouse.X - r.X) / r.Width, 0f, 1f);
                value = min + t * (max - min);
            }

            float k = (value - min) / (max - min);
            float trackH = 10 * S;
            var track = new Rectangle(r.X, r.Y + (r.Height - trackH) / 2, r.Width, trackH);
            DrawRectangleRounded(track, 1f, 8, Theme.Button);
            DrawRectangleRounded(new Rectangle(track.X, track.Y, Math.Max(trackH, track.Width * k), trackH), 1f, 8, accent);
            DrawCircleV(new Vector2(r.X + r.Width * k, r.Y + r.Height / 2), 15 * S, Color.White);
            DrawCircleV(new Vector2(r.X + r.Width * k, r.Y + r.Height / 2), 7 * S, accent);
            Block(new Rectangle(r.X - 16 * S, r.Y, r.Width + 32 * S, r.Height));
            return value;
        }

        /// <summary>Text rotated 90° counter-clockwise (reads bottom-to-top), centred in <paramref name="r"/>.</summary>
        public static void VerticalText(string text, Rectangle r, float size, Color color)
        {
            var m = Measure(text, size, true);
            DrawTextPro(_bold, text, new Vector2(r.X + (r.Width - m.Y) / 2, r.Y + (r.Height + m.X) / 2), Vector2.Zero, -90, size * S, 0, color);
        }

        public static string Hex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    /// <summary>
    /// Touch scrolling for a list: drag with one finger (with inertia) or use the mouse wheel. A press only becomes
    /// a scroll after it moves a little, so taps on rows still work; <see cref="Dragging"/> stays true on the
    /// release frame of a scroll so callers can ignore that release.
    /// </summary>
    internal sealed class DragScroll
    {
        public float Offset;
        private float _velocity, _pressX, _pressY, _pressOffset;
        private bool _tracking, _horizontal;

        public bool Dragging { get; private set; }

        public void Reset()
        {
            Offset = 0;
            _velocity = 0;
        }

        public void Update(Rectangle area, float contentHeight, float dt)
        {
            float max = Math.Max(0, contentHeight - area.Height);
            if (Ui.IsOver(area))
            {
                float wheel = GetMouseWheelMove();
                if (Math.Abs(wheel) > 0.01f)
                {
                    Offset -= wheel * 80 * Ui.S;
                    _velocity = 0;
                }
            }

            if (Ui.Pressed && Ui.IsOver(area))
            {
                _tracking = true;
                _horizontal = false;
                _pressX = Ui.Mouse.X;
                _pressY = Ui.Mouse.Y;
                _pressOffset = Offset;
                _velocity = 0;
                Dragging = false;
            }

            if (_tracking && Ui.Down)
            {
                float dx = Math.Abs(Ui.Mouse.X - _pressX), dy = Math.Abs(Ui.Mouse.Y - _pressY);
                // A press that first moves sideways is not a scroll (e.g. dragging an item out of a list).
                if (!Dragging && !_horizontal && dx > 14 * Ui.S && dx > dy) _horizontal = true;
                if (!Dragging && !_horizontal && dy > 14 * Ui.S && max > 0) Dragging = true;
                if (Dragging)
                {
                    float previous = Offset;
                    Offset = _pressOffset - (Ui.Mouse.Y - _pressY);
                    if (dt > 0) _velocity = _velocity * 0.5f + (Offset - previous) / dt * 0.5f;
                }
            }
            else if (_tracking)
            {
                _tracking = false; // release frame: keep Dragging so the release is not taken as a tap
            }
            else
            {
                Dragging = false;
                Offset += _velocity * dt;
                _velocity *= MathF.Exp(-dt * 4f);
                if (Math.Abs(_velocity) < 5) _velocity = 0;
            }

            if (Offset < 0 || Offset > max) _velocity = 0;
            Offset = Math.Clamp(Offset, 0, max);
        }
    }
}
