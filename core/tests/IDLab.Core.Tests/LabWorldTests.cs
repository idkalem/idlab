using System;
using System.Linq;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Events;
using IDLab.Core.Interaction;
using IDLab.Core.Micro;
using IDLab.Core.Model;
using Xunit;

namespace IDLab.Core.Tests
{
    public class LabWorldTests
    {
        private static (LabWorld world, EquipmentInstance burner, EquipmentInstance beaker, EquipmentInstance thermometer) HeatingSetup()
        {
            var world = new LabWorld();
            var burner = world.Spawn(EquipmentIds.BunsenBurner, Pose3.At(0, 0, 0));
            var beaker = world.Spawn(EquipmentIds.Beaker250Water, Pose3.At(0.3f, 0, 0));
            var thermometer = world.Spawn(EquipmentIds.Thermometer, Pose3.At(0.5f, 0, 0));

            var snaps = new SnapSystem(world);
            Assert.True(snaps.TryFindSnap(beaker, Pose3.At(0.01f, 0.2f, 0), out var onBurner));
            snaps.Apply(beaker, onBurner);
            Assert.True(snaps.TryFindSnap(thermometer, new Pose3(beaker.Pose.TransformPoint(new Vector3(0.02f, 0.01f, 0)), Quaternion.Identity), out var inBeaker));
            snaps.Apply(thermometer, inBeaker);
            return (world, burner, beaker, thermometer);
        }

        [Fact]
        public void BeakerSnapsOntoBurnerGauze()
        {
            var (world, burner, beaker, _) = HeatingSetup();
            Assert.Equal(burner, world.GetHost(beaker));
            Assert.Equal(0.21f, beaker.Pose.Position.Y, 3);
            Assert.Equal(0f, beaker.Pose.Position.X, 3);
            Assert.Same(beaker.Get<LiquidContainer>(), burner.Get<HeatSource>().HeatedContainer);
        }

        [Fact]
        public void SnapIgnoresOccupiedAndIncompatiblePoints()
        {
            var (world, burner, beaker, _) = HeatingSetup();
            var snaps = new SnapSystem(world);
            var secondBeaker = world.Spawn(EquipmentIds.Beaker250, Pose3.At(0.4f, 0, 0));

            // Gauze is taken by the first beaker.
            Assert.False(snaps.TryFindSnap(secondBeaker, Pose3.At(0, 0.21f, 0), out _));

            // A burner has no snap that fits anything near a thermometer port.
            Assert.False(snaps.TryFindSnap(burner, new Pose3(beaker.Pose.Position, Quaternion.Identity), out _));
        }

        [Fact]
        public void DropSnapUsesHorizontalDistanceWithinDropRange()
        {
            var world = new LabWorld();
            var beaker = world.Spawn(EquipmentIds.Beaker250Water, Pose3.At(0, 0, 0));
            var thermometer = world.Spawn(EquipmentIds.Thermometer, Pose3.At(0.5f, 0, 0));
            var snaps = new SnapSystem(world);

            // Hovering 12 cm above the beaker, roughly over a probe port.
            var hover = Pose3.At(0.015f, 0.12f, 0.005f);
            Assert.False(snaps.TryFindSnap(thermometer, hover, 0.05f, out _));
            Assert.True(snaps.TryFindDropSnap(thermometer, hover, 0.05f, 0.3f, out var drop));
            Assert.Equal(beaker, drop.Target);
            Assert.Equal("probe_port_a", drop.TargetSnap.Id);

            // Too far sideways → no snap.
            Assert.False(snaps.TryFindDropSnap(thermometer, Pose3.At(0.2f, 0.12f, 0), 0.05f, 0.3f, out _));
        }

