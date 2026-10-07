using System;
using System.Linq;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Events;
using IDLab.Core.Interaction;
using IDLab.Core.Model;
using Xunit;

namespace IDLab.Core.Tests
{
    public class ExtendedChemistryTests
    {
        private readonly SubstanceDatabase _db = SubstanceDatabase.CreateDefault();

        private Mixture Water(double ml)
        {
            var m = new Mixture(_db);
            m.Add(SubstanceIds.Water, ml / 18.015);
            return m;
        }

        private static double Run(ReactionEngine engine, Mixture m, double seconds, out ReactionOutcome last)
        {
            last = null;
            double heat = 0;
            for (double t = 0; t < seconds; t += 0.02)
            {
                last = engine.Process(m, 0.02);
                heat += last.HeatReleasedJ;
            }

            return heat;
        }

        [Fact]
        public void SulfuricAcidReleasesTwoProtons() =>
            Assert.Equal(1.0, AcidBase.ComputePH(With(Water(1000), SubstanceIds.SulfuricAcid, 0.05)), 1);

        [Fact]
        public void AmmoniaIsAWeakBase()
        {
            double ph = AcidBase.ComputePH(With(Water(1000), SubstanceIds.Ammonia, 0.1));
            Assert.InRange(ph, 11.0, 11.3); // pKb 4.74 → pH ≈ 11.1
        }

        [Fact]
        public void AmmoniumChlorideIsAcidic() =>
            Assert.InRange(AcidBase.ComputePH(With(Water(1000), SubstanceIds.AmmoniumChloride, 0.1)), 5.0, 5.3);

        [Fact]
        public void SilverNitratePlusSaltGivesWhitePrecipitate()
        {
            var engine = ReactionLibrary.Create();
            var m = With(With(Water(100), SubstanceIds.SilverNitrate, 0.01), SubstanceIds.SodiumChloride, 0.02);
            var outcome = engine.Process(m);
            Assert.Equal(0.01, m.Moles(SubstanceIds.SilverChloride), 9);
            Assert.Contains(outcome.Effects, e => e.Type == LabEventType.Precipitate && e.Code == "precip.agcl");
            Assert.True(m.HasSolids);
            Assert.Contains(m.Solids(), s => s.Key.Id == SubstanceIds.SilverChloride);
        }

        [Fact]
        public void ActivitySeriesMgFastZnSlowerCuNot()
        {
            var engine = ReactionLibrary.Create();
            double Progress(string metal)
            {
                var m = With(With(Water(50), SubstanceIds.HydrochloricAcid, 0.05), metal, 0.004);
                Run(engine, m, 5, out _);
                return 1 - m.Moles(metal) / 0.004;
            }

            double mg = Progress(SubstanceIds.Magnesium), zn = Progress(SubstanceIds.Zinc), fe = Progress(SubstanceIds.Iron), cu = Progress(SubstanceIds.Copper);
            Assert.True(mg > zn && zn > fe && fe > cu, $"mg {mg} zn {zn} fe {fe} cu {cu}");
            Assert.Equal(0, cu, 9);
        }

        [Fact]
        public void MetalAcidReleasesHydrogenOverTime()
        {
            var engine = ReactionLibrary.Create();
            var m = With(With(Water(50), SubstanceIds.HydrochloricAcid, 0.05), SubstanceIds.Zinc, 0.01);
            var first = engine.Process(m, 0.02);
            Assert.True(first.Ongoing);
            Assert.True(first.GasReleased["H₂"] > 0);
            Assert.True(m.Contains(SubstanceIds.Zinc));
        }

        [Fact]
        public void IronDisplacesCopperAndBlueFades()
        {
            var engine = ReactionLibrary.Create();
            var m = With(With(Water(100), SubstanceIds.CopperSulfate, 0.02), SubstanceIds.Iron, 0.05);
            float blueBefore = m.BlendedColor().B - m.BlendedColor().R;
            Run(engine, m, 200, out _);
            Assert.True(m.Moles(SubstanceIds.Copper) > 0.015);
            float blueAfter = m.BlendedColor().B - m.BlendedColor().R;
            Assert.True(blueAfter < blueBefore);
        }

