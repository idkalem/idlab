using System.Collections.Generic;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;

namespace IDLab.Core.Model
{
    public static class EquipmentIds
    {
        // Glassware
        public const string Beaker100 = "beaker.100";
        public const string Beaker250 = "beaker.250";
        public const string Beaker250Water = "beaker.250.water";
        public const string Beaker600 = "beaker.600";
        public const string Erlenmeyer = "flask.erlenmeyer.250";
        public const string RoundFlask = "flask.round.250";
        public const string GraduatedCylinder = "cylinder.100";
        public const string TestTube = "tube.test";
        public const string TubeRack = "rack.tubes";
        public const string PetriDish = "dish.petri";

        // Heat
        public const string BunsenBurner = "burner.bunsen";
        public const string HotPlate = "hotplate";

        // Measuring
        public const string Thermometer = "probe.thermometer";
        public const string PhProbe = "probe.ph";
        public const string Balance = "balance";
        public const string Burette = "burette.naoh";

        // Liquid reagents
        public const string WaterBottle = "bottle.water";
        public const string HclBottle = "bottle.hcl.1m";
        public const string H2so4Bottle = "bottle.h2so4";
        public const string Hno3Bottle = "bottle.hno3";
        public const string NaohBottle = "bottle.naoh.1m";
        public const string KohBottle = "bottle.koh";
        public const string Nh3Bottle = "bottle.nh3";
        public const string VinegarBottle = "bottle.acetic.0_8m";
        public const string NaclSolution = "bottle.nacl";
        public const string Cuso4Solution = "bottle.cuso4";
        public const string Fecl3Solution = "bottle.fecl3";
        public const string Agno3Solution = "bottle.agno3";
        public const string Kmno4Solution = "bottle.kmno4";
        public const string H2o2Bottle = "bottle.h2o2";
        public const string KiSolution = "bottle.ki";
        public const string EthanolBottle = "bottle.ethanol";
        public const string BleachBottle = "bottle.bleach";

        // Indicators
        public const string IndicatorBottle = "bottle.indicator";
        public const string PhenolphthaleinBottle = "dropper.phenolphthalein";

        // Solid reagents
        public const string SodiumJar = "jar.sodium";
        public const string NaclJar = "jar.nacl";
        public const string Nahco3Jar = "jar.nahco3";
        public const string Caco3Jar = "jar.caco3";
        public const string SugarJar = "jar.sugar";
        public const string Cuso4Jar = "jar.cuso4";
        public const string Kmno4Jar = "jar.kmno4";
        public const string MgJar = "jar.mg";
        public const string ZnJar = "jar.zn";
        public const string FeJar = "jar.fe";
        public const string CuJar = "jar.cu";
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
            var c = new EquipmentCatalog();

            // ------------------------------------------------------------------ glassware
            c.Register(Vessel(EquipmentIds.Beaker100, "Beaker 100 mL", "beaker", 0.050, 0.068, 0.05, null, "Small borosilicate beaker."));
            c.Register(Vessel(EquipmentIds.Beaker250, "Beaker 250 mL", "beaker", 0.068, 0.092, 0.1, null, "Empty borosilicate beaker."));
            c.Register(Vessel(EquipmentIds.Beaker250Water, "Beaker + 150 mL water", "beaker", 0.068, 0.092, 0.1, ReagentPreset.Water(150),
                "Beaker filled with 150 mL of tap water at room temperature."));
            c.Register(Vessel(EquipmentIds.Beaker600, "Beaker 600 mL", "beaker", 0.090, 0.125, 0.18, null, "Large beaker for water baths."));
            c.Register(Vessel(EquipmentIds.Erlenmeyer, "Erlenmeyer flask 250 mL", "erlenmeyer", 0.056, 0.13, 0.12, null,
                "Conical flask: swirl without spilling; used for titrations.", mouth: 0.030, probeY: 0.01f));
            c.Register(Vessel(EquipmentIds.RoundFlask, "Round-bottom flask 250 mL", "round_flask", 0.062, 0.12, 0.11, null,
                "Heats evenly; stands on a cork ring.", mouth: 0.026, probeY: 0.01f));
            c.Register(Vessel(EquipmentIds.GraduatedCylinder, "Graduated cylinder 100 mL", "cylinder", 0.026, 0.19, 0.15, null,
                "Measures liquid volumes accurately.", offsetY: 0.012f, probeY: 0.02f));
            c.Register(Vessel(EquipmentIds.TestTube, "Test tube", "test_tube", 0.016, 0.125, 0.012, null,
                "For small-scale reactions. Stand it in the rack.", baseKind: SnapKind.TubeBase, probeY: 0.01f));
            c.Register(Vessel(EquipmentIds.PetriDish, "Petri dish", "petri", 0.088, 0.014, 0.03, null, "Shallow dish for crystals and cultures."));