        [Fact]
        public void WaterHeatsThenPlateausAtBoilingPoint()
        {
            var (world, burner, beaker, thermometer) = HeatingSetup();
            var container = beaker.Get<LiquidContainer>();
            var probe = thermometer.Get<Probe>();
            burner.Get<HeatSource>().GasValve = 1.0;
            double startVolume = container.VolumeMilliliters;
            bool sawBoilingEvent = false;
            world.Events.Published += e => sawBoilingEvent |= e.Type == LabEventType.BoilingStarted;

            world.Simulate(30);
            double after30 = container.TemperatureC;
            Assert.InRange(after30, 35, 60); // ≈ 525 W into ≈ 710 J/K

            world.Simulate(400);
            Assert.True(container.IsBoiling);
            Assert.Equal(100.0, container.TemperatureC, 1);
            Assert.InRange(probe.Reading, 99.0, 100.01);
            Assert.True(container.VolumeMilliliters < startVolume - 10, "water should boil away");
            Assert.True(sawBoilingEvent);

            // The logged series shows the heating curve with its plateau.
            var series = world.Logger.Channels.Single().Series;
            Assert.True(series.Count > 1000);
            Assert.True(series.TryGetRange(300, out double min, out double max));
            Assert.InRange(max - min, 0, 1.0);
        }

        [Fact]
        public void HotWaterCoolsTowardAmbientWhenBurnerOff()
        {
            var (world, burner, beaker, _) = HeatingSetup();
            var container = beaker.Get<LiquidContainer>();
            burner.Get<HeatSource>().GasValve = 1.0;
            world.Simulate(60);
            double hot = container.TemperatureC;
            burner.Get<HeatSource>().GasValve = 0;
            world.Simulate(120);
            Assert.True(container.TemperatureC < hot);
            Assert.True(container.TemperatureC > world.AmbientTemperatureC);
        }

        [Fact]
        public void ThermometerLagsBehindLiquid()
        {
            var (world, burner, beaker, thermometer) = HeatingSetup();
            burner.Get<HeatSource>().GasValve = 1.0;
            world.Simulate(20);
            Assert.True(thermometer.Get<Probe>().Reading < beaker.Get<LiquidContainer>().TemperatureC);
        }

        [Fact]
        public void TiltedBottlePoursIntoBeakerBelowLip()
        {
            var world = new LabWorld();
            var beaker = world.Spawn(EquipmentIds.Beaker250, Pose3.At(0, 0, 0));
            var bottle = world.Spawn(EquipmentIds.HclBottle, Pose3.At(0, 0, 0));
            var bottleContainer = bottle.Get<LiquidContainer>();
            var beakerContainer = beaker.Get<LiquidContainer>();

            // Tip the bottle 100° about Z so its mouth points along -X and down; place it so the lip is above the beaker.
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(100 * Math.PI / 180));
            var pose = new Pose3(Vector3.Zero, rotation);
            Vector3 mouthOffset = pose.TransformPoint(new Vector3(0, 0.14f, 0));
            bottle.Pose = new Pose3(new Vector3(-mouthOffset.X + 0.0f, 0.2f, 0), rotation);

            world.Simulate(0.5);

            Assert.True(bottleContainer.IsPouring);
            Assert.Equal(beaker, bottleContainer.PourTarget);
            Assert.True(beakerContainer.VolumeMilliliters > 5);
            Assert.True(beakerContainer.PH < 1.0);
        }

        [Fact]
        public void PouringIntoNothingSpills()
        {
            var world = new LabWorld();
            var bottle = world.Spawn(EquipmentIds.WaterBottle, Pose3.At(0, 0.1f, 0));
            bool spilled = false;
            world.Events.Published += e => spilled |= e.Type == LabEventType.Spill;
            bottle.Pose = new Pose3(bottle.Pose.Position, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1.6f));
            double before = bottle.Get<LiquidContainer>().VolumeMilliliters;

            world.Simulate(1);