        [Fact]
        public void PeroxideNeedsCatalyst()
        {
            var engine = ReactionLibrary.Create();
            var plain = With(Water(50), SubstanceIds.HydrogenPeroxide, 0.04);
            Run(engine, plain, 2, out _);
            Assert.Equal(0.04, plain.Moles(SubstanceIds.HydrogenPeroxide), 9);

            var catalysed = With(With(Water(50), SubstanceIds.HydrogenPeroxide, 0.04), SubstanceIds.PotassiumIodide, 0.005);
            var outcome = engine.Process(catalysed, 0.02);
            Assert.Contains(outcome.Effects, e => e.Type == LabEventType.Foam);
            Run(engine, catalysed, 10, out _);
            Assert.True(catalysed.Moles(SubstanceIds.HydrogenPeroxide) < 0.001);
            Assert.Equal(0.005, catalysed.Moles(SubstanceIds.PotassiumIodide), 9);
        }

        [Fact]
        public void BakingSodaAndVinegarCools()
        {
            var engine = ReactionLibrary.Create();
            var m = With(With(Water(100), SubstanceIds.AceticAcid, 0.05), SubstanceIds.SodiumBicarbonate, 0.03);
            double heat = Run(engine, m, 5, out _);
            Assert.True(heat < 0, "endothermic");
            Assert.Equal(0.03, m.Moles(SubstanceIds.SodiumAcetate), 3);
        }

        [Fact]
        public void BleachPlusAmmoniaIsToxic()
        {
            var engine = ReactionLibrary.Create();
            var m = With(With(Water(100), SubstanceIds.SodiumHypochlorite, 0.02), SubstanceIds.Ammonia, 0.02);
            var outcome = engine.Process(m);
            Assert.Contains(outcome.Effects, e => e.Type == LabEventType.ToxicGas && e.Code == "hazard.chloramine");
        }

        [Fact]
        public void CopperSulfateTintsByConcentration()
        {
            var dilute = With(Water(100), SubstanceIds.CopperSulfate, 0.0005).BlendedColor();
            var strong = With(Water(100), SubstanceIds.CopperSulfate, 0.05).BlendedColor();
            Assert.True(strong.B - strong.R > dilute.B - dilute.R);
            Assert.True(strong.R < 0.3f);
        }

        [Fact]
        public void PhenolphthaleinTurnsPinkOnlyInBase()
        {
            var acid = With(With(Water(100), SubstanceIds.HydrochloricAcid, 0.001), SubstanceIds.Phenolphthalein, 1e-5);
            var basic = With(With(Water(100), SubstanceIds.SodiumHydroxide, 0.001), SubstanceIds.Phenolphthalein, 1e-5);
            var a = acid.DisplayColor(AcidBase.ComputePH(acid));
            var b = basic.DisplayColor(AcidBase.ComputePH(basic));
            Assert.True(b.R - b.G > 0.4f, b.ToString());
            Assert.True(Math.Abs(a.R - a.G) < 0.2f, a.ToString());
        }

        [Fact]
        public void SaltDissolvesInWaterButNotWithout()
        {
            var dry = new Mixture(_db);
            dry.Add(SubstanceIds.SodiumChloride, 0.1);
            Assert.False(dry.HasLiquid);
            Assert.True(dry.HasSolids);
            dry.Add(SubstanceIds.Water, 5);
            Assert.True(dry.HasLiquid);
            Assert.False(dry.HasSolids);
        }

        [Fact]
        public void PouringDecantsLiquidAndLeavesSolids()
        {
            var m = With(Water(100), SubstanceIds.CalciumCarbonate, 0.05);
            var poured = m.TakeLiquid(0.05);
            Assert.False(poured.Contains(SubstanceIds.CalciumCarbonate));
            Assert.Equal(0.05, m.Moles(SubstanceIds.CalciumCarbonate), 9);
        }

        [Fact]
        public void JarTipsSolidPiecesIntoBeaker()
        {
            var world = new LabWorld();
            var beaker = world.Spawn(EquipmentIds.Beaker250Water, Pose3.Identity);
            var jar = world.Spawn(EquipmentIds.MgJar, Pose3.Identity);
            var c = jar.Get<LiquidContainer>();
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(120 * Math.PI / 180));
            var lipOffset = new Pose3(Vector3.Zero, rotation).TransformPoint(new Vector3(0, 0.055f, 0));
            jar.Pose = new Pose3(new Vector3(-lipOffset.X, 0.16f, 0), rotation);
            double before = c.Contents.Moles(SubstanceIds.Magnesium);

