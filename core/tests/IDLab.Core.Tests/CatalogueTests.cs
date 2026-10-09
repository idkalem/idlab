using System;
using System.Linq;
using IDLab.Core.Chemistry;
using IDLab.Core.Events;
using Xunit;

namespace IDLab.Core.Tests
{
    /// <summary>The generated 1000+ chemical catalogue and the generic, ion-based reaction rules.</summary>
    public class CatalogueTests
    {
        private readonly SubstanceDatabase _db = SubstanceDatabase.Shared;
        private readonly ReactionEngine _engine = ReactionLibrary.Create();

        private Mixture Water(double ml)
        {
            var m = new Mixture(_db);
            m.Add(SubstanceIds.Water, ml / 18.015);
            return m;
        }

        private Substance Ionic(string cation, string anion) =>
            _db.FindIonic(cation, anion) ?? throw new InvalidOperationException($"{cation}+{anion} missing");

        private void Run(Mixture m, double seconds)
        {
            for (double t = 0; t < seconds; t += 0.02) _engine.Process(m, 0.02);
        }

        [Fact]
        public void CatalogueHasMoreThanAThousandNamedSubstances()
        {
            var visible = _db.All.Where(s => !s.Hidden).ToList();
            Assert.True(visible.Count >= 1000, $"only {visible.Count}");
            Assert.All(visible, s =>
            {
                Assert.False(string.IsNullOrEmpty(s.NameTr), s.Id);
                Assert.False(string.IsNullOrEmpty(s.Formula), s.Id);
                Assert.True(s.MolarMass > 0, s.Id);
            });
        }

        [Fact]
        public void MolarMassesAreComputedFromTheFormula()
        {
            Assert.Equal(153.82, _db.All.First(s => s.NameTr == "Karbon tetraklorür").MolarMass, 1);
            Assert.Equal(331.2, Ionic("Pb2", "NO3").MolarMass, 1);
            Assert.Equal(249.7, _db.All.First(s => s.NameTr == "Bakır(II) sülfat pentahidrat").MolarMass, 1);
        }

        [Fact]
        public void LeadNitratePlusPotassiumIodideGivesGoldenRain()
        {
            var m = Water(100);
            m.Add(Ionic("Pb2", "NO3").Id, 0.005);
            m.Add(SubstanceIds.PotassiumIodide, 0.02);
            var outcome = _engine.Process(m);
            var pbi2 = Ionic("Pb2", "I");
            Assert.Equal(0.005, m.Moles(pbi2.Id), 9);
            Assert.Equal(0.01, m.Moles(SubstanceIds.PotassiumNitrate), 9);
            Assert.Equal(0.01, m.Moles(SubstanceIds.PotassiumIodide), 9);
            Assert.Contains(outcome.Effects, e => e.Type == LabEventType.Precipitate && e.Code == "precip:" + pbi2.Id);
        }

        [Fact]
        public void BariumChloridePlusSulfuricAcidGivesBariumSulfate()
        {
            var m = Water(100);
            m.Add(Ionic("Ba", "Cl").Id, 0.01);
            m.Add(SubstanceIds.SulfuricAcid, 0.01);
            _engine.Process(m);
            Assert.Equal(0.01, m.Moles(Ionic("Ba", "SO4").Id), 9);
            Assert.Equal(0.02, m.Moles(SubstanceIds.HydrochloricAcid), 9);
        }

        [Fact]
        public void IronThreeAndThiocyanateFormBloodRedComplex()
        {
            var m = Water(100);
            m.Add(SubstanceIds.IronIIIChloride, 0.001);
            m.Add(Ionic("K", "SCN").Id, 0.01);
            _engine.Process(m);
            Assert.Equal(0.001, m.Moles(Ionic("Fe3", "SCN").Id), 9);
            var c = m.BlendedColor();
            Assert.True(c.R > c.B * 2, $"{c.R} {c.G} {c.B}");
        }