            Assert.True(spilled);
            Assert.True(bottle.Get<LiquidContainer>().VolumeMilliliters < before);
        }

        [Fact]
        public void NeutralizationInBeakerWarmsIt()
        {
            var world = new LabWorld();
            var beaker = world.Spawn(EquipmentIds.Beaker250, Pose3.At(0, 0, 0));
            var c = beaker.Get<LiquidContainer>();
            var acid = new Mixture(world.Substances, 20);
            ReagentPreset.Solution("acid", 50, SubstanceIds.HydrochloricAcid, 1.0).FillInto(acid);
            var @base = new Mixture(world.Substances, 20);
            ReagentPreset.Solution("base", 50, SubstanceIds.SodiumHydroxide, 1.0).FillInto(@base);

            c.ReceiveLiquid(acid);
            c.ReceiveLiquid(@base);
            world.Simulate(0.1);

            Assert.Equal(7.0, c.PH, 1);
            Assert.InRange(c.TemperatureC, 25, 28); // ≈ 2.85 kJ into ≈ 0.5 kJ/K
        }

        [Fact]
        public void HazardEventsArePublishedWithPosition()
        {
            var world = new LabWorld();
            var beaker = world.Spawn(EquipmentIds.Beaker250Water, Pose3.At(1, 0, 2));
            LabEvent explosion = null;
            world.Events.Published += e => { if (e.Type == LabEventType.Explosion) explosion = e; };

            beaker.Get<LiquidContainer>().AddSubstance(SubstanceIds.Sodium, 0.06);
            world.Simulate(0.04);

            Assert.NotNull(explosion);
            Assert.Equal(beaker.Id, explosion.InstanceId);
            Assert.Equal(1f, explosion.Position.X, 3);
            Assert.True(explosion.IsHazard);
        }

        [Fact]
        public void RemovingProbeRemovesItsChannel()
        {
            var (world, _, _, thermometer) = HeatingSetup();
            Assert.Single(world.Logger.Channels);
            world.Remove(thermometer);
            Assert.Empty(world.Logger.Channels);
            Assert.DoesNotContain(world.Connections, c => c.Involves(thermometer));
        }

        [Fact]
        public void MicroViewShowsIonsForSaltyWater()
        {
            var world = new LabWorld();
            var beaker = world.Spawn(EquipmentIds.Beaker250Water, Pose3.Identity);
            var c = beaker.Get<LiquidContainer>();
            c.AddSubstance(SubstanceIds.HydrochloricAcid, 0.01);

            var state = MicroScaleModel.Describe(c, 60);

            Assert.Equal(MicroViewMode.IonDissociation, state.Mode);
            int hydronium = state.Species.Single(s => s.Id == SpeciesIds.Hydronium).Count;
            int chloride = state.Species.Single(s => s.Id == SpeciesIds.ChlorideIon).Count;
            Assert.Equal(hydronium, chloride);
            Assert.Equal(60, state.Species.Sum(s => s.Count));
        }

        [Fact]
        public void MicroViewSpeedRisesWithTemperature()
        {
            Assert.Equal(1.0, MicroScaleModel.SpeedFactor(20), 6);
            Assert.True(MicroScaleModel.SpeedFactor(100) > 1.1);
        }

        [Fact]
        public void ClockRunsFixedStepsScaledByTimeScale()
        {
            var world = new LabWorld();
            world.Clock.TimeScale = 10;
            int steps = world.Advance(0.1);
            Assert.Equal(50, steps);
            Assert.Equal(1.0, world.Time, 6);
        }

        [Fact]
        public void EveryCatalogItemSpawns()
        {
            var world = new LabWorld();
            foreach (var def in world.Catalog.All)
            {
                world.Spawn(def.Id, Pose3.Identity);
            }

            world.Simulate(1);
            Assert.Equal(world.Catalog.All.Count, world.Instances.Count());
        }

        [Fact]
        public void ExperimentsReferenceKnownEquipment()
        {
            var catalog = EquipmentCatalog.CreateDefault();
            foreach (var experiment in ExperimentCatalog.CreateDefault().All)
            {
                foreach (var id in experiment.Equipment.Concat(experiment.InitialLayout.Select(l => l.id)))
                {
                    Assert.True(catalog.Contains(id), $"{experiment.Id} references unknown '{id}'");
                }
            }
        }
    }
}
