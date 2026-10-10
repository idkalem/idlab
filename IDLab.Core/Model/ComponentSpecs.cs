using IDLab.Core.Components;

namespace IDLab.Core.Model
{
    /// <summary>
    /// Serializable description of a simulation component. Equipment definitions are pure data;
    /// specs are turned into live <see cref="LabComponent"/>s when an instance is spawned.
    /// </summary>
    public abstract class ComponentSpec
    {
        public abstract LabComponent CreateComponent();
    }

    public sealed class LiquidContainerSpec : ComponentSpec
    {
        public double InnerDiameterM { get; set; }
        public double InnerHeightM { get; set; }
        public double GlassMassKg { get; set; }
        public ReagentPreset InitialContents { get; set; }

        /// <summary>Inner diameter of the opening (bottle neck). 0 = open-topped, same as the body.</summary>
        public double MouthDiameterM { get; set; }

        /// <summary>Base centre of the vessel in the equipment's frame (default: the equipment origin).</summary>
        public System.Numerics.Vector3 LocalOffset { get; set; }

        public override LabComponent CreateComponent() =>
            new LiquidContainer(InnerDiameterM, InnerHeightM, GlassMassKg, InitialContents, MouthDiameterM, LocalOffset);
    }

    public sealed class HeatSourceSpec : ComponentSpec
    {
        public double MaxPowerW { get; set; } = 1500;
        public double Efficiency { get; set; } = 0.35;
        public string HeatingSnapId { get; set; } = "heat_surface";
        public bool OpenFlame { get; set; } = true;

        public override LabComponent CreateComponent() => new HeatSource(MaxPowerW, Efficiency, HeatingSnapId, OpenFlame);
    }

    public sealed class ProbeSpec : ComponentSpec
    {
        public ProbeQuantity Quantity { get; set; }
        public double TimeConstantS { get; set; } = 2.0;
        public string TipSnapId { get; set; } = "tip";

        public override LabComponent CreateComponent() => new Probe(Quantity, TimeConstantS, TipSnapId);
    }
}

namespace IDLab.Core.Model
{
    public sealed class StopcockSpec : ComponentSpec
    {
        public double MaxFlowLitersPerSecond { get; set; } = 0.002;
        public System.Numerics.Vector3 OutletLocal { get; set; }

        public override IDLab.Core.Components.LabComponent CreateComponent() =>
            new IDLab.Core.Components.Stopcock(MaxFlowLitersPerSecond, OutletLocal);
    }

    public sealed class BalanceSpec : ComponentSpec
    {
        public string PanSnapId { get; set; } = "pan";

        public override IDLab.Core.Components.LabComponent CreateComponent() => new IDLab.Core.Components.Balance(PanSnapId);
    }
}
