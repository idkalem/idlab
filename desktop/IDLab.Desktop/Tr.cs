using System.Collections.Generic;
using IDLab.Core.Chemistry;
using IDLab.Core.Events;
using IDLab.Core.Model;

namespace IDLab.Desktop
{
    /// <summary>Turkish texts for the smart-board edition (the core keeps English ids and messages).</summary>
    internal static class Tr
    {
        private static readonly Dictionary<string, (string name, string description)> Equipment = new Dictionary<string, (string, string)>
        {
            [EquipmentIds.Beaker100] = ("Beher 100 mL", "Küçük beher; az miktarda çözelti için."),
            [EquipmentIds.Beaker250] = ("Beher 250 mL", "Boş cam beher; karıştırma ve ısıtma için."),
            [EquipmentIds.Beaker250Water] = ("Beher + 150 mL su", "Oda sıcaklığında 150 mL su."),
            [EquipmentIds.Beaker600] = ("Beher 600 mL", "Büyük beher; su banyosu için."),
            [EquipmentIds.Erlenmeyer] = ("Erlenmayer 250 mL", "Konik şişe; çalkalarken taşırmaz, titrasyonda kullanılır."),
            [EquipmentIds.RoundFlask] = ("Balon joje 250 mL", "Yuvarlak dipli; düzgün ısınır, mantar halka üstünde durur."),
            [EquipmentIds.GraduatedCylinder] = ("Dereceli silindir 100 mL", "Sıvı hacmini hassas ölçer."),
            [EquipmentIds.TestTube] = ("Deney tüpü", "Küçük ölçekli tepkimeler için; tüplüğe yerleştirin."),
            [EquipmentIds.TubeRack] = ("Tüplük", "Dört deney tüpünü dik tutar."),
            [EquipmentIds.PetriDish] = ("Petri kabı", "Sığ kap; kristal ve kültürler için."),
            [EquipmentIds.BunsenBurner] = ("Bunsen beki + üçayak", "Kırmızı vanayı çevirerek alevi ayarlayın."),
            [EquipmentIds.HotPlate] = ("Isıtıcı tabla", "Alevsiz elektrikli ısıtıcı; yanıcı sıvılar için güvenli."),
            [EquipmentIds.Thermometer] = ("Dijital termometre", "Ucu sıvıya daldırıldığında ölçer."),
            [EquipmentIds.PhProbe] = ("pH probu", "Sulu çözeltinin pH değerini ölçer."),
            [EquipmentIds.Balance] = ("Hassas terazi", "Kefedeki kabı tartar (0,01 g). Dara ile sıfırlayın."),
            [EquipmentIds.Burette] = ("Büret (0,1 M NaOH)", "Musluğu açarak damla damla ekleyin."),
            [EquipmentIds.WaterBottle] = ("Saf su (400 mL)", "Saf su şişesi."),
            [EquipmentIds.HclBottle] = ("HCl 1,0 M", "Hidroklorik asit (kuvvetli asit)."),
            [EquipmentIds.H2so4Bottle] = ("H₂SO₄ 0,5 M", "Sülfürik asit (kuvvetli, iki protonlu)."),
            [EquipmentIds.Hno3Bottle] = ("HNO₃ 1,0 M", "Nitrik asit (kuvvetli asit, yükseltgen)."),
            [EquipmentIds.NaohBottle] = ("NaOH 1,0 M", "Sodyum hidroksit (kuvvetli baz)."),
            [EquipmentIds.KohBottle] = ("KOH 1,0 M", "Potasyum hidroksit (kuvvetli baz)."),
            [EquipmentIds.Nh3Bottle] = ("NH₃ 1,0 M", "Amonyak çözeltisi (zayıf baz)."),
            [EquipmentIds.VinegarBottle] = ("Asetik asit 0,8 M", "Sirke asidi (zayıf asit)."),
            [EquipmentIds.NaclSolution] = ("NaCl 1,0 M", "Tuzlu su çözeltisi."),
            [EquipmentIds.Cuso4Solution] = ("CuSO₄ 0,5 M", "Mavi bakır(II) sülfat çözeltisi."),
            [EquipmentIds.Fecl3Solution] = ("FeCl₃ 0,5 M", "Turuncu demir(III) klorür çözeltisi."),
            [EquipmentIds.Agno3Solution] = ("AgNO₃ 0,1 M", "Gümüş nitrat; ışıktan korumak için koyu şişede."),
            [EquipmentIds.Kmno4Solution] = ("KMnO₄ 0,01 M", "Mor potasyum permanganat çözeltisi."),
            [EquipmentIds.H2o2Bottle] = ("H₂O₂ %3", "Hidrojen peroksit; KI ile hızla bozunur."),
            [EquipmentIds.KiSolution] = ("KI 0,5 M", "Potasyum iyodür; H₂O₂ bozunmasında katalizör."),
            [EquipmentIds.EthanolBottle] = ("Etanol %96", "Alkol; ALEVE YAKLAŞTIRMAYIN!"),
            [EquipmentIds.BleachBottle] = ("Çamaşır suyu (NaOCl)", "Asitlerle ve amonyakla ASLA karıştırılmaz."),
            [EquipmentIds.IndicatorBottle] = ("Üniversal indikatör", "pH'a göre renk değiştirir (damlalık)."),
            [EquipmentIds.PhenolphthaleinBottle] = ("Fenolftalein", "Bazda pembe, asitte renksiz (damlalık)."),
            [EquipmentIds.NaclJar] = ("Tuz (NaCl) 20 g", "Suda çözünür."),
            [EquipmentIds.Nahco3Jar] = ("Karbonat (NaHCO₃) 20 g", "Asitle CO₂ gazı çıkarır."),
            [EquipmentIds.Caco3Jar] = ("Mermer (CaCO₃) 20 g", "Suda çözünmez; asitle köpürür."),
            [EquipmentIds.SugarJar] = ("Şeker 20 g", "Suda çözünür, iyon oluşturmaz."),
            [EquipmentIds.Cuso4Jar] = ("CuSO₄ kristali 10 g", "Mavi kristaller; suda çözünür."),
            [EquipmentIds.Kmno4Jar] = ("KMnO₄ kristali 1 g", "Birkaç kristal suyu mora boyar."),
            [EquipmentIds.MgJar] = ("Magnezyum şerit 1 g", "Asitle hızla H₂ çıkarır."),
            [EquipmentIds.ZnJar] = ("Çinko granül 5 g", "Asitle orta hızda H₂ çıkarır."),
            [EquipmentIds.FeJar] = ("Demir tozu 5 g", "Asitle yavaş tepkime; CuSO₄'ten bakır çöktürür."),
            [EquipmentIds.CuJar] = ("Bakır talaş 5 g", "Asitle tepkime vermez; AgNO₃'ten gümüş çöktürür."),
            [EquipmentIds.SodiumJar] = ("Sodyum metali (1,4 g)", "Suyla şiddetli tepkime verir!"),
        };

