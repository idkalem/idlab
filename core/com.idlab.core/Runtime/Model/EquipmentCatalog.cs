using System.Collections.Generic;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;

namespace IDLab.Core.Model
{
    public static class EquipmentIds
    {
        public const string Beaker250 = "beaker.250";
        public const string Beaker250Water = "beaker.250.water";
        public const string BunsenBurner = "burner.bunsen";
        public const string Thermometer = "probe.thermometer";
        public const string PhProbe = "probe.ph";
        public const string WaterBottle = "bottle.water";
        public const string HclBottle = "bottle.hcl.1m";
        public const string NaohBottle = "bottle.naoh.1m";
        public const string VinegarBottle = "bottle.acetic.0_8m";
        public const string IndicatorBottle = "bottle.indicator";
        public const string BleachBottle = "bottle.bleach";
        public const string SodiumJar = "jar.sodium";
    }

    /// <summary>Registry of every equipment definition the inventory can offer.</summary>
    public sealed class EquipmentCatalog
    {
        private readonly Dictionary<string, EquipmentDefinition> _byId = new Dictionary<string, EquipmentDefinition>();
        private readonly List<EquipmentDefinition> _ordered = new List<EquipmentDefinition>();

        public IReadOnlyList<EquipmentDefinition> All => _ordered;

        public void Register(EquipmentDefinition definition)
        {
            if (_byId.ContainsKey(definition.Id))
            {
                throw new System.ArgumentException($"Duplicate equipment id '{definition.Id}'.");
            }

            _byId.Add(definition.Id, definition);
            _ordered.Add(definition);
        }

        public EquipmentDefinition Get(string id)
        {
            if (_byId.TryGetValue(id, out var d))
            {
                return d;
            }

            throw new KeyNotFoundException($"Unknown equipment '{id}'.");
        }

        public bool Contains(string id) => _byId.ContainsKey(id);

        public static EquipmentCatalog CreateDefault()
        {
            var catalog = new EquipmentCatalog();

            catalog.Register(Beaker(EquipmentIds.Beaker250, "Beaker 250 mL", "Empty borosilicate beaker.", null));
            catalog.Register(Beaker(EquipmentIds.Beaker250Water, "Beaker + 150 mL water", "Beaker filled with 150 mL of tap water at room temperature.",
                ReagentPreset.Water(150)));

            var burner = new EquipmentDefinition
            {
                Id = EquipmentIds.BunsenBurner,
                DisplayName = "Bunsen burner + tripod",
                Description = "Gas burner under a tripod and wire gauze. Turn the gas valve to adjust the flame.",
                Category = EquipmentCategory.HeatSource,
                Disciplines = Discipline.Chemistry | Discipline.Physics,
                VisualKey = "burner",
                BoundsSize = new Vector3(0.16f, 0.21f, 0.16f),
                AccentColor = ColorRGBA.FromHex(0x3A7BFF),
            };
            burner.SnapPoints.Add(new SnapPointDefinition("heat_surface", SnapKind.HeatSurface, 0f, 0.21f, 0f));
            burner.Components.Add(new HeatSourceSpec { MaxPowerW = 1500, Efficiency = 0.35 });
            catalog.Register(burner);

            catalog.Register(ProbeDef(EquipmentIds.Thermometer, "Digital thermometer", "thermometer", ProbeQuantity.Temperature, 2.0,
                "Stainless probe with a 2 s response time. Drop it into a beaker."));
            catalog.Register(ProbeDef(EquipmentIds.PhProbe, "pH probe", "ph_probe", ProbeQuantity.PH, 1.0,
                "Glass-electrode pH probe. Reads only when immersed in an aqueous solution."));

            catalog.Register(Bottle(EquipmentIds.WaterBottle, "Water (400 mL)", ReagentPreset.Water(400), 0x4FA3FF));
            catalog.Register(Bottle(EquipmentIds.HclBottle, "HCl 1.0 M",
                ReagentPreset.Solution("1.0 M hydrochloric acid", 250, SubstanceIds.HydrochloricAcid, 1.0), 0xFF5A4F));
            catalog.Register(Bottle(EquipmentIds.NaohBottle, "NaOH 1.0 M",
                ReagentPreset.Solution("1.0 M sodium hydroxide", 250, SubstanceIds.SodiumHydroxide, 1.0), 0x7B61FF));
            catalog.Register(Bottle(EquipmentIds.VinegarBottle, "Acetic acid 0.8 M",
                ReagentPreset.Solution("0.8 M acetic acid", 250, SubstanceIds.AceticAcid, 0.8), 0xFFB347));
            catalog.Register(Bottle(EquipmentIds.IndicatorBottle, "Universal indicator",
                ReagentPreset.Solution("Universal indicator solution", 100, SubstanceIds.UniversalIndicator, 0.002), 0x3CB43C));
            catalog.Register(Bottle(EquipmentIds.BleachBottle, "Bleach (NaOCl 0.7 M)",
                ReagentPreset.Solution("0.7 M sodium hypochlorite", 250, SubstanceIds.SodiumHypochlorite, 0.7), 0xE6E14A));

            var jar = new EquipmentDefinition
            {
                Id = EquipmentIds.SodiumJar,
                DisplayName = "Sodium metal (1.4 g)",
                Description = "Reactive alkali metal stored dry. Reacts violently with water.",
                Category = EquipmentCategory.Reagent,
                Disciplines = Discipline.Chemistry,
                VisualKey = "jar",
                BoundsSize = new Vector3(0.05f, 0.06f, 0.05f),
                AccentColor = ColorRGBA.FromHex(0xFF8C1A),
            };
            jar.SnapPoints.Add(new SnapPointDefinition("base", SnapKind.ContainerBase, Vector3.Zero));
            jar.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = 0.04, InnerHeightM = 0.05, GlassMassKg = 0.06,
                InitialContents = ReagentPreset.Pure("Sodium metal", SubstanceIds.Sodium, 0.06),
            });
            catalog.Register(jar);

