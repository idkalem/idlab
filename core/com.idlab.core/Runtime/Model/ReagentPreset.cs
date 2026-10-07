using System.Collections.Generic;
using IDLab.Core.Chemistry;

namespace IDLab.Core.Model
{
    public readonly struct ReagentPart
    {
        public readonly string SubstanceId;
        public readonly double Moles;   // NaN when given in grams
        public readonly double Grams;

        public ReagentPart(string substanceId, double moles)
        {
            SubstanceId = substanceId;
            Moles = moles;
            Grams = double.NaN;
        }

        private ReagentPart(string substanceId, double moles, double grams)
        {
            SubstanceId = substanceId;
            Moles = moles;
            Grams = grams;
        }

        public static ReagentPart FromGrams(string substanceId, double grams) => new ReagentPart(substanceId, double.NaN, grams);
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

        /// <summary>A solid reagent jar: <paramref name="grams"/> of one substance.</summary>
        public static ReagentPreset Solid(string label, string substanceId, double grams) =>
            new ReagentPreset(label, new[] { ReagentPart.FromGrams(substanceId, grams) });

        /// <summary>Pure liquid by volume (e.g. ethanol).</summary>
        public static ReagentPreset Liquid(string label, string substanceId, double milliliters, double density, double molarMass) =>
            new ReagentPreset(label, new[] { new ReagentPart(substanceId, milliliters * density / molarMass) });

        public void FillInto(Mixture mixture)
        {
            foreach (var part in Parts)
            {
                double moles = double.IsNaN(part.Moles) ? part.Grams / mixture.Database.Get(part.SubstanceId).MolarMass : part.Moles;
                mixture.Add(part.SubstanceId, moles);
            }
        }

        private static double WaterMoles(double milliliters) => milliliters * 1.0 / 18.015; // ρ = 1 g/mL
    }
}
