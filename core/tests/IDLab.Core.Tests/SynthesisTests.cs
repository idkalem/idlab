using System.Linq;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Interaction;
using IDLab.Core.Model;
using Xunit;

namespace IDLab.Core.Tests
{
    public class SynthesisTests
    {
        private readonly SubstanceDatabase _db = SubstanceDatabase.Shared;

        [Theory]
        [InlineData(new[] { "Na", "H₂O" }, new[] { "NaOH", "H₂" }, "2Na + 2H₂O → 2NaOH + H₂")]
        [InlineData(new[] { "Al", "HCl" }, new[] { "AlCl₃", "H₂" }, "2Al + 6HCl → 2AlCl₃ + 3H₂")]
        [InlineData(new[] { "Fe", "O₂" }, new[] { "Fe₃O₄" }, "3Fe + 2O₂ → Fe₃O₄")]
        [InlineData(new[] { "Ca(OH)₂", "H₃PO₄" }, new[] { "Ca₃(PO₄)₂", "H₂O" }, "3Ca(OH)₂ + 2H₃PO₄ → Ca₃(PO₄)₂ + 6H₂O")]
        [InlineData(new[] { "CuSO₄·5H₂O" }, new[] { "CuSO₄", "H₂O" }, "CuSO₄·5H₂O → CuSO₄ + 5H₂O")]
        public void BalancesEquations(string[] left, string[] right, string expected)
        {
            Assert.Equal(expected, Synthesis.Balance(left, right));
        }

        [Fact]
        public void FindsTextbookRoutesToCommonSalts()
        {
            var zncl2 = _db.FindIonic("Zn", "Cl");
            var kinds = Synthesis.RoutesTo(_db, zncl2).Select(r => r.Kind).ToList();
            Assert.Contains(SynthesisKind.MetalAcid, kinds);
            Assert.Contains(SynthesisKind.Neutralise, kinds);
            Assert.Contains(SynthesisKind.Displace, kinds);

            var agcl = _db.FindIonic("Ag", "Cl");
            var precip = Synthesis.RoutesTo(_db, agcl).Single(r => r.Kind == SynthesisKind.Precipitate);
            Assert.Equal("AgNO₃ + NaCl → AgCl + NaNO₃", precip.Equation);

            var mgo = _db.FindByFormula("MgO");
            Assert.Contains(Synthesis.RoutesTo(_db, mgo), r => r.Kind == SynthesisKind.Burn && r.Equation == "2Mg + O₂ → 2MgO");

            var fes = _db.FindIonic("Fe2", "S");
            Assert.Contains(Synthesis.RoutesTo(_db, fes), r => r.Kind == SynthesisKind.Combine && r.Heat);
        }

        [Fact]
        public void ElementCardListsCompoundsMadeFromIt()
        {
            var zn = _db.FindElement("Zn");
            var made = Synthesis.FromElement(_db, zn).Select(r => r.Product.Formula).ToList();
            Assert.Contains("ZnO", made);
            Assert.Contains("ZnCl₂", made);
            Assert.Contains("ZnS", made);
            var s = _db.FindElement("S");
            Assert.Contains("FeS", Synthesis.FromElement(_db, s).Select(r => r.Product.Formula));
        }

        [Fact]
        public void IronAndSulfurCombineWhenHeated()
        {
            var world = new LabWorld();
            var burner = world.Spawn(EquipmentIds.BunsenBurner, Pose3.Identity);
            var tube = world.Spawn(EquipmentIds.Beaker100, Pose3.At(0.3f, 0, 0));
            var snaps = new SnapSystem(world);
            Assert.True(snaps.TryFindDropSnap(tube, Pose3.At(0, 0.25f, 0), 0.05f, 0.4f, out var on));
            snaps.Apply(tube, on);
            var c = tube.Get<LiquidContainer>();
            var fe = world.Substances.FindElement("Fe");
            var sulfur = world.Substances.FindElement("S");
            c.AddSubstance(fe.Id, 0.02);
            c.AddSubstance(sulfur.Id, 0.02);
            world.Simulate(5);
            var fes = world.Substances.FindIonic("Fe2", "S");
            Assert.Equal(0, c.Contents.Moles(fes.Id), 6); // nothing happens cold
            burner.Get<HeatSource>().GasValve = 1;
            world.Simulate(120);
            Assert.True(c.Contents.Moles(fes.Id) > 0.01, $"FeS {c.Contents.Moles(fes.Id)}");
        }

        [Fact]
        public void EveryRouteToZincChlorideWorksInTheSimulation()
        {
            var zncl2 = _db.FindIonic("Zn", "Cl");
            foreach (var route in Synthesis.RoutesTo(_db, zncl2))
            {
                var world = new LabWorld();
                var beaker = world.Spawn(EquipmentIds.Beaker250Water, Pose3.Identity);
                var c = beaker.Get<LiquidContainer>();
                foreach (var r in route.Reagents) c.AddSubstance(r.Id, 0.02);
                world.Simulate(300);
                Assert.True(c.Contents.Moles(zncl2.Id) > 0.003, $"{route.Kind}: {c.Contents.Moles(zncl2.Id)}");
            }
        }
    }
}