        private static readonly Dictionary<string, string> Substances = new Dictionary<string, string>
        {
            [SubstanceIds.Water] = "Su", [SubstanceIds.Ethanol] = "Etanol", [SubstanceIds.HydrochloricAcid] = "Hidroklorik asit",
            [SubstanceIds.SulfuricAcid] = "Sülfürik asit", [SubstanceIds.NitricAcid] = "Nitrik asit", [SubstanceIds.AceticAcid] = "Asetik asit",
            [SubstanceIds.SodiumHydroxide] = "Sodyum hidroksit", [SubstanceIds.PotassiumHydroxide] = "Potasyum hidroksit",
            [SubstanceIds.Ammonia] = "Amonyak", [SubstanceIds.SodiumChloride] = "Sodyum klorür", [SubstanceIds.PotassiumChloride] = "Potasyum klorür",
            [SubstanceIds.SodiumNitrate] = "Sodyum nitrat", [SubstanceIds.PotassiumNitrate] = "Potasyum nitrat",
            [SubstanceIds.SodiumSulfate] = "Sodyum sülfat", [SubstanceIds.PotassiumSulfate] = "Potasyum sülfat",
            [SubstanceIds.SodiumAcetate] = "Sodyum asetat", [SubstanceIds.PotassiumAcetate] = "Potasyum asetat",
            [SubstanceIds.AmmoniumChloride] = "Amonyum klorür", [SubstanceIds.AmmoniumNitrate] = "Amonyum nitrat",
            [SubstanceIds.AmmoniumSulfate] = "Amonyum sülfat", [SubstanceIds.MagnesiumChloride] = "Magnezyum klorür",
            [SubstanceIds.ZincChloride] = "Çinko klorür", [SubstanceIds.IronIIChloride] = "Demir(II) klorür",
            [SubstanceIds.CalciumChloride] = "Kalsiyum klorür", [SubstanceIds.MagnesiumSulfate] = "Magnezyum sülfat",
            [SubstanceIds.ZincSulfate] = "Çinko sülfat", [SubstanceIds.IronIISulfate] = "Demir(II) sülfat",
            [SubstanceIds.CopperNitrate] = "Bakır(II) nitrat", [SubstanceIds.CopperSulfate] = "Bakır(II) sülfat",
            [SubstanceIds.IronIIIChloride] = "Demir(III) klorür", [SubstanceIds.PotassiumPermanganate] = "Potasyum permanganat",
            [SubstanceIds.SilverNitrate] = "Gümüş nitrat", [SubstanceIds.SilverChloride] = "Gümüş klorür (çökelek)",
            [SubstanceIds.CopperHydroxide] = "Bakır(II) hidroksit (çökelek)", [SubstanceIds.IronIIIHydroxide] = "Demir(III) hidroksit (çökelek)",
            [SubstanceIds.Magnesium] = "Magnezyum", [SubstanceIds.Zinc] = "Çinko", [SubstanceIds.Iron] = "Demir", [SubstanceIds.Copper] = "Bakır",
            [SubstanceIds.Silver] = "Gümüş", [SubstanceIds.Sodium] = "Sodyum", [SubstanceIds.SodiumBicarbonate] = "Sodyum bikarbonat",
            [SubstanceIds.CalciumCarbonate] = "Kalsiyum karbonat", [SubstanceIds.Sucrose] = "Sakaroz (şeker)",
            [SubstanceIds.HydrogenPeroxide] = "Hidrojen peroksit", [SubstanceIds.PotassiumIodide] = "Potasyum iyodür",
            [SubstanceIds.SodiumHypochlorite] = "Sodyum hipoklorit", [SubstanceIds.UniversalIndicator] = "Üniversal indikatör",
            [SubstanceIds.Phenolphthalein] = "Fenolftalein",
        };