            var rack = Def(EquipmentIds.TubeRack, "Test-tube rack", "rack", EquipmentCategory.Glassware, Discipline.Chemistry | Discipline.Biology,
                new Vector3(0.18f, 0.075f, 0.05f), 0x8A6A45, "Holds four test tubes upright.");
            for (int i = 0; i < 4; i++)
            {
                rack.SnapPoints.Add(new SnapPointDefinition($"slot{i}", SnapKind.TubeSlot, -0.06f + i * 0.04f, 0.006f, 0f));
            }

            c.Register(rack);

            // ------------------------------------------------------------------ heat sources
            var burner = Def(EquipmentIds.BunsenBurner, "Bunsen burner + tripod", "burner", EquipmentCategory.HeatSource,
                Discipline.Chemistry | Discipline.Physics, new Vector3(0.16f, 0.21f, 0.16f), 0x3A7BFF,
                "Gas burner under a tripod and wire gauze. Turn the gas valve to adjust the flame.");
            burner.SnapPoints.Add(new SnapPointDefinition("heat_surface", SnapKind.HeatSurface, 0f, 0.21f, 0f));
            burner.Components.Add(new HeatSourceSpec { MaxPowerW = 1500, Efficiency = 0.35, OpenFlame = true });
            c.Register(burner);

            var plate = Def(EquipmentIds.HotPlate, "Hot plate", "hotplate", EquipmentCategory.HeatSource, Discipline.Chemistry | Discipline.Biology,
                new Vector3(0.17f, 0.07f, 0.19f), 0xFF6B35, "Electric heater without a flame — safe for flammable liquids.");
            plate.SnapPoints.Add(new SnapPointDefinition("heat_surface", SnapKind.HeatSurface, 0f, 0.07f, 0f));
            plate.Components.Add(new HeatSourceSpec { MaxPowerW = 600, Efficiency = 0.7, OpenFlame = false });
            c.Register(plate);

            // ------------------------------------------------------------------ measuring
            c.Register(ProbeDef(EquipmentIds.Thermometer, "Digital thermometer", "thermometer", ProbeQuantity.Temperature, 2.0,
                "Stainless probe with a 2 s response time. Drop it into a container."));
            c.Register(ProbeDef(EquipmentIds.PhProbe, "pH probe", "ph_probe", ProbeQuantity.PH, 1.0,
                "Glass-electrode pH probe. Reads only when immersed in an aqueous solution."));

            var balance = Def(EquipmentIds.Balance, "Electronic balance", "balance", EquipmentCategory.Measuring, Discipline.All,
                new Vector3(0.17f, 0.065f, 0.2f), 0x35C4FF, "Weighs what stands on its pan (0.01 g). Use Tare to zero it.");
            balance.SnapPoints.Add(new SnapPointDefinition("pan", SnapKind.ScalePan, 0f, 0.065f, 0.01f));
            balance.Components.Add(new BalanceSpec());
            c.Register(balance);

