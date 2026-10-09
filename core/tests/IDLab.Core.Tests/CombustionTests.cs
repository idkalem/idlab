using System.Collections.Generic;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Events;
using IDLab.Core.Interaction;
using IDLab.Core.Model;
using Xunit;

namespace IDLab.Core.Tests
{
    public class CombustionTests
    {
        private static (LabWorld world, LiquidContainer c, HeatSource heat, List<LabEvent> events) OnHeater(string heater, string substanceId, double moles)
        {
            var world = new LabWorld();
            var h = world.Spawn(heater, Pose3.Identity);
            var beaker = world.Spawn(EquipmentIds.Beaker100, Pose3.At(0.3f, 0, 0));
            var snaps = new SnapSystem(world);
            Assert.True(snaps.TryFindDropSnap(beaker, Pose3.At(0, 0.25f, 0), 0.05f, 0.4f, out var on));
            snaps.Apply(beaker, on);
            var c = beaker.Get<LiquidContainer>();
            c.AddSubstance(substanceId, moles);
            var events = new List<LabEvent>();
            world.Events.Published += events.Add;
            return (world, c, h.Get<HeatSource>(), events);
        }

        [Fact]
        public void MagnesiumBurnsToMagnesiumOxideInTheFlame()
        {
            var (world, c, heat, events) = OnHeater(EquipmentIds.BunsenBurner, SubstanceIds.Magnesium, 0.02);
            heat.GasValve = 1;
            world.Simulate(60);
            Assert.Contains(events, e => e.Type == LabEventType.Fire && e.Code.StartsWith("fire.solid:"));
            var mgo = world.Substances.FindByFormula("MgO");
            Assert.True(c.Contents.Moles(mgo.Id) > 0.005, $"MgO {c.Contents.Moles(mgo.Id)}");
            Assert.True(c.Contents.Moles(SubstanceIds.Magnesium) < 0.015);
        }

        [Fact]
        public void SulfurBurnsToSulfurDioxideGas()
        {
            var s = new LabWorld().Substances.FindElement("S");
            var (world, c, heat, events) = OnHeater(EquipmentIds.BunsenBurner, s.Id, 0.03);
            heat.GasValve = 1;
            bool so2 = false;
            for (int i = 0; i < 600 && !so2; i++)
            {
                world.Simulate(0.1);
                so2 = c.OnFire && c.GasFormula == "SO₂";
            }

            Assert.True(so2);
            Assert.True(c.Contents.Moles(s.Id) < 0.03);
        }

        [Fact]
        public void EtherVapourExplodesOverAFlame()
        {
            var ether = new LabWorld().Substances.FindByFormula("(C₂H₅)₂O");
            var (world, c, heat, events) = OnHeater(EquipmentIds.BunsenBurner, ether.Id, 0.2);
            heat.GasValve = 1;
            world.Simulate(30);
            Assert.Contains(events, e => e.Type == LabEventType.Explosion && e.Code.StartsWith("boom.vapour:"));
        }

        [Fact]
        public void EtherIgnitesOnAHotPlateButEthanolDoesNot()
        {
            var ether = new LabWorld().Substances.FindByFormula("(C₂H₅)₂O");
            var (world, _, heat, events) = OnHeater(EquipmentIds.HotPlate, ether.Id, 0.2);
            heat.GasValve = 1;
            world.Simulate(60);
            Assert.Contains(events, e => e.Type == LabEventType.Fire || e.Type == LabEventType.Explosion);

            var (world2, _, heat2, events2) = OnHeater(EquipmentIds.HotPlate, SubstanceIds.Ethanol, 0.3);
            heat2.GasValve = 1;
            world2.Simulate(60);
            Assert.DoesNotContain(events2, e => e.Type == LabEventType.Fire || e.Type == LabEventType.Explosion);
        }

        [Fact]
        public void TntExplodesWhenHeated()
        {
            var tnt = new LabWorld().Substances.FindByFormula("C₇H₅N₃O₆");
            var (world, c, heat, events) = OnHeater(EquipmentIds.BunsenBurner, tnt.Id, 0.004);
            heat.GasValve = 1;
            world.Simulate(60);
            Assert.Contains(events, e => e.Type == LabEventType.Explosion && e.Code.StartsWith("boom.explosive:"));
            Assert.Equal(0, c.Contents.Moles(tnt.Id), 6);
        }

        [Fact]
        public void CopperTurnsBlackWithoutBurning()
        {
            var cu = new LabWorld().Substances.FindElement("Cu");
            var (world, c, heat, events) = OnHeater(EquipmentIds.BunsenBurner, cu.Id, 0.05);
            heat.GasValve = 1;
            world.Simulate(90);
            Assert.DoesNotContain(events, e => e.Type == LabEventType.Fire);
            Assert.True(c.Contents.Moles(world.Substances.FindByFormula("CuO").Id) > 1e-4);
        }

        [Fact]
        public void ThermiteFlashesWhenHeated()
        {
            var db = new LabWorld().Substances;
            var (world, c, heat, events) = OnHeater(EquipmentIds.BunsenBurner, db.FindElement("Al").Id, 0.04);
            c.AddSubstance(db.FindIonic("Fe3", "O").Id, 0.02);
            heat.GasValve = 1;
            world.Simulate(120);
            Assert.Contains(events, e => e.Type == LabEventType.Explosion && e.Code == "boom.thermite");
            Assert.True(c.Contents.Moles(db.FindIonic("Al", "O").Id) > 0.01); // the iron formed may go on burning in the flame
        }

        [Fact]
        public void AmmoniumNitrateExplodesOnStrongHeating()
        {
            var db = new LabWorld().Substances;
            var an = db.FindByFormula("NH₄NO₃");
            var (world, _, heat, events) = OnHeater(EquipmentIds.BunsenBurner, an.Id, 0.05);
            heat.GasValve = 1;
            world.Simulate(120);
            Assert.Contains(events, e => e.Type == LabEventType.Explosion);
        }

        [Fact]
        public void OxideLookupCountsAtoms()
        {
            var db = new LabWorld().Substances;
            Assert.Equal(3, Combustion.AtomsOf("Fe₃O₄", "Fe"));
            Assert.Equal(1, Combustion.AtomsOf("CCl₄", "C"));
            var (al2o3, per) = Combustion.OxideOf(db, db.FindElement("Al"));
            Assert.Equal("Al₂O₃", al2o3.Formula);
            Assert.Equal(0.5, per, 6);
        }
    }
}
