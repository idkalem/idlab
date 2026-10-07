using System;
using System.Numerics;
using IDLab.Core;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using IDLab.Core.Interaction;
using IDLab.Core.Model;

// Runs lab scenarios without Unity — handy for content designers checking numbers and for CI smoke tests.
string scenario = args.Length > 0 ? args[0] : "heating";
switch (scenario)
{
    case "heating": Heating(); break;
    case "neutralize": Neutralize(); break;
    default: Console.WriteLine("usage: sim-cli [heating|neutralize]"); break;
}

static void Heating()
{
    var world = new LabWorld();
    var burner = world.Spawn(EquipmentIds.BunsenBurner, Pose3.Identity);
    var beaker = world.Spawn(EquipmentIds.Beaker250Water, Pose3.At(0.3f, 0, 0));
    var thermometer = world.Spawn(EquipmentIds.Thermometer, Pose3.At(0.5f, 0, 0));
    var snaps = new SnapSystem(world);
    snaps.TryFindDropSnap(beaker, Pose3.At(0, 0.25f, 0), 0.05f, 0.4f, out var onBurner);
    snaps.Apply(beaker, onBurner);
    snaps.TryFindDropSnap(thermometer, new Pose3(beaker.Pose.TransformPoint(new Vector3(0.018f, 0.12f, 0.008f)), Quaternion.Identity), 0.05f, 0.4f, out var inBeaker);
    snaps.Apply(thermometer, inBeaker);
    burner.Get<HeatSource>().GasValve = 1;
    world.Events.Published += e => Console.WriteLine($"  event: {e}");

    var c = beaker.Get<LiquidContainer>();
    Console.WriteLine("time_s,thermometer_C,liquid_C,volume_mL");
    for (int t = 0; t <= 600; t += 30)
    {
        Console.WriteLine($"{t},{thermometer.Get<Probe>().Reading:0.0},{c.TemperatureC:0.0},{c.VolumeMilliliters:0.0}");
        world.Simulate(30);
    }
}

static void Neutralize()
{
    var world = new LabWorld();
    var beaker = world.Spawn(EquipmentIds.Beaker250, Pose3.Identity);
    var c = beaker.Get<LiquidContainer>();
    var acid = new Mixture(world.Substances);
    ReagentPreset.Solution("HCl", 50, SubstanceIds.HydrochloricAcid, 1.0).FillInto(acid);
    c.ReceiveLiquid(acid);
    Console.WriteLine("naoh_mL,pH,temp_C");
    for (int ml = 0; ml <= 80; ml += 5)
    {
        world.Simulate(0.1);
        Console.WriteLine($"{ml},{c.PH:0.00},{c.TemperatureC:0.0}");
        var dose = new Mixture(world.Substances);
        ReagentPreset.Solution("NaOH", 5, SubstanceIds.SodiumHydroxide, 1.0).FillInto(dose);
        c.ReceiveLiquid(dose);
    }
}