            world.Simulate(0.5);

            Assert.True(c.IsPouringSolid || beaker.Get<LiquidContainer>().Contents.Contains(SubstanceIds.Magnesium) ||
                        beaker.Get<LiquidContainer>().Contents.Contains(SubstanceIds.MagnesiumChloride));
            Assert.True(c.Contents.Moles(SubstanceIds.Magnesium) < before);
        }

        [Fact]
        public void BuretteDeliversDropByDropIntoFlaskBelow()
        {
            var world = new LabWorld();
            var burette = world.Spawn(EquipmentIds.Burette, Pose3.Identity);
            var flask = world.Spawn(EquipmentIds.Erlenmeyer, Pose3.Identity);
            var acid = new Mixture(world.Substances);
            ReagentPreset.Solution("", 20, SubstanceIds.HydrochloricAcid, 0.1).FillInto(acid);
            flask.Get<LiquidContainer>().ReceiveLiquid(acid);
            var tap = burette.Get<Stopcock>();
            tap.Valve = 1;

            world.Simulate(5);
            Assert.True(tap.IsFlowing);
            Assert.Equal(flask, tap.Target);
            world.Simulate(15); // 1.5 mL/s → empties after ~17 s: well past the equivalence point
            Assert.InRange(tap.DeliveredMilliliters, 19, 25.1);
            Assert.True(flask.Get<LiquidContainer>().PH > 7);
        }

        [Fact]
        public void BalanceShowsMassLossAsCo2Escapes()
        {
            var world = new LabWorld();
            var balance = world.Spawn(EquipmentIds.Balance, Pose3.Identity);
            var flask = world.Spawn(EquipmentIds.Erlenmeyer, Pose3.At(0.3f, 0, 0));
            var snaps = new SnapSystem(world);
            Assert.True(snaps.TryFindDropSnap(flask, Pose3.At(0, 0.1f, 0.01f), 0.05f, 0.4f, out var onPan));
            snaps.Apply(flask, onPan);
            var c = flask.Get<LiquidContainer>();
            var acid = new Mixture(world.Substances);
            ReagentPreset.Solution("", 50, SubstanceIds.HydrochloricAcid, 1.0).FillInto(acid);
            c.ReceiveLiquid(acid);
            world.Simulate(0.1);
            var bal = balance.Get<Balance>();
            double before = bal.ReadingGrams;
            Assert.InRange(before, 170, 175); // 120 g glass + ~51 g solution

            c.AddSubstance(SubstanceIds.CalciumCarbonate, 0.02); // 2 g
            world.Simulate(60);
            Assert.True(bal.ReadingGrams < before + 2.0 - 0.6, $"{bal.ReadingGrams} vs {before}"); // 0.88 g CO₂ escaped
        }

        [Fact]
        public void EthanolCatchesFireOverOpenFlameButNotOnHotPlate()
        {
            foreach (var (heater, expectFire) in new[] { (EquipmentIds.BunsenBurner, true), (EquipmentIds.HotPlate, false) })
            {
                var world = new LabWorld();
                var h = world.Spawn(heater, Pose3.Identity);
                var beaker = world.Spawn(EquipmentIds.Beaker100, Pose3.At(0.3f, 0, 0));
                var snaps = new SnapSystem(world);
                Assert.True(snaps.TryFindDropSnap(beaker, Pose3.At(0, 0.25f, 0), 0.05f, 0.4f, out var on));
                snaps.Apply(beaker, on);
                beaker.Get<LiquidContainer>().AddSubstance(SubstanceIds.Ethanol, 0.5);
                h.Get<HeatSource>().GasValve = 1;
                bool fire = false;
                world.Events.Published += e => fire |= e.Type == LabEventType.Fire;
                world.Simulate(30);
                Assert.Equal(expectFire, fire);
            }
        }

        private static Mixture With(Mixture m, string id, double moles)
        {
            m.Add(id, moles);
            return m;
        }
    }
}