        private static readonly Dictionary<string, (string title, string summary, string[] objectives)> Experiments =
            new Dictionary<string, (string, string, string[])>
            {
                [ExperimentCatalog.SandboxId] = ("Serbest Laboratuvar", "Tüm malzemeler ve kimyasallar kullanılabilir. Kendi deneyini kur.", new string[0]),
                ["chem.heating-curve"] = ("Suyun Isınma Eğrisi", "Suyu Bunsen bekiyle ısıt, sıcaklık–zaman grafiğini çiz ve kaynama platosunu gözle.", new[]
                {
                    "Beheri bekin üzerindeki tel kafese yerleştir.",
                    "Termometreyi behere daldır, gaz vanasını aç.",
                    "Sıcaklık 100 °C'de neden artmayı bırakıyor? Açıkla.",
                }),
                ["chem.acid-base"] = ("Asitler, Bazlar ve pH", "HCl, NaOH ve asetik asidi karıştır; indikatör rengini, pH'ı ve nötrleşme ısısını gözle.", new[]
                {
                    "Behere birkaç damla indikatör ve biraz HCl dök (pH probunu daldır).",
                    "NaOH ekleyerek pH 7'ye ulaşmaya çalış.",
                    "Nötrleşme sırasında sıcaklık neden artıyor?",
                }),
                ["chem.titration"] = ("Asit–Baz Titrasyonu", "Büretteki 0,1 M NaOH ile erlenmayerdeki HCl'nin derişimini bul. Fenolftalein pembeleşince dur.", new[]
                {
                    "Erlenmayere 20 mL HCl ve 2–3 damla fenolftalein koy; büretin altına yerleştir.",
                    "Musluğu açıp NaOH'ı damla damla ekle; pembe renk kalıcı olunca kapat.",
                    "M₁·V₁ = M₂·V₂ ile asidin derişimini hesapla.",
                }),
                ["chem.gases"] = ("Gaz Çıkışı Tepkimeleri", "Metal + asit (H₂), karbonat + asit (CO₂), H₂O₂ + KI (O₂). Hangisi daha hızlı köpürüyor?", new[]
                {
                    "Tüplere HCl doldur; Mg, Zn ve Cu ekle. Aktiflik sırasını gözle.",
                    "Mermer ve karbonat ile CO₂ çıkışını karşılaştır.",
                    "H₂O₂'ye KI ekle: oksijen köpüğü (fil diş macunu).",
                }),
                ["chem.precipitation"] = ("Çökelme ve Yer Değiştirme", "İyonik çözeltileri karıştırıp renkli çökelekler oluştur; metalleri tuz çözeltilerine at.", new[]
                {
                    "CuSO₄ + NaOH: açık mavi Cu(OH)₂ çökeleği.",
                    "AgNO₃ + NaCl: beyaz AgCl çökeleği. FeCl₃ + NaOH: kahverengi.",
                    "Demiri CuSO₄'e at: bakır kaplanır, mavi renk solar.",
                }),
                ["chem.mass"] = ("Kütlenin Korunumu", "Terazideki açık erlenmayerde mermer + asit tepkimesi: CO₂ kaçtıkça kütle azalır.", new[]
                {
                    "Erlenmayeri teraziye koy, 50 mL HCl dök ve daraya al.",
                    "Mermer parçalarını ekle; kütle neden azalıyor?",
                    "Kapalı kapta kütle değişir miydi? Tartış.",
                }),
                ["chem.safety"] = ("Laboratuvar Güvenliği", "Çamaşır suyu + asit/amonyak, sodyum + su ve alev yanında alkolün neden tehlikeli olduğunu güvenle gör.", new[]
                {
                    "Çamaşır suyunu HCl veya NH₃ ile karıştırmanın sonucunu gözle.",
                    "Sodyumu suya at (öğretmen gözetiminde!).",
                    "Etanolü bekte ve ısıtıcı tablada ısıtıp karşılaştır.",
                }),
            };

