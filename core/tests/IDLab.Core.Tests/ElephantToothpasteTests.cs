using System.Collections.Generic;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Events;
using IDLab.Core.Model;
using Xunit;

namespace IDLab.Core.Tests
{
    public class ElephantToothpasteTests
    {
        private static (LabWorld world, LiquidContainer flask, List<LabEvent> events) Flask(double peroxideMolar, bool soap)
        {
            var world = new LabWorld();
            var flask = world.Spawn(EquipmentIds.Erlenmeyer, Pose3.Identity).Get<LiquidContainer>();
            var m = new Mixture(world.Substances);
            ReagentPreset.Solution("", 50, SubstanceIds.HydrogenPeroxide, peroxideMolar).FillInto(m);
            if (soap) ReagentPreset.Liquid("", SubstanceIds.Detergent, 5, 1.03, 376.5).FillInto(m);
            flask.ReceiveLiquid(m);
            var events = new List<LabEvent>();
            world.Events.Published += events.Add;
            return (world, flask, events);
        }

        [Fact]
        public void YeastAndSoapMakeElephantToothpaste()
        {
            var (world, flask, events) = Flask(3.5, soap: true);
            flask.AddSubstance(SubstanceIds.DryYeast, 0.02); // 2 g
            bool fast = false;
            for (int i = 0; i < 100; i++)
            {
                world.Simulate(0.1);
                fast |= flask.GasFormula == "O₂" && flask.GasRateMolPerS > 0.005;
            }

            Assert.True(fast, "oxygen should rush out");
            Assert.Contains(events, e => e.Type == LabEventType.Foam && e.Code == "foam.elephant");
            Assert.True(flask.Contents.Moles(SubstanceIds.HydrogenPeroxide) < 0.175 * 0.05, $"H2O2 left {flask.Contents.Moles(SubstanceIds.HydrogenPeroxide)} T {flask.TemperatureC}");
            Assert.True(flask.TemperatureC > 25, "the decomposition is exothermic");
        }

        [Fact]
        public void HotYeastIsDenaturedAndDoesNothing()
        {
            Assert.Equal(0, YeastCatalaseRule.Activity(80));
            Assert.True(YeastCatalaseRule.Activity(66) < 0.4);
            Assert.True(YeastCatalaseRule.Activity(37) > YeastCatalaseRule.Activity(15));
        }

        [Fact]
        public void PeroxideAloneIsStable()
        {
            var (world, flask, events) = Flask(3.5, soap: true);
            world.Simulate(10);
            Assert.Equal(0.175, flask.Contents.Moles(SubstanceIds.HydrogenPeroxide), 3);
            Assert.DoesNotContain(events, e => e.Type == LabEventType.Foam);
        }
    }
}
