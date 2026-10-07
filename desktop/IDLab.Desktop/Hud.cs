using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using IDLab.Core.Components;
using IDLab.Core.Data;
using IDLab.Core.Events;
using IDLab.Core.Model;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Heads-up display for the smart board: top bar, equipment inventory (left), live graphs (right),
    /// micro view (bottom-left), inspector / experiment card (bottom-right), camera buttons and toasts.
    /// Every control is a large touch target; no keyboard is required.
    /// </summary>
    internal sealed class Hud
    {
        private readonly Lab _lab;
        private readonly MicroView _micro;
        private readonly List<(string text, Color color, float age)> _toasts = new List<(string, Color, float)>();
        private Rectangle _top, _inventory, _data, _microRect, _inspector;
        private bool _showInventory = true, _showData = true, _showMicro = true;
        private float _inventoryScroll, _dataScroll;
        private double _window = 120;
        private float _quitArmed;
        private int _speedIndex = 1;
        private static readonly (string label, float scale)[] Speeds = { ("Durdur", 0), ("1×", 1), ("5×", 5), ("20×", 20) };

        public bool QuitRequested { get; private set; }

        public Hud(Lab lab, MicroView micro)
        {
            _lab = lab;
            _micro = micro;
            lab.Notified += (e, text) =>
            {
                if (e.Type == LabEventType.Info) return;
                Color c = e.Type == LabEventType.Explosion || e.Type == LabEventType.FuseBlown ? Theme.Danger
                    : e.IsHazard || e.Type == LabEventType.Spill ? Theme.Warning : Theme.Accent;
                Toast(text, c);
            };
        }

        public void Toast(string text, Color color)
        {
            int existing = _toasts.FindIndex(t => t.text == text);
            if (existing >= 0)
            {
                _toasts[existing] = (text, color, 0f); // refresh instead of stacking duplicates
                return;
            }

            if (_toasts.Count >= 3) _toasts.RemoveAt(0);
            _toasts.Add((text, color, 0f));
        }

        public void SetSpeed(int index)
        {
            _speedIndex = index;
            var clock = _lab.World.Clock;
            clock.Paused = Speeds[index].scale <= 0;
            if (!clock.Paused) clock.TimeScale = Speeds[index].scale;
        }

        // ------------------------------------------------------------------ layout

        public void Layout()
        {
            float s = Ui.S, w = GetScreenWidth(), h = GetScreenHeight();
            float pad = 10 * s, topH = 66 * s;
            _top = new Rectangle(0, 0, w, topH);
            _inventory = _showInventory ? new Rectangle(pad, topH + pad, 310 * s, h - topH - 2 * pad) : default;
            _data = _showData ? new Rectangle(w - 460 * s - pad, topH + pad, 460 * s, h - topH - 2 * pad) : default;
            float freeLeft = _showInventory ? _inventory.X + _inventory.Width : 0;
            float freeRight = _showData ? _data.X : w;
            _microRect = _showMicro ? new Rectangle(freeLeft + 12 * s, h - 392 * s - 12 * s, 380 * s, 392 * s) : default;
            float inspW = Math.Min(560 * s, freeRight - freeLeft - 24 * s - (_showMicro ? _microRect.Width + 12 * s : 0));
            inspW = Math.Max(inspW, 360 * s);
            _inspector = new Rectangle(freeRight - inspW - 12 * s, h - 262 * s - 12 * s, inspW, 262 * s);
            _lab.Viewport = new Rectangle(freeLeft, topH, freeRight - freeLeft, h - topH - 210 * s);
        }

        public bool IsOverInventory(Vector2 p) => _showInventory && CheckCollisionPointRec(p, _inventory);

        // ------------------------------------------------------------------ draw

        public void Draw(float dt)
        {
            DrawTopBar();
            if (_showInventory) DrawInventory();
            if (_showData) DrawData();
            if (_showMicro) DrawMicro();
            DrawInspector();
            DrawCameraButtons();
            DrawToasts(dt);
            _quitArmed = Math.Max(0, _quitArmed - dt);
        }

        private void DrawTopBar()
        {
            float s = Ui.S;
            DrawRectangleRec(_top, Theme.TopBar);
            DrawRectangle(0, (int)_top.Height - 1, (int)_top.Width, Math.Max(1, (int)s), Theme.Border);
            Ui.Block(_top);
            float y = 11 * s, bh = 44 * s, x = 18 * s;
            Ui.Text("ID", x, 16 * s, 28, Theme.Accent, true);
            Ui.Text("LAB", x + Ui.Measure("ID ", 28, true).X, 16 * s, 28, Theme.Text, true);
            x += 130 * s;

            // Right side, laid out from the right edge.
            float rx = _top.Width - 14 * s;
            Rectangle Next(float width) { rx -= width; var r = new Rectangle(rx, y, width, bh); rx -= 8 * s; return r; }

            if (Ui.Button("quit", Next(96 * s), _quitArmed > 0 ? "Emin mi?" : "Kapat", _quitArmed > 0 ? Theme.Danger : (Color?)null))
            {
                if (_quitArmed > 0) QuitRequested = true;
                else _quitArmed = 3f;
            }

            if (Ui.Button("full", Next(122 * s), IsWindowState(ConfigFlags.BorderlessWindowMode) ? "Pencere" : "Tam ekran"))
            {
                ToggleBorderlessWindowed();
            }

            if (Ui.Button("tmicro", Next(78 * s), "Mikro", selected: _showMicro)) _showMicro = !_showMicro;
            if (Ui.Button("tdata", Next(78 * s), "Veri", selected: _showData)) _showData = !_showData;
            if (Ui.Button("tinv", Next(130 * s), "Malzemeler", selected: _showInventory)) _showInventory = !_showInventory;
            rx -= 10 * s;
            if (Ui.Button("reset", Next(96 * s), "Sıfırla", Theme.AccentDim)) _lab.LoadExperiment(_lab.Experiment.Id);
            var clockRect = Next(150 * s);
            string clock = string.Format(CultureInfo.CurrentCulture, "t = {0:0.0} s", _lab.World.Time);
            Ui.TextCentered(_lab.World.Clock.Paused ? clock + " ‖" : clock, clockRect, 18, Theme.Muted);
            for (int i = Speeds.Length - 1; i >= 0; i--)
            {
                if (Ui.Button("speed" + i, Next(i == 0 ? 92 * s : 62 * s), Speeds[i].label, null, 17, _speedIndex == i)) SetSpeed(i);
            }

            // Experiment selector fills the middle.
            var playable = _lab.Experiments.All.Where(e => e.Status == ExperimentStatus.Playable).ToList();
            int index = playable.IndexOf(_lab.Experiment);
            if (Ui.Button("expPrev", new Rectangle(x, y, bh, bh), "◀")) _lab.LoadExperiment(playable[(index - 1 + playable.Count) % playable.Count].Id);
            float titleW = Math.Max(160 * s, rx - x - 2 * bh - 30 * s);
            var titleRect = new Rectangle(x + bh + 8 * s, y, titleW, bh);
            DrawRectangleRounded(titleRect, 0.35f, 8, Theme.Raised);
            Ui.TextCentered(Tr.Title(_lab.Experiment), titleRect, 19, Theme.Text, true);
            if (Ui.Button("expNext", new Rectangle(titleRect.X + titleW + 8 * s, y, bh, bh), "▶")) _lab.LoadExperiment(playable[(index + 1) % playable.Count].Id);
        }

        private void DrawInventory()
        {
            float s = Ui.S;
            var r = _inventory;
            Ui.Panel(r, Theme.Panel);
            Ui.Text("MALZEMELER", r.X + 16 * s, r.Y + 14 * s, 20, Theme.Text, true);
            Ui.Wrapped("Tezgâha sürükleyin, dokunarak ekleyin. Kaldırmak için buraya geri sürükleyin.", r.X + 16 * s, r.Y + 44 * s, r.Width - 32 * s, 13, Theme.Muted);

            var list = new Rectangle(r.X + 8 * s, r.Y + 92 * s, r.Width - 16 * s, r.Height - 100 * s);
            if (Ui.IsOver(list)) _inventoryScroll -= GetMouseWheelMove() * 60 * s;
            float contentH = 0;
            BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
            float y = list.Y - _inventoryScroll;
            foreach (var group in _lab.Inventory().GroupBy(d => d.Category).OrderBy(g => g.Key))
            {
                Ui.Text(Tr.Category(group.Key), list.X + 8 * s, y + 8 * s, 12, Theme.Accent, true);
                y += 30 * s;
                foreach (var def in group)
                {
                    var row = new Rectangle(list.X, y, list.Width, 60 * s);
                    DrawInventoryRow(def, row, list);
                    y += 66 * s;
                }
            }

            contentH = y + _inventoryScroll - list.Y;
            EndScissorMode();
            _inventoryScroll = Math.Clamp(_inventoryScroll, 0, Math.Max(0, contentH - list.Height));
        }

        private void DrawInventoryRow(EquipmentDefinition def, Rectangle row, Rectangle clip)
        {
            float s = Ui.S;
            string id = "inv:" + def.Id;
            bool hot = Ui.IsOver(row) && Ui.IsOver(clip);
            if (Ui.Pressed && hot) Ui.SetActive(id);
            bool active = Ui.Active == id;
            DrawRectangleRounded(row, 0.25f, 8, active ? Gfx.Lerp(Theme.Raised, Theme.Accent, 0.25f) : hot ? Gfx.Lerp(Theme.Raised, Color.White, 0.05f) : Theme.Raised);
            DrawRectangleRounded(new Rectangle(row.X + 10 * s, row.Y + 12 * s, 6 * s, row.Height - 24 * s), 1f, 4, Gfx.FromCore(def.AccentColor));
            Ui.Text(Tr.Name(def), row.X + 26 * s, row.Y + 9 * s, 17, Theme.Text, true);
            Ui.Text(Tr.Description(def), row.X + 26 * s, row.Y + 34 * s, 13, Theme.Muted);

            if (active && Ui.Down && Ui.Mouse.X > _inventory.X + _inventory.Width)
            {
                Ui.SetActive("spawn-drag");
                _lab.BeginSpawnDrag(def.Id);
            }
            else if (active && Ui.Released && hot)
            {
                var rng = new Random();
                var item = _lab.Spawn(def.Id, new Vector3((float)(rng.NextDouble() * 0.3 - 0.15), 0.15f, (float)(rng.NextDouble() * 0.2 - 0.15)));
                _lab.Selected = item;
            }
        }

        // ------------------------------------------------------------------ live data

        private void DrawData()
        {
            float s = Ui.S;
            var r = _data;
            Ui.Panel(r, Theme.Panel);
            Ui.Text("CANLI VERİ", r.X + 16 * s, r.Y + 18 * s, 20, Theme.Text, true);
            float bx = r.X + r.Width - 12 * s;
            Rectangle Btn(float width) { bx -= width; var b = new Rectangle(bx, r.Y + 12 * s, width, 38 * s); bx -= 6 * s; return b; }
            if (Ui.Button("csv", Btn(96 * s), "Kaydet", null, 14)) ExportCsv();
            if (Ui.Button("clear", Btn(90 * s), "Temizle", null, 14)) _lab.World.Logger.Clear();
            if (Ui.Button("window", Btn(76 * s), _window > 0 ? "2 dk" : "Tümü", null, 14)) _window = _window > 0 ? 0 : 120;

            var channels = _lab.World.Logger.Channels;
            var area = new Rectangle(r.X + 10 * s, r.Y + 62 * s, r.Width - 20 * s, r.Height - 72 * s);
            if (channels.Count == 0)
            {
                Ui.Wrapped("Tezgâhta sensör yok.\nBir termometreyi veya pH probunu bir kabın içine bırakın; ölçümler burada canlı çizilir.",
                    area.X + 8 * s, area.Y + 8 * s, area.Width - 16 * s, 15, Theme.Muted);
                return;
            }

            if (Ui.IsOver(area)) _dataScroll -= GetMouseWheelMove() * 80 * s;
            float cardH = 270 * s;
            _dataScroll = Math.Clamp(_dataScroll, 0, Math.Max(0, channels.Count * (cardH + 10 * s) - area.Height));
            BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
            for (int i = 0; i < channels.Count; i++)
            {
                DrawCard(channels[i], new Rectangle(area.X, area.Y + i * (cardH + 10 * s) - _dataScroll, area.Width, cardH), Theme.Series[i % Theme.Series.Length]);
            }

            EndScissorMode();
        }

        private string ChannelTitle(DataChannel ch)
        {
            var owner = _lab.World.Find(ch.OwnerInstanceId);
            string quantity = ch.Unit == "pH" ? "pH" : "Sıcaklık";
            return owner != null ? $"{quantity} · {Tr.Name(owner)}" : quantity;
        }

        private void DrawCard(DataChannel ch, Rectangle r, Color color)
        {
            float s = Ui.S;
            var ci = CultureInfo.CurrentCulture;
            DrawRectangleRounded(r, 0.06f, 8, Theme.Raised);
            Ui.Text(ChannelTitle(ch), r.X + 14 * s, r.Y + 12 * s, 14, Theme.Muted);
            var series = ch.Series;
            string value = series.Count > 0 ? string.Format(ci, "{0:0.0} {1}", series.LastValue, ch.Unit) : "--";
            var vm = Ui.Measure(value, 30, true);
            Ui.Text(value, r.X + r.Width - vm.X - 14 * s, r.Y + 30 * s, 30, color, true);

            double now = _lab.World.Time;
            double xMax = _window > 0 ? Math.Max(_window, now) : Math.Max(10, now);
            double xMin = _window > 0 ? Math.Max(0, xMax - _window) : 0;
            double yMin = ch.SuggestedMin, yMax = ch.SuggestedMax;
            if (series.TryGetRange(xMin, out double lo, out double hi))
            {
                yMin = Math.Min(yMin, lo);
                yMax = Math.Max(yMax, hi);
            }

            double tick = NiceStep((yMax - yMin) / 4);
            yMin = Math.Floor(yMin / tick) * tick;
            yMax = Math.Ceiling(yMax / tick) * tick;
            int ticks = (int)Math.Round((yMax - yMin) / tick);

            var plot = new Rectangle(r.X + 52 * s, r.Y + 74 * s, r.Width - 66 * s, r.Height - 106 * s);
            for (int i = 0; i <= ticks; i++)
            {
                float gy = plot.Y + plot.Height * i / ticks;
                DrawLineEx(new Vector2(plot.X, gy), new Vector2(plot.X + plot.Width, gy), 1, Theme.Grid);
                double v = yMax - tick * i;
                var label = string.Format(ci, "{0:0}", v);
                Ui.Text(label, plot.X - Ui.Measure(label, 12).X - 8 * s, gy - 8 * s, 12, Theme.Muted);
            }

            for (int i = 0; i <= 6; i++)
            {
                float gx = plot.X + plot.Width * i / 6f;
                DrawLineEx(new Vector2(gx, plot.Y), new Vector2(gx, plot.Y + plot.Height), 1, Theme.Grid);
            }

            Ui.Text(string.Format(ci, "{0:0}", xMin), plot.X, plot.Y + plot.Height + 6 * s, 12, Theme.Muted);
            var endLabel = string.Format(ci, "{0:0} s", xMax);
            Ui.Text(endLabel, plot.X + plot.Width - Ui.Measure(endLabel, 12).X, plot.Y + plot.Height + 6 * s, 12, Theme.Muted);
            Ui.Text("zaman (s)", plot.X + plot.Width / 2 - 30 * s, plot.Y + plot.Height + 6 * s, 12, Theme.Muted);

            if (series.Count < 2) return;
            Vector2 Map(double t, double v) => new Vector2(
                plot.X + (float)((t - xMin) / (xMax - xMin)) * plot.Width,
                plot.Y + plot.Height - (float)((v - yMin) / (yMax - yMin)) * plot.Height);
            int first = 0;
            while (first < series.Count - 1 && series.TimeAt(first + 1) < xMin) first++;
            int step = Math.Max(1, (series.Count - first) / Math.Max(32, (int)plot.Width));
            Vector2 prev = Map(series.TimeAt(first), series.ValueAt(first));
            for (int i = first + step; i < series.Count; i += step)
            {
                var p = Map(series.TimeAt(i), series.ValueAt(i));
                DrawLineEx(prev, p, 3 * s, color);
                prev = p;
            }

            var last = Map(series.LastTime, series.LastValue);
            DrawLineEx(prev, last, 3 * s, color);
            DrawCircleV(last, 6 * s, color);
        }

        private static double NiceStep(double raw)
        {
            if (raw <= 0) return 1;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double n = raw / mag;
            return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : 10) * mag;
        }

        private void ExportCsv()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IDLab");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"IDLab-veri-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                var sb = new StringBuilder("kanal;zaman_s;deger;birim\n");
                foreach (var ch in _lab.World.Logger.Channels)
                {
                    for (int i = 0; i < ch.Series.Count; i++)
                    {
                        sb.Append(ChannelTitle(ch)).Append(';')
                          .Append(ch.Series.TimeAt(i).ToString("0.00", CultureInfo.CurrentCulture)).Append(';')
                          .Append(ch.Series.ValueAt(i).ToString("0.000", CultureInfo.CurrentCulture)).Append(';')
                          .Append(ch.Unit).Append('\n');
                    }
                }

                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
                Toast("Veri kaydedildi: " + file, Theme.Success);
            }
            catch (Exception ex)
            {
                Toast("Veri kaydedilemedi: " + ex.Message, Theme.Danger);
            }
        }

        // ------------------------------------------------------------------ micro view

        private void DrawMicro()
        {
            float s = Ui.S;
            var r = _microRect;
            Ui.Panel(r, Theme.Panel);
            string title = _micro.Container != null ? "MİKRO GÖRÜNÜM · " + Tr.Name(_micro.Container.Owner) : "MİKRO GÖRÜNÜM";
            Ui.Text(title, r.X + 14 * s, r.Y + 10 * s, 14, Theme.Text, true);
            var tex = _micro.Texture;
            var dest = new Rectangle(r.X + 10 * s, r.Y + 36 * s, r.Width - 20 * s, (r.Width - 20 * s) * 460f / 640f);
            DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, -tex.Height), dest, Vector2.Zero, 0, Color.White);

            float ly = dest.Y + dest.Height + 8 * s, lx = r.X + 14 * s;
            foreach (var sp in _micro.State.Species)
            {
                string label = $"{sp.Id} ×{sp.Count}";
                var m = Ui.Measure(label, 13, true);
                if (lx + m.X + 22 * s > r.X + r.Width) { lx = r.X + 14 * s; ly += 20 * s; }
                DrawCircleV(new Vector2(lx + 6 * s, ly + 8 * s), 6 * s, Gfx.FromCore(sp.Color));
                Ui.Text(label, lx + 16 * s, ly, 13, Theme.Text, true);
                lx += m.X + 30 * s;
            }

            Ui.Wrapped(_micro.Caption(), r.X + 14 * s, ly + 24 * s, r.Width - 28 * s, 12, Theme.Muted);
        }

        // ------------------------------------------------------------------ inspector

        private void DrawInspector()
        {
            float s = Ui.S;
            var r = _inspector;
            Ui.Panel(r, Theme.Panel);
            var item = _lab.Selected;
            var ci = CultureInfo.CurrentCulture;
            if (item == null)
            {
                var e = _lab.Experiment;
                Ui.Text(Tr.Title(e), r.X + 18 * s, r.Y + 14 * s, 20, Theme.Text, true);
                float y = r.Y + 46 * s;
                y += Ui.Wrapped(Tr.Summary(e), r.X + 18 * s, y, r.Width - 36 * s, 14, Theme.Muted);
                var objectives = Tr.Objectives(e);
                for (int i = 0; i < objectives.Length; i++)
                {
                    y += 4 * s + Ui.Wrapped($"{i + 1}. {objectives[i]}", r.X + 18 * s, y + 4 * s, r.Width - 36 * s, 15, Theme.Text);
                }

                Ui.Wrapped("İpucu: Seçmek için bir malzemeye dokunun. Açıyı çevirmek için boş alanı sürükleyin.",
                    r.X + 18 * s, r.Y + r.Height - 46 * s, r.Width - 36 * s, 13, Theme.Accent);
                return;
            }

            Ui.Text(Tr.Name(item.Inst), r.X + 18 * s, r.Y + 14 * s, 20, Theme.Text, true);
            var lines = new List<string>();
            if (item.Container != null)
            {
                var c = item.Container;
                lines.Add(string.Format(ci, "Hacim: {0:0.0} mL  (kapasite {1:0} mL)", c.VolumeMilliliters, c.CapacityLiters * 1000));
                lines.Add(string.Format(ci, "Sıcaklık: {0:0.0} °C{1}", c.TemperatureC, c.IsBoiling ? "  · kaynıyor" : ""));
                lines.Add(double.IsNaN(c.PH) ? "pH: --" : string.Format(ci, "pH: {0:0.00}", c.PH));
                var solutes = c.Contents.Components.Where(kv => kv.Key != "water").OrderByDescending(kv => kv.Value).Take(3)
                    .Select(kv => string.Format(ci, "{0} {1:0.#} mmol", c.Contents.Database.Get(kv.Key).Formula, kv.Value * 1000)).ToList();
                if (solutes.Count > 0) lines.Add("Çözünenler: " + string.Join(", ", solutes));
                if (c.IsPouring)
                {
                    lines.Add(string.Format(ci, "Döküyor: {0:0} mL/s → {1}", c.PourRateLitersPerSecond * 1000,
                        c.PourTarget != null ? Tr.Name(c.PourTarget) : "tezgâha (dökülüyor!)"));
                }
            }
            else if (item.Heat != null)
            {
                var hs = item.Heat;
                lines.Add(string.Format(ci, "Gaz vanası: %{0:0}", hs.GasValve * 100));
                lines.Add(hs.IsLit ? string.Format(ci, "Alev gücü: {0:0} W", hs.FlamePowerW) : "Alev: kapalı");
                lines.Add(hs.HeatedContainer != null
                    ? string.Format(ci, "Isıtılan: {0} (+{1:0} W)", Tr.Name(hs.HeatedContainer.Owner), hs.DeliveredPowerW)
                    : "Isıtılan: yok · tel kafese bir kap koyun");
            }
            else if (item.Probe != null)
            {
                var p = item.Probe;
                lines.Add(double.IsNaN(p.Reading) ? "Ölçüm: --"
                    : p.Quantity == ProbeQuantity.Temperature ? string.Format(ci, "Ölçüm: {0:0.0} °C", p.Reading) : string.Format(ci, "Ölçüm: pH {0:0.00}", p.Reading));
                lines.Add(p.ImmersedIn != null ? "Daldırıldığı kap: " + Tr.Name(p.ImmersedIn.Owner) : "Ucunu bir kabın içine bırakın.");
                lines.Add(string.Format(ci, "Tepki süresi: τ = {0:0.0} s", p.TimeConstantS));
            }

            float ly = r.Y + 48 * s;
            foreach (var line in lines)
            {
                ly += Ui.Wrapped(line, r.X + 18 * s, ly, r.Width - 36 * s, 15, Theme.Text, false, 1.3f);
            }

            // Controls row
            float cy = r.Y + r.Height - 62 * s, ch = 48 * s;
            float right = r.X + r.Width - 16 * s;
            if (Ui.Button("remove", new Rectangle(right - 104 * s, cy, 104 * s, ch), "Kaldır", Gfx.Lerp(Theme.Danger, Theme.Button, 0.45f), 16))
            {
                _lab.Remove(item);
                _lab.Selected = null;
                return;
            }

            right -= 116 * s;
            if (item.Heat != null)
            {
                Ui.Text(string.Format(ci, "Gaz %{0:0}", item.Heat.GasValve * 100), r.X + 18 * s, cy + 14 * s, 15, Theme.Muted, true);
                item.Heat.GasValve = Ui.Slider("gas", new Rectangle(r.X + 112 * s, cy, right - r.X - 130 * s, ch), (float)item.Heat.GasValve, 0, 1, Theme.Accent);
            }
            else if (item.Container != null)
            {
                if (Ui.Button("empty", new Rectangle(right - 104 * s, cy, 104 * s, ch), "Boşalt", null, 16)) item.Container.Contents.Clear();
                right -= 116 * s;
                if (item.Host == null)
                {
                    Ui.Text(string.Format(ci, "Dök {0:0}°", item.Tilt), r.X + 18 * s, cy + 14 * s, 15, Theme.Muted, true);
                    item.Tilt = Ui.Slider("tilt", new Rectangle(r.X + 112 * s, cy, right - r.X - 130 * s, ch), item.Tilt, 0, 130, Theme.Warning);
                }
                else
                {
                    Ui.Wrapped("Dökmek için kabı bekten/kaptan alın.", r.X + 18 * s, cy + 6 * s, right - r.X - 30 * s, 13, Theme.Muted);
                }
            }
        }

        // ------------------------------------------------------------------ camera buttons + toasts

        private void DrawCameraButtons()
        {
            float s = Ui.S, size = 54 * s, gap = 8 * s;
            float x = _lab.Viewport.X + _lab.Viewport.Width - size - 14 * s;
            float y = _lab.Viewport.Y + 16 * s;
            float dt = GetFrameTime();
            var buttons = new (string id, string label, Action act)[]
            {
                ("camL", "◀", () => _lab.Yaw += 60 * dt),
                ("camR", "▶", () => _lab.Yaw -= 60 * dt),
                ("camU", "▲", () => _lab.Pitch += 40 * dt),
                ("camD", "▼", () => _lab.Pitch -= 40 * dt),
                ("camIn", "+", () => _lab.Distance = Math.Clamp(_lab.Distance * (1 - dt * 0.8f), 0.3f, 2.2f)),
                ("camOut", "−", () => _lab.Distance = Math.Clamp(_lab.Distance * (1 + dt * 0.8f), 0.3f, 2.2f)),
            };
            foreach (var b in buttons)
            {
                if (Ui.HoldButton(b.id, new Rectangle(x, y, size, size), b.label)) b.act();
                y += size + gap;
            }

            if (Ui.Button("camReset", new Rectangle(x, y, size, size), "↺", null, 24)) _lab.ResetView();
        }

        private void DrawToasts(float dt)
        {
            float s = Ui.S;
            float width = Math.Min(760 * s, _lab.Viewport.Width - 160 * s);
            float x = _lab.Viewport.X + (_lab.Viewport.Width - width) / 2, y = _top.Height + 14 * s;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.age += dt;
                if (t.age > 5f) { _toasts.RemoveAt(i); continue; }
                _toasts[i] = t;
            }

            foreach (var t in _toasts)
            {
                float a = Math.Clamp((5f - t.age) / 0.6f, 0, 1);
                var r = new Rectangle(x, y, width, 54 * s);
                DrawRectangleRounded(r, 0.3f, 8, Gfx.WithAlpha(Theme.Raised, 0.95f * a));
                DrawRectangleRounded(new Rectangle(r.X + 8 * s, r.Y + 10 * s, 6 * s, r.Height - 20 * s), 1f, 4, Gfx.WithAlpha(t.color, a));
                Ui.Text(t.text, r.X + 26 * s, r.Y + 16 * s, 17, Gfx.WithAlpha(Theme.Text, a), true);
                y += r.Height + 8 * s;
            }
        }
    }
}