            return catalog;
        }

        private static EquipmentDefinition Beaker(string id, string name, string description, ReagentPreset contents)
        {
            var d = new EquipmentDefinition
            {
                Id = id,
                DisplayName = name,
                Description = description,
                Category = EquipmentCategory.Glassware,
                Disciplines = Discipline.Chemistry | Discipline.Biology | Discipline.Physics,
                VisualKey = "beaker",
                BoundsSize = new Vector3(0.072f, 0.095f, 0.072f),
            };
            d.SnapPoints.Add(new SnapPointDefinition("base", SnapKind.ContainerBase, Vector3.Zero));
            d.SnapPoints.Add(new SnapPointDefinition("probe_port_a", SnapKind.ProbePort, 0.018f, 0.006f, 0.008f));
            d.SnapPoints.Add(new SnapPointDefinition("probe_port_b", SnapKind.ProbePort, -0.018f, 0.006f, -0.008f));
            d.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = 0.068, InnerHeightM = 0.092, GlassMassKg = 0.1, InitialContents = contents,
            });
            return d;
        }

        private static EquipmentDefinition Bottle(string id, string name, ReagentPreset contents, uint accentHex)
        {
            var d = new EquipmentDefinition
            {
                Id = id,
                DisplayName = name,
                Description = contents.Label + ". Tilt to pour into a container below the spout.",
                Category = EquipmentCategory.Reagent,
                Disciplines = Discipline.Chemistry,
                VisualKey = "bottle",
                BoundsSize = new Vector3(0.075f, 0.15f, 0.075f),
                AccentColor = ColorRGBA.FromHex(accentHex),
            };
            d.SnapPoints.Add(new SnapPointDefinition("base", SnapKind.ContainerBase, Vector3.Zero));
            d.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = 0.07, InnerHeightM = 0.14, GlassMassKg = 0.25, InitialContents = contents,
            });
            return d;
        }

        private static EquipmentDefinition ProbeDef(string id, string name, string visual, ProbeQuantity quantity, double tau, string description)
        {
            var d = new EquipmentDefinition
            {
                Id = id,
                DisplayName = name,
                Description = description,
                Category = EquipmentCategory.Sensor,
                Disciplines = Discipline.All,
                VisualKey = visual,
                BoundsSize = new Vector3(0.012f, 0.2f, 0.012f),
                AccentColor = quantity == ProbeQuantity.Temperature ? ColorRGBA.FromHex(0xFF6B35) : ColorRGBA.FromHex(0x35C4FF),
            };
            d.SnapPoints.Add(new SnapPointDefinition("tip", SnapKind.ProbeTip, Vector3.Zero));
            d.Components.Add(new ProbeSpec { Quantity = quantity, TimeConstantS = tau });
            return d;
        }
    }
}
