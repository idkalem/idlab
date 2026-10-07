using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    public enum SubstanceKind
    {
        Solvent,
        StrongAcid,
        WeakAcid,
        StrongBase,
        Salt,
        Indicator,
        ReactiveMetal,
        Oxidizer,
    }

    /// <summary>A particle species shown in the micro view when a substance dissolves (e.g. Na⁺, Cl⁻).</summary>
    public readonly struct IonPart
    {
        public readonly string SpeciesId;
        public readonly int Count;

        public IonPart(string speciesId, int count = 1)
        {
            SpeciesId = speciesId;
            Count = count;
        }
    }

    /// <summary>Thermophysical + chemical properties of a pure substance.</summary>
    public sealed class Substance
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Formula { get; set; }
        public SubstanceKind Kind { get; set; }

        public double MolarMass { get; set; }           // g/mol
        public double Density { get; set; } = 1.0;       // g/mL (as it contributes to solution volume)
        public double SpecificHeat { get; set; } = 4184; // J/(kg·K)

        /// <summary>Normal boiling point (°C) for substances that evaporate from an open container; NaN otherwise.</summary>
        public double BoilingPointC { get; set; } = double.NaN;
        public double LatentHeatVaporization { get; set; }   // J/kg

        /// <summary>H⁺ released per formula unit (acids) / OH⁻ released (bases).</summary>
        public int Protons { get; set; }
        public int Hydroxides { get; set; }

        /// <summary>Acid dissociation constant for weak acids.</summary>
        public double Ka { get; set; }

        /// <summary>For weak acids: the salt formed on neutralisation (its conjugate base).</summary>
        public string ConjugateBaseId { get; set; }

        /// <summary>Particle species this substance shows as when dissolved (empty = molecular).</summary>
        public List<IonPart> Ions { get; } = new List<IonPart>();

        /// <summary>Micro-view species for the undissociated molecule.</summary>
        public string MolecularSpeciesId { get; set; }

        public ColorRGBA Color { get; set; } = new ColorRGBA(1, 1, 1, 0.2f);

        public bool IsVolatile => !double.IsNaN(BoilingPointC) && LatentHeatVaporization > 0;
        public bool IsAcid => Kind == SubstanceKind.StrongAcid || Kind == SubstanceKind.WeakAcid;

        public override string ToString() => $"{Name} ({Formula})";
    }
}
