using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Data;
using IDLab.Core.Events;
using IDLab.Core.Model;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    internal enum HudTab { Lab, Chemicals, Periodic, Info }

    /// <summary>
    /// Heads-up display for the smart board: top bar with tabs (Laboratuvar / Periyodik Tablo / Bilgi), and on the
    /// lab tab the equipment inventory (left), live graphs (right), micro view (bottom-left), inspector / experiment
    /// card (bottom-right), camera buttons and toasts. Side panels collapse to thin strips. Every control is a large
    /// touch target; no keyboard is required.
    /// </summary>
    internal sealed class Hud
    {
        private readonly Lab _lab;
        private readonly MicroView _micro;
        private readonly List<(string text, Color color, float age)> _toasts = new List<(string, Color, float)>();
        private readonly DragScroll _inventoryScroll = new DragScroll(), _dataScroll = new DragScroll();
        private readonly Dictionary<string, string> _shortDescriptions = new Dictionary<string, string>();
        private Rectangle _top, _inventory, _data, _microRect, _inspector;
        private bool _showInventory = true, _showData = true, _showMicro = true;
        private float _inventoryContent, _dataContent;
        private int _chip;
        private double _window = 120;
        private float _quitArmed;
        private int _speedIndex = 1;
        private static readonly (string label, float scale)[] Speeds = { ("Durdur", 0), ("1×", 1), ("5×", 5), ("20×", 20) };
        private static readonly (HudTab tab, string label)[] Tabs = { (HudTab.Lab, "Laboratuvar"), (HudTab.Chemicals, "Kimyasallar"), (HudTab.Periodic, "Periyodik Tablo"), (HudTab.Info, "Bilgi") };

        private static readonly (string label, EquipmentCategory[] categories)[] Chips =
        {
            ("Tümü", null),
            ("Cam", new[] { EquipmentCategory.Glassware }),
            ("Isıtıcı", new[] { EquipmentCategory.HeatSource }),
            ("Ölçüm", new[] { EquipmentCategory.Sensor, EquipmentCategory.Measuring }),
            ("Sıvılar", new[] { EquipmentCategory.Reagent }),
            ("İndikatör", new[] { EquipmentCategory.Indicator }),
            ("Katılar", new[] { EquipmentCategory.SolidReagent }),
            ("Çubuklar", new[] { EquipmentCategory.Electrical }),
        };

        public bool QuitRequested { get; private set; }

        /// <summary>Number of ready-to-play experiments (shown in the about card).</summary>
        public int ExperimentCount { get; set; }

        private bool _aboutOpen;

        public bool ModalOpen => _aboutOpen;

        public void OpenAbout() => _aboutOpen = true;
        public HudTab Tab { get; set; } = HudTab.Lab;

        /// <summary>Number of chemicals in the catalogue (shown on the inventory button).</summary>
        public int ChemicalCount { get; set; }

        /// <summary>Screen area below the top bar (used by the periodic-table and info tabs).</summary>
        public Rectangle ContentArea { get; private set; }

        public Hud(Lab lab, MicroView micro)
        {
            _lab = lab;
            _micro = micro;
            lab.Notified += (e, text) =>
            {
                if (e.Type == LabEventType.Info) return;
                Color c = e.Type == LabEventType.Explosion || e.Type == LabEventType.FuseBlown || e.Type == LabEventType.Fire ? Theme.Danger
                    : e.IsHazard || e.Type == LabEventType.Spill ? Theme.Warning : Theme.Accent;
                Toast(text, c);
            };
            lab.AutoPourStarted += (from, to, ml) =>
            {
                string amount = double.IsPositiveInfinity(ml) ? "tümü" : string.Format(CultureInfo.CurrentCulture, "{0:0.#} mL", ml);
                Toast($"{Tr.Name(from.Inst)} → {Tr.Name(to.Inst)}: {amount} dökülüyor", Theme.Accent);
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
            float pad = 10 * s, topH = 66 * s, strip = 48 * s;
            _top = new Rectangle(0, 0, w, topH);
            ContentArea = new Rectangle(0, topH, w, h - topH);
            _inventory = _showInventory
                ? new Rectangle(pad, topH + pad, 344 * s, h - topH - 2 * pad)
                : new Rectangle(pad, topH + pad, strip, 230 * s);
            _data = _showData
                ? new Rectangle(w - 460 * s - pad, topH + pad, 460 * s, h - topH - 2 * pad)
                : new Rectangle(w - strip - pad, topH + pad, strip, 200 * s);
            float freeLeft = _inventory.Right(), freeRight = _data.X;
            _microRect = _showMicro
                ? new Rectangle(freeLeft + 12 * s, h - 392 * s - 12 * s, 380 * s, 392 * s)
                : new Rectangle(freeLeft + 12 * s, h - 54 * s - 12 * s, 270 * s, 54 * s);
            float inspW = Math.Min(600 * s, freeRight - freeLeft - 36 * s - (_showMicro ? _microRect.Width : 0));
            inspW = Math.Max(inspW, 380 * s);
            float inspH = 290 * s;
            _inspector = new Rectangle(freeRight - inspW - 12 * s, h - inspH - 12 * s, inspW, inspH);
            _lab.Viewport = new Rectangle(freeLeft, topH, freeRight - freeLeft, h - topH - 220 * s);
        }

        public bool IsOverInventory(Vector2 p) => Tab == HudTab.Lab && _showInventory && CheckCollisionPointRec(p, _inventory);

        // ------------------------------------------------------------------ draw

        public void Draw(float dt)
        {
            if (Tab == HudTab.Lab)
            {
                DrawInventory(dt);
                DrawData(dt);
                DrawMicro();
                DrawInspector();
                DrawCameraButtons();
            }

            DrawTopBar();
            DrawToasts(dt);
            if (_aboutOpen)
            {
                Ui.Suspended = false;
                _aboutOpen = Brand.DrawAbout(ChemicalCount, ExperimentCount);
            }
            _quitArmed = Math.Max(0, _quitArmed - dt);
        }

        private void DrawTopBar()
        {
            float s = Ui.S;
            DrawRectangleRec(_top, Theme.TopBar);
            DrawRectangle(0, (int)_top.Height - 1, (int)_top.Width, Math.Max(1, (int)s), Theme.Border);
            Ui.Block(_top);
            float y = 11 * s, bh = 44 * s, x = 18 * s;
            // Logo + name; tapping it opens the about card.
            var logoRect = new Rectangle(x - 4 * s, 9 * s, 48 * s, 48 * s);
            Brand.DrawLogo(logoRect);
            Ui.Text("ID Lab", x + 52 * s, 18 * s, 25, Theme.Text, true);
            if (Ui.Button("about", new Rectangle(x - 6 * s, 6 * s, 150 * s, 54 * s), "", new Color(0, 0, 0, 0), 1))
            {
                _aboutOpen = true;
            }

            Brand.DrawLogo(logoRect);
            Ui.Text("ID Lab", x + 52 * s, 18 * s, 25, Theme.Text, true);
            x += 156 * s;

            // Tabs.
            foreach (var (tab, label) in Tabs)
            {
                float tw = Ui.Measure(label, 17, true).X + 40 * s;
                var r = new Rectangle(x, y, tw, bh);
                bool on = Tab == tab;
                if (Ui.Button("tab-" + tab, r, label, on ? Gfx.Lerp(Theme.Accent, Theme.Button, 0.55f) : Theme.Button, 17)) Tab = tab;
                if (on) DrawRectangleRounded(new Rectangle(r.X + 14 * s, r.Y + r.Height + 4 * s, r.Width - 28 * s, 4 * s), 1f, 4, Theme.Accent);
                x += tw + 8 * s;
            }

            x += 16 * s;

            // Right side, laid out from the right edge.
            float rx = _top.Width - 14 * s;
            Rectangle Next(float width) { rx -= width; var r = new Rectangle(rx, y, width, bh); rx -= 8 * s; return r; }

            if (Ui.Button("quit", Next(_quitArmed > 0 ? 140 * s : 96 * s), _quitArmed > 0 ? "Emin misiniz?" : "Kapat", _quitArmed > 0 ? Theme.Danger : (Color?)null))
            {
                if (_quitArmed > 0) QuitRequested = true;
                else _quitArmed = 3f;
            }

            if (Ui.Button("full", Next(118 * s), IsWindowState(ConfigFlags.BorderlessWindowMode) ? "Pencere" : "Tam ekran", null, 16))
            {
                ToggleBorderlessWindowed();
                PointerCursor.Ensure();
            }

            if (Ui.Button("quality", Next(122 * s), Quality.ShortLabel, null, 15))
            {
                Quality.Cycle();
                if (Quality.TakeMsaaNotice()) Toast("Yüksek kalitenin kenar yumuşatması (MSAA) uygulama yeniden açılınca devreye girer.", Theme.Warning);
                Toast(Quality.Mode == QualityMode.Auto ? "Grafik kalitesi tahtanın hızına göre otomatik ayarlanır."
                    : Quality.Mode == QualityMode.High ? "Yüksek kalite: en keskin görüntü (güçlü bilgisayarlar için)."
                    : Quality.Mode == QualityMode.Balanced ? "Dengeli: 3B sahne biraz daha düşük çözünürlükte çizilir."
                    : "Hızlı: yavaş tahtalarda en akıcı çalışma.", Theme.Accent);
            }

            if (Ui.Button("theme", Next(52 * s), Theme.Dark ? "☀" : "☾", null, 22))
            {
                Theme.Apply(!Theme.Dark);
                Settings.Set("tema", Theme.Dark ? "koyu" : "acik");
            }

            if (Tab != HudTab.Lab)
            {
                string hint = Tab == HudTab.Periodic ? "Bir elemente dokunun · Döndürmek için sürükleyin"
                    : Tab == HudTab.Chemicals ? "Arayın, seçin, tezgâha ekleyin"
                    : "Konu seçin · Kaydırmak için sürükleyin";
                var hm = Ui.Measure(hint, 15);
                if (rx - x > hm.X + 20 * s) Ui.Text(hint, rx - hm.X - 12 * s, y + (bh - hm.Y) / 2, 15, Theme.Muted);
                return;
            }

            rx -= 8 * s;
            if (Ui.Button("reset", Next(96 * s), "Sıfırla", Theme.AccentDim)) _lab.LoadExperiment(_lab.Experiment.Id);
            // The clock gives way to the experiment selector on narrow screens.
            if (rx - x > 980 * s)
            {
                var clockRect = Next(118 * s);
                string clock = string.Format(CultureInfo.CurrentCulture, "t = {0:0.0} s", _lab.World.Time);
                Ui.TextCentered(_lab.World.Clock.Paused ? clock + " ‖" : clock, clockRect, 17, Theme.Muted);
            }
            for (int i = Speeds.Length - 1; i >= 0; i--)
            {
                if (Ui.Button("speed" + i, Next(i == 0 ? 92 * s : 60 * s), Speeds[i].label, null, 16, _speedIndex == i)) SetSpeed(i);
            }

            // Experiment selector fills the middle.
            var playable = _lab.Experiments.All.Where(e => e.Status == ExperimentStatus.Playable).ToList();
            int index = playable.IndexOf(_lab.Experiment);
            float titleW = rx - x - 2 * bh - 24 * s;
            if (titleW < 120 * s) return;
            if (Ui.Button("expPrev", new Rectangle(x, y, bh, bh), "◀")) _lab.LoadExperiment(playable[(index - 1 + playable.Count) % playable.Count].Id);
            var titleRect = new Rectangle(x + bh + 8 * s, y, titleW, bh);
            DrawRectangleRounded(titleRect, 0.35f, 8, Theme.Raised);
            string title = Tr.Title(_lab.Experiment);
            float size = Ui.Measure(title, 18, true).X < titleW - 20 * s ? 18 : 14;
            Ui.TextCentered(title, titleRect, size, Theme.Text, true);
            if (Ui.Button("expNext", new Rectangle(titleRect.Right() + 8 * s, y, bh, bh), "▶")) _lab.LoadExperiment(playable[(index + 1) % playable.Count].Id);
        }

        /// <summary>A collapsed side panel: a tall button with vertical text.</summary>
        private static bool Strip(string id, Rectangle r, string label, string arrow)
        {
            float s = Ui.S;
            bool clicked = Ui.Button(id, r, "", Theme.Panel);
            Ui.TextCentered(arrow, new Rectangle(r.X, r.Y + 8 * s, r.Width, 30 * s), 18, Theme.Accent, true);
            Ui.VerticalText(label, new Rectangle(r.X, r.Y + 40 * s, r.Width, r.Height - 48 * s), 15, Theme.Text);
            return clicked;
        }

        // ------------------------------------------------------------------ inventory

        private void DrawInventory(float dt)
        {
            float s = Ui.S;
            var r = _inventory;
            if (!_showInventory)
            {
                if (Strip("inv-open", r, "MALZEMELER", "▶")) _showInventory = true;
                return;
            }

            Ui.Panel(r, Theme.Panel);
            Ui.Text("MALZEMELER", r.X + 16 * s, r.Y + 16 * s, 20, Theme.Text, true);
            if (Ui.Button("inv-close", new Rectangle(r.Right() - 52 * s, r.Y + 10 * s, 42 * s, 38 * s), "◀", null, 16)) _showInventory = false;
            Ui.Text("Sürükleyin ya da dokunun · geri sürükleyerek kaldırın", r.X + 16 * s, r.Y + 46 * s, 12, Theme.Muted);
            if (Ui.Button("inv-chem", new Rectangle(r.X + 12 * s, r.Y + 68 * s, r.Width - 24 * s, 44 * s), $"Kimyasal dolabı ({ChemicalCount} kimyasal) →",
                Gfx.Lerp(Theme.Success, Theme.Button, 0.55f), 15))
            {
                Tab = HudTab.Chemicals;
            }

            // Category chips (only those with something to show).
            var all = _lab.Inventory().ToList();
            float cx = r.X + 12 * s, cy = r.Y + 122 * s, chipH = 38 * s;
            for (int i = 0; i < Chips.Length; i++)
            {
                var cats = Chips[i].categories;
                if (cats != null && !all.Any(d => cats.Contains(d.Category))) continue;
                float cw = Ui.Measure(Chips[i].label, 14, true).X + 24 * s;
                if (cx + cw > r.Right() - 12 * s)
                {
                    cx = r.X + 12 * s;
                    cy += chipH + 6 * s;
                }

                if (Ui.Button("chip" + i, new Rectangle(cx, cy, cw, chipH), Chips[i].label, null, 14, _chip == i))
                {
                    _chip = i;
                    _inventoryScroll.Reset();
                }

                cx += cw + 6 * s;
            }

            var chipCats = Chips[_chip].categories;
            var defs = chipCats == null ? all : all.Where(d => chipCats.Contains(d.Category)).ToList();
            if (defs.Count == 0)
            {
                _chip = 0;
                defs = all;
            }

            var list = new Rectangle(r.X + 8 * s, cy + chipH + 10 * s, r.Width - 16 * s, r.Bottom() - (cy + chipH + 18 * s));
            _inventoryScroll.Update(list, _inventoryContent, dt);
            BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
            float y = list.Y - _inventoryScroll.Offset;
            EquipmentCategory? last = null;
            foreach (var def in defs.OrderBy(d => d.Category))
            {
                if (chipCats == null && last != def.Category)
                {
                    Ui.Text(Tr.Category(def.Category), list.X + 8 * s, y + 8 * s, 12, Theme.Accent, true);
                    y += 30 * s;
                    last = def.Category;
                }

                var row = new Rectangle(list.X, y, list.Width, 76 * s);
                if (row.Bottom() > list.Y && row.Y < list.Bottom()) DrawInventoryRow(def, row, list);
                y += 82 * s;
            }

            _inventoryContent = y + _inventoryScroll.Offset - list.Y;
            EndScissorMode();
        }

        private string ShortDescription(EquipmentDefinition def, float width)
        {
            string key = def.Id + "@" + (int)width;
            if (_shortDescriptions.TryGetValue(key, out var text)) return text;
            text = Tr.Description(def);
            float limit = 13 * Ui.S * 1.25f * 2 + 1;
            while (text.Length > 4 && Ui.Wrapped(text, 0, 0, width, 13, Theme.Muted, false, 1.25f, draw: false) > limit)
            {
                int cut = text.LastIndexOf(' ', Math.Max(0, text.Length - 3));
                text = (cut > 0 ? text.Substring(0, cut) : text.Substring(0, text.Length - 4)).TrimEnd(',', '.', ';', ' ') + "…";
            }

            _shortDescriptions[key] = text;
            return text;
        }

        private void DrawInventoryRow(EquipmentDefinition def, Rectangle row, Rectangle clip)
        {
            float s = Ui.S;
            string id = "inv:" + def.Id;
            bool hot = Ui.IsOver(row) && Ui.IsOver(clip);
            if (Ui.Pressed && hot) Ui.SetActive(id);
            if (Ui.Active == id && _inventoryScroll.Dragging) Ui.SetActive("inv-scroll"); // vertical drag = scroll, not spawn
            bool active = Ui.Active == id;
            DrawRectangleRounded(row, 0.2f, 8, active ? Gfx.Lerp(Theme.Raised, Theme.Accent, 0.25f) : hot ? Gfx.Lerp(Theme.Raised, Color.White, 0.05f) : Theme.Raised);
            var thumb = new Rectangle(row.X + 6 * s, row.Y + 6 * s, row.Height - 12 * s, row.Height - 12 * s);
            Thumbnails.Draw(def.Id, thumb);
            DrawRectangleRounded(new Rectangle(thumb.Right() + 8 * s, row.Y + 14 * s, 4 * s, row.Height - 28 * s), 1f, 4, Gfx.FromCore(def.AccentColor));
            float tx = thumb.Right() + 20 * s, tw = row.Right() - tx - 10 * s;
            Ui.Text(Tr.Name(def), tx, row.Y + 9 * s, 16, Theme.Text, true);
            Ui.Wrapped(ShortDescription(def, tw), tx, row.Y + 35 * s, tw, 13, Theme.Muted, false, 1.25f);

            if (active && Ui.Down && Ui.Mouse.X > _inventory.Right())
            {
                Ui.SetActive("spawn-drag");
                _lab.BeginSpawnDrag(def.Id);
            }
            else if (active && Ui.Released && hot && !_inventoryScroll.Dragging)
            {
                var item = _lab.Spawn(def.Id, _lab.FreeSpot() + new Vector3(0, 0.03f, 0));
                _lab.Selected = item;
            }
        }

        // ------------------------------------------------------------------ live data

        private void DrawData(float dt)
        {
            float s = Ui.S;
            var r = _data;
            if (!_showData)
            {
                if (Strip("data-open", r, "CANLI VERİ", "◀")) _showData = true;
                return;
            }

            Ui.Panel(r, Theme.Panel);
            if (Ui.Button("data-close", new Rectangle(r.X + 10 * s, r.Y + 12 * s, 42 * s, 38 * s), "▶", null, 16)) _showData = false;
            Ui.Text("CANLI VERİ", r.X + 62 * s, r.Y + 20 * s, 19, Theme.Text, true);
            float bx = r.Right() - 12 * s;
            Rectangle Btn(float width) { bx -= width; var b = new Rectangle(bx, r.Y + 12 * s, width, 38 * s); bx -= 6 * s; return b; }
            if (Ui.Button("csv", Btn(86 * s), "Kaydet", null, 14)) ExportCsv();
            if (Ui.Button("clear", Btn(84 * s), "Temizle", null, 14)) _lab.World.Logger.Clear();
            if (Ui.Button("window", Btn(66 * s), _window > 0 ? "2 dk" : "Tümü", null, 14)) _window = _window > 0 ? 0 : 120;

            var channels = _lab.World.Logger.Channels;
            var area = new Rectangle(r.X + 10 * s, r.Y + 62 * s, r.Width - 20 * s, r.Height - 72 * s);
            var curves = _lab.Titrations.Where(kv => kv.Value.Points.Count >= 2 && _lab.World.Find(kv.Key) != null).Select(kv => kv.Value).ToList();
            if (channels.Count == 0 && curves.Count == 0)
            {
                Ui.Wrapped("Tezgâhta sensör yok.\nBir termometreyi ya da pH probunu bir kabın içine bırakın, kabı teraziye koyun veya büreti kullanın; ölçümler burada canlı çizilir.",
                    area.X + 8 * s, area.Y + 8 * s, area.Width - 16 * s, 15, Theme.Muted);
                return;
            }

            float cardH = 250 * s, curveH = 330 * s;
            _dataContent = curves.Count * (curveH + 10 * s) + channels.Count * (cardH + 10 * s);
            _dataScroll.Update(area, _dataContent, dt);
            BeginScissorMode((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
            float cy = area.Y - _dataScroll.Offset;
            foreach (var curve in curves)
            {
                DrawTitrationCard(curve, new Rectangle(area.X, cy, area.Width, curveH));
                cy += curveH + 10 * s;
            }

            for (int i = 0; i < channels.Count; i++)
            {
                DrawCard(channels[i], new Rectangle(area.X, cy, area.Width, cardH), Theme.Series[i % Theme.Series.Length]);
                cy += cardH + 10 * s;
            }

            EndScissorMode();
        }

        private static string Quantity(DataChannel ch)
        {
            switch (ch.Unit)
            {
                case "pH": return "pH";
                case "°C": return "Sıcaklık";
                case "g": return "Kütle";
                case "mL": return "Verilen hacim";
                case "A": return "Akım";
                default: return ch.Label;
            }
        }

        private string ChannelTitle(DataChannel ch)
        {
            var owner = _lab.World.Find(ch.OwnerInstanceId);
            string quantity = Quantity(ch);
            return owner != null ? $"{quantity} · {Tr.Name(owner)}" : quantity;
        }

        private void DrawCard(DataChannel ch, Rectangle r, Color color)
        {
            float s = Ui.S;
            var ci = CultureInfo.CurrentCulture;
            DrawRectangleRounded(r, 0.06f, 8, Theme.Raised);
            Ui.Text(ChannelTitle(ch), r.X + 14 * s, r.Y + 12 * s, 14, Theme.Muted);
            var series = ch.Series;
            string format = ch.Unit == "g" || ch.Unit == "pH" ? "{0:0.00} {1}" : "{0:0.0} {1}";
            string value = series.Count > 0 ? string.Format(ci, format, series.LastValue, ch.Unit) : "--";
            var vm = Ui.Measure(value, 28, true);
            Ui.Text(value, r.X + r.Width - vm.X - 14 * s, r.Y + 30 * s, 28, color, true);

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
            int ticks = Math.Max(1, (int)Math.Round((yMax - yMin) / tick));

            var plot = new Rectangle(r.X + 52 * s, r.Y + 70 * s, r.Width - 66 * s, r.Height - 100 * s);
            for (int i = 0; i <= ticks; i++)
            {
                float gy = plot.Y + plot.Height * i / ticks;
                DrawLineEx(new Vector2(plot.X, gy), new Vector2(plot.X + plot.Width, gy), 1, Theme.Grid);
                double v = yMax - tick * i;
                var label = string.Format(ci, tick < 1 ? "{0:0.#}" : "{0:0}", v);
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

        /// <summary>
        /// Equivalence point of a titration curve: the middle of the steepest rise, accepted only when the curve
        /// really jumps there (≥ 2 pH units/mL and passes neutral). Returns NaN before the jump.
        /// </summary>
        internal static double EquivalencePoint(IReadOnlyList<(double ml, double ph)> pts)
        {
            double best = 0, at = double.NaN;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                // Compare over ≥ 0.1 mL so single noisy samples do not win.
                int j = i + 1;
                while (j < pts.Count - 1 && pts[j].ml - pts[i].ml < 0.1) j++;
                double dv = pts[j].ml - pts[i].ml;
                if (dv < 0.02) continue;
                double slope = (pts[j].ph - pts[i].ph) / dv;
                if (slope > best && pts[i].ph < 8.5 && pts[j].ph > 5.5)
                {
                    best = slope;
                    at = (pts[i].ml + pts[j].ml) / 2;
                }
            }

            return best >= 2 ? at : double.NaN;
        }

        private void DrawTitrationCard(Lab.TitrationCurve curve, Rectangle r)
        {
            float s = Ui.S;
            var ci = CultureInfo.CurrentCulture;
            var pts = curve.Points;
            DrawRectangleRounded(r, 0.06f, 8, Theme.Raised);
            Ui.Text("Titrasyon eğrisi · pH – eklenen NaOH", r.X + 14 * s, r.Y + 12 * s, 14, Theme.Muted);
            var last = pts[pts.Count - 1];
            string value = string.Format(ci, "pH {0:0.00}  ·  {1:0.00} mL", last.ph, last.ml);
            var vm = Ui.Measure(value, 22, true);
            Ui.Text(value, r.Right() - vm.X - 14 * s, r.Y + 32 * s, 22, Theme.Series[0], true);

            double eq = EquivalencePoint(pts);
            var plot = new Rectangle(r.X + 44 * s, r.Y + 66 * s, r.Width - 58 * s, r.Height - 136 * s);
            double xMax = Math.Max(5, Math.Ceiling(Math.Max(last.ml, double.IsNaN(eq) ? 0 : eq * 1.3) / 5) * 5);
            Vector2 Map(double ml, double ph) => new Vector2(
                plot.X + (float)(ml / xMax) * plot.Width,
                plot.Y + plot.Height - (float)(Math.Clamp(ph, 0, 14) / 14) * plot.Height);

            // Phenolphthalein's colour-change range (pH 8.2–10).
            var band = new Rectangle(plot.X, Map(0, 10).Y, plot.Width, Map(0, 8.2).Y - Map(0, 10).Y);
            DrawRectangleRec(band, new Color(236, 72, 153, 40));
            Ui.Text("fenolftalein", plot.Right() - Ui.Measure("fenolftalein", 11).X - 4 * s, band.Y + 2 * s, 11, new Color(236, 120, 170, 255));
            for (int ph = 0; ph <= 14; ph += 2)
            {
                float gy = Map(0, ph).Y;
                DrawLineEx(new Vector2(plot.X, gy), new Vector2(plot.Right(), gy), 1, Theme.Grid);
                var label = ph.ToString(ci);
                Ui.Text(label, plot.X - Ui.Measure(label, 12).X - 8 * s, gy - 8 * s, 12, Theme.Muted);
            }

            int xTicks = (int)(xMax / 5);
            for (int i = 0; i <= xTicks; i++)
            {
                float gx = Map(i * 5, 0).X;
                DrawLineEx(new Vector2(gx, plot.Y), new Vector2(gx, plot.Bottom()), 1, Theme.Grid);
                var label = i == xTicks ? (i * 5).ToString(ci) + " mL" : (i * 5).ToString(ci);
                float lw = Ui.Measure(label, 12).X;
                Ui.Text(label, i == xTicks ? gx - lw : gx - lw / 2, plot.Bottom() + 4 * s, 12, Theme.Muted);
            }

            var prev = Map(pts[0].ml, pts[0].ph);
            for (int i = 1; i < pts.Count; i++)
            {
                var p = Map(pts[i].ml, pts[i].ph);
                DrawLineEx(prev, p, 3 * s, Theme.Series[0]);
                prev = p;
            }

            DrawCircleV(prev, 6 * s, Theme.Series[0]);

            float ty = plot.Bottom() + 24 * s;
            if (!double.IsNaN(eq))
            {
                float ex = Map(eq, 0).X;
                for (float y = plot.Y; y < plot.Bottom(); y += 8 * s) DrawLineEx(new Vector2(ex, y), new Vector2(ex, Math.Min(y + 4 * s, plot.Bottom())), 2 * s, Theme.Warning);
                string line1 = string.Format(ci, "Dönüm noktası ≈ {0:0.0} mL NaOH", eq);
                Ui.Text(line1, r.X + 14 * s, ty, 15, Theme.Warning, true);
                if (curve.AcidMl > 0.5 && curve.TitrantMolarity > 0)
                {
                    double ca = curve.TitrantMolarity * eq / curve.AcidMl;
                    string line2 = string.Format(ci, "Asit derişimi = M·V / V(asit) = {0:0.###} × {1:0.0} / {2:0.0} ≈ {3:0.000} M", curve.TitrantMolarity, eq, curve.AcidMl, ca);
                    Ui.Text(line2, r.X + 14 * s, ty + 22 * s, 13, Theme.Text);
                }
            }
            else
            {
                Ui.Wrapped("Büretten damla damla ekleyin. pH birden yükselip renk pembeye döndüğünde dönüm noktası burada işaretlenir.",
                    r.X + 14 * s, ty, r.Width - 28 * s, 13, Theme.Muted);
            }
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
            if (Ui.Button("micro-toggle", new Rectangle(r.Right() - 48 * s, r.Y + 8 * s, 38 * s, 38 * s), _showMicro ? "▼" : "▲", null, 15))
            {
                _showMicro = !_showMicro;
            }

            if (!_showMicro)
            {
                Ui.Text("MİKRO GÖRÜNÜM", r.X + 16 * s, r.Y + 17 * s, 15, Theme.Text, true);
                return;
            }

            Ui.Text(title, r.X + 14 * s, r.Y + 14 * s, 14, Theme.Text, true);
            var tex = _micro.Texture;
            var dest = new Rectangle(r.X + 10 * s, r.Y + 52 * s, r.Width - 20 * s, (r.Width - 20 * s) * 460f / 640f);
            DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, -tex.Height), dest, Vector2.Zero, 0, Color.White);

            float ly = dest.Y + dest.Height + 8 * s, lx = r.X + 14 * s;
            foreach (var sp in _micro.State.Species)
            {
                string label = $"{Elements.PrettySpecies(sp.Id)} ×{sp.Count}";
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
                for (int i = 0; i < objectives.Length && y < r.Bottom() - 70 * s; i++)
                {
                    y += 4 * s + Ui.Wrapped($"{i + 1}. {objectives[i]}", r.X + 18 * s, y + 4 * s, r.Width - 36 * s, 15, Theme.Text);
                }

                Ui.Wrapped("İpucu: Seçmek için bir malzemeye dokunun. Açıyı çevirmek için boş alanı sürükleyin. Kimyasal bilgileri için \"Bilgi\" sekmesine bakın.",
                    r.X + 18 * s, r.Y + r.Height - 52 * s, r.Width - 36 * s, 13, Theme.Accent);
                return;
            }

            Ui.Text(Tr.Name(item.Inst), r.X + 18 * s, r.Y + 14 * s, 20, Theme.Text, true);
            var lines = new List<(string text, Color color)>();
            var balance = item.Inst.Get<Balance>();
            var c = item.Container;
            var hazards = Hazard.None;
            if (item.Tap != null && c != null)
            {
                var tap = item.Tap;
                lines.Add((string.Format(ci, "Verilen: {0:0.0} mL   ·   Kalan: {1:0.0} mL", tap.DeliveredMilliliters, c.VolumeMilliliters), Theme.Text));
                lines.Add((tap.PendingMilliliters > 1e-4 ? string.Format(ci, "Damlatılıyor… {0:0.00} mL kaldı", tap.PendingMilliliters)
                    : tap.IsFlowing ? string.Format(ci, "Akış: {0:0.00} mL/s", tap.FlowLitersPerSecond * 1000) : "Musluk kapalı", Theme.Text));
                if (c.VolumeMilliliters < 0.05) lines.Add(("Büret boşaldı. \"Doldur\" ile yeniden 25 mL doldurun.", Theme.Warning));
                else if (tap.Target != null) lines.Add(("Altındaki kap: " + Tr.Name(tap.Target), Theme.Text));
                else lines.Add(("Ucunun altına bir erlenmayer ya da beher koyun.", Theme.Warning));
                if (c.VolumeMilliliters >= 0.05) AddContents(c, lines, ref hazards);
            }
            else if (c != null)
            {
                lines.Add((c.SolidVolumeMilliliters > 0.05
                    ? string.Format(ci, "Sıvı: {0:0.0} mL  ·  Katı: {1:0.0} mL  (kapasite {2:0} mL)", c.LiquidVolumeMilliliters, c.SolidVolumeMilliliters, c.CapacityLiters * 1000)
                    : string.Format(ci, "Hacim: {0:0.0} mL  (kapasite {1:0} mL)", c.VolumeMilliliters, c.CapacityLiters * 1000), Theme.Text));
                string ph = double.IsNaN(c.PH) ? "pH: --" : string.Format(ci, "pH: {0:0.00}", c.PH);
                lines.Add((string.Format(ci, "Sıcaklık: {0:0.0} °C{1}   ·   {2}", c.TemperatureC, c.IsBoiling ? " (kaynıyor)" : "", ph), Theme.Text));
                AddContents(c, lines, ref hazards);
                if (c.GasRateMolPerS > 1e-7 && !string.IsNullOrEmpty(c.GasFormula))
                {
                    lines.Add((string.Format(ci, "Gaz çıkışı: {0} · {1:0.00} mmol/s", Tr.Gas(c.GasFormula), c.GasRateMolPerS * 1000), Theme.Accent));
                }

                if (c.OnFire) lines.Add(("YANIYOR! Söndür düğmesiyle kabın ağzını kapatın.", Theme.Danger));
                if (c.IsPouring)
                {
                    lines.Add((string.Format(ci, "{0}: {1:0.0} mL/s → {2}", c.IsPouringSolid ? "Katı dökülüyor" : "Döküyor", c.PourRateLitersPerSecond * 1000,
                        c.PourTarget != null ? Tr.Name(c.PourTarget) : "tezgâha (dökülüyor!)"), c.PourTarget != null ? Theme.Text : Theme.Warning));
                }
            }
            else if (item.Heat != null)
            {
                var hs = item.Heat;
                string knob = hs.OpenFlame ? "Gaz vanası" : "Güç";
                lines.Add((string.Format(ci, "{0}: %{1:0}", knob, hs.GasValve * 100), Theme.Text));
                lines.Add((hs.IsLit ? string.Format(ci, hs.OpenFlame ? "Alev gücü: {0:0} W" : "Isıtıcı gücü: {0:0} W", hs.FlamePowerW) : hs.OpenFlame ? "Alev: kapalı" : "Isıtıcı: kapalı", Theme.Text));
                lines.Add((hs.HeatedContainer != null
                    ? string.Format(ci, "Isıtılan: {0} (+{1:0} W)", Tr.Name(hs.HeatedContainer.Owner), hs.DeliveredPowerW)
                    : hs.OpenFlame ? "Isıtılan: yok · tel kafese bir kap koyun" : "Isıtılan: yok · tablanın üstüne bir kap koyun", Theme.Muted));
                if (hs.OpenFlame) lines.Add(("Etanol gibi yanıcı sıvıları alevle değil ısıtıcı tablayla ısıtın.", Theme.Warning));
            }
            else if (item.Supply != null)
            {
                AddSupplyLines(item.Supply, lines);
            }
            else if (item.Rod != null)
            {
                AddRodLines(item.Rod, lines);
            }
            else if (item.Probe != null)
            {
                var p = item.Probe;
                lines.Add((double.IsNaN(p.Reading) ? "Ölçüm: --"
                    : p.Quantity == ProbeQuantity.Temperature ? string.Format(ci, "Ölçüm: {0:0.0} °C", p.Reading) : string.Format(ci, "Ölçüm: pH {0:0.00}", p.Reading), Theme.Text));
                lines.Add((p.ImmersedIn != null ? "Daldırıldığı kap: " + Tr.Name(p.ImmersedIn.Owner) : "Ucunu bir kabın içine bırakın.", Theme.Text));
                lines.Add((string.Format(ci, "Tepki süresi: τ = {0:0.0} s", p.TimeConstantS), Theme.Muted));
            }
            else if (balance != null)
            {
                lines.Add((balance.HasLoad ? string.Format(ci, "Okunan: {0:0.00} g", balance.ReadingGrams) : "Kefe boş: üstüne bir kap koyun.", Theme.Text));
                lines.Add((string.Format(ci, "Dara: {0:0.00} g", balance.TareGrams), Theme.Muted));
                lines.Add(("Önce boş kabı koyup \"Dara\"ya basın; sonra eklediğiniz maddenin kütlesini doğrudan okuyun.", Theme.Muted));
            }
            else
            {
                lines.Add((Tr.Description(item.Def), Theme.Muted));
            }

            // Hazard pictograms (top-right).
            float px = r.Right() - 16 * s;
            foreach (var h in Textures.PictogramsFor(hazards))
            {
                px -= 44 * s;
                var pic = Textures.Pictogram(h);
                DrawTexturePro(pic, new Rectangle(0, 0, pic.Width, pic.Height), new Rectangle(px, r.Y + 8 * s, 42 * s, 42 * s), Vector2.Zero, 0, Color.White);
                if (CheckCollisionPointRec(Ui.Mouse, new Rectangle(px, r.Y + 8 * s, 42 * s, 42 * s)))
                {
                    var m = Ui.Measure(Tr.Hazard(h), 14, true);
                    var tip = new Rectangle(px + 21 * s - m.X / 2 - 8 * s, r.Y - 34 * s, m.X + 16 * s, 28 * s);
                    DrawRectangleRounded(tip, 0.4f, 6, Theme.Raised);
                    Ui.TextCentered(Tr.Hazard(h), tip, 14, Theme.Text, true);
                }
            }

            float ly = r.Y + 50 * s, controlsY = r.Bottom() - 62 * s;
            bool quickPour = c != null && item.CanTilt && item.Host == null && item.Tap == null;
            bool doses = item.Tap != null && c != null;
            float linesEnd = quickPour || doses ? controlsY - 58 * s : controlsY;
            foreach (var (text, color) in lines)
            {
                if (ly > linesEnd - 18 * s) break;
                ly += Ui.Wrapped(text, r.X + 18 * s, ly, r.Width - 36 * s, 14, color, false, 1.3f);
            }

            if (quickPour) DrawQuickPour(item, c, new Rectangle(r.X + 16 * s, controlsY - 56 * s, r.Width - 32 * s, 46 * s));
            if (doses) DrawBuretteDoses(item, c, new Rectangle(r.X + 16 * s, controlsY - 56 * s, r.Width - 32 * s, 46 * s));

            DrawControls(item, c, balance, new Rectangle(r.X + 16 * s, controlsY, r.Width - 32 * s, 48 * s));
        }

        private static string ElementTr(IDLab.Core.LabWorld world, string symbol)
        {
            var el = symbol == null ? null : world.Substances.FindElement(symbol);
            return el != null ? Tr.Substance(el) : symbol;
        }

        private static string Milligrams(IDLab.Core.LabWorld world, string symbol, double moles)
        {
            var el = world.Substances.FindElement(symbol);
            double mg = moles * (el?.MolarMass ?? 60) * 1000;
            return string.Format(CultureInfo.CurrentCulture, mg < 10 ? "{0:0.00} mg" : "{0:0.0} mg", mg);
        }

        private void AddRodLines(Rod rod, List<(string, Color)> lines)
        {
            var ci = CultureInfo.CurrentCulture;
            var world = _lab.World;
            if (rod.IsGlass) lines.Add(("Cam baget: tepkimeye girmez, karıştırmak için kullanılır.", Theme.Muted));
            else if (rod.IsGraphite) lines.Add(("Grafit (karbon) elektrot: tepkimeye girmez; elektrolizde akımı iletir.", Theme.Muted));
            else lines.Add((string.Format(ci, "Metal: {0} ({1})", ElementTr(world, rod.Material), rod.Material), Theme.Text));

            if (rod.ImmersedIn != null)
            {
                lines.Add((string.Format(ci, "Daldırıldığı kap: {0} · ıslak boy {1:0.0} cm", Tr.Name(rod.ImmersedIn.Owner), rod.ImmersedLengthM * 100), Theme.Text));
            }
            else
            {
                lines.Add(("Ucunu çözelti dolu bir kabın içine bırakın.", Theme.Warning));
            }

            foreach (var layer in rod.Layers)
            {
                string what = layer.Crystals ? "kristalleri" : "kaplama";
                lines.Add((string.Format(ci, "{0} {1}: {2}{3}", ElementTr(world, layer.Symbol), what, Milligrams(world, layer.Symbol, layer.Moles),
                    layer.Crystals ? "" : string.Format(ci, " · ≈{0:0.00} µm", rod.ThicknessM(layer) * 1e6)), Theme.Accent));
            }

            if (rod.DissolvedMoles > 1e-8)
            {
                lines.Add((string.Format(ci, "Çözünüp çözeltiye geçen {0}: {1}", ElementTr(world, rod.Material).ToLower(Tr.TrCulture), Milligrams(world, rod.Material, rod.DissolvedMoles)), Theme.Text));
            }

            if (rod.Polarity != 0)
            {
                lines.Add((rod.Polarity < 0 ? "Güç kaynağına bağlı: siyah kablo (−) · KATOT: üzeri kaplanır." : "Güç kaynağına bağlı: kırmızı kablo (+) · ANOT: metal çözünür ya da gaz çıkar.",
                    rod.Polarity < 0 ? Theme.Accent : Theme.Warning));
            }
            else if (rod.ImmersedIn != null && rod.Conducts && !rod.IsGraphite && rod.CoatingRateMolPerS <= 1e-12)
            {
                // Explain why nothing happens (activity series).
                var e = IDLab.Core.Chemistry.ActivitySeries.Get(rod.SurfaceSymbol);
                foreach (var kv in rod.ImmersedIn.Contents.Components)
                {
                    var sub = world.Substances.Get(kv.Key);
                    var dep = sub.IsIonic && kv.Value > 1e-8 ? IDLab.Core.Chemistry.ActivitySeries.ForCation(sub.CationKey) : null;
                    if (dep == null || e == null || dep.Symbol == rod.SurfaceSymbol) continue;
                    lines.Add((string.Format(ci, "Kendiliğinden tepkime yok: {0}, {1} iyonlarından daha az aktif (aktiflik sırası).",
                        ElementTr(world, rod.SurfaceSymbol), ElementTr(world, dep.Symbol).ToLower(Tr.TrCulture)), Theme.Muted));
                    break;
                }
            }
        }

        private void AddSupplyLines(PowerSupply ps, List<(string, Color)> lines)
        {
            var ci = CultureInfo.CurrentCulture;
            var world = _lab.World;
            lines.Add((string.Format(ci, "Gerilim: {0:0.0} V   ·   Akım: {1:0.000} A", ps.Voltage, ps.CurrentA), Theme.Text));
            if (ps.Negative == null || ps.Positive == null)
            {
                lines.Add(("Kablolar bağlı değil. İki çubuğu (ör. demir ve bakır) aynı çözeltiye daldırıp gerilimi açın; kablolar kendiliğinden bağlanır.", Theme.Warning));
                return;
            }

            lines.Add(("(−) Katot (kaplanan): " + Tr.Name(ps.Negative) + "   ·   (+) Anot: " + Tr.Name(ps.Positive), Theme.Text));
            if (ps.OpenCircuit)
            {
                lines.Add(("Devre açık: iki çubuk da aynı çözeltinin içinde olmalı.", Theme.Warning));
                return;
            }

            if (ps.Voltage <= 0.01) lines.Add(("Gerilimi açın (aşağıdaki kaydırıcı).", Theme.Muted));
            if (ps.CathodeReaction != null) lines.Add(("Katot (−): " + ps.CathodeReaction, Theme.Accent));
            if (ps.AnodeReaction != null) lines.Add(("Anot (+): " + ps.AnodeReaction, Theme.Accent));
            var cathode = ps.Negative.Get<Rod>();
            string plated = null;
            if (cathode != null)
            {
                foreach (var layer in cathode.Layers)
                {
                    if (!layer.Crystals) plated = ElementTr(world, layer.Symbol).ToLower(Tr.TrCulture) + " " + Milligrams(world, layer.Symbol, layer.Moles);
                }
            }

            lines.Add((string.Format(ci, "Geçen yük: {0:0.0} C{1}", ps.ChargeC, plated != null ? "   ·   Katotta: " + plated : ""), Theme.Text));
            lines.Add(("Faraday: biriken madde (mol) = I · t / (n · 96 485)", Theme.Muted));
        }

        private static void AddContents(LiquidContainer c, List<(string, Color)> lines, ref Hazard hazards)
        {
            var ci = CultureInfo.CurrentCulture;
            var db = c.Contents.Database;
            var parts = new List<string>();
            foreach (var kv in c.Contents.Components.OrderByDescending(kv => kv.Value))
            {
                var sub = db.Get(kv.Key);
                if (kv.Value > 1e-7) hazards |= sub.Hazards;
                if (kv.Key == SubstanceIds.Water || parts.Count >= 3) continue;
                bool dissolved = sub.IsDissolved(c.Contents.HasWater);
                string amount = !dissolved && sub.Phase == SubstancePhase.Solid
                    ? string.Format(ci, "{0:0.##} g", kv.Value * sub.MolarMass)
                    : string.Format(ci, "{0:0.#} mmol", kv.Value * 1000);
                parts.Add($"{Tr.Substance(sub)} ({sub.Formula}) {amount}");
            }

            if (parts.Count > 0) lines.Add(("İçerik: " + string.Join(", ", parts), Theme.Text));
            else if (c.Contents.HasWater) lines.Add(("İçerik: saf su", Theme.Text));
            else if (c.Contents.IsEmpty) lines.Add(("Boş", Theme.Muted));
        }

        private void DrawControls(Item item, LiquidContainer c, Balance balance, Rectangle row)
        {
            float s = Ui.S, gap = 10 * s;
            var ci = CultureInfo.CurrentCulture;
            float right = row.Right();
            Rectangle Btn(float width) { right -= width; var b = new Rectangle(right, row.Y, width, row.Height); right -= gap; return b; }
            Rectangle SliderArea(float labelW) => new Rectangle(row.X + labelW + 14 * s, row.Y, right - row.X - labelW - 30 * s, row.Height);

            if (Ui.Button("remove", Btn(100 * s), "Kaldır", Gfx.Lerp(Theme.Danger, Theme.Button, 0.45f), 16))
            {
                _lab.Remove(item);
                _lab.Selected = null;
                return;
            }

            if (c != null && c.OnFire && Ui.Button("extinguish", Btn(110 * s), "Söndür", Theme.Danger, 16)) c.Extinguish();

            if (item.Tap != null)
            {
                if (Ui.Button("tap-refill", Btn(110 * s), "Doldur", Theme.AccentDim, 16))
                {
                    // Refill to the 0 mark with fresh titrant and restart the delivered-volume counter.
                    c.Contents.Clear();
                    var fresh = new Mixture(c.Contents.Database);
                    ReagentPreset.Solution("", 25, SubstanceIds.SodiumHydroxide, 0.1).FillInto(fresh);
                    c.ReceiveLiquid(fresh);
                    item.Tap.ResetCounter();
                }
                Ui.Text(string.Format(ci, "Musluk %{0:0}", item.Tap.Valve * 100), row.X, row.Y + 14 * s, 14, Theme.Muted, true);
                item.Tap.Valve = Ui.Slider("tap", SliderArea(100 * s), (float)item.Tap.Valve, 0, 1, Theme.Accent);
            }
            else if (item.Heat != null)
            {
                string label = string.Format(ci, item.Heat.OpenFlame ? "Gaz %{0:0}" : "Güç %{0:0}", item.Heat.GasValve * 100);
                Ui.Text(label, row.X, row.Y + 14 * s, 15, Theme.Muted, true);
                item.Heat.GasValve = Ui.Slider("gas", SliderArea(84 * s), (float)item.Heat.GasValve, 0, 1, item.Heat.OpenFlame ? Theme.Accent : Theme.Warning);
            }
            else if (balance != null)
            {
                if (Ui.Button("tare", Btn(110 * s), "Dara", Theme.AccentDim, 16)) balance.Tare();
            }
            else if (item.Supply != null)
            {
                var ps = item.Supply;
                bool wired = ps.Negative != null && ps.Positive != null;
                if (wired)
                {
                    if (Ui.Button("ps-swap", Btn(130 * s), "Kutupları çevir", Theme.AccentDim, 15)) ps.Swap();
                    if (Ui.Button("ps-off", Btn(80 * s), "Ayır", null, 15)) ps.Disconnect();
                }
                else if (Ui.Button("ps-wire", Btn(90 * s), "Bağla", Theme.AccentDim, 15))
                {
                    if (!ps.AutoConnect()) Toast("Önce iki çubuğu aynı çözeltiye daldırın.", Theme.Warning);
                }

                Ui.Text(string.Format(ci, "{0:0.0} V", ps.Voltage), row.X, row.Y + 14 * s, 15, Theme.Muted, true);
                float v = Ui.Slider("volts", SliderArea(70 * s), (float)ps.Voltage, 0, (float)PowerSupply.MaxVoltage, Theme.Warning);
                ps.Voltage = Math.Round(v * 10) / 10.0;
            }
            else if (item.Rod != null)
            {
                var rod = item.Rod;
                if (rod.IsGlass)
                {
                    if (Ui.Button("stir", Btn(120 * s), "Karıştır", Theme.AccentDim, 16))
                    {
                        if (rod.ImmersedIn != null) rod.Stir(4);
                        else Toast("Bageti önce bir kabın içine bırakın.", Theme.Warning);
                    }
                }
                else if (rod.Layers.Count > 0 && Ui.Button("polish", Btn(170 * s), "Zımparala (temizle)", null, 15))
                {
                    rod.Clean();
                }
            }
            else if (c != null)
            {
                if (Ui.Button("empty", Btn(100 * s), "Boşalt", null, 16))
                {
                    c.Contents.Clear();
                    if (c.OnFire) c.Extinguish();
                }

                if (item.Host == null)
                {
                    Ui.Text(string.Format(ci, "Dök {0:0}°", item.Tilt), row.X, row.Y + 14 * s, 15, Theme.Muted, true);
                    float tilt = Ui.Slider("tilt", SliderArea(84 * s), item.Tilt, 0, 130, Theme.Warning);
                    if (Ui.Active == "tilt")
                    {
                        item.AutoPourMl = -1; // the hand on the slider takes over from an automatic pour
                        item.Tilt = tilt;
                    }
                }
                else
                {
                    Ui.Wrapped("Dökmek için kabı bulunduğu yerden alın.", row.X, row.Y + 6 * s, right - row.X - 10 * s, 13, Theme.Muted);
                }
            }
        }

        /// <summary>Metered burette doses: one drop (0.05 mL) up to 5 mL, let out drop-wise into the flask below.</summary>
        private void DrawBuretteDoses(Item item, LiquidContainer c, Rectangle row)
        {
            float s = Ui.S;
            var tap = item.Tap;
            string head = "Ekle →";
            Ui.Text(head, row.X, row.Y + 13 * s, 15, Theme.Muted, true);
            float x = row.X + Ui.Measure(head, 15, true).X + 12 * s;
            var options = new (string label, double ml)[] { ("1 damla", 0.05), ("0,5 mL", 0.5), ("1 mL", 1), ("5 mL", 5) };
            bool busy = tap.PendingMilliliters > 1e-4;
            int n = options.Length + (busy ? 1 : 0);
            float bw = (row.Right() - x - (n - 1) * 8 * s) / n;
            foreach (var (label, ml) in options)
            {
                if (Ui.Button("dose-" + label, new Rectangle(x, row.Y, bw, row.Height), label, Gfx.Lerp(Theme.Accent, Theme.Button, 0.6f), 15))
                {
                    if (c.VolumeMilliliters < 0.05) Toast("Büret boş. \"Doldur\" ile doldurun.", Theme.Warning);
                    else
                    {
                        if (tap.Target == null) Toast("Büretin altında kap yok: sıvı tezgâha damlar. Erlenmayeri sehpanın tabanına koyun.", Theme.Warning);
                        tap.Dispense(ml);
                    }
                }

                x += bw + 8 * s;
            }

            if (busy && Ui.Button("dose-stop", new Rectangle(x, row.Y, bw, row.Height), "Durdur", Theme.Danger, 15)) tap.CancelDispense();
        }

        /// <summary>One-tap pours: the container is lifted over the nearest beaker, tipped until the amount has left, then put back.</summary>
        private void DrawQuickPour(Item item, LiquidContainer c, Rectangle row)
        {
            float s = Ui.S;
            var ci = CultureInfo.CurrentCulture;
            if (item.ChosenTarget != null && !_lab.Items.Contains(item.ChosenTarget)) item.ChosenTarget = null;
            var target = item.PourInto ?? item.ChosenTarget ?? _lab.NearestPourTarget(item, item.Home ?? item.Position, 0.35f);
            if (item.AutoPourMl >= 0)
            {
                double poured = item.AutoPourStart - (c.LiquidVolumeMilliliters + c.SolidVolumeMilliliters);
                string status = double.IsPositiveInfinity(item.AutoPourMl)
                    ? string.Format(ci, "Dökülüyor… {0:0.#} mL", Math.Max(0, poured))
                    : string.Format(ci, "Dökülüyor… {0:0.#} / {1:0} mL", Math.Max(0, poured), item.AutoPourMl);
                Ui.Text(status, row.X, row.Y + 13 * s, 15, Theme.Accent, true);
                if (Ui.Button("pour-stop", new Rectangle(row.Right() - 120 * s, row.Y, 120 * s, row.Height), "Durdur", Theme.Danger, 15)) _lab.StopAutoPour(item);
                return;
            }

            if (_lab.PourPickSource == item)
            {
                Ui.Text("Tezgâhta dökülecek kaba dokunun…", row.X, row.Y + 13 * s, 15, Theme.Accent, true);
                if (Ui.Button("pour-pick-cancel", new Rectangle(row.Right() - 120 * s, row.Y, 120 * s, row.Height), "Vazgeç", null, 15)) _lab.PourPickSource = null;
                return;
            }

            bool solid = !c.Contents.HasLiquid && c.Contents.HasSolids;
            double total = c.LiquidVolumeMilliliters + c.SolidVolumeMilliliters;
            var options = solid
                ? new (string label, double ml)[] { ("Biraz", Math.Max(0.3, total * 0.15)), ("Yarısı", total / 2), ("Tümü", double.PositiveInfinity) }
                : new (string label, double ml)[] { ("10 mL", 10), ("25 mL", 25), ("50 mL", 50), ("Tümü", double.PositiveInfinity) };
            // "Dök → [Beher ▾]": the target button picks another container by tapping it on the bench.
            string head = "Dök →";
            Ui.Text(head, row.X, row.Y + 13 * s, 15, Theme.Muted, true);
            float x = row.X + Ui.Measure(head, 15, true).X + 10 * s;
            string targetLabel = target != null ? ShortName(target) : "Kap seç";
            float tw = Math.Min(150 * s, Ui.Measure(targetLabel, 14, true).X + 28 * s);
            if (Ui.Button("pour-target", new Rectangle(x, row.Y, tw, row.Height), targetLabel, Theme.AccentDim, 14))
            {
                _lab.PourPickSource = item;
                _lab.PourPickAmount = double.NaN;
                Toast("Hangi kaba dökülecek? Tezgâhta o kaba dokunun.", Theme.Accent);
            }

            x += tw + 10 * s;
            float bw = (row.Right() - x - (options.Length - 1) * 8 * s) / options.Length;
            foreach (var (label, ml) in options)
            {
                if (Ui.Button("pour-" + label, new Rectangle(x, row.Y, bw, row.Height), label, Gfx.Lerp(Theme.Warning, Theme.Button, 0.62f), 15))
                {
                    if (total <= 0.1) Toast("Kap boş.", Theme.Warning);
                    else if (target == null)
                    {
                        _lab.PourPickSource = item;
                        _lab.PourPickAmount = ml;
                        Toast("Dökülecek kaba dokunun.", Theme.Accent);
                    }
                    else
                    {
                        item.PourInto = target;
                        _lab.StartAutoPour(item, ml);
                    }
                }

                x += bw + 8 * s;
            }
        }

        private static string ShortName(Item item)
        {
            string name = Tr.Name(item.Inst);
            int colon = name.IndexOf(':');
            if (colon > 0) name = name.Substring(0, colon);
            return name.Length > 14 ? name.Substring(0, 13) + "…" : name;
        }

        // ------------------------------------------------------------------ camera buttons + toasts

        private void DrawCameraButtons()
        {
            float s = Ui.S, size = 52 * s, gap = 8 * s;
            float x = _lab.Viewport.X + _lab.Viewport.Width - size - 14 * s;
            float y = _lab.Viewport.Y + 16 * s;
            float dt = GetFrameTime();
            var buttons = new (string id, string label, Action act)[]
            {
                ("camL", "◀", () => _lab.Yaw += 60 * dt),
                ("camR", "▶", () => _lab.Yaw -= 60 * dt),
                ("camU", "▲", () => _lab.Pitch += 40 * dt),
                ("camD", "▼", () => _lab.Pitch -= 40 * dt),
                ("camIn", "+", () => _lab.Distance = Math.Clamp(_lab.Distance * (1 - dt * 0.8f), 0.3f, 3.5f)),
                ("camOut", "−", () => _lab.Distance = Math.Clamp(_lab.Distance * (1 + dt * 0.8f), 0.3f, 3.5f)),
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
            var area = Tab == HudTab.Lab ? _lab.Viewport : ContentArea;
            float width = Math.Min(760 * s, area.Width - 160 * s);
            float x = area.X + (area.Width - width) / 2, y = _top.Height + 14 * s;
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
                float h = Math.Max(54 * s, Ui.Wrapped(t.text, 0, 0, width - 40 * s, 17, Theme.Text, true, 1.25f, draw: false) + 30 * s);
                var r = new Rectangle(x, y, width, h);
                DrawRectangleRounded(r, Math.Min(0.3f, 16 * s / h), 8, Gfx.WithAlpha(Theme.Raised, 0.95f * a));
                DrawRectangleRounded(new Rectangle(r.X + 8 * s, r.Y + 10 * s, 6 * s, r.Height - 20 * s), 1f, 4, Gfx.WithAlpha(t.color, a));
                Ui.Wrapped(t.text, r.X + 26 * s, r.Y + 15 * s, width - 40 * s, 17, Gfx.WithAlpha(Theme.Text, a), true, 1.25f);
                y += r.Height + 8 * s;
            }
        }
    }
}
