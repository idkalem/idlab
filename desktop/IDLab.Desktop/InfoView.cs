using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Model;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// "Bilmemiz Gerekenler" tab: a topic list on the left and a touch-scrollable page on the right (safety rules,
    /// hazard pictograms, equipment, pH, reaction types, activity series, formulas, heat, separation, periodic trends).
    /// Pages are drawn in immediate mode with a running cursor, so content height is known after each frame.
    /// </summary>
    internal sealed class InfoView
    {
        private sealed class Topic
        {
            public string Title, Subtitle;
            public Color Color;
            public Action Body;
        }

        private readonly Lab _lab;
        private readonly List<Topic> _topics = new List<Topic>();
        private readonly DragScroll _scroll = new DragScroll();
        private int _current;
        private Rectangle _list, _page, _content;
        private float _contentHeight;
        private float _x, _y, _w; // page cursor

        public InfoView(Lab lab)
        {
            _lab = lab;
            Add("Laboratuvar güvenliği", "Kazaların çoğu basit kurallarla önlenir.", 0xFF5A4F, SafetyPage);
            Add("Tehlike işaretleri (GHS)", "Şişe etiketlerindeki kırmızı çerçeveli simgeler.", 0xFFB347, HazardPage);
            Add("Laboratuvar malzemeleri", "Hangi malzeme ne işe yarar?", 0x35C4FF, EquipmentPage);
            Add("Asitler, bazlar ve pH", "pH ölçeği, indikatörler ve nötrleşme.", 0x4FE07A, AcidBasePage);
            Add("Tepkime türleri", "Laboratuvarda deneyebileceğiniz tepkimeler.", 0xC07BFF, ReactionsPage);
            Add("Metallerin aktifliği", "Hangi metal asitte çözünür, hangisi çözünmez?", 0xF7C948, ActivityPage);
            Add("Mol kavramı ve formüller", "Mol, derişim, seyreltme, gazlar.", 0x4DB8FF, MolePage);
            Add("Isı ve hâl değişimi", "Q = m·c·ΔT ve ısınma eğrisi.", 0xFF7A45, HeatPage);
            Add("Ayırma yöntemleri", "Karışımlar fiziksel yollarla nasıl ayrılır?", 0x3CC9A0, SeparationPage);
            Add("Periyodik özellikler", "Grup, periyot ve değişim eğilimleri.", 0xB57CFF, TrendsPage);
            Add("Fizik formülleri", "Hareket, kuvvet, enerji, elektrik.", 0x8BD17C, PhysicsPage);
            Add("Mikroskop ve hücre", "Mikroskop kullanımı ve hücre organelleri.", 0xFF7EB6, BiologyPage);
        }

        public void Select(int index)
        {
            _current = Math.Clamp(index, 0, _topics.Count - 1);
            _scroll.Reset();
        }

        private void Add(string title, string subtitle, uint color, Action body) =>
            _topics.Add(new Topic { Title = title, Subtitle = subtitle, Color = Gfx.Rgba(color), Body = body });

        // ------------------------------------------------------------------ frame

        public void Update(float dt, Rectangle area)
        {
            float s = Ui.S, pad = 10 * s;
            _list = new Rectangle(area.X + pad, area.Y + pad, 360 * s, area.Height - 2 * pad);
            _page = new Rectangle(_list.Right() + pad, area.Y + pad, area.Width - _list.Width - 3 * pad, area.Height - 2 * pad);
            _content = new Rectangle(_page.X + 30 * s, _page.Y + 112 * s, _page.Width - 60 * s, _page.Height - 124 * s);
            _scroll.Update(_content, _contentHeight + 30 * s, dt);
        }

        public void Draw()
        {
            float s = Ui.S;
            DrawTopicList();

            var topic = _topics[_current];
            Ui.Panel(_page, Theme.Panel);
            DrawRectangleRounded(new Rectangle(_page.X + 30 * s, _page.Y + 26 * s, 8 * s, 62 * s), 1f, 4, topic.Color);
            Ui.Text(topic.Title, _page.X + 54 * s, _page.Y + 22 * s, 34, Theme.Text, true);
            Ui.Text(topic.Subtitle, _page.X + 56 * s, _page.Y + 66 * s, 17, Theme.Muted);
            DrawRectangle((int)_content.X, (int)(_content.Y - 8 * s), (int)_content.Width, Math.Max(1, (int)s), Theme.Border);

            BeginScissorMode((int)_content.X, (int)_content.Y, (int)_content.Width, (int)_content.Height);
            _x = _content.X;
            _w = _content.Width - 18 * s;
            _y = _content.Y + 10 * s - _scroll.Offset;
            float start = _y;
            topic.Body();
            _contentHeight = _y - start;
            EndScissorMode();

            // Scroll indicator.
            if (_contentHeight > _content.Height)
            {
                float trackH = _content.Height;
                float barH = Math.Max(40 * s, trackH * _content.Height / (_contentHeight + 30 * s));
                float t = _scroll.Offset / Math.Max(1, _contentHeight + 30 * s - _content.Height);
                var bar = new Rectangle(_content.Right() - 6 * s, _content.Y + (trackH - barH) * t, 6 * s, barH);
                DrawRectangleRounded(bar, 1f, 4, Gfx.WithAlpha(Theme.Muted, 0.5f));
            }
        }

        private void DrawTopicList()
        {
            float s = Ui.S;
            var r = _list;
            Ui.Panel(r, Theme.Panel);
            Ui.Text("BİLMEMİZ GEREKENLER", r.X + 18 * s, r.Y + 18 * s, 19, Theme.Text, true);
            float rowH = Math.Min(64 * s, (r.Height - 70 * s) / _topics.Count - 6 * s);
            float y = r.Y + 58 * s;
            for (int i = 0; i < _topics.Count; i++)
            {
                var t = _topics[i];
                var row = new Rectangle(r.X + 10 * s, y, r.Width - 20 * s, rowH);
                bool on = i == _current;
                if (Ui.Button("info-topic" + i, row, "", on ? Gfx.Lerp(t.Color, Theme.Raised, 0.72f) : Theme.Raised))
                {
                    _current = i;
                    _scroll.Reset();
                }

                DrawRectangleRounded(new Rectangle(row.X + 10 * s, row.Y + rowH * 0.22f, 6 * s, rowH * 0.56f), 1f, 4, t.Color);
                var m = Ui.Measure(t.Title, 17, true);
                Ui.Text(t.Title, row.X + 28 * s, row.Y + (rowH - m.Y) / 2, 17, on ? Theme.Text : Gfx.Lerp(Theme.Text, Theme.Muted, 0.3f), true);
                y += rowH + 6 * s;
            }
        }

        // ------------------------------------------------------------------ page building blocks

        private void Heading(string text)
        {
            float s = Ui.S;
            _y += 10 * s;
            Ui.Text(text, _x, _y, 23, Theme.Text, true);
            _y += 32 * s;
            DrawRectangle((int)_x, (int)_y, (int)(70 * s), Math.Max(2, (int)(3 * s)), _topics[_current].Color);
            _y += 16 * s;
        }

        private void Paragraph(string text, float size = 17, Color? color = null)
        {
            _y += Ui.Wrapped(text, _x, _y, _w, size, color ?? Theme.Text, false, 1.35f);
            _y += 8 * Ui.S;
        }

        private void Bullets(params string[] lines)
        {
            float s = Ui.S;
            foreach (var line in lines)
            {
                DrawCircleV(new Vector2(_x + 8 * s, _y + 12 * s), 4 * s, _topics[_current].Color);
                _y += Ui.Wrapped(line, _x + 24 * s, _y, _w - 24 * s, 17, Theme.Text, false, 1.35f) + 6 * s;
            }

            _y += 6 * s;
        }

        private void Gap(float h) => _y += h * Ui.S;

        /// <summary>Cards in a grid: a bold title (optionally large, for formulas) above wrapped body text.</summary>
        private void Cards(int columns, float titleSize, params (string title, string body, Color accent)[] cards)
        {
            float s = Ui.S, gap = 12 * s;
            columns = _w < 760 * s ? Math.Max(1, columns - 1) : columns;
            float cw = (_w - gap * (columns - 1)) / columns;
            for (int i = 0; i < cards.Length; i += columns)
            {
                float rowH = 0;
                for (int j = i; j < Math.Min(cards.Length, i + columns); j++)
                {
                    float h = Ui.Wrapped(cards[j].title, 0, 0, cw - 40 * s, titleSize, Theme.Text, true, 1.2f, draw: false)
                              + Ui.Wrapped(cards[j].body, 0, 0, cw - 40 * s, 16, Theme.Text, false, 1.35f, draw: false);
                    rowH = Math.Max(rowH, h + 36 * s);
                }

                for (int j = i; j < Math.Min(cards.Length, i + columns); j++)
                {
                    var r = new Rectangle(_x + (j - i) * (cw + gap), _y, cw, rowH);
                    DrawRectangleRounded(r, Math.Min(0.2f, 24 * s / rowH), 8, Theme.Raised);
                    DrawRectangleRounded(new Rectangle(r.X, r.Y + 14 * s, 5 * s, rowH - 28 * s), 1f, 4, cards[j].accent);
                    float ty = r.Y + 16 * s;
                    ty += Ui.Wrapped(cards[j].title, r.X + 22 * s, ty, cw - 40 * s, titleSize, cards[j].accent, true, 1.2f);
                    Ui.Wrapped(cards[j].body, r.X + 22 * s, ty + 2 * s, cw - 40 * s, 16, Theme.Text, false, 1.35f);
                }

                _y += rowH + gap;
            }

            _y += 6 * s;
        }

        private void Cards(int columns, params (string title, string body, Color accent)[] cards) => Cards(columns, 19, cards);

        /// <summary>A table with a header row; column widths are fractions of the page width.</summary>
        private void Table(float[] widths, string[] header, params string[][] rows)
        {
            float s = Ui.S, padX = 14 * s, padY = 10 * s;
            float total = widths.Sum();
            var cols = widths.Select(w => w / total * _w).ToArray();

            void Row(string[] cells, bool head, int index)
            {
                float h = 0;
                for (int c = 0; c < cells.Length; c++)
                {
                    h = Math.Max(h, Ui.Wrapped(cells[c], 0, 0, cols[c] - 2 * padX, head ? 15 : 16, Theme.Text, head || c == 0, 1.3f, draw: false));
                }

                h += 2 * padY;
                var bg = head ? Gfx.Lerp(_topics[_current].Color, Theme.Raised, 0.78f) : index % 2 == 0 ? Theme.Raised : Gfx.Lerp(Theme.Raised, Theme.Panel, 0.6f);
                DrawRectangleRec(new Rectangle(_x, _y, _w, h), bg);
                float cx = _x;
                for (int c = 0; c < cells.Length; c++)
                {
                    Ui.Wrapped(cells[c], cx + padX, _y + padY, cols[c] - 2 * padX, head ? 15 : 16, head ? Theme.Text : c == 0 ? Theme.Text : Gfx.Lerp(Theme.Text, Theme.Muted, 0.25f), head || c == 0, 1.3f);
                    cx += cols[c];
                }

                _y += h;
            }

            Row(header, true, -1);
            for (int i = 0; i < rows.Length; i++) Row(rows[i], false, i);
            _y += 18 * s;
        }

        private static Color C(uint rgb) => Gfx.Rgba(rgb);

        // ------------------------------------------------------------------ pages

        private void SafetyPage()
        {
            Paragraph("Laboratuvara girmeden önce bu kuralları okuyun. Simülasyonda yaptığınız hatalar güvenlidir; gerçek laboratuvarda aynı hata yaralanmaya yol açabilir.");
            Cards(2,
                ("Koruyucu donanım", "Önlük, koruyucu gözlük ve gerektiğinde eldiven giyin. Uzun saçları toplayın, açık ayakkabı giymeyin.", C(0x35C4FF)),
                ("Yemek, içmek, tatmak yasak", "Laboratuvarda yiyecek ve içecek bulundurulmaz. Kimyasallar tadılmaz, şişeye eğilip koklanmaz; gerekirse elle yelpazelenerek koklanır.", C(0xFFB347)),
                ("Asit suya eklenir", "Seyreltirken her zaman ASİT, SUYUN içine yavaşça eklenir; suyu asidin üstüne dökmeyin. Açığa çıkan ısı sıçramaya yol açar.", C(0xFF5A4F)),
                ("Isıtırken dikkat", "Deney tüpünün ağzını kimseye doğru tutmayın, tüp maşası kullanın. Etanol gibi yanıcı sıvıları açık alevde değil, ısıtıcı tablada ısıtın.", C(0xFF7A45)),
                ("Rastgele karıştırmayın", "Çamaşır suyu + asit → zehirli klor gazı. Çamaşır suyu + amonyak → zehirli kloramin. Ne olacağını bilmediğiniz kimyasalları karıştırmayın.", C(0xFF5A4F)),
                ("Çeker ocak", "Zehirli ya da kötü kokulu gaz çıkaran deneyler çeker ocağın içinde, camı aşağı indirilerek yapılır.", C(0x4FE07A)),
                ("Etiket ve miktar", "Etiketsiz kimyasal kullanmayın. Gerektiği kadar alın; artan kimyasalı şişesine geri dökmeyin, şişenin kapağını hemen kapatın.", C(0xC07BFF)),
                ("Atıklar", "Kimyasal atıkları lavaboya değil, öğretmeninizin gösterdiği atık kabına dökün. Kırık camları ayrı kutuya atın.", C(0x3CC9A0)),
                ("Kaza anında", "Göze kimyasal kaçarsa göz duşunda en az 15 dakika yıkayın. Cilde dökülürse bol suyla yıkayın. Küçük yangında kabın ağzını kapatın; her kazayı hemen öğretmene bildirin.", C(0xFF5A4F)),
                ("Düzenli çalışın", "Tezgâhı temiz tutun, çantaları uzağa koyun. Sıcak cam ile soğuk cam aynı görünür: dokunmadan önce kontrol edin.", C(0x8A9BB4)));
            Heading("Acil durum donanımları");
            Bullets(
                "Göz duşu ve acil duş: Laboratuvarın arka köşesinde. Kimyasal sıçradığında hemen kullanılır.",
                "Yangın tüpü ve yangın battaniyesi: Sağ duvarda. Elektrik ve sıvı yangınlarında su kullanılmaz.",
                "İlk yardım dolabı ve acil çıkış: Yerlerini ilk derste öğrenin.");
        }

        private void HazardPage()
        {
            float s = Ui.S;
            Paragraph("GHS (Küresel Uyumlaştırılmış Sistem), kimyasal tehlikeleri dünyanın her yerinde aynı simgelerle gösterir. Etikette ayrıca bir uyarı kelimesi bulunur: TEHLİKE (ciddi) ya da DİKKAT (daha az ciddi).");
            var db = _lab.World.Substances;
            var info = new (Hazard h, string meaning)[]
            {
                (Hazard.Explosive, "Isı, darbe ya da sürtünmeyle patlayabilir (ör. TNT, nitrogliserin)."),
                (Hazard.Flammable, "Alev, kıvılcım ya da sıcak yüzeyle kolayca tutuşur; suyla yanıcı gaz çıkaranlar da bu simgeyi taşır (ör. sodyum)."),
                (Hazard.Oxidizer, "Oksijen verir; yanmayı şiddetlendirir. Yanıcı maddelerden uzak tutulur."),
                (Hazard.CompressedGas, "Basınç altında gaz içerir; ısınınca patlayabilir, soğuk gaz yakabilir."),
                (Hazard.Corrosive, "Deriyi yakar, gözde kalıcı hasar bırakabilir, metalleri aşındırır."),
                (Hazard.Toxic, "Az miktarı bile yutulduğunda, solunduğunda ya da deriye değdiğinde zehirler."),
                (Hazard.Irritant, "Deriyi, gözü veya solunum yolunu tahriş eder; alerji yapabilir ya da zararlıdır."),
                (Hazard.HealthHazard, "Uzun sürede kansere, genetik hasara ya da solunum hassasiyetine yol açabilir."),
                (Hazard.Environment, "Su canlıları için zararlıdır. Lavaboya dökülmez, atık kabına konur."),
            };

            int columns = _w > 900 * s ? 2 : 1;
            float gap = 12 * s, cw = (_w - gap * (columns - 1)) / columns, cardH = 150 * s;
            for (int i = 0; i < info.Length; i++)
            {
                var (h, meaning) = info[i];
                var r = new Rectangle(_x + (i % columns) * (cw + gap), _y, cw, cardH);
                DrawRectangleRounded(r, 0.12f, 8, Theme.Raised);
                var pic = Textures.Pictogram(h);
                DrawTexturePro(pic, new Rectangle(0, 0, pic.Width, pic.Height), new Rectangle(r.X + 14 * s, r.Y + 15 * s, 120 * s, 120 * s), Vector2.Zero, 0, Color.White);
                float tx = r.X + 150 * s, tw = r.Width - 166 * s;
                Ui.Text(Tr.Hazard(h), tx, r.Y + 14 * s, 20, Theme.Text, true);
                string code = "GHS0" + Textures.GhsNumber(h).ToString(CultureInfo.InvariantCulture);
                Ui.Text(code, r.Right() - Ui.Measure(code, 14, true).X - 16 * s, r.Y + 17 * s, 14, Theme.Muted, true);
                float ty = r.Y + 44 * s + Ui.Wrapped(meaning, tx, r.Y + 44 * s, tw, 15, Theme.Text, false, 1.3f);
                var mask = h == Hazard.Flammable ? Hazard.Flammable | Hazard.WaterReactive : h;
                var examples = db.All.Where(x => !x.Hidden && (x.Hazards & mask) != 0).OrderBy(x => x.Name.Length).Select(Tr.Substance).Distinct().Take(5).ToList();
                if (examples.Count > 0)
                {
                    Ui.Wrapped("Bu laboratuvarda: " + string.Join(", ", examples), tx, ty + 4 * s, tw, 14, Theme.Muted, false, 1.25f);
                }

                if (i % columns == columns - 1 || i == info.Length - 1) _y += cardH + gap;
            }

            Gap(8);
            Heading("Etiket okuma");
            Bullets(
                "Ürün adı ve formül: Şişede ne olduğunu ve derişimini (ör. HCl 1,0 M) gösterir.",
                "Uyarı kelimesi ve simgeler: Hangi tehlikeye karşı önlem alınacağını gösterir.",
                "Tehlike ifadeleri (H) ve önlem ifadeleri (P): Ör. \"Ciddi cilt yanıklarına yol açar\", \"Koruyucu gözlük kullanın\".",
                "Koyu renkli (amber) şişeler ışıkta bozunan kimyasallar içindir: gümüş nitrat, potasyum permanganat, hidrojen peroksit.");
        }

        private void EquipmentPage()
        {
            float s = Ui.S;
            Paragraph("Simülasyondaki malzemeler. Malzemeler panelinden tezgâha sürükleyerek hepsini deneyebilirsiniz.");
            var groups = _lab.World.Catalog.All
                .Where(d => d.Category == EquipmentCategory.Glassware || d.Category == EquipmentCategory.HeatSource ||
                            d.Category == EquipmentCategory.Sensor || d.Category == EquipmentCategory.Measuring)
                .Where(d => d.Id != EquipmentIds.Beaker250Water)
                .GroupBy(d => d.Category).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                Heading(Tr.Category(g.Key).Substring(0, 1) + Tr.Category(g.Key).Substring(1).ToLower(new CultureInfo("tr-TR")));
                var defs = g.ToList();
                int columns = Math.Max(1, (int)(_w / (300 * s)));
                float gap = 12 * s, cw = (_w - gap * (columns - 1)) / columns, cardH = 122 * s;
                for (int i = 0; i < defs.Count; i++)
                {
                    var r = new Rectangle(_x + (i % columns) * (cw + gap), _y, cw, cardH);
                    DrawRectangleRounded(r, 0.12f, 8, Theme.Raised);
                    Thumbnails.Draw(defs[i].Id, new Rectangle(r.X + 8 * s, r.Y + 8 * s, 106 * s, 106 * s));
                    Ui.Wrapped(Tr.Name(defs[i]), r.X + 124 * s, r.Y + 12 * s, cw - 134 * s, 16, Theme.Text, true, 1.2f);
                    Ui.Wrapped(Tr.Description(defs[i]), r.X + 124 * s, r.Y + 58 * s, cw - 134 * s, 13, Theme.Muted, false, 1.25f);
                    if (i % columns == columns - 1 || i == defs.Count - 1) _y += cardH + gap;
                }
            }

            Heading("Gerçek laboratuvardaki diğer malzemeler");
            Table(new[] { 1f, 2.2f }, new[] { "Malzeme", "Ne işe yarar?" },
                new[] { "Spor ve bağlama parçası", "Büret, huni ya da balon jojeyi dik ve sabit tutar." },
                new[] { "Huni ve süzgeç kâğıdı", "Katıyı sıvıdan süzerek ayırır (ör. AgCl çökeleği)." },
                new[] { "Porselen kapsül", "Çözeltiyi buharlaştırarak çözüneni kristal hâlde geri kazanır." },
                new[] { "Saat camı", "Küçük miktarda katıyı tartmak ya da kabın ağzını örtmek için." },
                new[] { "Spatül ve kaşık", "Katı kimyasalları şişeden almak için; her kimyasala temiz spatül." },
                new[] { "Pipet ve puar", "Belirli hacimde sıvıyı hassas aktarır. Ağızla pipetleme yapılmaz!" },
                new[] { "Tüp maşası", "Deney tüpünü alevde ısıtırken tutmak için." },
                new[] { "Desikatör", "Nem çeken maddeleri kuru tutar." },
                new[] { "Termometre (cıvasız)", "Sıcaklık ölçer; karıştırma çubuğu olarak kullanılmaz." });
        }

        private void AcidBasePage()
        {
            float s = Ui.S;
            Paragraph("pH, bir çözeltinin ne kadar asidik ya da bazik olduğunu gösterir. 25 °C'de pH < 7 asidik, pH = 7 nötr, pH > 7 baziktir. pH 1 azalınca H₃O⁺ derişimi 10 kat artar.");

            // pH scale coloured like universal indicator, with everyday examples above and below.
            float barX = _x + 20 * s, barW = _w - 40 * s, barH = 54 * s;
            float barY = _y + 110 * s;
            for (int i = 0; i < 140; i++)
            {
                double ph = i / 10.0;
                var c = Gfx.FromCore(IndicatorColors.UniversalIndicator(ph));
                c.A = 255;
                DrawRectangleRec(new Rectangle(barX + barW * i / 140f, barY, barW / 140f + 1, barH), c);
            }

            for (int p = 0; p <= 14; p++)
            {
                float px = barX + barW * p / 14f;
                string label = p.ToString(CultureInfo.InvariantCulture);
                var m = Ui.Measure(label, 17, true);
                Ui.Text(label, px - m.X / 2, barY + (barH - m.Y) / 2, 17, p >= 4 && p <= 10 ? new Color(20, 24, 32, 255) : Color.White, true);
            }

            var examples = new (double ph, string name)[]
            {
                (1.5, "Mide asidi"), (2.2, "Limon suyu"), (2.9, "Sirke"), (4.3, "Domates"), (5.0, "Kahve"), (5.6, "Yağmur"),
                (6.6, "Süt"), (7.0, "Saf su"), (7.4, "Kan"), (8.1, "Deniz suyu"), (8.3, "Karbonat"), (9.5, "Diş macunu"),
                (10.5, "Sabun"), (11.6, "Ev amonyağı"), (12.6, "Çamaşır suyu"), (13.8, "Lavabo açıcı"),
            };
            for (int i = 0; i < examples.Length; i++)
            {
                var (ph, name) = examples[i];
                float px = barX + barW * (float)(ph / 14);
                bool above = i % 2 == 0;
                int lane = i % 4 < 2 ? 0 : 1;
                float ly = above ? barY - 34 * s - lane * 36 * s : barY + barH + 12 * s + lane * 36 * s;
                DrawLineEx(new Vector2(px, above ? ly + 24 * s : barY + barH), new Vector2(px, above ? barY : ly), 1.5f * s, Theme.Muted);
                var m = Ui.Measure(name, 14, true);
                float lx = Math.Clamp(px - m.X / 2, _x, _x + _w - m.X);
                Ui.Text(name, lx, ly, 14, Theme.Text, true);
            }

            Ui.Text("◀ ASİDİK", barX, barY + barH + 88 * s, 16, Theme.Danger, true);
            var nm = Ui.Measure("NÖTR", 16, true);
            Ui.Text("NÖTR", barX + barW / 2 - nm.X / 2, barY + barH + 88 * s, 16, Theme.Success, true);
            var bm = Ui.Measure("BAZİK ▶", 16, true);
            Ui.Text("BAZİK ▶", barX + barW - bm.X, barY + barH + 88 * s, 16, C(0x7C8CFF), true);
            _y = barY + barH + 128 * s;

            Heading("Asit mi, baz mı?");
            Table(new[] { 1f, 1.6f, 1.6f }, new[] { "Özellik", "Asitler", "Bazlar" },
                new[] { "Suda verdiği iyon", "H⁺ (H₃O⁺)", "OH⁻" },
                new[] { "Tadı / dokusu", "Ekşi (asla tatmayın!)", "Acı, kayganlık hissi" },
                new[] { "Turnusol", "Maviyi kırmızıya çevirir", "Kırmızıyı maviye çevirir" },
                new[] { "Metallerle", "Aktif metallerle H₂ gazı çıkarır", "Amfoter metaller (Al, Zn) dışında tepkime vermez" },
                new[] { "Kuvvetli örnek", "HCl, HNO₃, H₂SO₄", "NaOH, KOH, Ca(OH)₂" },
                new[] { "Zayıf örnek", "CH₃COOH (sirke), H₂CO₃", "NH₃ (amonyak)" });

            Heading("İndikatörler");
            Paragraph("İndikatör, renk değiştirdiği pH aralığına göre seçilir. Aşağıdaki şeritler 0–14 pH ölçeğine göre çizilmiştir.", 16, Theme.Muted);
            var indicators = new (string name, double lo, double hi, Color a, Color b)[]
            {
                ("Turnusol", 5.0, 8.0, C(0xD83A3A), C(0x3A5CD8)),
                ("Metil oranj", 3.1, 4.4, C(0xE0302A), C(0xF2C531)),
                ("Bromtimol mavisi", 6.0, 7.6, C(0xE8D23A), C(0x2F5FD0)),
                ("Fenolftalein", 8.2, 10.0, C(0xF4F4F4), C(0xEE1F8C)),
            };
            foreach (var ind in indicators)
            {
                Ui.Text(ind.name, _x, _y + 6 * s, 16, Theme.Text, true);
                float sx = _x + 200 * s, sw = _w - 200 * s - 160 * s;
                for (int i = 0; i < 100; i++)
                {
                    double ph = i * 0.14;
                    float t = (float)Math.Clamp((ph - ind.lo) / (ind.hi - ind.lo), 0, 1);
                    DrawRectangleRec(new Rectangle(sx + sw * i / 100f, _y, sw / 100f + 1, 30 * s), Gfx.Lerp(ind.a, ind.b, t));
                }

                DrawRectangleLinesEx(new Rectangle(sx, _y, sw, 30 * s), 1, Theme.Border);
                Ui.Text(string.Format(CultureInfo.CurrentCulture, "pH {0:0.#}–{1:0.#}", ind.lo, ind.hi), sx + sw + 14 * s, _y + 6 * s, 15, Theme.Muted);
                _y += 42 * s;
            }

            Gap(10);
            Heading("Nötrleşme");
            Cards(2, 22,
                ("Asit + Baz → Tuz + Su", "HCl + NaOH → NaCl + H₂O. Tepkime ısı açığa çıkarır (ekzotermik); sıcaklık artışını termometreyle ölçebilirsiniz.", C(0x4FE07A)),
                ("pH = −log[H₃O⁺]", "pOH = −log[OH⁻] ve 25 °C'de pH + pOH = 14. Saf suda [H₃O⁺] = [OH⁻] = 10⁻⁷ M.", C(0x35C4FF)),
                ("Titrasyon", "Derişimi bilinen bazı büretten damla damla ekleyerek bilinmeyen asidin derişimi bulunur. Dönüm noktasında fenolftalein pembeleşir.", C(0xC07BFF)),
                ("Tampon çözelti", "Zayıf asit ve tuzu (ör. CH₃COOH + CH₃COONa) birlikte bulunursa az miktar asit/baz eklense de pH pek değişmez. Kanımız tampondur.", C(0xFFB347)));
        }

        private void ReactionsPage()
        {
            Paragraph("Kimyasal tepkimede atomlar yeniden düzenlenir; yeni maddeler oluşur. Toplam kütle ve atom sayısı korunur. (↑ gaz çıkışını, ↓ çökeleği gösterir.)");
            Table(new[] { 1.1f, 2.1f, 1.6f }, new[] { "Tür", "Örnek (bu laboratuvarda denenebilir)", "Gözlem" },
                new[] { "Nötrleşme", "HCl + NaOH → NaCl + H₂O", "Isınma, indikatör rengi değişir" },
                new[] { "Metal + asit", "Zn + 2HCl → ZnCl₂ + H₂↑", "Metal yüzeyinde kabarcıklar" },
                new[] { "Karbonat + asit", "NaHCO₃ + CH₃COOH → CH₃COONa + H₂O + CO₂↑", "Köpürme" },
                new[] { "Çökelme", "AgNO₃ + NaCl → AgCl↓ + NaNO₃", "Beyaz bulanıklık, dibe çöken katı" },
                new[] { "Çökelme", "CuSO₄ + 2NaOH → Cu(OH)₂↓ + Na₂SO₄", "Açık mavi çökelek" },
                new[] { "Yer değiştirme (redoks)", "Fe + CuSO₄ → FeSO₄ + Cu", "Demir bakırla kaplanır, mavi renk solar" },
                new[] { "Yer değiştirme (redoks)", "Cu + 2AgNO₃ → Cu(NO₃)₂ + 2Ag", "Gümüş kristalleri, çözelti maviye döner" },
                new[] { "Ayrışma (katalizli)", "2H₂O₂ → 2H₂O + O₂↑  (KI katalizör)", "Hızlı köpük: \"fil diş macunu\"" },
                new[] { "Aktif metal + su", "2Na + 2H₂O → 2NaOH + H₂↑", "Şiddetli tepkime; patlayabilir!" },
                new[] { "Yanma", "C₂H₅OH + 3O₂ → 2CO₂ + 3H₂O", "Alev, ısı ve ışık" });
            Heading("Tepkime olduğunu nasıl anlarız?");
            Bullets("Renk değişimi", "Gaz çıkışı (kabarcık, köpük, koku)", "Çökelek oluşması (bulanıklık)", "Sıcaklık değişimi (ısınma ya da soğuma)", "Işık veya ses çıkması");
            Heading("Tepkime hızını etkileyen etkenler");
            Cards(2,
                ("Sıcaklık", "Sıcaklık arttıkça tanecikler daha hızlı ve daha sık çarpışır; tepkime hızlanır.", C(0xFF7A45)),
                ("Derişim", "Daha derişik çözeltide birim hacimde daha çok tanecik vardır; çarpışma sayısı artar.", C(0x35C4FF)),
                ("Temas yüzeyi", "Toz hâlindeki katı, parça hâlindekinden daha hızlı tepkimeye girer.", C(0x4FE07A)),
                ("Katalizör", "Tepkimeyi hızlandırır ama kendisi harcanmaz (ör. H₂O₂ bozunmasında KI).", C(0xC07BFF)));
        }

        private void ActivityPage()
        {
            float s = Ui.S;
            Paragraph("Metaller elektron verme isteklerine göre sıralanır. Soldaki metal daha aktiftir: daha kolay iyon olur ve sağındaki metalin iyonunu çözeltiden metal olarak çıkarır.");
            string[] series = { "K", "Ca", "Na", "Mg", "Al", "Zn", "Fe", "Ni", "Sn", "Pb", "H", "Cu", "Hg", "Ag", "Pt", "Au" };
            float cell = Math.Min(86 * s, _w / series.Length - 6 * s);
            float x = _x;
            for (int i = 0; i < series.Length; i++)
            {
                var el = Elements.All.First(e => e.Symbol == series[i]);
                var r = new Rectangle(x, _y, cell, cell);
                float t = i / (float)(series.Length - 1);
                var c = series[i] == "H" ? C(0x8A9BB4) : Gfx.Lerp(C(0xFF5A4F), C(0xF7C948), t);
                DrawRectangleRounded(r, 0.18f, 6, c);
                Ui.Text(el.Z.ToString(CultureInfo.InvariantCulture), r.X + 6 * s, r.Y + 4 * s, 11, new Color(20, 20, 28, 220), true);
                var m = Ui.Measure(el.Symbol, 26, true);
                Ui.Text(el.Symbol, r.X + (cell - m.X) / 2, r.Y + (cell - m.Y) / 2 + 4 * s, 26, new Color(20, 20, 28, 255), true);
                x += cell + 6 * s;
            }

            _y += cell + 10 * s;
            Ui.Text("◀ daha aktif (kolay tepkime verir)", _x, _y, 15, Theme.Danger, true);
            var nm = Ui.Measure("daha soy (zor tepkime verir) ▶", 15, true);
            Ui.Text("daha soy (zor tepkime verir) ▶", x - 6 * s - nm.X, _y, 15, C(0xF7C948), true);
            _y += 40 * s;

            Heading("Ne işimize yarar?");
            Bullets(
                "H'nin solundaki metaller (Mg, Zn, Fe ...) seyreltik HCl veya H₂SO₄ ile H₂ gazı çıkarır. Sağdakiler (Cu, Ag, Au) çıkarmaz.",
                "Sırada ne kadar soldaysa o kadar hızlı: laboratuvarda Mg hızlı, Zn orta hızda, Fe yavaş kabarcık çıkarır; Cu hiç tepkime vermez.",
                "Aktif metal, daha az aktif metalin tuz çözeltisine konursa onu metal olarak çöktürür: Fe + CuSO₄ → FeSO₄ + Cu.",
                "K, Na, Ca soğuk suyla bile tepkime verir; bu yüzden sodyum gaz yağı içinde saklanır.",
                "Au ve Pt neredeyse hiç tepkime vermez; bu yüzden takı ve elektronikte kullanılır.");
            Heading("Laboratuvarda deneyin");
            Table(new[] { 1.2f, 1.6f, 1.6f }, new[] { "Deney", "Ne yapılır?", "Beklenen" },
                new[] { "Metal + asit", "Tüplere Mg, Zn, Fe, Cu koyup HCl ekleyin.", "Mg > Zn > Fe kabarcık; Cu değişmez." },
                new[] { "Demir çivisi", "CuSO₄ çözeltisine demir tozu ekleyin.", "Kırmızımsı bakır, mavi renk açılır." },
                new[] { "Gümüş ağacı", "AgNO₃ çözeltisine bakır talaş ekleyin.", "Gri gümüş, çözelti maviye döner." });
        }

        private void MolePage()
        {
            Paragraph("Kimyada tanecikler mol ile sayılır. 1 mol, 6,022 × 10²³ tanecik demektir (Avogadro sayısı).");
            Cards(2, 26,
                ("n = m / M", "Mol sayısı = kütle (g) / mol kütlesi (g/mol).\nÖrnek: 18 g su (M = 18 g/mol) = 1 mol.", C(0x4DB8FF)),
                ("N = n · Nₐ", "Tanecik sayısı = mol × 6,022 × 10²³.\nÖrnek: 0,5 mol demir ≈ 3,01 × 10²³ atom.", C(0xC07BFF)),
                ("M = n / V", "Molarite (mol/L) = çözünen mol / çözelti hacmi (L).\nÖrnek: 0,1 mol NaCl, 500 mL'de 0,2 M.", C(0x4FE07A)),
                ("M₁·V₁ = M₂·V₂", "Seyreltme: su eklenince mol sayısı değişmez.\nÖrnek: 10 mL 1 M HCl → 100 mL'ye seyreltilirse 0,1 M.", C(0xFFB347)),
                ("M(asit)·V(asit) = M(baz)·V(baz)", "Titrasyonda dönüm noktası (tek protonlu asit, tek hidroksitli baz).\nÖrnek: 25 mL 0,1 M NaOH, 25 mL HCl'yi nötrlerse HCl 0,1 M.", C(0xFF7A45)),
                ("P·V = n·R·T", "İdeal gaz denklemi. R = 0,082 L·atm/(mol·K), T kelvin.\nNormal koşullarda (0 °C, 1 atm) 1 mol gaz 22,4 L.", C(0x35C4FF)),
                ("d = m / V", "Yoğunluk (g/mL). Suyun yoğunluğu ≈ 1 g/mL.\nÖrnek: 100 mL etanol ≈ 79 g.", C(0x3CC9A0)),
                ("% kütlece = m(çözünen) / m(çözelti) × 100", "Örnek: 20 g tuz + 80 g su → %20'lik çözelti.", C(0xFF7EB6)));
            Heading("Kütlenin korunumu");
            Paragraph("Kapalı bir kapta tepkimeden önceki toplam kütle, tepkimeden sonraki toplam kütleye eşittir (Lavoisier). Açık kapta gaz çıkan tepkimelerde (ör. karbonat + asit) terazi azalma gösterir, çünkü CO₂ havaya karışır. Terazi deneyinde bunu gözleyebilirsiniz.");
            Heading("Sık kullanılan mol kütleleri (g/mol)");
            Table(new[] { 1f, 1f, 1f, 1f }, new[] { "Madde", "M", "Madde", "M" },
                new[] { "H₂O", "18", "NaCl", "58,5" },
                new[] { "HCl", "36,5", "NaOH", "40" },
                new[] { "H₂SO₄", "98", "CaCO₃", "100" },
                new[] { "CO₂", "44", "CuSO₄", "159,6" },
                new[] { "C₂H₅OH", "46", "NaHCO₃", "84" });
        }

        private void HeatPage()
        {
            float s = Ui.S;
            Cards(2, 26,
                ("Q = m · c · ΔT", "Bir maddeyi ısıtmak için gereken ısı (J) = kütle (g) × öz ısı × sıcaklık değişimi.\nÖrnek: 100 g suyu 20 °C'den 70 °C'ye: 100 × 4,18 × 50 ≈ 20,9 kJ.", C(0xFF7A45)),
                ("Q = m · L", "Hâl değişimi ısısı. Sıcaklık sabitken madde hâl değiştirir.\nSu için erime ısısı 334 J/g, buharlaşma ısısı 2260 J/g.", C(0x35C4FF)));

            Heading("Suyun ısınma eğrisi");
            // Schematic heating curve: ice → melting plateau → water → boiling plateau → steam.
            float gx = _x + 70 * s, gw = Math.Min(_w - 90 * s, 900 * s), gh = 300 * s, gy = _y + 10 * s;
            DrawRectangleRec(new Rectangle(gx, gy, gw, gh), Theme.Raised);
            float T(float c) => gy + gh - (c + 30) / 160f * gh;
            for (int c = -20; c <= 120; c += 20)
            {
                DrawLineEx(new Vector2(gx, T(c)), new Vector2(gx + gw, T(c)), 1, Theme.Grid);
                string label = c + " °C";
                Ui.Text(label, gx - Ui.Measure(label, 13).X - 8 * s, T(c) - 8 * s, 13, Theme.Muted);
            }

            var pts = new[]
            {
                new Vector2(0.00f, -20), new Vector2(0.08f, 0), new Vector2(0.30f, 0), new Vector2(0.52f, 100),
                new Vector2(0.88f, 100), new Vector2(0.98f, 118),
            };
            Color[] segColors = { C(0x9FD8FF), C(0x4FE07A), C(0x35C4FF), C(0xFFB347), C(0xFF7A45) };
            string[] segLabels = { "buz ısınır", "erime (0 °C)", "su ısınır", "kaynama (100 °C)", "buhar" };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = new Vector2(gx + pts[i].X * gw, T(pts[i].Y));
                var b = new Vector2(gx + pts[i + 1].X * gw, T(pts[i + 1].Y));
                DrawLineEx(a, b, 5 * s, segColors[i]);
                var mid = (a + b) / 2;
                var m = Ui.Measure(segLabels[i], 14, true);
                float ly = pts[i].Y == pts[i + 1].Y ? mid.Y - 30 * s : mid.Y + 8 * s;
                Ui.Text(segLabels[i], Math.Min(mid.X + (pts[i].Y == pts[i + 1].Y ? -m.X / 2 : 10 * s), gx + gw - m.X - 6 * s), ly, 14, segColors[i], true);
            }

            Ui.Text("zaman (sabit ısıtma) →", gx + gw - Ui.Measure("zaman (sabit ısıtma) →", 13).X, gy + gh + 8 * s, 13, Theme.Muted);
            _y = gy + gh + 40 * s;
            Paragraph("Erime ve kaynama sırasında verilen ısı tanecikler arasındaki bağları koparmak için harcanır; bu yüzden sıcaklık sabit kalır. \"Isınma Eğrisi\" deneyinde termometre grafiğinde 100 °C'deki düzlüğü görebilirsiniz. Kaynama noktası basınca bağlıdır: yüksek rakımda su 100 °C'nin altında kaynar.");

            Heading("Öz ısılar, J/(g·°C)");
            Table(new[] { 1.4f, 1f, 1.4f, 1f }, new[] { "Madde", "c", "Madde", "c" },
                new[] { "Su", "4,18", "Alüminyum", "0,90" },
                new[] { "Buz", "2,09", "Cam", "0,84" },
                new[] { "Su buharı", "2,01", "Demir", "0,45" },
                new[] { "Etanol", "2,44", "Bakır", "0,385" });
            Bullets(
                "Ekzotermik tepkime çevreye ısı verir (nötrleşme, yanma, sodyum + su): kap ısınır.",
                "Endotermik tepkime çevreden ısı alır (amonyum nitratın suda çözünmesi): kap soğur.",
                "Suyun öz ısısı büyüktür; bu yüzden denizler karalardan daha yavaş ısınır ve soğur.");
        }

        private void SeparationPage()
        {
            Paragraph("Karışımlar, bileşenlerinin farklı fiziksel özellikleri (tanecik boyutu, yoğunluk, kaynama noktası, çözünürlük, manyetiklik) kullanılarak ayrılır. Bileşikler ise ancak kimyasal yollarla ayrışır.");
            Table(new[] { 1f, 1.4f, 1.6f }, new[] { "Yöntem", "Neye dayanır?", "Örnek" },
                new[] { "Süzme", "Tanecik boyutu, çözünmeme", "Kum + su; AgCl çökeleğini ayırmak" },
                new[] { "Buharlaştırma", "Çözücünün uçucu olması", "Tuzlu sudan tuz elde etmek" },
                new[] { "Kristallendirme", "Sıcaklıkla çözünürlüğün değişmesi", "Sıcak CuSO₄ çözeltisinden mavi kristaller" },
                new[] { "Basit damıtma", "Kaynama noktası farkı", "Tuzlu sudan saf su" },
                new[] { "Ayrımsal damıtma", "Yakın kaynama noktaları", "Etanol + su, ham petrol" },
                new[] { "Ayırma hunisi", "Karışmama ve yoğunluk farkı", "Su + zeytinyağı" },
                new[] { "Dekantasyon (aktarma)", "Yoğunluk farkı, çökme", "Çökeleğin üstündeki sıvıyı başka kaba aktarmak" },
                new[] { "Mıknatıs", "Manyetik özellik", "Demir tozu + kükürt" },
                new[] { "Kromatografi", "Yüzeye tutunma farkı", "Mürekkepteki boyaları ayırmak" },
                new[] { "Santrifüj", "Yoğunluk farkı (hızlı döndürme)", "Kan hücrelerini plazmadan ayırmak" });
            Heading("Laboratuvar ipucu");
            Paragraph("Simülasyonda bir behere çökelek oluşturup (ör. AgNO₃ + NaCl) yavaşça eğdiğinizde önce berrak sıvı akar; katı dipte kalır. Bu, dekantasyondur.");
        }

        private void TrendsPage()
        {
            float s = Ui.S;
            Paragraph("Periyodik tabloda elementler atom numarasına göre dizilir. Aynı gruptakiler benzer kimyasal özellik gösterir, çünkü değerlik elektron sayıları aynıdır.");

            // Schematic table with trend arrows.
            float tw = Math.Min(_w - 260 * s, 760 * s), th = tw * 7 / 18f;
            float tx = _x + 120 * s, ty = _y + 40 * s;
            float cw = tw / 18, ch = th / 7;
            foreach (var e in Elements.All.Where(e => e.Row <= 7))
            {
                var c = Elements.CategoryColor(e.Category);
                DrawRectangleRec(new Rectangle(tx + (e.Column - 1) * cw + 1, ty + (e.Row - 1) * ch + 1, cw - 2, ch - 2), Gfx.WithAlpha(c, 0.75f));
            }

            Color arrowA = C(0xFF7A45), arrowB = C(0x35C4FF);
            void Arrow(Vector2 a, Vector2 b, Color c)
            {
                DrawLineEx(a, b, 6 * s, c);
                var d = Vector2.Normalize(b - a);
                var n = new Vector2(-d.Y, d.X);
                DrawTriangle(b + d * 14 * s, b - n * 12 * s, b + n * 12 * s, c);
                DrawTriangle(b + d * 14 * s, b + n * 12 * s, b - n * 12 * s, c);
            }

            Arrow(new Vector2(tx + tw, ty - 18 * s), new Vector2(tx + 20 * s, ty - 18 * s), arrowA);
            Arrow(new Vector2(tx - 24 * s, ty), new Vector2(tx - 24 * s, ty + th - 14 * s), arrowA);
            Arrow(new Vector2(tx, ty + th + 22 * s), new Vector2(tx + tw - 20 * s, ty + th + 22 * s), arrowB);
            Arrow(new Vector2(tx + tw + 24 * s, ty + th), new Vector2(tx + tw + 24 * s, ty + 14 * s), arrowB);
            Ui.Text("Atom yarıçapı ve metalik özellik artar", tx + 30 * s, ty - 46 * s, 15, arrowA, true);
            Ui.Text("İyonlaşma enerjisi ve elektronegatiflik artar", tx + 30 * s, ty + th + 36 * s, 15, arrowB, true);
            _y = ty + th + 74 * s;

            Table(new[] { 1.1f, 1.7f, 1.6f }, new[] { "Özellik", "Grupta yukarıdan aşağıya", "Periyotta soldan sağa" },
                new[] { "Atom yarıçapı", "Artar (katman sayısı artar)", "Azalır (çekirdek yükü artar)" },
                new[] { "İyonlaşma enerjisi", "Azalır", "Genel olarak artar" },
                new[] { "Elektronegatiflik", "Azalır", "Artar (en büyük: F = 3,98)" },
                new[] { "Metalik özellik", "Artar", "Azalır" });
            Heading("Önemli gruplar");
            Cards(2,
                ("1A · Alkali metaller", "Li, Na, K ... Tek değerlik elektronu; suyla şiddetle tepkime verir, gaz yağında saklanır.", Elements.CategoryColor(ElementCategory.AlkaliMetal)),
                ("2A · Toprak alkali metaller", "Be, Mg, Ca ... İki değerlik elektronu; Mg şerit asitte hızla çözünür.", Elements.CategoryColor(ElementCategory.AlkalineEarth)),
                ("7A · Halojenler", "F, Cl, Br, I ... Bir elektron alarak −1 yüklü iyon olur; tuz yapıcıdır.", Elements.CategoryColor(ElementCategory.Halogen)),
                ("8A · Soy gazlar", "He, Ne, Ar ... Değerlik katmanı dolu; neredeyse hiç tepkime vermez.", Elements.CategoryColor(ElementCategory.NobleGas)),
                ("B grupları · Geçiş metalleri", "Fe, Cu, Zn, Ag ... Renkli bileşikler (CuSO₄ mavi) ve katalizörler.", Elements.CategoryColor(ElementCategory.Transition)),
                ("Blok bilgisi", "Son elektronun girdiği orbital: s, p, d ya da f bloğu. Periyot numarası = katman sayısı.", Theme.Accent));
            Paragraph("Ayrıntılar için \"Periyodik Tablo\" sekmesinde bir elemente dokunun: 3D atom modeli ve tüm bilgileri açılır.", 16, Theme.Accent);
        }

        private void PhysicsPage()
        {
            Cards(3, 24,
                ("v = Δx / Δt", "Hız (m/s) = yer değiştirme / zaman.", C(0x35C4FF)),
                ("a = Δv / Δt", "İvme (m/s²) = hız değişimi / zaman.", C(0x35C4FF)),
                ("F = m · a", "Newton'un 2. yasası. Kuvvet (N) = kütle (kg) × ivme.", C(0xFF7A45)),
                ("G = m · g", "Ağırlık. Dünya'da g ≈ 9,8 m/s².", C(0xFF7A45)),
                ("P = F / A", "Katı basıncı (Pa) = kuvvet / yüzey alanı.", C(0xC07BFF)),
                ("P = h · d · g", "Sıvı basıncı derinlik ve yoğunlukla artar.", C(0xC07BFF)),
                ("W = F · x", "İş (J) = kuvvet × yol (kuvvet yönünde).", C(0x4FE07A)),
                ("Eₖ = ½ m v²", "Kinetik enerji (J).", C(0x4FE07A)),
                ("Eₚ = m · g · h", "Yer çekimi potansiyel enerjisi (J).", C(0x4FE07A)),
                ("V = I · R", "Ohm yasası: gerilim (V) = akım (A) × direnç (Ω).", C(0xF7C948)),
                ("P = V · I", "Elektriksel güç (W).", C(0xF7C948)),
                ("F = d · V · g", "Kaldırma kuvveti: sıvının yoğunluğu × cismin batan hacmi × g.", C(0x3CC9A0)));
            Heading("Birimler");
            Table(new[] { 1f, 1f, 1.6f }, new[] { "Büyüklük", "SI birimi", "Dönüşüm" },
                new[] { "Uzunluk", "metre (m)", "1 m = 100 cm = 1000 mm" },
                new[] { "Kütle", "kilogram (kg)", "1 kg = 1000 g" },
                new[] { "Hacim", "metreküp (m³)", "1 L = 1000 mL = 1 dm³; 1 mL = 1 cm³" },
                new[] { "Sıcaklık", "kelvin (K)", "T(K) = t(°C) + 273" },
                new[] { "Basınç", "pascal (Pa)", "1 atm = 101 325 Pa = 76 cmHg" },
                new[] { "Enerji", "joule (J)", "1 cal = 4,18 J" });
        }

        private void BiologyPage()
        {
            Heading("Mikroskop kullanımı");
            Bullets(
                "Mikroskobu iki elle, bir el kolunda bir el tabanında olacak şekilde taşıyın.",
                "İncelemeye her zaman en küçük büyütmeli objektifle (4× ya da 10×) başlayın.",
                "Preparatı tablaya koyup mandallarla sabitleyin; önce makro, sonra mikro vida ile netleyin.",
                "Toplam büyütme = oküler büyütmesi × objektif büyütmesi (ör. 10× × 40× = 400×).",
                "Büyük büyütmede yalnızca mikro vidayı kullanın; objektif preparata değmemelidir.");
            Heading("Hücre organelleri");
            Table(new[] { 1f, 2f, 0.9f, 0.9f }, new[] { "Organel", "Görevi", "Hayvan", "Bitki" },
                new[] { "Hücre zarı", "Madde giriş-çıkışını denetler.", "Var", "Var" },
                new[] { "Hücre duvarı", "Hücreye destek ve şekil verir (selüloz).", "Yok", "Var" },
                new[] { "Çekirdek", "Kalıtım maddesini (DNA) taşır, hücreyi yönetir.", "Var", "Var" },
                new[] { "Mitokondri", "Oksijenli solunumla enerji (ATP) üretir.", "Var", "Var" },
                new[] { "Kloroplast", "Fotosentez yapar, klorofil içerir.", "Yok", "Var" },
                new[] { "Ribozom", "Protein sentezler.", "Var", "Var" },
                new[] { "Koful", "Su ve madde depolar; bitkide büyük ve merkezidir.", "Küçük", "Büyük" },
                new[] { "Sentrozom", "Hücre bölünmesinde iğ ipliklerini oluşturur.", "Var", "Yok*" });
            Paragraph("* Çoğu bitki hücresinde bulunmaz. Fizik ve biyoloji deney modülleri sonraki sürümlerde eklenecektir.", 14, Theme.Muted);
        }
    }
}