            var burette = Def(EquipmentIds.Burette, "Burette 25 mL (NaOH 0.1 M)", "burette", EquipmentCategory.Measuring, Discipline.Chemistry,
                new Vector3(0.12f, 0.58f, 0.14f), 0x7B61FF, "Stand with a burette of 0.1 M NaOH. Open the tap to add drop by drop.");
            burette.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = 0.0105, InnerHeightM = 0.29, GlassMassKg = 0.08, MouthDiameterM = 0.0105,
                LocalOffset = new Vector3(0, 0.27f, 0),
                InitialContents = ReagentPreset.Solution("0.1 M sodium hydroxide", 25, SubstanceIds.SodiumHydroxide, 0.1),
            });
            burette.Components.Add(new StopcockSpec { MaxFlowLitersPerSecond = 0.0015, OutletLocal = new Vector3(0, 0.215f, 0) });
            c.Register(burette);

            // ------------------------------------------------------------------ liquid reagents
            c.Register(Bottle(EquipmentIds.WaterBottle, "Water (400 mL)", ReagentPreset.Water(400), 0x4FA3FF));
            c.Register(Bottle(EquipmentIds.HclBottle, "HCl 1.0 M", ReagentPreset.Solution("1.0 M hydrochloric acid", 250, SubstanceIds.HydrochloricAcid, 1.0), 0xFF5A4F));
            c.Register(Bottle(EquipmentIds.H2so4Bottle, "H₂SO₄ 0.5 M", ReagentPreset.Solution("0.5 M sulfuric acid", 250, SubstanceIds.SulfuricAcid, 0.5), 0xE0412F));
            c.Register(Bottle(EquipmentIds.Hno3Bottle, "HNO₃ 1.0 M", ReagentPreset.Solution("1.0 M nitric acid", 250, SubstanceIds.NitricAcid, 1.0), 0xF5A623));
            c.Register(Bottle(EquipmentIds.NaohBottle, "NaOH 1.0 M", ReagentPreset.Solution("1.0 M sodium hydroxide", 250, SubstanceIds.SodiumHydroxide, 1.0), 0x7B61FF));
            c.Register(Bottle(EquipmentIds.KohBottle, "KOH 1.0 M", ReagentPreset.Solution("1.0 M potassium hydroxide", 250, SubstanceIds.PotassiumHydroxide, 1.0), 0x9A7BFF));
            c.Register(Bottle(EquipmentIds.Nh3Bottle, "NH₃ 1.0 M", ReagentPreset.Solution("1.0 M ammonia solution", 250, SubstanceIds.Ammonia, 1.0), 0x5FD3C8));
            c.Register(Bottle(EquipmentIds.VinegarBottle, "Acetic acid 0.8 M", ReagentPreset.Solution("0.8 M acetic acid", 250, SubstanceIds.AceticAcid, 0.8), 0xFFB347));
            c.Register(Bottle(EquipmentIds.NaclSolution, "NaCl 1.0 M", ReagentPreset.Solution("1.0 M sodium chloride", 250, SubstanceIds.SodiumChloride, 1.0), 0xC9D6E3));
            c.Register(Bottle(EquipmentIds.Cuso4Solution, "CuSO₄ 0.5 M", ReagentPreset.Solution("0.5 M copper(II) sulfate", 250, SubstanceIds.CopperSulfate, 0.5), 0x1F6FE5));
            c.Register(Bottle(EquipmentIds.Fecl3Solution, "FeCl₃ 0.5 M", ReagentPreset.Solution("0.5 M iron(III) chloride", 250, SubstanceIds.IronIIIChloride, 0.5), 0xE8901A));
            c.Register(Bottle(EquipmentIds.Agno3Solution, "AgNO₃ 0.1 M", ReagentPreset.Solution("0.1 M silver nitrate", 250, SubstanceIds.SilverNitrate, 0.1), 0x8C6A3F, "amber"));
            c.Register(Bottle(EquipmentIds.Kmno4Solution, "KMnO₄ 0.01 M", ReagentPreset.Solution("0.01 M potassium permanganate", 250, SubstanceIds.PotassiumPermanganate, 0.01), 0x8E2A9E, "amber"));
            c.Register(Bottle(EquipmentIds.H2o2Bottle, "H₂O₂ 3%", ReagentPreset.Solution("3% hydrogen peroxide", 250, SubstanceIds.HydrogenPeroxide, 0.88), 0x9AD0FF, "amber"));
            c.Register(Bottle(EquipmentIds.KiSolution, "KI 0.5 M", ReagentPreset.Solution("0.5 M potassium iodide", 250, SubstanceIds.PotassiumIodide, 0.5), 0xD8C8A0));
            c.Register(Bottle(EquipmentIds.EthanolBottle, "Ethanol 96%",
                ReagentPreset.Liquid("Ethanol", SubstanceIds.Ethanol, 250, 0.789, 46.07), 0xFF8C1A));
            c.Register(Bottle(EquipmentIds.BleachBottle, "Bleach (NaOCl 0.7 M)",
                ReagentPreset.Solution("0.7 M sodium hypochlorite", 250, SubstanceIds.SodiumHypochlorite, 0.7), 0xE6E14A));

            // ------------------------------------------------------------------ indicators (dropper bottles: pour drop by drop)
            c.Register(Dropper(EquipmentIds.IndicatorBottle, "Universal indicator",
                ReagentPreset.Solution("Universal indicator solution", 30, SubstanceIds.UniversalIndicator, 0.02), 0x3CB43C));
            c.Register(Dropper(EquipmentIds.PhenolphthaleinBottle, "Phenolphthalein",
                ReagentPreset.Solution("Phenolphthalein solution", 30, SubstanceIds.Phenolphthalein, 0.01), 0xEE1F8C));

            // ------------------------------------------------------------------ solid reagents (wide-mouth jars)
            c.Register(Jar(EquipmentIds.NaclJar, "NaCl (salt) 20 g", SubstanceIds.SodiumChloride, 20, 0xC9D6E3));
            c.Register(Jar(EquipmentIds.Nahco3Jar, "NaHCO₃ (baking soda) 20 g", SubstanceIds.SodiumBicarbonate, 20, 0xF0F0F0));
            c.Register(Jar(EquipmentIds.Caco3Jar, "CaCO₃ (marble chips) 20 g", SubstanceIds.CalciumCarbonate, 20, 0xE8E2D4));
            c.Register(Jar(EquipmentIds.SugarJar, "Sugar 20 g", SubstanceIds.Sucrose, 20, 0xFFE9B0));
            c.Register(Jar(EquipmentIds.Cuso4Jar, "CuSO₄ crystals 10 g", SubstanceIds.CopperSulfate, 10, 0x1F6FE5));
            c.Register(Jar(EquipmentIds.Kmno4Jar, "KMnO₄ crystals 1 g", SubstanceIds.PotassiumPermanganate, 1, 0x7A0F8C));
            c.Register(Jar(EquipmentIds.MgJar, "Magnesium ribbon 1 g", SubstanceIds.Magnesium, 1, 0xC9CCD2));
            c.Register(Jar(EquipmentIds.ZnJar, "Zinc granules 5 g", SubstanceIds.Zinc, 5, 0xAEB4BE));
            c.Register(Jar(EquipmentIds.FeJar, "Iron filings 5 g", SubstanceIds.Iron, 5, 0x55585E));
            c.Register(Jar(EquipmentIds.CuJar, "Copper turnings 5 g", SubstanceIds.Copper, 5, 0xC8703A));
            c.Register(Jar(EquipmentIds.SodiumJar, "Sodium metal (1.4 g)", SubstanceIds.Sodium, 1.38, 0xFF8C1A,
                "Reactive alkali metal stored dry. Reacts violently with water."));

            return c;
        }

        // ------------------------------------------------------------------ builders

        private static EquipmentDefinition Def(string id, string name, string visual, EquipmentCategory category, Discipline disciplines,
            Vector3 bounds, uint accent, string description) =>
            new EquipmentDefinition
            {
                Id = id, DisplayName = name, Description = description, Category = category, Disciplines = disciplines,
                VisualKey = visual, BoundsSize = bounds, AccentColor = ColorRGBA.FromHex(accent),
            };

        private static EquipmentDefinition Vessel(string id, string name, string visual, double innerD, double innerH, double glassKg,
            ReagentPreset contents, string description, double mouth = 0, float offsetY = 0, float probeY = 0.006f,
            SnapKind baseKind = SnapKind.ContainerBase)
        {
            float outer = (float)innerD + 0.006f;
            var size = visual == "erlenmeyer" ? new Vector3(0.088f, (float)innerH + 0.01f, 0.088f)
                : visual == "round_flask" ? new Vector3(0.09f, (float)innerH + 0.03f, 0.09f)
                : visual == "cylinder" ? new Vector3(0.07f, (float)innerH + offsetY + 0.01f, 0.07f)
                : new Vector3(outer, (float)innerH + offsetY, outer);
            var d = Def(id, name, visual, EquipmentCategory.Glassware, Discipline.Chemistry | Discipline.Biology | Discipline.Physics,
                size, 0x99AABB, description);
            d.SnapPoints.Add(new SnapPointDefinition("base", baseKind, Vector3.Zero));
            float port = (float)innerD * 0.26f;
            d.SnapPoints.Add(new SnapPointDefinition("probe_port_a", SnapKind.ProbePort, port, offsetY + probeY, port * 0.45f));
            if (innerD > 0.04)
            {
                d.SnapPoints.Add(new SnapPointDefinition("probe_port_b", SnapKind.ProbePort, -port, offsetY + probeY, -port * 0.45f));
            }

            d.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = innerD, InnerHeightM = innerH, GlassMassKg = glassKg, InitialContents = contents,
                MouthDiameterM = mouth, LocalOffset = new Vector3(0, offsetY, 0),
            });
            return d;
        }

        private static EquipmentDefinition Bottle(string id, string name, ReagentPreset contents, uint accentHex, string visual = "bottle")
        {
            var d = Def(id, name, visual, EquipmentCategory.Reagent, Discipline.Chemistry, new Vector3(0.076f, 0.16f, 0.076f), accentHex,
                contents.Label + ". Tilt to pour into a container below the neck.");
            d.SnapPoints.Add(new SnapPointDefinition("base", SnapKind.ContainerBase, Vector3.Zero));
            d.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = 0.07, InnerHeightM = 0.14, GlassMassKg = 0.25, InitialContents = contents, MouthDiameterM = 0.027,
            });
            return d;
        }

        private static EquipmentDefinition Dropper(string id, string name, ReagentPreset contents, uint accentHex)
        {
            var d = Def(id, name, "dropper", EquipmentCategory.Indicator, Discipline.Chemistry, new Vector3(0.036f, 0.095f, 0.036f), accentHex,
                contents.Label + ". Tilt to add a few drops.");
            d.SnapPoints.Add(new SnapPointDefinition("base", SnapKind.ContainerBase, Vector3.Zero));
            d.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = 0.03, InnerHeightM = 0.06, GlassMassKg = 0.04, InitialContents = contents, MouthDiameterM = 0.006,
            });
            return d;
        }

        private static EquipmentDefinition Jar(string id, string name, string substanceId, double grams, uint accentHex, string description = null)
        {
            var d = Def(id, name, "jar", EquipmentCategory.SolidReagent, Discipline.Chemistry, new Vector3(0.058f, 0.06f, 0.058f), accentHex,
                description ?? $"{grams:0.#} g solid. Tilt to tip a few pieces into a container.");
            d.SnapPoints.Add(new SnapPointDefinition("base", SnapKind.ContainerBase, Vector3.Zero));
            d.Components.Add(new LiquidContainerSpec
            {
                InnerDiameterM = 0.05, InnerHeightM = 0.055, GlassMassKg = 0.07, MouthDiameterM = 0.044,
                InitialContents = ReagentPreset.Solid(name, substanceId, grams),
            });
            return d;
        }

        private static EquipmentDefinition ProbeDef(string id, string name, string visual, ProbeQuantity quantity, double tau, string description)
        {
            var d = Def(id, name, visual, EquipmentCategory.Sensor, Discipline.All, new Vector3(0.012f, 0.2f, 0.012f),
                quantity == ProbeQuantity.Temperature ? 0xFF6B35u : 0x35C4FFu, description);
            d.SnapPoints.Add(new SnapPointDefinition("tip", SnapKind.ProbeTip, Vector3.Zero));
            d.Components.Add(new ProbeSpec { Quantity = quantity, TimeConstantS = tau });
            return d;
        }
    }
}
