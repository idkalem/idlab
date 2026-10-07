using System;

namespace IDLab.Core.Model
{
    [Flags]
    public enum Discipline
    {
        None = 0,
        Physics = 1 << 0,
        Chemistry = 1 << 1,
        Biology = 1 << 2,
        All = Physics | Chemistry | Biology,
    }

    public enum EquipmentCategory
    {
        Glassware,
        Reagent,
        HeatSource,
        Sensor,
        Electrical,
        Optics,
        Mechanics,
        Biology,
    }

    /// <summary>
    /// What a snap point physically is. Compatibility between kinds lives in <see cref="IDLab.Core.Interaction.SnapRules"/>.
    /// </summary>
    public enum SnapKind
    {
        ContainerBase,   // bottom of a beaker/flask/bottle
        HeatSurface,     // gauze on a tripod above a burner, hot plate surface
        ProbeTip,        // tip of a thermometer / pH probe
        ProbePort,       // place inside a container where a probe can rest
        Terminal,        // battery / bulb / resistor terminal       (circuits module)
        WireEnd,         // end of a wire                            (circuits module)
        RailMount,       // optics bench rail slot                   (optics module)
        StageSlot,       // microscope stage slide clip              (biology module)
    }

    public enum ExperimentStatus
    {
        Playable,
        Planned,
    }
}
