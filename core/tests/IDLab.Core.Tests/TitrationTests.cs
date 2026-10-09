using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Interaction;
using IDLab.Core.Model;
using Xunit;

namespace IDLab.Core.Tests
{
    public class TitrationTests
    {
        private static void Dispense(LabWorld world, Stopcock tap, double ml)
        {
            tap.Dispense(ml);
            for (int i = 0; i < 20000 && tap.PendingMilliliters > 1e-9; i++) world.Advance(0.02);
            world.Advance(0.1);
        }

        [Fact]
        public void FlaskDroppedNearTheStandSnapsUnderTheTip()
        {
            var world = new LabWorld();
            var snaps = new SnapSystem(world);
            var burette = world.Spawn(EquipmentIds.Burette, Pose3.Identity);
            var flask = world.Spawn(EquipmentIds.Erlenmeyer, Pose3.At(0.3f, 0, 0));
            // dropped 3 cm off the tip — far outside the old 1.2 cm catch radius
            Assert.True(snaps.TryFindDropSnap(flask, Pose3.At(0.03f, 0.05f, 0.02f), 0.05f, 0.4f, out var c));
            Assert.Equal(burette, c.Target);
            snaps.Apply(flask, c);
            var tap = burette.Get<Stopcock>();
            Dispense(world, tap, 1.0);
            Assert.Equal(1.0, flask.Get<LiquidContainer>().VolumeMilliliters, 1);
        }

        [Fact]
        public void MeteredDoseDeliversExactlyThatMuch()
        {
            var world = new LabWorld();
            var burette = world.Spawn(EquipmentIds.Burette, Pose3.Identity);
            world.Spawn(EquipmentIds.Erlenmeyer, Pose3.Identity);
            var tap = burette.Get<Stopcock>();
            Dispense(world, tap, 0.05); // one drop
            Assert.Equal(0.05, tap.DeliveredMilliliters, 3);
            Dispense(world, tap, 0.5);
            Assert.Equal(0.55, tap.DeliveredMilliliters, 3);
        }

        [Fact]
        public void UnknownAcidReachesEndPointAt22Millilitres()
        {
            var world = new LabWorld();
            var burette = world.Spawn(EquipmentIds.Burette, Pose3.Identity);
            var flask = world.Spawn(EquipmentIds.TitrationFlask, Pose3.Identity).Get<LiquidContainer>();
            var tap = burette.Get<Stopcock>();
            Dispense(world, tap, 21.5);
            Assert.InRange(flask.PH, 1.5, 4.0);
            Dispense(world, tap, 1.0);
            Assert.InRange(flask.PH, 10.0, 12.5);
        }
    }
}
