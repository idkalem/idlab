using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// "Periyodik Tablo" tab: all 118 elements as 3D blocks (height can show a property), a category legend that
    /// filters, and — for the selected element — a rotating Bohr model (protons, neutrons, electron shells) plus an
    /// information card.
    /// </summary>
    internal sealed class PeriodicView
    {
        private enum HeightMode { Flat, Mass, Electronegativity, Density, Melting }

        private static readonly (HeightMode mode, string label)[] Modes =
        {
            (HeightMode.Flat, "Düz"), (HeightMode.Mass, "Atom kütlesi"), (HeightMode.Electronegativity, "Elektronegatiflik"),
            (HeightMode.Density, "Yoğunluk"), (HeightMode.Melting, "Erime noktası"),
        };

        private const float Pitch = 1.08f;
        private readonly Dictionary<int, float> _heights = new Dictionary<int, float>();
        private Element _selected = Elements.All[25]; // iron
        private Element _hovered;
        private HeightMode _mode = HeightMode.Flat;
        private ElementCategory? _filter;
        private float _yaw, _pitch = 58f, _distance = 24f;
        private Camera3D _camera;
        private Rectangle _tableArea, _panel;
        private RenderTexture2D _atom;
        private float _time, _selectAnim;
        private bool _orbiting;
        private Vector2 _press;
        private float _infoScroll;
        private float _fittedWidth;

        public void Init()
        {
            _atom = LoadRenderTexture(640, 560);
            foreach (var e in Elements.All) _heights[e.Z] = 0.3f;
        }

        private static Vector3 TilePosition(Element e) =>
            new Vector3((9.5f - e.Column) * Pitch, 0, (5.5f - e.Row) * Pitch - (e.Row >= 9 ? 0.5f : 0f));

        private float TargetHeight(Element e)
        {
            double v, max;
            switch (_mode)
            {
                case HeightMode.Mass: v = e.Mass; max = 300; break;
                case HeightMode.Electronegativity: v = e.Electronegativity; max = 4; break;
                case HeightMode.Density: v = double.IsNaN(e.Density) ? double.NaN : Math.Log10(1 + e.Density * 100); max = Math.Log10(1 + 2300); break;
                case HeightMode.Melting: v = e.MeltingK; max = 4000; break;
                default: return 0.3f;
            }

            return double.IsNaN(v) ? 0.15f : 0.3f + (float)(v / max) * 3.6f;
        }

        // ------------------------------------------------------------------ frame

        public void Update(float dt, Rectangle area, bool pointerFree)
        {
            _time += dt;
            _selectAnim = Math.Min(1, _selectAnim + dt * 3);
            float s = Ui.S;
            _panel = new Rectangle(area.X + area.Width - 560 * s - 10 * s, area.Y + 10 * s, 560 * s, area.Height - 20 * s);
            _tableArea = new Rectangle(area.X, area.Y, area.Width - _panel.Width - 20 * s, area.Height);
            if (Math.Abs(_fittedWidth - _tableArea.Width) > 1)
            {
                // Distance at which the 19.5 m wide table (plus margins) spans the area horizontally.
                _fittedWidth = _tableArea.Width;
                float sh0 = GetScreenHeight();
                float ppmNeeded = _tableArea.Width * 0.94f / 21.5f;
                float byWidth = sh0 / (2f * ppmNeeded * MathF.Tan(17.5f * MathF.PI / 180f));
                float ppmHeight = (_tableArea.Height - 190 * s) / 12.5f;
                float byHeight = sh0 / (2f * ppmHeight * MathF.Tan(17.5f * MathF.PI / 180f));
                _distance = Math.Clamp(Math.Max(byWidth, byHeight), 10f, 40f);
            }


            foreach (var e in Elements.All)
            {
                float h = _heights[e.Z];
                _heights[e.Z] = h + (TargetHeight(e) - h) * (1 - MathF.Exp(-dt * 6));
            }

            // Camera: target the table centre, shifted so the table is centred in its area.
            float yaw = _yaw * MathF.PI / 180f, pitch = _pitch * MathF.PI / 180f;
            var forward = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
            var target = new Vector3(0, 0, -0.4f);
            float sw = GetScreenWidth(), sh = GetScreenHeight();
            float ppm = sh / (2f * _distance * MathF.Tan(17.5f * MathF.PI / 180f));
            float dx = (_tableArea.X + _tableArea.Width / 2 - sw / 2) / ppm;
            float dy = (_tableArea.Y + _tableArea.Height / 2 - sh / 2) / ppm;
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up = Vector3.Cross(right, forward);
            var shift = -right * dx + up * dy;
            _camera = new Camera3D(target - forward * _distance + shift, target + shift, Vector3.UnitY, 35f, CameraProjection.Perspective);

            _hovered = null;
            bool overTable = CheckCollisionPointRec(Ui.Mouse, _tableArea) && pointerFree;
            if (overTable)
            {
                var ray = GetScreenToWorldRay(Ui.Mouse, _camera);
                float best = float.MaxValue;
                foreach (var e in Elements.All)
                {
                    var p = TilePosition(e);
                    float h = _heights[e.Z] + (e == _selected ? 0.3f * _selectAnim : 0);
                    var hit = GetRayCollisionBox(ray, new BoundingBox(p + new Vector3(-0.5f, 0, -0.5f), p + new Vector3(0.5f, h, 0.5f)));
                    if (hit.Hit && hit.Distance < best)
                    {
                        best = hit.Distance;
                        _hovered = e;
                    }
                }

                float wheel = GetMouseWheelMove();
                if (Math.Abs(wheel) > 0.01f) _distance = Math.Clamp(_distance * (1 - wheel * 0.1f), 10f, 40f);
                if (Ui.Pressed)
                {
                    _press = Ui.Mouse;
                    _orbiting = false;
                }
            }

            if (Ui.Down && overTable && !Ui.Captured && Vector2.Distance(Ui.Mouse, _press) > Ui.TapSlop)
            {
                _orbiting = true;
            }

            if (_orbiting && Ui.Down)
            {
                var d = GetMouseDelta();
                _yaw -= d.X * 0.2f;
                _pitch = Math.Clamp(_pitch + d.Y * 0.15f, 20f, 85f);
            }

            if (Ui.Released && overTable && !_orbiting && !Ui.Captured && _hovered != null)
            {
                _selected = _hovered;
                _selectAnim = 0;
                _infoScroll = 0;
            }

            if (!Ui.Down) _orbiting = false;
            RenderAtom(dt);
        }

        public void Draw()
        {
            BeginMode3D(_camera);
            Gfx.Ambient = new Vector3(0.42f, 0.45f, 0.52f);
            Gfx.FogColor = new Vector3(0.043f, 0.059f, 0.09f);
            Gfx.FogRange = new Vector2(1000f, 1f);
            Gfx.BeginFrame(_camera.Position);
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(21, 0.1f, 12.5f), Gfx.Translate(0, -0.06f, -0.6f)), Theme.Dark ? new Color(22, 28, 40, 255) : new Color(205, 214, 226, 255));
            foreach (var e in Elements.All)
            {
                var p = TilePosition(e);
                float h = _heights[e.Z] + (e == _selected ? 0.3f * _selectAnim : 0);
                var c = Elements.CategoryColor(e.Category);
                bool dim = _filter.HasValue && _filter.Value != e.Category;
                if (dim) c = Gfx.Lerp(c, Theme.Dark ? new Color(40, 46, 58, 255) : new Color(225, 230, 238, 255), 0.75f);
                Vector3 emission = default;
                if (e == _selected) emission = new Vector3(0.35f);
                else if (e == _hovered) emission = new Vector3(0.18f);
                Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(0.96f, h, 0.96f), Gfx.Translate(p + new Vector3(0, h / 2, 0))), c, emission);
            }

            EndMode3D();

            // Labels on top of the tiles.
            foreach (var e in Elements.All)
            {
                var p = TilePosition(e);
                float h = _heights[e.Z] + (e == _selected ? 0.3f * _selectAnim : 0);
                var top = GetWorldToScreen(p + new Vector3(0, h, 0), _camera);
                var edge = GetWorldToScreen(p + new Vector3(0.5f, h, 0), _camera);
                float px = Vector2.Distance(top, edge) * 2;
                if (px < 18 || !CheckCollisionPointRec(top, _tableArea)) continue;
                float size = Math.Clamp(px * 0.42f, 9, 34) / Ui.S;
                var m = Ui.Measure(e.Symbol, size, true);
                bool dim = _filter.HasValue && _filter.Value != e.Category;
                var textColor = dim ? new Color(150, 156, 170, 200) : new Color(16, 18, 26, 255);
                Ui.Text(e.Symbol, top.X - m.X / 2, top.Y - m.Y / 2 + 2 * Ui.S, size, textColor, true);
                if (px > 34)
                {
                    string z = e.Z.ToString(CultureInfo.InvariantCulture);
                    float zs = size * 0.4f;
                    var zm = Ui.Measure(z, zs);
                    Ui.Text(z, top.X - zm.X / 2, top.Y - m.Y / 2 - zm.Y + 2 * Ui.S, zs, textColor);
                }
            }

            DrawToolbar();
            DrawLegend();
            DrawPanel();
        }

        private void DrawToolbar()
        {
            float s = Ui.S;
            float x = _tableArea.X + 20 * s, y = _tableArea.Y + 16 * s;
            Ui.Text("Yükseklik:", x, y + 10 * s, 15, Theme.Muted, true);
            x += 100 * s;
            foreach (var (mode, label) in Modes)
            {
                float w = Ui.Measure(label, 15, true).X + 30 * s;
                if (Ui.Button("pt-mode-" + mode, new Rectangle(x, y, w, 40 * s), label, null, 15, _mode == mode))
                {
                    _mode = mode;
                    if (mode != HeightMode.Flat && _pitch < 66f) _pitch = 66f; // look down so columns do not hide each other
                }
                x += w + 8 * s;
            }

            if (_hovered != null)
            {
                string tip = $"{_hovered.Z} · {_hovered.Name} ({_hovered.Symbol})";
                Ui.Text(tip, _tableArea.X + 20 * s, y + 52 * s, 17, Theme.Text, true);
            }
        }

        private void DrawLegend()
        {
            float s = Ui.S;
            float x = _tableArea.X + 20 * s, y = _tableArea.Y + _tableArea.Height - 104 * s;
            float maxX = _tableArea.X + _tableArea.Width - 20 * s;
            foreach (ElementCategory cat in Enum.GetValues(typeof(ElementCategory)))
            {
                string label = Elements.CategoryName(cat);
                float w = Ui.Measure(label, 14, true).X + 44 * s;
                if (x + w > maxX)
                {
                    x = _tableArea.X + 20 * s;
                    y += 46 * s;
                }

                var r = new Rectangle(x, y, w, 40 * s);
                bool on = _filter == cat;
                if (Ui.Button("pt-cat-" + cat, r, "", on ? Gfx.Lerp(Elements.CategoryColor(cat), Theme.Button, 0.55f) : Theme.Button))
                {
                    _filter = on ? (ElementCategory?)null : cat;
                }

                DrawRectangleRounded(new Rectangle(x + 12 * s, y + 12 * s, 16 * s, 16 * s), 0.3f, 4, Elements.CategoryColor(cat));
                Ui.Text(label, x + 36 * s, y + 10 * s, 14, Theme.Text, true);
                x += w + 8 * s;
            }
        }

        // ------------------------------------------------------------------ atom model

        private void RenderAtom(float dt)
        {
            var e = _selected;
            var cam = new Camera3D(new Vector3(0, 2.2f, -11.5f), Vector3.Zero, Vector3.UnitY, 40f, CameraProjection.Perspective);
            BeginTextureMode(_atom);
            ClearBackground(new Color(10, 15, 26, 255));
            BeginMode3D(cam);
            Gfx.Ambient = new Vector3(0.38f, 0.4f, 0.48f);
            Gfx.FogColor = new Vector3(0.04f, 0.06f, 0.1f);
            Gfx.FogRange = new Vector2(1000f, 1f);
            Gfx.BeginFrame(cam.Position);
            var spin = Quaternion.CreateFromAxisAngle(Vector3.UnitY, _time * 0.35f);

            // Nucleus: protons (red) and neutrons (grey) packed in a ball; large nuclei are drawn scaled down.
            int protons = e.Z, neutrons = e.Neutrons;
            int total = protons + neutrons;
            int shown = Math.Min(total, 90);
            float nucleonR = shown < total ? 0.2f : 0.24f;
            float ballR = nucleonR * 1.15f * MathF.Cbrt(shown);
            int shownProtons = (int)Math.Round(shown * (protons / (double)Math.Max(1, total)));
            var rng = new Random(e.Z * 31);
            for (int i = 0; i < shown; i++)
            {
                // Fibonacci sphere shells give an even, deterministic packing.
                // Direction from a Fibonacci sphere, radius from an unrelated low-discrepancy sequence, so the
                // nucleons fill a ball instead of lining up on a spiral.
                float r = ballR * MathF.Cbrt(0.08f + 0.92f * ((i * 0.7548777f + 0.31f) % 1f));
                float phi = MathF.Acos(1 - 2 * (i + 0.5f) / shown);
                float theta = i * 2.39996f;
                var pos = new Vector3(r * MathF.Sin(phi) * MathF.Cos(theta), r * MathF.Cos(phi), r * MathF.Sin(phi) * MathF.Sin(theta));
                pos = Vector3.Transform(pos, spin);
                bool proton = (i * 7919 % shown) < shownProtons;
                Gfx.Draw(Gfx.Sphere, Gfx.Mul(Gfx.Scale(nucleonR), Gfx.Translate(pos)), proton ? new Color(230, 70, 70, 255) : new Color(150, 165, 185, 255));
            }

            // Electron shells (K, L, M ...) as tilted orbits with electrons travelling around them.
            int shells = e.Shells.Length;
            float maxR = 4.6f;
            for (int k = 0; k < shells; k++)
            {
                float radius = ballR + 0.7f + (maxR - ballR - 0.7f) * (shells == 1 ? 0.4f : k / (float)(shells - 1));
                // Rings in the XY plane face the camera; small, different tilts keep them readable as ellipses.
                var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (k % 2 == 0 ? 1 : -1) * (18 + k * 7) * MathF.PI / 180f) *
                           Quaternion.CreateFromAxisAngle(Vector3.UnitX, (22 + k * 5) * MathF.PI / 180f);
                var q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, _time * 0.1f) * tilt;
                var shellColor = new Color(70, 170, 255, 180);
                Vector3 Point(float a) => Vector3.Transform(new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, 0), q);
                var prev = Point(0);
                for (int seg = 1; seg <= 72; seg++)
                {
                    var p = Point(seg / 72f * MathF.PI * 2);
                    DrawLine3D(prev, p, shellColor);
                    prev = p;
                }

                int count = e.Shells[k];
                float speed = 1.4f / (1 + k * 0.6f);
                for (int i = 0; i < count; i++)
                {
                    float a = _time * speed + i * MathF.PI * 2 / count;
                    Gfx.Draw(Gfx.Sphere, Gfx.Mul(Gfx.Scale(0.13f), Gfx.Translate(Point(a))), new Color(255, 220, 90, 255), new Vector3(0.6f, 0.5f, 0.15f));
                }
            }

            EndMode3D();
            EndTextureMode();
        }

        // ------------------------------------------------------------------ info card

        private void DrawPanel()
        {
            float s = Ui.S;
            var r = _panel;
            Ui.Panel(r, Theme.Panel);
            var e = _selected;
            var c = Elements.CategoryColor(e.Category);
            var ci = CultureInfo.CurrentCulture;

            // Header: big symbol tile + name.
            var tile = new Rectangle(r.X + 18 * s, r.Y + 18 * s, 112 * s, 112 * s);
            DrawRectangleRounded(tile, 0.15f, 8, c);
            Ui.Text(e.Z.ToString(CultureInfo.InvariantCulture), tile.X + 10 * s, tile.Y + 6 * s, 18, new Color(20, 20, 28, 255), true);
            var sm = Ui.Measure(e.Symbol, 50, true);
            Ui.Text(e.Symbol, tile.X + (tile.Width - sm.X) / 2, tile.Y + 34 * s, 50, new Color(20, 20, 28, 255), true);
            Ui.Text(e.Name, tile.Right() + 18 * s, tile.Y + 8 * s, 32, Theme.Text, true);
            Ui.Text(Elements.CategoryName(e.Category), tile.Right() + 18 * s, tile.Y + 52 * s, 17, c, true);
            Ui.Text(string.Format(ci, "Atom kütlesi: {0:0.###} u", e.Mass), tile.Right() + 18 * s, tile.Y + 80 * s, 16, Theme.Muted);

            // Atom model.
            float aw = r.Width - 36 * s;
            var dest = new Rectangle(r.X + 18 * s, tile.Y + tile.Height + 14 * s, aw, aw * 560f / 640f * 0.82f);
            DrawTexturePro(_atom.Texture, new Rectangle(0, 560 * 0.09f, 640, -560 * 0.82f), dest, Vector2.Zero, 0, Color.White);
            DrawRectangleRoundedLinesEx(dest, 0.03f, 6, 1.5f * s, Theme.Border);
            string particles = $"p⁺ {e.Z}   n⁰ {e.Neutrons}   e⁻ {e.Z}";
            string shells = "Elektron katmanları: " + string.Join("-", e.Shells);
            var pm = Ui.Measure(particles, 16, true);
            var shm = Ui.Measure(shells, 15, true);
            DrawRectangleRounded(new Rectangle(dest.X + 6 * s, dest.Y + 6 * s, pm.X + 14 * s, pm.Y + 8 * s), 0.4f, 6, new Color(10, 15, 26, 210));
            DrawRectangleRounded(new Rectangle(dest.X + 6 * s, dest.Y + dest.Height - 32 * s, shm.X + 14 * s, shm.Y + 8 * s), 0.4f, 6, new Color(10, 15, 26, 210));
            Ui.Text(particles, dest.X + 12 * s, dest.Y + 10 * s, 16, Theme.Text, true);
            Ui.Text(shells, dest.X + 12 * s, dest.Y + dest.Height - 28 * s, 15, new Color(255, 220, 90, 255), true);
            if (e.Neutrons + e.Z > 90)
            {
                Ui.Text("(çekirdek ölçekli gösterildi)", dest.X + dest.Width - 210 * s, dest.Y + 10 * s, 12, Theme.Muted);
            }

            // Facts table (scrolls on small screens).
            var facts = new List<(string k, string v)>
            {
                ("Atom numarası", e.Z.ToString(CultureInfo.InvariantCulture)),
                ("Grup / Periyot / Blok", $"{(e.Group > 0 ? e.Group.ToString(CultureInfo.InvariantCulture) : "f")} / {e.Period} / {e.Block}"),
                ("Elektron dizilimi", Elements.PrettyConfiguration(e.Configuration)),
                ("Elektronegatiflik", double.IsNaN(e.Electronegativity) ? "—" : e.Electronegativity.ToString("0.00", ci)),
                ("Erime noktası", Elements.Celsius(e.MeltingK)),
                ("Kaynama noktası", Elements.Celsius(e.BoilingK)),
                ("Yoğunluk", double.IsNaN(e.Density) ? "—" : string.Format(ci, "{0:0.####} g/cm³", e.Density)),
                ("25 °C'de hâli", Elements.PhaseName(e.Phase)),
                ("Keşif", e.DiscoveryYear > 0 ? e.DiscoveryYear.ToString(CultureInfo.InvariantCulture) : "Antik çağlardan beri biliniyor"),
                ("Kökeni", e.IsSynthetic ? "Yapay (laboratuvarda üretildi)" : "Doğada bulunur"),
            };

            var area = new Rectangle(r.X + 18 * s, dest.Y + dest.Height + 12 * s, r.Width - 36 * s, r.Y + r.Height - (dest.Y + dest.Height + 24 * s));
            if (Ui.IsOver(area)) _infoScroll -= GetMouseWheelMove() * 40 * s;
            BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
            float y = area.Y - _infoScroll;
            foreach (var (k, v) in facts)
            {
                Ui.Text(k, area.X, y, 15, Theme.Muted);
                Ui.Text(v, area.X + 190 * s, y, 15, Theme.Text, true);
                y += 26 * s;
            }

            y += 6 * s;
            DrawRectangleRounded(new Rectangle(area.X, y, area.Width, 4 * s), 1, 4, c);
            y += 14 * s;
            y += Ui.Wrapped(e.Note, area.X, y, area.Width, 16, Theme.Text);
            float content = y + _infoScroll - area.Y;
            EndScissorMode();
            _infoScroll = Math.Clamp(_infoScroll, 0, Math.Max(0, content - area.Height));
        }
    }

    internal static class RectExtensions
    {
        public static float Right(this Rectangle r) => r.X + r.Width;
        public static float Bottom(this Rectangle r) => r.Y + r.Height;
    }
}
