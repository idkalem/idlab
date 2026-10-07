using System;
using IDLab.Core.Chemistry;
using Xunit;

namespace IDLab.Core.Tests
{
    public class ChemistryTests
    {
        private readonly SubstanceDatabase _db = SubstanceDatabase.CreateDefault();

        private Mixture Solution(double waterMl, string solute = null, double soluteMoles = 0)
        {
            var m = new Mixture(_db);
            m.Add(SubstanceIds.Water, waterMl / 18.015);
            if (solute != null)
            {
                m.Add(solute, soluteMoles);
            }

            return m;
        }

        [Fact]
        public void PureWaterIsNeutral() => Assert.Equal(7.0, AcidBase.ComputePH(Solution(100)), 3);

        [Fact]
        public void StrongAcidPH() => Assert.Equal(1.0, AcidBase.ComputePH(Solution(1000, SubstanceIds.HydrochloricAcid, 0.1)), 1);

        [Fact]
        public void StrongBasePH() => Assert.Equal(12.0, AcidBase.ComputePH(Solution(1000, SubstanceIds.SodiumHydroxide, 0.01)), 1);

        [Fact]
        public void WeakAcidPH()
        {
            // 0.1 M acetic acid → pH ≈ 2.88
            Assert.Equal(2.88, AcidBase.ComputePH(Solution(1000, SubstanceIds.AceticAcid, 0.1)), 1);
        }

        [Fact]
        public void BufferFollowsHendersonHasselbalch()
        {
            var m = Solution(1000, SubstanceIds.AceticAcid, 0.1);
            m.Add(SubstanceIds.SodiumAcetate, 0.1);
            Assert.Equal(-Math.Log10(1.8e-5), AcidBase.ComputePH(m), 2);
        }

        [Fact]
        public void EquivalencePointOfWeakAcidIsBasic()
        {
            var m = Solution(1000, SubstanceIds.SodiumAcetate, 0.1);
            Assert.InRange(AcidBase.ComputePH(m), 8.7, 9.0);
        }

        [Fact]
        public void NoWaterMeansNoPH()
        {
            var m = new Mixture(_db);
            m.Add(SubstanceIds.Sodium, 0.05);
            Assert.True(double.IsNaN(AcidBase.ComputePH(m)));
        }

        [Fact]
        public void NeutralizationConsumesReagentsAndReleasesHeat()
        {
            var engine = ReactionEngine.CreateDefault(_db);
            var m = Solution(100, SubstanceIds.HydrochloricAcid, 0.05);
            m.Add(SubstanceIds.SodiumHydroxide, 0.03);

            var outcome = engine.Process(m);

            Assert.Equal(0.02, m.Moles(SubstanceIds.HydrochloricAcid), 9);
            Assert.Equal(0.0, m.Moles(SubstanceIds.SodiumHydroxide), 9);
            Assert.Equal(0.03, m.Moles(SubstanceIds.SodiumChloride), 9);
            Assert.Equal(0.03 * 57_100, outcome.HeatReleasedJ, 3);
            Assert.Contains("neutralize.hcl.naoh", outcome.AppliedRules);
        }

        [Fact]
        public void BleachPlusAcidReleasesToxicGas()
        {
            var engine = ReactionEngine.CreateDefault(_db);
            var m = Solution(100, SubstanceIds.SodiumHypochlorite, 0.05);
            m.Add(SubstanceIds.HydrochloricAcid, 0.05);

            var outcome = engine.Process(m);

            Assert.Contains(outcome.Effects, e => e.Type == Events.LabEventType.ToxicGas);
            Assert.Equal(0.025, m.Moles(SubstanceIds.SodiumHypochlorite), 9);
            Assert.False(m.Contains(SubstanceIds.HydrochloricAcid));
        }

        [Fact]
        public void LargeSodiumInWaterExplodes()
        {
            var engine = ReactionEngine.CreateDefault(_db);
            var m = Solution(100, SubstanceIds.Sodium, 0.06);

            var outcome = engine.Process(m);

            Assert.Contains(outcome.Effects, e => e.Type == Events.LabEventType.Explosion);
            Assert.Equal(0.06, m.Moles(SubstanceIds.SodiumHydroxide), 9);
            Assert.True(AcidBase.ComputePH(m) > 13);
        }

        [Fact]
        public void TakeVolumeSplitsProportionally()
        {
            var m = Solution(200, SubstanceIds.HydrochloricAcid, 0.2);
            double before = m.Molarity(SubstanceIds.HydrochloricAcid);

            var taken = m.TakeVolume(m.VolumeLiters / 4);

            Assert.Equal(before, taken.Molarity(SubstanceIds.HydrochloricAcid), 9);
            Assert.Equal(before, m.Molarity(SubstanceIds.HydrochloricAcid), 9);
            Assert.Equal(0.05, taken.Moles(SubstanceIds.HydrochloricAcid), 9);
        }

        [Fact]
        public void MergeMixesTemperatureByHeatCapacity()
        {
            var hot = Solution(100);
            hot.TemperatureC = 80;
            var cold = Solution(300);
            cold.TemperatureC = 20;

            cold.MergeFrom(hot);

            Assert.Equal(35.0, cold.TemperatureC, 6);
            Assert.Equal(0.4, cold.VolumeLiters, 6);
        }

        [Fact]
        public void IndicatorShowsRedInAcidGreenNeutralPurpleBase()
        {
            var acid = IndicatorColors.UniversalIndicator(1);
            var neutral = IndicatorColors.UniversalIndicator(7);
            var basic = IndicatorColors.UniversalIndicator(13);
            Assert.True(acid.R > acid.G && acid.R > acid.B);
            Assert.True(neutral.G > neutral.R && neutral.G > neutral.B);
            Assert.True(basic.B > basic.G);
        }

        [Theory]
        [InlineData(0.5, 10)]
        [InlineData(0.9, 30)]
        public void PourModelOnlyPoursPastThreshold(double fill, double extraDegrees)
        {
            const double h = 0.092, d = 0.068;
            double threshold = PourModel.ThresholdTiltDegrees(fill, h, d);
            Assert.Equal(0, PourModel.FlowRateLitersPerSecond(fill, h, d, threshold - 1));
            Assert.True(PourModel.FlowRateLitersPerSecond(fill, h, d, threshold + extraDegrees) > 0);
        }

        [Fact]
        public void FullerVesselsPourEarlier()
        {
            Assert.True(PourModel.ThresholdTiltDegrees(0.9, 0.092, 0.068) < PourModel.ThresholdTiltDegrees(0.4, 0.092, 0.068));
        }
    }
}
