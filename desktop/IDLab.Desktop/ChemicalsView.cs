using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using IDLab.Core.Chemistry;
using IDLab.Core.Model;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// "Kimyasallar" tab: the 1000+ substance catalogue. Find a chemical by typing its name or formula (on-screen
    /// keyboard for the smart board), by picking elements on a periodic table, or build an ionic compound from a
    /// cation and an anion (charge balance shown). The detail card shows the data and puts a bottle or jar of the
    /// chemical on the bench.
    /// </summary>
    internal sealed class ChemicalsView
    {
        private enum Mode { Search, Elements, Ions }

        private sealed class Entry
        {
            public Substance S;
            public string Name, Formula, FormulaKey, Category;
            public string[] Keys;
            public HashSet<string> Elements;
        }

        private static readonly (string key, string label)[] Categories =
        {
            (null, "Tümü"), ("ionic", "İyonik"), ("acid", "Asitler"), ("element", "Elementler"), ("organic", "Organik"),
            ("inorganic", "İnorganik"), ("biochem", "Biyokimya"), ("indicator", "İndikatör"), ("drug", "İlaç"),
        };

        // Ion tables for the compound builder (keys match the generated catalogue).
        private static readonly (string key, string formula, int charge, string name)[] Cations =
        {
            ("Li", "Li", 1, "Lityum"), ("Na", "Na", 1, "Sodyum"), ("K", "K", 1, "Potasyum"), ("Rb", "Rb", 1, "Rubidyum"), ("Cs", "Cs", 1, "Sezyum"),
            ("NH4", "NH4", 1, "Amonyum"), ("Ag", "Ag", 1, "Gümüş"), ("Cu1", "Cu", 1, "Bakır(I)"), ("Mg", "Mg", 2, "Magnezyum"),
            ("Ca", "Ca", 2, "Kalsiyum"), ("Sr", "Sr", 2, "Stronsiyum"), ("Ba", "Ba", 2, "Baryum"), ("Be", "Be", 2, "Berilyum"), ("Zn", "Zn", 2, "Çinko"),
            ("Cd", "Cd", 2, "Kadmiyum"), ("Hg2", "Hg", 2, "Cıva(II)"), ("Pb2", "Pb", 2, "Kurşun(II)"), ("Sn2", "Sn", 2, "Kalay(II)"),
            ("Mn2", "Mn", 2, "Mangan(II)"), ("Fe2", "Fe", 2, "Demir(II)"), ("Co2", "Co", 2, "Kobalt(II)"), ("Ni2", "Ni", 2, "Nikel(II)"),
            ("Cu2", "Cu", 2, "Bakır(II)"), ("Fe3", "Fe", 3, "Demir(III)"), ("Al", "Al", 3, "Alüminyum"), ("Cr3", "Cr", 3, "Krom(III)"),
            ("Bi3", "Bi", 3, "Bizmut(III)"), ("Sn4", "Sn", 4, "Kalay(IV)"), ("Mn4", "Mn", 4, "Mangan(IV)"), ("Pb4", "Pb", 4, "Kurşun(IV)"),
        };

        private static readonly (string key, string formula, int charge, string name)[] Anions =
        {
            ("F", "F", 1, "florür"), ("Cl", "Cl", 1, "klorür"), ("Br", "Br", 1, "bromür"), ("I", "I", 1, "iyodür"), ("OH", "OH", 1, "hidroksit"),
            ("NO3", "NO3", 1, "nitrat"), ("NO2", "NO2", 1, "nitrit"), ("CN", "CN", 1, "siyanür"), ("SCN", "SCN", 1, "tiyosiyanat"),
            ("ClO", "ClO", 1, "hipoklorit"), ("ClO3", "ClO3", 1, "klorat"), ("ClO4", "ClO4", 1, "perklorat"), ("BrO3", "BrO3", 1, "bromat"),
            ("IO3", "IO3", 1, "iyodat"), ("MnO4", "MnO4", 1, "permanganat"), ("HCO3", "HCO3", 1, "bikarbonat"), ("HSO4", "HSO4", 1, "bisülfat"),
            ("H2PO4", "H2PO4", 1, "dihidrojen fosfat"), ("CH3COO", "CH3COO", 1, "asetat"), ("HCOO", "HCOO", 1, "format"),
            ("C6H5COO", "C6H5COO", 1, "benzoat"), ("C3H5O3", "C3H5O3", 1, "laktat"), ("C17H35COO", "C17H35COO", 1, "stearat"), ("H", "H", 1, "hidrür"),
            ("O", "O", 2, "oksit"), ("O2", "O2", 2, "peroksit"), ("S", "S", 2, "sülfür"), ("SO4", "SO4", 2, "sülfat"), ("SO3", "SO3", 2, "sülfit"),
            ("S2O3", "S2O3", 2, "tiyosülfat"), ("CO3", "CO3", 2, "karbonat"), ("C2O4", "C2O4", 2, "oksalat"), ("CrO4", "CrO4", 2, "kromat"),
            ("Cr2O7", "Cr2O7", 2, "dikromat"), ("SiO3", "SiO3", 2, "silikat"), ("HPO4", "HPO4", 2, "hidrojen fosfat"), ("B4O7", "B4O7", 2, "tetraborat"),
            ("MoO4", "MoO4", 2, "molibdat"), ("WO4", "WO4", 2, "tungstat"), ("PO4", "PO4", 3, "fosfat"), ("N", "N", 3, "nitrür"),
            ("AsO4", "AsO4", 3, "arsenat"), ("C6H5O7", "C6H5O7", 3, "sitrat"), ("FeCN6_3", "Fe(CN)6", 3, "ferrisiyanür"), ("FeCN6_4", "Fe(CN)6", 4, "ferrosiyanür"),
        };

        private static readonly string[][] KeyRows =
        {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
            new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "Ğ", "Ü" },
            new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L", "Ş", "İ" },
            new[] { "Z", "X", "C", "V", "B", "N", "M", "Ö", "Ç", "(", ")" },
        };

        private readonly Lab _lab;
        private readonly Hud _hud;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<string, int> _elementUse = new Dictionary<string, int>();
        private readonly HashSet<string> _symbols;
        private readonly HashSet<string> _picked = new HashSet<string>();
        private readonly DragScroll _scroll = new DragScroll();
        private readonly DragScroll _detailScroll = new DragScroll();
        private List<Entry> _results = new List<Entry>();
        private string _filterKey = "";
        private Mode _mode = Mode.Search;
        private int _category;
        private bool _exactElements;
        private string _query = "";
        private bool _focused, _keyboard;
        private string _cation, _anion;
        private Entry _selected;
        private float _time, _listContent, _detailContent;
        private Rectangle _left, _right, _list, _detail, _keyboardRect;

        public int Count => _entries.Count;

        public ChemicalsView(Lab lab, Hud hud)
        {
            _lab = lab;
            _hud = hud;
            _symbols = new HashSet<string>(Elements.All.Select(e => e.Symbol));
            var tr = new CultureInfo("tr-TR");
            foreach (var s in lab.World.Substances.All.Where(x => !x.Hidden))
            {
                var e = new Entry
                {
                    S = s, Name = Tr.Substance(s), Formula = s.Formula, Category = s.Category ?? "inorganic",
                    FormulaKey = Normalize(s.Formula), Elements = ElementsOf(s.Formula),
                };
                var keys = new List<string> { Normalize(e.Name), Normalize(s.Name ?? "") };
                keys.AddRange(s.Aliases.Select(Normalize));
                if (!string.IsNullOrEmpty(s.Cas)) keys.Add(Normalize(s.Cas));
                e.Keys = keys.Where(k => k.Length > 0).Distinct().ToArray();
                _entries.Add(e);
                foreach (var el in e.Elements) _elementUse[el] = _elementUse.TryGetValue(el, out int n) ? n + 1 : 1;
            }

            // Alphabetical, ignoring locant prefixes such as "1-", "2,2-", "o-", "tert-".
            static string SortKey(string n)
            {
                int i = 0;
                while (i < n.Length && (char.IsDigit(n[i]) || n[i] == ',' || n[i] == '-')) i++;
                foreach (var p in new[] { "o-", "m-", "p-", "tert-", "sek-", "cis-", "trans-" })
                {
                    if (n.Length > i + p.Length && string.Compare(n, i, p, 0, p.Length, StringComparison.Ordinal) == 0) return n.Substring(i + p.Length);
                }

                return n.Substring(i);
            }

            _entries.Sort((a, b) => string.Compare(SortKey(a.Name), SortKey(b.Name), tr, CompareOptions.IgnoreCase));
            Refilter();
        }

        // ------------------------------------------------------------------ text helpers

        /// <summary>Lower-case ASCII key for matching: Turkish letters folded, subscripts to digits, punctuation dropped.</summary>
        public static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char raw in text)
            {
                char c = raw;
                switch (c)
                {
                    case 'İ': case 'I': case 'ı': case 'î': case 'Î': c = 'i'; break;
                    case 'Ş': case 'ş': c = 's'; break;
                    case 'Ğ': case 'ğ': c = 'g'; break;
                    case 'Ü': case 'ü': c = 'u'; break;
                    case 'Ö': case 'ö': c = 'o'; break;
                    case 'Ç': case 'ç': c = 'c'; break;
                    case 'Â': case 'â': c = 'a'; break;
                }

                if (c >= '₀' && c <= '₉') c = (char)('0' + (c - '₀'));
                c = char.ToLowerInvariant(c);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            }

            return sb.ToString();
        }

        private HashSet<string> ElementsOf(string formula)
        {
            var set = new HashSet<string>();
            for (int i = 0; i < formula.Length; i++)
            {
                char c = formula[i];
                if (c < 'A' || c > 'Z') continue;
                if (i + 1 < formula.Length && char.IsLower(formula[i + 1]) && _symbols.Contains(formula.Substring(i, 2)))
                {
                    set.Add(formula.Substring(i, 2));
                    i++;
                }
                else if (_symbols.Contains(c.ToString()))
                {
                    set.Add(c.ToString());
                }
            }

            return set;
        }

        private static int Score(Entry e, string q)
        {
            if (q.Length == 0) return 10;
            if (e.FormulaKey == q) return 0;
            int best = int.MaxValue;
            foreach (var k in e.Keys)
            {
                if (k == q) best = Math.Min(best, 1);
                else if (k.StartsWith(q, StringComparison.Ordinal)) best = Math.Min(best, 2);
                else if (k.Contains(q)) best = Math.Min(best, 5);
            }

            if (e.FormulaKey.StartsWith(q, StringComparison.Ordinal)) best = Math.Min(best, 3);
            else if (q.Length >= 2 && e.FormulaKey.Contains(q)) best = Math.Min(best, 6);
            return best;
        }

        private void Refilter()
        {
            string q = Normalize(_query);
            string cat = Categories[_category].key;
            string key = $"{q}|{cat}|{_mode}|{_exactElements}|{string.Join(",", _picked.OrderBy(x => x))}|{_cation}|{_anion}";
            if (key == _filterKey) return;
            _filterKey = key;
            var scored = new List<(Entry e, int score)>();
            foreach (var e in _entries)
            {
                if (cat != null && e.Category != cat) continue;
                if (_mode == Mode.Elements && _picked.Count > 0)
                {
                    if (!_picked.IsSubsetOf(e.Elements)) continue;
                    if (_exactElements && !e.Elements.SetEquals(_picked)) continue;
                }

                if (_mode == Mode.Ions)
                {
                    if (_cation != null && e.S.CationKey != _cation) continue;
                    if (_anion != null && e.S.AnionKey != _anion) continue;
                    if (_cation == null && _anion == null && !e.S.IsIonic) continue;
                }

                int sc = Score(e, q);
                if (sc == int.MaxValue) continue;
                scored.Add((e, sc));
            }

            _results = scored.OrderBy(x => x.score).ThenBy(x => x.e.Name.Length).Select(x => x.e).ToList();
            if (q.Length == 0) _results = scored.Select(x => x.e).ToList();
            _scroll.Reset();
        }

        // ------------------------------------------------------------------ frame

        public void Update(float dt, Rectangle area)
        {
            _time += dt;
            float s = Ui.S, pad = 10 * s;
            float leftW = Math.Max(area.Width * 0.6f, 700 * s);
            _left = new Rectangle(area.X + pad, area.Y + pad, leftW - pad, area.Height - 2 * pad);
            _right = new Rectangle(_left.Right() + pad, area.Y + pad, area.Right() - _left.Right() - 2 * pad, area.Height - 2 * pad);
            float kbH = 330 * s;
            _keyboardRect = _keyboard ? new Rectangle(area.X + (area.Width - Math.Min(area.Width - 40 * s, 1200 * s)) / 2, area.Bottom() - kbH - 10 * s,
                Math.Min(area.Width - 40 * s, 1200 * s), kbH) : default;

            if (_focused)
            {
                // Physical keyboard (teacher's laptop / USB keyboard).
                int ch;
                while ((ch = GetCharPressed()) > 0)
                {
                    if (ch >= 32 && _query.Length < 40) _query += char.ConvertFromUtf32(ch);
                }

                if ((IsKeyPressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace)) && _query.Length > 0)
                {
                    _query = _query.Substring(0, _query.Length - 1);
                }

                if (IsKeyPressed(KeyboardKey.Enter)) _keyboard = false;
                if (IsKeyPressed(KeyboardKey.Escape)) _query = "";
            }

            Refilter();
            _scroll.Update(_list, _listContent, dt);
            _detailScroll.Update(_detail, _detailContent, dt);
        }

        public void Draw()
        {
            DrawLeft();
            DrawDetail();
            if (_keyboard) DrawKeyboard();
        }

        // ------------------------------------------------------------------ left: search, filters, results

        private void DrawLeft()
        {
            float s = Ui.S;
            var r = _left;
            Ui.Panel(r, Theme.Panel);
            float x = r.X + 18 * s, y = r.Y + 16 * s, w = r.Width - 36 * s;
            Ui.Text("KİMYASAL DOLABI", x, y, 20, Theme.Text, true);
            string count = string.Format(CultureInfo.CurrentCulture, "{0} kimyasal · {1} sonuç", _entries.Count, _results.Count);
            Ui.Text(count, r.Right() - 18 * s - Ui.Measure(count, 15).X, y + 4 * s, 15, Theme.Muted);
            y += 38 * s;

            // Search box.
            var box = new Rectangle(x, y, w - 150 * s, 58 * s);
            bool hot = Ui.IsOver(box);
            if (Ui.Pressed && hot)
            {
                _focused = true;
                _keyboard = true;
            }

            DrawRectangleRounded(box, 0.3f, 8, _focused ? Gfx.Lerp(Theme.Raised, Theme.Accent, 0.12f) : Theme.Raised);
            DrawRectangleRoundedLinesEx(box, 0.3f, 8, 2 * s, _focused ? Theme.Accent : Theme.Border);
            Ui.Block(box);
            float tx = box.X + 18 * s, ty = box.Y + 16 * s;
            if (_query.Length == 0)
            {
                Ui.Text("Ad veya formül yazın: karbon tetraklorür, CCl4, göztaşı…", tx, ty, 18, Theme.Muted);
            }
            else
            {
                Ui.Text(_query, tx, ty, 20, Theme.Text, true);
            }

            if (_focused && (int)(_time * 2) % 2 == 0)
            {
                float cx = tx + (_query.Length > 0 ? Ui.Measure(_query, 20, true).X + 2 * s : 0);
                DrawRectangle((int)cx, (int)(ty - 2 * s), Math.Max(2, (int)(2 * s)), (int)(26 * s), Theme.Accent);
            }

            if (_query.Length > 0 && Ui.Button("chem-clear", new Rectangle(box.Right() - 50 * s, box.Y + 9 * s, 40 * s, 40 * s), "✕", null, 16))
            {
                _query = "";
            }

            if (Ui.Button("chem-kb", new Rectangle(box.Right() + 10 * s, y, 140 * s, 58 * s), _keyboard ? "Klavyeyi gizle" : "Klavye", _keyboard ? Theme.AccentDim : (Color?)null, 15))
            {
                _keyboard = !_keyboard;
                _focused = _keyboard || _focused;
            }

            y += 70 * s;

            // Mode buttons.
            var modes = new[] { (Mode.Search, "Ara / Listele"), (Mode.Elements, "Elementlerden seç"), (Mode.Ions, "İyonlardan bileşik yap") };
            float mw = (w - 16 * s) / 3;
            for (int i = 0; i < modes.Length; i++)
            {
                if (Ui.Button("chem-mode" + i, new Rectangle(x + i * (mw + 8 * s), y, mw, 46 * s), modes[i].Item2, null, 16, _mode == modes[i].Item1))
                {
                    _mode = modes[i].Item1;
                }
            }

            y += 56 * s;

            // Category chips.
            float cx2 = x;
            for (int i = 0; i < Categories.Length; i++)
            {
                float cw = Ui.Measure(Categories[i].label, 14, true).X + 26 * s;
                if (cx2 + cw > x + w)
                {
                    cx2 = x;
                    y += 44 * s;
                }

                if (Ui.Button("chem-cat" + i, new Rectangle(cx2, y, cw, 38 * s), Categories[i].label, null, 14, _category == i)) _category = i;
                cx2 += cw + 6 * s;
            }

            y += 50 * s;
            if (_mode == Mode.Elements) y = DrawElementPicker(x, y, w);
            else if (_mode == Mode.Ions) y = DrawIonBuilder(x, y, w);

            _list = new Rectangle(x - 8 * s, y, w + 16 * s, r.Bottom() - y - 10 * s);
            DrawResults();
        }

        private float DrawElementPicker(float x, float y, float w)
        {
            float s = Ui.S;
            float cell = Math.Min(w / 18f, 46 * s);
            float gx = x + (w - cell * 18) / 2;
            foreach (var e in Elements.All)
            {
                if (e.Z > 103) continue;
                float rowY = e.Row >= 9 ? y + (e.Row - 2) * cell + 8 * s : y + (e.Row - 1) * cell;
                var rc = new Rectangle(gx + (e.Column - 1) * cell + 1, rowY + 1, cell - 2, cell - 2);
                bool used = _elementUse.ContainsKey(e.Symbol);
                bool on = _picked.Contains(e.Symbol);
                var col = Elements.CategoryColor(e.Category);
                if (!used) col = Gfx.Lerp(col, Theme.Panel, 0.8f);
                else if (!on && _picked.Count > 0) col = Gfx.Lerp(col, Theme.Panel, 0.45f);
                bool hot = Ui.IsOver(rc);
                if (Ui.Pressed && hot && used) Ui.SetActive("el-" + e.Symbol);
                if (Ui.Released && hot && Ui.Active == "el-" + e.Symbol)
                {
                    if (!_picked.Remove(e.Symbol)) _picked.Add(e.Symbol);
                }

                DrawRectangleRounded(rc, 0.18f, 4, hot && used ? Gfx.Lerp(col, Color.White, 0.25f) : col);
                if (on) DrawRectangleRoundedLinesEx(rc, 0.18f, 4, 3 * s, Color.White);
                var m = Ui.Measure(e.Symbol, cell * 0.42f / s, true);
                Ui.Text(e.Symbol, rc.X + (rc.Width - m.X) / 2, rc.Y + (rc.Height - m.Y) / 2, cell * 0.42f / s, used ? new Color(16, 18, 26, 255) : new Color(90, 96, 110, 255), true);
            }

            Ui.Block(new Rectangle(gx, y, cell * 18, cell * 9 + 8 * s));
            y += cell * 9 + 18 * s;
            string picked = _picked.Count == 0 ? "Bileşiklerini görmek için element(ler)e dokunun." : "Seçili: " + string.Join(" + ", _picked.OrderBy(p => p));
            Ui.Text(picked, x, y + 10 * s, 16, _picked.Count == 0 ? Theme.Muted : Theme.Text, true);
            float bx = x + w;
            if (_picked.Count > 0)
            {
                bx -= 110 * s;
                if (Ui.Button("el-clear", new Rectangle(bx, y, 110 * s, 40 * s), "Temizle", null, 14)) _picked.Clear();
                bx -= 10 * s;
            }

            bx -= 250 * s;
            if (Ui.Button("el-exact", new Rectangle(bx, y, 250 * s, 40 * s), _exactElements ? "✓ Yalnızca bu elementler" : "Yalnızca bu elementler", null, 14, _exactElements))
            {
                _exactElements = !_exactElements;
            }

            return y + 52 * s;
        }

        private float DrawIonBuilder(float x, float y, float w)
        {
            float s = Ui.S;
            Ui.Text("1) Katyon (+) seçin", x, y, 15, Theme.Warning, true);
            y += 26 * s;
            y = IonChips(Cations, x, y, w, true);
            Ui.Text("2) Anyon (−) seçin", x, y + 4 * s, 15, Theme.Accent, true);
            y += 30 * s;
            y = IonChips(Anions, x, y, w, false);

            // Charge balance.
            var box = new Rectangle(x, y + 4 * s, w, 74 * s);
            DrawRectangleRounded(box, 0.2f, 8, Theme.Raised);
            if (_cation != null && _anion != null)
            {
                var c = Cations.First(k => k.key == _cation);
                var a = Anions.First(k => k.key == _anion);
                int l = Lcm(c.charge, a.charge), nc = l / c.charge, na = l / a.charge;
                var compound = _lab.World.Substances.FindIonic(_cation, _anion);
                string eq = $"{(nc > 1 ? nc + " " : "")}{Elements.PrettySpecies(Species(c.formula, c.charge))}  +  {(na > 1 ? na + " " : "")}{Elements.PrettySpecies(Species(a.formula, -a.charge))}  →  " +
                            (compound != null ? compound.Formula : "?");
                Ui.Text(eq, box.X + 16 * s, box.Y + 10 * s, 22, Theme.Text, true);
                string note = string.Format(CultureInfo.CurrentCulture, "Yük dengesi: {0} × (+{1}) + {2} × (−{3}) = 0", nc, c.charge, na, a.charge) +
                              (compound != null ? $"   ·   {Tr.Substance(compound)}" : "   ·   Bu iyon çifti kararlı bir bileşik oluşturmaz.");
                Ui.Text(note, box.X + 16 * s, box.Y + 44 * s, 14, compound != null ? Theme.Success : Theme.Warning);
                if (compound != null && _selected?.S != compound) _selected = _entries.FirstOrDefault(e => e.S == compound) ?? _selected;
            }
            else
            {
                Ui.Text("Bir katyon ve bir anyon seçin: yükler dengelenerek formül oluşur (ör. Ca²⁺ + 2 Cl⁻ → CaCl₂).", box.X + 16 * s, box.Y + 26 * s, 15, Theme.Muted);
            }

            return box.Bottom() + 12 * s;
        }

        private float IonChips((string key, string formula, int charge, string name)[] ions, float x, float y, float w, bool cation)
        {
            float s = Ui.S, cx = x, h = 36 * s;
            foreach (var ion in ions)
            {
                string label = Elements.PrettySpecies(Species(ion.formula, cation ? ion.charge : -ion.charge));
                float cw = Ui.Measure(label, 14, true).X + 20 * s;
                if (cx + cw > x + w)
                {
                    cx = x;
                    y += h + 5 * s;
                }

                bool on = cation ? _cation == ion.key : _anion == ion.key;
                var bg = on ? Gfx.Lerp(cation ? Theme.Warning : Theme.Accent, Theme.Button, 0.4f) : Theme.Button;
                if (Ui.Button((cation ? "cat-" : "an-") + ion.key, new Rectangle(cx, y, cw, h), label, bg, 14))
                {
                    if (cation) _cation = on ? null : ion.key;
                    else _anion = on ? null : ion.key;
                }

                if (Ui.IsOver(new Rectangle(cx, y, cw, h)))
                {
                    _hoverIon = (cation ? CapitalName(ion.name) : ion.name) + $" ({label})";
                }

                cx += cw + 5 * s;
            }

            if (_hoverIon != null)
            {
                Ui.Text(_hoverIon, x + w - Ui.Measure(_hoverIon, 13).X, y + h + 4 * s, 13, Theme.Muted);
            }

            _hoverIon = null;
            return y + h + 20 * s;
        }

        private string _hoverIon;

        private static string CapitalName(string n) => n;

        private static string Species(string formula, int charge)
        {
            string sign = charge > 0 ? "+" : "-";
            int n = Math.Abs(charge);
            return n == 1 ? formula + sign : $"{formula}^{n}{sign}";
        }

        private static int Lcm(int a, int b)
        {
            int x = a, y = b;
            while (y != 0) (x, y) = (y, x % y);
            return a * b / x;
        }

        private void DrawResults()
        {
            float s = Ui.S;
            var r = _list;
            float rowH = 62 * s, gap = 4 * s;
            _listContent = _results.Count * (rowH + gap);
            if (_results.Count == 0)
            {
                Ui.Wrapped("Sonuç bulunamadı. Yazımı kontrol edin ya da formülü deneyin (ör. NaCl, H2SO4, C6H12O6).", r.X + 12 * s, r.Y + 10 * s, r.Width - 24 * s, 16, Theme.Muted);
                return;
            }

            BeginScissorMode((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
            int first = Math.Max(0, (int)(_scroll.Offset / (rowH + gap)));
            int last = Math.Min(_results.Count - 1, first + (int)(r.Height / (rowH + gap)) + 2);
            for (int i = first; i <= last; i++)
            {
                var e = _results[i];
                var row = new Rectangle(r.X + 8 * s, r.Y + i * (rowH + gap) - _scroll.Offset, r.Width - 16 * s, rowH);
                bool hot = Ui.IsOver(row) && Ui.IsOver(r);
                if (Ui.Pressed && hot) Ui.SetActive("chem-row");
                if (Ui.Released && hot && Ui.Active == "chem-row" && !_scroll.Dragging)
                {
                    _selected = e;
                    _detailScroll.Reset();
                    _keyboard = false;
                }

                bool sel = _selected == e;
                DrawRectangleRounded(row, 0.2f, 8, sel ? Gfx.Lerp(Theme.Raised, Theme.Accent, 0.28f) : hot ? Gfx.Lerp(Theme.Raised, Color.White, 0.05f) : Theme.Raised);
                DrawSwatch(e.S, new Vector2(row.X + 30 * s, row.Y + rowH / 2), 17 * s);
                Ui.Text(e.Name, row.X + 60 * s, row.Y + 9 * s, 17, Theme.Text, true);
                Ui.Text(e.Formula + "   ·   " + CategoryName(e.Category) + "   ·   " + PhaseName(e.S), row.X + 60 * s, row.Y + 35 * s, 14, Theme.Muted);
                float px = row.Right() - 10 * s;
                foreach (var h in Textures.PictogramsFor(e.S.Hazards).Take(4))
                {
                    px -= 40 * s;
                    var pic = Textures.Pictogram(h);
                    DrawTexturePro(pic, new Rectangle(0, 0, pic.Width, pic.Height), new Rectangle(px, row.Y + 11 * s, 40 * s, 40 * s), Vector2.Zero, 0, Color.White);
                }
            }

            EndScissorMode();
            Ui.Block(r);
        }

        private static void DrawSwatch(Substance sub, Vector2 c, float radius)
        {
            var col = Gfx.FromCore(sub.Color);
            if (sub.IsGas) col = new Color(200, 220, 240, 120);
            col.A = (byte)Math.Max(col.A, (byte)120);
            DrawCircleV(c, radius, Gfx.Lerp(Theme.Panel, Color.White, 0.08f));
            DrawCircleV(c, radius - 3 * Ui.S, col);
            DrawCircleLinesV(c, radius, Theme.Border);
        }

        public static string CategoryName(string c)
        {
            switch (c)
            {
                case "ionic": return "İyonik bileşik";
                case "acid": return "Asit";
                case "element": return "Element";
                case "organic": return "Organik";
                case "biochem": return "Biyokimya";
                case "indicator": return "İndikatör";
                case "drug": return "İlaç / katkı";
                default: return "İnorganik";
            }
        }

        public static string PhaseName(Substance s)
        {
            if (s.IsGas) return "gaz";
            switch (s.Phase)
            {
                case SubstancePhase.Liquid: return "sıvı";
                case SubstancePhase.Solid: return "katı";
                default: return "sulu çözelti";
            }
        }

        private static string KindName(Substance s)
        {
            var ci = CultureInfo.CurrentCulture;
            switch (s.Kind)
            {
                case SubstanceKind.StrongAcid: return "Kuvvetli asit";
                case SubstanceKind.WeakAcid: return string.Format(ci, "Zayıf asit (Ka = {0:0.0#E+0})", s.Ka);
                case SubstanceKind.StrongBase: return "Kuvvetli baz";
                case SubstanceKind.WeakBase: return string.Format(ci, "Zayıf baz (Kb = {0:0.0#E+0})", s.Kb);
                case SubstanceKind.Salt: return s.IsIonic ? "Tuz (iyonik bileşik)" : "Tuz";
                case SubstanceKind.Metal: return "Metal";
                case SubstanceKind.ReactiveMetal: return "Aktif metal";
                case SubstanceKind.Solvent: return "Çözücü";
                case SubstanceKind.Indicator: return "İndikatör";
                case SubstanceKind.Oxidizer: return "Yükseltgen";
                default: return s.ElementSymbol != null ? "Ametal / soy gaz" : "Moleküler bileşik";
            }
        }

        // ------------------------------------------------------------------ right: detail card + add to bench

        private void DrawDetail()
        {
            float s = Ui.S;
            var r = _right;
            Ui.Panel(r, Theme.Panel);
            if (_selected == null)
            {
                Ui.Text("Kimyasal seçin", r.X + 22 * s, r.Y + 22 * s, 24, Theme.Text, true);
                Ui.Wrapped("Soldaki listeden bir kimyasala dokunun. Bilgileri, tehlike işaretleri ve tezgâha ekleme seçenekleri burada görünür.\n\n" +
                           "Arama örnekleri:\n• karbon tetraklorür   • CCl4   • göztaşı\n• sofra tuzu   • C6H12O6   • kezzap\n\n" +
                           "\"Elementlerden seç\" ile Na ve Cl'ye dokunarak bu elementleri içeren bileşikleri, \"İyonlardan bileşik yap\" ile katyon + anyon seçerek formülün nasıl oluştuğunu görebilirsiniz.",
                    r.X + 22 * s, r.Y + 66 * s, r.Width - 44 * s, 16, Theme.Muted, false, 1.35f);
                return;
            }

            var sub = _selected.S;
            var ci = CultureInfo.CurrentCulture;
            _detail = new Rectangle(r.X + 14 * s, r.Y + 14 * s, r.Width - 28 * s, r.Height - 28 * s - 200 * s);
            BeginScissorMode((int)_detail.X, (int)_detail.Y, (int)_detail.Width, (int)_detail.Height);
            float x = _detail.X + 8 * s, w = _detail.Width - 16 * s, y = _detail.Y + 6 * s - _detailScroll.Offset;
            float start = y;
            DrawSwatch(sub, new Vector2(x + 34 * s, y + 34 * s), 32 * s);
            y += Ui.Wrapped(_selected.Name, x + 84 * s, y, w - 84 * s, 26, Theme.Text, true, 1.15f);
            Ui.Text(sub.Formula, x + 84 * s, y + 2 * s, 30, Theme.Accent, true);
            y = Math.Max(y + 44 * s, start + 76 * s);
            if (!string.IsNullOrEmpty(sub.Name) && sub.Name != _selected.Name)
            {
                Ui.Text(sub.Name, x, y, 15, Theme.Muted);
                y += 24 * s;
            }

            if (sub.Aliases.Count > 0)
            {
                var aliases = sub.Aliases.Where(a => Normalize(a) != _selected.FormulaKey && a != sub.ElementSymbol).ToList();
                if (aliases.Count > 0) y += Ui.Wrapped("Diğer adları: " + string.Join(", ", aliases), x, y, w, 15, Theme.Text, false, 1.3f) + 4 * s;
            }

            // Hazards.
            var hz = Textures.PictogramsFor(sub.Hazards).ToList();
            if (hz.Count > 0)
            {
                float px = x;
                foreach (var h in hz)
                {
                    var pic = Textures.Pictogram(h);
                    DrawTexturePro(pic, new Rectangle(0, 0, pic.Width, pic.Height), new Rectangle(px, y, 62 * s, 62 * s), Vector2.Zero, 0, Color.White);
                    px += 66 * s;
                }

                y += 66 * s;
                y += Ui.Wrapped(string.Join(" · ", hz.Select(Tr.Hazard)), x, y, w, 14, Theme.Warning, true, 1.3f) + 6 * s;
            }
            else if (sub.DataLimited)
            {
                y += Ui.Wrapped("Bu kayıtta tehlike verisi yok (CRC el kitabı: yalnızca formül ve fiziksel sabitler). Bilinmeyen maddeye tehlikeliymiş gibi davranın.",
                    x, y, w, 14, Theme.Warning, false, 1.3f) + 6 * s;
            }
            else
            {
                Ui.Text("GHS tehlike işareti yok (yine de dikkatli çalışın).", x, y, 14, Theme.Success);
                y += 26 * s;
            }

            var facts = new List<(string, string)>
            {
                ("Mol kütlesi", string.Format(ci, "{0:0.##} g/mol", sub.MolarMass)),
                ("25 °C'de hâli", PhaseName(sub)),
                ("Suda", sub.DataLimited ? "bilinmiyor (kayıtta veri yok)" : sub.Phase == SubstancePhase.Liquid ? (sub.Id == SubstanceIds.Water ? "—" : sub.Soluble || sub.Ions.Count > 0 ? "karışır / çözünür" : "çözünmez")
                    : sub.Phase == SubstancePhase.Aqueous && !sub.IsGas ? "çözünmüş hâlde" : sub.Soluble ? "çözünür" : "çözünmez"),
                ("Türü", KindName(sub)),
                ("Kategori", CategoryName(_selected.Category)),
            };
            if (sub.IsIonic && sub.CationKey != "H" && sub.Ions.Count > 0 && (sub.Soluble || sub.Phase == SubstancePhase.Aqueous))
            {
                facts.Add(("Suda iyonları", string.Join(" + ", sub.Ions.Select(i => (i.Count > 1 ? i.Count + " " : "") + Elements.PrettySpecies(i.SpeciesId)))));
            }

            if (!double.IsNaN(sub.BoilingPointC) && sub.Phase == SubstancePhase.Liquid) facts.Add(("Kaynama noktası", string.Format(ci, "{0:0.#} °C", sub.BoilingPointC)));
            if (!sub.DensityEstimated && (sub.Phase == SubstancePhase.Liquid || (sub.Category != "ionic" && sub.Phase == SubstancePhase.Solid)))
            {
                facts.Add(("Yoğunluk", string.Format(ci, "{0:0.###} g/mL", sub.Density)));
            }

            if (sub.ElementSymbol != null)
            {
                var el = Elements.All.FirstOrDefault(e => e.Symbol == sub.ElementSymbol);
                if (el != null) facts.Add(("Atom numarası", el.Z.ToString(CultureInfo.InvariantCulture)));
            }

            if (!string.IsNullOrEmpty(sub.Cas)) facts.Add(("CAS numarası", sub.Cas));

            foreach (var (k, v) in facts)
            {
                Ui.Text(k, x, y, 15, Theme.Muted);
                float hgt = Ui.Wrapped(v, x + 170 * s, y, w - 170 * s, 15, Theme.Text, true, 1.25f);
                y += Math.Max(26 * s, hgt + 6 * s);
            }

            _detailContent = y + _detailScroll.Offset - _detail.Y + 10 * s;
            EndScissorMode();
            DrawAddButtons(new Rectangle(r.X + 18 * s, r.Bottom() - 210 * s, r.Width - 36 * s, 196 * s), sub);
        }

        private void DrawAddButtons(Rectangle area, Substance sub)
        {
            float s = Ui.S;
            DrawRectangle((int)area.X, (int)area.Y, (int)area.Width, Math.Max(1, (int)s), Theme.Border);
            Ui.Text("TEZGÂHA EKLE", area.X, area.Y + 12 * s, 15, Theme.Text, true);
            var options = new List<(string label, Action act)>();
            string f = sub.Formula;
            bool soluble = sub.Soluble || sub.Phase == SubstancePhase.Aqueous;
            if (sub.IsGas)
            {
                if (soluble)
                {
                    options.Add(("Sulu çözelti 0,1 M", () => AddSolution(sub, 0.1)));
                    options.Add(("Sulu çözelti 1 M", () => AddSolution(sub, 1.0)));
                }
            }
            else if (sub.Phase == SubstancePhase.Solid)
            {
                options.Add(("Kavanoz 5 g", () => AddJar(sub, 5)));
                options.Add(("Kavanoz 20 g", () => AddJar(sub, 20)));
                if (sub.Soluble)
                {
                    options.Add(("Çözelti 0,1 M (250 mL)", () => AddSolution(sub, 0.1)));
                    options.Add(("Çözelti 1 M (250 mL)", () => AddSolution(sub, 1.0)));
                }
            }
            else if (sub.Phase == SubstancePhase.Liquid)
            {
                options.Add(("Şişe 100 mL (saf)", () => AddLiquid(sub, 100)));
                options.Add(("Şişe 250 mL (saf)", () => AddLiquid(sub, 250)));
                if (sub.Soluble && sub.Id != SubstanceIds.Water) options.Add(("Sulu çözelti 1 M", () => AddSolution(sub, 1.0)));
            }
            else
            {
                options.Add(("Çözelti 0,1 M (250 mL)", () => AddSolution(sub, 0.1)));
                options.Add(("Çözelti 1 M (250 mL)", () => AddSolution(sub, 1.0)));
            }

            if (options.Count == 0)
            {
                Ui.Wrapped($"{Tr.Substance(sub)} oda sıcaklığında bir gazdır ve suda çözünmez; okul laboratuvarında tüpte saklanır. Simülasyonda tezgâha eklenemez.",
                    area.X, area.Y + 44 * s, area.Width, 15, Theme.Muted, false, 1.3f);
                return;
            }

            float bw = (area.Width - 10 * s) / 2, bh = 60 * s;
            for (int i = 0; i < options.Count; i++)
            {
                var rc = new Rectangle(area.X + (i % 2) * (bw + 10 * s), area.Y + 44 * s + (i / 2) * (bh + 10 * s), bw, bh);
                if (Ui.Button("chem-add" + i, rc, options[i].label, Gfx.Lerp(Theme.Success, Theme.Button, 0.6f), 16)) options[i].act();
            }
        }

        // ------------------------------------------------------------------ spawning

        private static uint Accent(Substance sub)
        {
            var c = Gfx.FromCore(sub.Color);
            if (c.R > 225 && c.G > 225 && c.B > 225) return 0x9AB0C8;
            return ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        }

        private static bool LightSensitive(Substance sub) =>
            sub.CationKey == "Ag" || sub.AnionKey == "MnO4" || sub.Id == SubstanceIds.HydrogenPeroxide || sub.Formula.StartsWith("HNO", StringComparison.Ordinal);

        private void AddSolution(Substance sub, double molarity)
        {
            string conc = molarity.ToString("0.0#", CultureInfo.CurrentCulture);
            string id = $"custom.sol.{sub.Id}.{molarity.ToString("0.###", CultureInfo.InvariantCulture)}";
            Spawn(id, () => EquipmentCatalog.CreateBottle(id, $"{sub.Formula} {conc} M",
                ReagentPreset.Solution($"{conc} M {Tr.Substance(sub)}", 250, sub.Id, molarity), Accent(sub), LightSensitive(sub)), Tr.Substance(sub) + " çözeltisi");
        }

        private void AddLiquid(Substance sub, double ml)
        {
            string id = $"custom.liq.{sub.Id}.{ml:0}";
            Spawn(id, () => EquipmentCatalog.CreateBottle(id, $"{Tr.Substance(sub)} {ml:0} mL",
                ReagentPreset.Liquid(Tr.Substance(sub), sub.Id, ml, sub.Density, sub.MolarMass), Accent(sub), LightSensitive(sub)), Tr.Substance(sub));
        }

        private void AddJar(Substance sub, double grams)
        {
            string id = $"custom.jar.{sub.Id}.{grams:0}";
            Spawn(id, () => EquipmentCatalog.CreateJar(id, $"{Tr.Substance(sub)} {grams:0} g", sub.Id, grams, Accent(sub),
                $"{Tr.Substance(sub)} ({sub.Formula}), {grams:0} g"), Tr.Substance(sub));
        }

        private void Spawn(string id, Func<EquipmentDefinition> create, string what)
        {
            var catalog = _lab.World.Catalog;
            if (!catalog.Contains(id)) catalog.Register(create());
            var def = catalog.Get(id);
            Thumbnails.Ensure(def);
            var item = _lab.Spawn(id, _lab.FreeSpot() + new Vector3(0, 0.03f, 0));
            _lab.Selected = item;
            _hud.Tab = HudTab.Lab;
            _keyboard = false;
            _focused = false;
            _hud.Toast($"{what} tezgâha eklendi.", Theme.Success);
        }

        // ------------------------------------------------------------------ on-screen keyboard

        private void DrawKeyboard()
        {
            float s = Ui.S;
            var r = _keyboardRect;
            Ui.Panel(r, Gfx.WithAlpha(Theme.TopBar, 0.98f));
            DrawRectangleRoundedLinesEx(r, 0.06f, 8, 2 * s, Theme.Border);
            float gap = 8 * s, kh = (r.Height - 6 * gap) / 5;
            var tr = new CultureInfo("tr-TR");
            for (int row = 0; row < KeyRows.Length; row++)
            {
                var keys = KeyRows[row];
                float kw = (r.Width - gap * 13) / 12;
                float rowW = keys.Length * kw + (keys.Length - 1) * gap;
                float x = r.X + (r.Width - rowW) / 2 - (row == 0 ? (kw + gap) / 2 : 0);
                float y = r.Y + gap + row * (kh + gap);
                foreach (var k in keys)
                {
                    if (Ui.Button("kb-" + k, new Rectangle(x, y, kw, kh), k, null, 22)) Type(k.ToLower(tr));
                    x += kw + gap;
                }

                if (row == 0 && Ui.Button("kb-back", new Rectangle(x, y, kw * 1.6f, kh), "Sil", Gfx.Lerp(Theme.Warning, Theme.Button, 0.6f), 18) && _query.Length > 0)
                {
                    _query = _query.Substring(0, _query.Length - 1);
                }
            }

            float by = r.Y + gap + 4 * (kh + gap), bx = r.X + gap * 2;
            float unit = (r.Width - gap * 6) / 10;
            if (Ui.Button("kb-clear", new Rectangle(bx, by, unit * 2, kh), "Temizle", null, 18)) _query = "";
            if (Ui.Button("kb-space", new Rectangle(bx + unit * 2 + gap, by, unit * 5, kh), "Boşluk", null, 18)) Type(" ");
            if (Ui.Button("kb-close", new Rectangle(bx + unit * 7 + gap * 2, by, unit * 3 - gap * 2, kh), "Tamam", Theme.AccentDim, 18))
            {
                _keyboard = false;
            }
        }

        private void Type(string text)
        {
            if (_query.Length < 40) _query += text;
            _focused = true;
        }
    }
}
