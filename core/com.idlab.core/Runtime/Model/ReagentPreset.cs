using System.Collections.Generic;
using IDLab.Core.Chemistry;

namespace IDLab.Core.Model
{
    public readonly struct ReagentPart
    {
        public readonly string SubstanceId;
        public readonly double Moles;

        public ReagentPart(string substanceId, double moles)
        {
            SubstanceId = substanceId;
            Moles = moles;
        }
    }

    /// <summary>Initial contents of a container (e.g. "250 mL of 1.0 M HCl").</summary>
    public sealed class ReagentPreset
    {
        public string Label { get; }
        public IReadOnlyList<ReagentPart> Parts { get; }

        public ReagentPreset(string label, IReadOnlyList<ReagentPart> parts)
        {
            Label = label;
            Parts = parts;
        }

        public static ReagentPreset Water(double milliliters) =>
            new ReagentPreset($"{milliliters:0} mL water", new[]
            {
                new ReagentPart(SubstanceIds.Water, WaterMoles(milliliters)),
            });

        /// <summary>An aqueous solution: <paramref name="milliliters"/> of water carrying <paramref name="molarity"/> mol/L of solute.</summary>
        public static ReagentPreset Solution(string label, double milliliters, string soluteId, double molarity) =>
            new ReagentPreset(label, new[]
            {
                new ReagentPart(SubstanceIds.Water, WaterMoles(milliliters)),
                new ReagentPart(soluteId, molarity * milliliters / 1000.0),
            });

        public static ReagentPreset Pure(string label, string substanceId, double moles) =>
            new ReagentPreset(label, new[] { new ReagentPart(substanceId, moles) });

        public void FillInto(Mixture mixture)
        {
            foreach (var part in Parts)
            {
                mixture.Add(part.SubstanceId, part.Moles);
            }
        }

        private static double WaterMoles(double milliliters) => milliliters * 1.0 / 18.015; // ρ = 1 g/mL
    }
}