        public static string Name(EquipmentDefinition d) => Equipment.TryGetValue(d.Id, out var t) ? t.name : d.DisplayName;
        public static string Description(EquipmentDefinition d) => Equipment.TryGetValue(d.Id, out var t) ? t.description : d.Description;
        public static string Name(EquipmentInstance i) => $"{Name(i.Definition)} #{i.Id}";
        public static string Substance(Substance s) => Substances.TryGetValue(s.Id, out var n) ? n : s.Name;

        public static string Title(ExperimentDefinition e) => Experiments.TryGetValue(e.Id, out var t) ? t.title : e.Title;
        public static string Summary(ExperimentDefinition e) => Experiments.TryGetValue(e.Id, out var t) ? t.summary : e.Summary;
        public static string[] Objectives(ExperimentDefinition e) => Experiments.TryGetValue(e.Id, out var t) ? t.objectives : e.Objectives.ToArray();

        public static string Category(EquipmentCategory c)
        {
            switch (c)
            {
                case EquipmentCategory.Glassware: return "CAM MALZEMELER";
                case EquipmentCategory.HeatSource: return "ISITICILAR";
                case EquipmentCategory.Sensor: return "SENSÖRLER";
                case EquipmentCategory.Measuring: return "ÖLÇÜM ALETLERİ";
                case EquipmentCategory.Reagent: return "SIVI KİMYASALLAR";
                case EquipmentCategory.Indicator: return "İNDİKATÖRLER";
                case EquipmentCategory.SolidReagent: return "KATI KİMYASALLAR";
                default: return c.ToString().ToUpperInvariant();
            }
        }