        [Fact]
        public void AluminiumReactsWithHydrochloricAcid()
        {
            var m = Water(100);
            var al = _db.FindElement("Al");
            m.Add(al.Id, 0.01);
            m.Add(SubstanceIds.HydrochloricAcid, 0.1);
            Run(m, 120);
            Assert.True(m.Moles(al.Id) < 0.001);
            Assert.Equal(0.01 - m.Moles(al.Id), m.Moles(Ionic("Al", "Cl").Id), 6);
        }

        [Fact]
        public void IronDisplacesSilver()
        {
            var m = Water(100);
            m.Add(SubstanceIds.Iron, 0.01);
            m.Add(SubstanceIds.SilverNitrate, 0.01);
            Run(m, 600);
            Assert.True(m.Moles(SubstanceIds.Silver) > 0.009, m.Moles(SubstanceIds.Silver).ToString());
        }

        [Fact]
        public void PotassiumExplodesInWater()
        {
            var m = Water(100);
            var k = _db.FindElement("K");
            m.Add(k.Id, 0.03);
            var outcome = _engine.Process(m);
            Assert.Contains(outcome.Effects, e => e.Type == LabEventType.Explosion);
            Assert.True(outcome.GasReleased["H₂"] > 0);
            Run(m, 3);
            Assert.True(m.Moles(SubstanceIds.PotassiumHydroxide) > 0.029);
        }

        [Fact]
        public void QuicklimeSlakesAndHeats()
        {
            var m = Water(100);
            var cao = Ionic("Ca", "O");
            m.Add(cao.Id, 0.05);
            double heat = 0;
            for (double t = 0; t < 30; t += 0.02) heat += _engine.Process(m, 0.02).HeatReleasedJ;
            Assert.True(m.Moles(cao.Id) < 1e-4);
            Assert.True(heat > 3000);
            Assert.True(AcidBase.ComputePH(m) > 12);
        }

        [Fact]
        public void CopperOxideDissolvesInSulfuricAcid()
        {
            var m = Water(100);
            var cuo = Ionic("Cu2", "O");
            m.Add(cuo.Id, 0.01);
            m.Add(SubstanceIds.SulfuricAcid, 0.02);
            Run(m, 120);
            Assert.True(m.Moles(SubstanceIds.CopperSulfate) > 0.0099);
        }

        [Fact]
        public void SodaPlusAcidFizzes()
        {
            var m = Water(100);
            m.Add(Ionic("Na", "CO3").Id, 0.01);
            m.Add(SubstanceIds.HydrochloricAcid, 0.02);
            double co2 = 0;
            for (double t = 0; t < 10; t += 0.02)
            {
                var o = _engine.Process(m, 0.02);
                if (o.GasReleased.TryGetValue("CO₂", out double g)) co2 += g;
            }

            Assert.Equal(0.01, co2, 4);
            Assert.Equal(0.02, m.Moles(SubstanceIds.SodiumChloride), 4);
        }

        [Theory]
        [InlineData("Na", "CO3", 11.3, 11.9)]
        [InlineData("Fe3", "Cl", 1.4, 2.3)]
        [InlineData("Cu2", "SO4", 4.0, 4.8)]
        [InlineData("Na", "CH3COO", 8.7, 9.1)]
        [InlineData("Na", "HSO4", 1.3, 1.8)]
        public void SaltSolutionsHaveRealisticPH(string cation, string anion, double lo, double hi)
        {
            var m = Water(1000);
            m.Add(Ionic(cation, anion).Id, 0.1);
            Assert.InRange(AcidBase.ComputePH(m), lo, hi);
        }

        [Fact]
        public void SolubilityRulesHold()
        {
            Assert.False(Ionic("Ag", "Cl").Soluble);
            Assert.False(Ionic("Ba", "SO4").Soluble);
            Assert.False(Ionic("Ca", "CO3").Soluble);
            Assert.True(Ionic("Ca", "Cl").Soluble);
            Assert.True(Ionic("Na", "PO4").Soluble);
            Assert.False(Ionic("Fe3", "OH").Soluble);
            Assert.Null(_db.FindIonic("Al", "CO3")); // does not exist
            Assert.Null(_db.FindIonic("Fe3", "I"));
        }
    }
}
