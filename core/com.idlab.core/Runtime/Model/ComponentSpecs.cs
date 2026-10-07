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

        public override LabComponent CreateComponent() =>
            new LiquidContainer(InnerDiameterM, InnerHeightM, GlassMassKg, InitialContents, MouthDiameterM);
    }

    public sealed class HeatSourceSpec : ComponentSpec
    {
        public double MaxPowerW { get; set; } = 1500;
        public double Efficiency { get; set; } = 0.35;
        public string HeatingSnapId { get; set; } = "heat_surface";

        public override LabComponent CreateComponent() => new HeatSource(MaxPowerW, Efficiency, HeatingSnapId);
    }

    public sealed class ProbeSpec : ComponentSpec
    {
        public ProbeQuantity Quantity { get; set; }
        public double TimeConstantS { get; set; } = 2.0;
        public string TipSnapId { get; set; } = "tip";

        public override LabComponent CreateComponent() => new Probe(Quantity, TimeConstantS, TipSnapId);
    }
}