        public static string Hazard(Hazard h)
        {
            switch (h)
            {
                case IDLab.Core.Chemistry.Hazard.Corrosive: return "Aşındırıcı";
                case IDLab.Core.Chemistry.Hazard.Toxic: return "Zehirli";
                case IDLab.Core.Chemistry.Hazard.Flammable: return "Yanıcı";
                case IDLab.Core.Chemistry.Hazard.Oxidizer: return "Yükseltgen";
                case IDLab.Core.Chemistry.Hazard.Irritant: return "Tahriş edici";
                case IDLab.Core.Chemistry.Hazard.Environment: return "Çevreye zararlı";
                case IDLab.Core.Chemistry.Hazard.WaterReactive: return "Suyla tepkimeli";
                case IDLab.Core.Chemistry.Hazard.Explosive: return "Patlayıcı";
                case IDLab.Core.Chemistry.Hazard.CompressedGas: return "Basınçlı gaz";
                case IDLab.Core.Chemistry.Hazard.HealthHazard: return "Ciddi sağlık tehlikesi";
                default: return "";
            }
        }

        public static string Gas(string formula)
        {
            switch (formula)
            {
                case "H₂": return "hidrojen (H₂)";
                case "CO₂": return "karbondioksit (CO₂)";
                case "O₂": return "oksijen (O₂)";
                case "Cl₂": return "klor (Cl₂) — ZEHİRLİ";
                case "NH₂Cl": return "kloramin — ZEHİRLİ";
                default: return formula;
            }
        }

        public static string Event(LabEvent e, string ownerName)
        {
            switch (e.Code)
            {
                case "hazard.sodium.explosion": return "PATLAMA! Sodyum suyla şiddetle tepkimeye girdi, hidrojen tutuştu.";
                case "hazard.sodium.fizz": return "Sodyum suda köpürerek koşturuyor, hidrojen gazı çıkıyor.";
                case "hazard.chlorine": return "ZEHİRLİ KLOR GAZI! Çamaşır suyu asitle asla karıştırılmamalı.";
                case "hazard.chloramine": return "ZEHİRLİ KLORAMİN! Çamaşır suyu amonyakla asla karıştırılmamalı.";
                case "hazard.fire.ethanol": return "YANGIN! Alkol buharı alev aldı. 'Söndür' ile kabın ağzını kapatın.";
                case "gas.co2": return $"{ownerName}: köpürme! Karbondioksit gazı çıkıyor.";
                case "gas.h2": return $"{ownerName}: metal yüzeyinde hidrojen kabarcıkları oluşuyor.";
                case "foam.o2": return $"{ownerName}: oksijen köpüğü taşıyor (fil diş macunu)!";
                case "precip.agcl": return $"{ownerName}: beyaz gümüş klorür (AgCl) çökeleği oluştu.";
                case "precip.cuoh2": return $"{ownerName}: açık mavi bakır(II) hidroksit çökeleği oluştu.";
                case "precip.feoh3": return $"{ownerName}: kahverengi demir(III) hidroksit çökeleği oluştu.";
                case "displace.cu": return $"{ownerName}: metalin üzeri bakırla kaplanıyor, mavi renk soluyor.";
                case "displace.ag": return $"{ownerName}: bakırın üzerinde gümüş kristalleri büyüyor, çözelti maviye dönüyor.";
                case "boiling": return $"{ownerName} kaynamaya başladı.";
                case "spill.overflow": return $"{ownerName} taştı!";
                case "spill.solid": return "Katı parçalar tezgâha döküldü.";
                case "spill.liquid": return "Sıvı tezgâha döküldü.";
            }

            switch (e.Type)
            {
                case LabEventType.Smoke: return "Duman çıkıyor!";
                case LabEventType.Spark: return "Kıvılcım! Kısa devre.";
                case LabEventType.FuseBlown: return "Sigorta attı!";
                default: return e.Message;
            }
        }
    }
}
