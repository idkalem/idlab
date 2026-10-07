using System.Collections.Generic;
using IDLab.Core.Events;
using IDLab.Core.Model;

namespace IDLab.Desktop
{
    /// <summary>Turkish texts for the smart-board edition (the core keeps English ids and messages).</summary>
    internal static class Tr
    {
        private static readonly Dictionary<string, (string name, string description)> Equipment = new Dictionary<string, (string, string)>
        {
            [EquipmentIds.Beaker250] = ("Beher 250 mL", "Boş cam beher."),
            [EquipmentIds.Beaker250Water] = ("Beher + 150 mL su", "Oda sıcaklığında 150 mL su."),
            [EquipmentIds.BunsenBurner] = ("Bunsen beki + üçayak", "Kırmızı vanayı çevirerek alevi ayarlayın."),
            [EquipmentIds.Thermometer] = ("Dijital termometre", "Ucu sıvıya daldırıldığında ölçer."),
            [EquipmentIds.PhProbe] = ("pH probu", "Sulu çözeltinin pH değerini ölçer."),
            [EquipmentIds.WaterBottle] = ("Su (400 mL)", "Saf su şişesi."),
            [EquipmentIds.HclBottle] = ("HCl 1,0 M", "Hidroklorik asit (kuvvetli asit)."),
            [EquipmentIds.NaohBottle] = ("NaOH 1,0 M", "Sodyum hidroksit (kuvvetli baz)."),
            [EquipmentIds.VinegarBottle] = ("Asetik asit 0,8 M", "Sirke asidi (zayıf asit)."),
            [EquipmentIds.IndicatorBottle] = ("Üniversal indikatör", "pH'a göre renk değiştirir."),
            [EquipmentIds.BleachBottle] = ("Çamaşır suyu (NaOCl)", "Asitlerle ASLA karıştırılmaz."),
            [EquipmentIds.SodiumJar] = ("Sodyum metali (1,4 g)", "Suyla şiddetli tepkime verir."),
        };

        private static readonly Dictionary<string, (string title, string summary, string[] objectives)> Experiments =
            new Dictionary<string, (string, string, string[])>
            {
                [ExperimentCatalog.SandboxId] = ("Serbest Laboratuvar", "Tüm malzemeler kullanılabilir. Kendi deneyini kur.", new string[0]),
                ["chem.heating-curve"] = ("Suyun Isınma Eğrisi", "Suyu Bunsen bekiyle ısıt, sıcaklık–zaman grafiğini çiz ve kaynama platosunu gözle.", new[]
                {
                    "Beheri bekin üzerindeki tel kafese yerleştir.",
                    "Termometreyi behere daldır, gaz vanasını aç.",
                    "Sıcaklık 100 °C'de neden artmayı bırakıyor? Açıkla.",
                }),
                ["chem.acid-base"] = ("Asitler, Bazlar ve pH", "HCl, NaOH ve asetik asidi karıştır; indikatör rengini, pH'ı ve nötrleşme ısısını gözle.", new[]
                {
                    "Behere biraz indikatör ve HCl dök (pH probunu daldır).",
                    "NaOH ekleyerek pH 7'ye ulaşmaya çalış.",
                    "Nötrleşme sırasında sıcaklık neden artıyor?",
                }),
                ["chem.safety"] = ("Laboratuvar Güvenliği", "Çamaşır suyu + asit ve sodyum + su karışımlarının neden tehlikeli olduğunu güvenle gör.", new[]
                {
                    "Çamaşır suyunu HCl ile karıştırmanın sonucunu gözle.",
                    "Sodyum metalini suya ekle (öğretmen gözetiminde!).",
                }),
            };

        public static string Name(EquipmentDefinition d) => Equipment.TryGetValue(d.Id, out var t) ? t.name : d.DisplayName;
        public static string Description(EquipmentDefinition d) => Equipment.TryGetValue(d.Id, out var t) ? t.description : d.Description;
        public static string Name(EquipmentInstance i) => $"{Name(i.Definition)} #{i.Id}";

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
                case EquipmentCategory.Reagent: return "KİMYASALLAR";
                default: return c.ToString().ToUpperInvariant();
            }
        }

        public static string Event(LabEvent e, string ownerName)
        {
            switch (e.Type)
            {
                case LabEventType.BoilingStarted: return $"{ownerName} kaynamaya başladı.";
                case LabEventType.Spill: return e.Message.Contains("overflowed") ? $"{ownerName} taştı!" : "Sıvı tezgâha döküldü.";
                case LabEventType.Fizz: return "Sodyum suyla köpürerek tepkimeye giriyor, hidrojen gazı çıkıyor.";
                case LabEventType.Explosion: return "PATLAMA! Sodyum suyla şiddetle tepkimeye girdi, hidrojen tutuştu.";
                case LabEventType.ToxicGas: return "ZEHİRLİ KLOR GAZI! Çamaşır suyu asitle asla karıştırılmamalı.";
                case LabEventType.Smoke: return "Duman çıkıyor!";
                case LabEventType.Spark: return "Kıvılcım! Kısa devre.";
                case LabEventType.FuseBlown: return "Sigorta attı!";
                default: return e.Message;
            }
        }
    }
}
