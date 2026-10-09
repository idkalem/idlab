using System;
using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    public enum SubstanceKind
    {
        Solvent,
        StrongAcid,
        WeakAcid,
        StrongBase,
        WeakBase,
        Salt,
        Indicator,
        ReactiveMetal,
        Metal,
        Oxidizer,
        Molecular,
    }

    /// <summary>How a pure substance exists in the lab.</summary>
    public enum SubstancePhase
    {
        /// <summary>A liquid in its own right (water, ethanol) — a solvent.</summary>
        Liquid,

        /// <summary>Only ever handled dissolved (HCl, NaOH solutions): part of the liquid phase.</summary>
        Aqueous,

        /// <summary>A solid; dissolves in water if <see cref="Substance.Soluble"/>, otherwise stays as grains/precipitate.</summary>
        Solid,
    }

    /// <summary>What solid pieces look like when poured or lying in a container.</summary>
    public enum SolidForm
    {
        Powder,
        Crystals,
        Granules,
        Chunks,
        Ribbon,
        Turnings,
        Precipitate,
    }

    /// <summary>GHS hazard pictograms (used for labels, safety cards and warnings).</summary>
    [Flags]
    public enum Hazard
    {
        None = 0,
        Corrosive = 1 << 0,
        Toxic = 1 << 1,
        Flammable = 1 << 2,
        Oxidizer = 1 << 3,
        Irritant = 1 << 4,
        Environment = 1 << 5,
        WaterReactive = 1 << 6,
        Explosive = 1 << 7,
        CompressedGas = 1 << 8,

        /// <summary>GHS08: carcinogen, mutagen, respiratory sensitiser.</summary>
        HealthHazard = 1 << 9,
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
        public SubstancePhase Phase { get; set; } = SubstancePhase.Aqueous;

        /// <summary>For solids: dissolves in water (ions/molecules join the liquid phase).</summary>
        public bool Soluble { get; set; }

        public SolidForm Form { get; set; } = SolidForm.Powder;
        public Hazard Hazards { get; set; }

        public double MolarMass { get; set; }           // g/mol
        public double Density { get; set; } = 1.0;       // g/mL (as it contributes to solution volume)
        public double SpecificHeat { get; set; } = 4184; // J/(kg·K)

        /// <summary>Normal boiling point (°C) for substances that evaporate from an open container; NaN otherwise.</summary>
        public double BoilingPointC { get; set; } = double.NaN;
        public double LatentHeatVaporization { get; set; }   // J/kg

        /// <summary>H⁺ released per formula unit (acids) / OH⁻ accepted (bases).</summary>
        public int Protons { get; set; }
        public int Hydroxides { get; set; }

        /// <summary>Acid dissociation constant (weak acids) / base constant Kb (weak bases).</summary>
        public double Ka { get; set; }
        public double Kb { get; set; }

        /// <summary>For salts: the weak acid or weak base whose conjugate ion this salt carries (CH₃COONa → acetic acid).</summary>
        public string ConjugateId { get; set; }

        /// <summary>Conjugate ions per formula unit ((NH₄)₂SO₄ → 2).</summary>
        public int ConjugateCount { get; set; } = 1;

        /// <summary>Particle species this substance shows as when dissolved (empty = molecular).</summary>
        public List<IonPart> Ions { get; } = new List<IonPart>();

        /// <summary>Micro-view species for the undissociated molecule / solid.</summary>
        public string MolecularSpeciesId { get; set; }

        public ColorRGBA Color { get; set; } = new ColorRGBA(1, 1, 1, 0.2f);

        /// <summary>How strongly a dissolved substance tints a solution, per mol/L (0 = colourless).</summary>
        public double TintStrength { get; set; }

        // ------------------------------------------------------------------ catalogue / search metadata

        /// <summary>Turkish name (front-ends may localise further).</summary>
        public string NameTr { get; set; }

        /// <summary>Extra search names (common and trade names, IUPAC alternatives).</summary>
        public List<string> Aliases { get; } = new List<string>();

        /// <summary>Catalogue group: "ionic", "acid", "element", "organic", "inorganic", "biochem", "indicator", "drug".</summary>
        public string Category { get; set; } = "inorganic";

        /// <summary>Internal helper species (e.g. the HCO₃⁻ ion used for carbonate hydrolysis) — not listed in catalogues.</summary>
        public bool Hidden { get; set; }

        /// <summary>Only handbook data (formula, melting/boiling point, density) is known: no hazard or solubility data.</summary>
        public bool DataLimited { get; set; }

        /// <summary>The density is an estimate (not shown to users).</summary>
        public bool DensityEstimated { get; set; }

        /// <summary>A radioactive element (Tc, Pm, Po … Og).</summary>
        public bool Radioactive { get; set; }

        /// <summary>Made only a few atoms at a time in particle accelerators (Z ≥ 100): no bulk properties.</summary>
        public bool Synthetic { get; set; }

        /// <summary>CAS registry number, when known.</summary>
        public string Cas { get; set; }

        /// <summary>A gas at 25 °C; only usable in the lab dissolved in water (if soluble).</summary>
        public bool IsGas { get; set; }

        /// <summary>For pure elements: the element symbol.</summary>
        public string ElementSymbol { get; set; }

        // ------------------------------------------------------------------ ionic composition (for generic reactions)

        /// <summary>Cation key ("Na", "Fe3", "NH4", "H" for acids) — null for molecular substances.</summary>
        public string CationKey { get; set; }
        public int CationCharge { get; set; }
        public int CationCount { get; set; }

        /// <summary>Anion key ("Cl", "SO4", "OH", "O" …).</summary>
        public string AnionKey { get; set; }
        public int AnionCharge { get; set; }
        public int AnionCount { get; set; }

        public bool IsIonic => CationKey != null && AnionKey != null;

        /// <summary>Soluble but strongly coloured complex that forms from its ions on mixing (Fe(SCN)₃).</summary>
        public bool FormsComplex { get; set; }

        /// <summary>Weak-acid behaviour of the salt itself: hydrated metal cations (Fe³⁺, Al³⁺) or acid anions (HSO₄⁻).</summary>
        public double SaltAcidKa { get; set; }
        public int SaltAcidCount { get; set; } = 1;

        public bool IsVolatile => !double.IsNaN(BoilingPointC) && LatentHeatVaporization > 0;
        public bool IsAcid => Kind == SubstanceKind.StrongAcid || Kind == SubstanceKind.WeakAcid;
        public bool IsBase => Kind == SubstanceKind.StrongBase || Kind == SubstanceKind.WeakBase;
        public bool IsFlammable => (Hazards & Hazard.Flammable) != 0;

        /// <summary>Ions in water, given whether a solvent is present.</summary>
        public bool IsDissolved(bool waterPresent) =>
            Phase == SubstancePhase.Aqueous || (Phase == SubstancePhase.Solid && Soluble && waterPresent);

        /// <summary>Counts as part of the liquid phase (pours, sets the liquid level and colour).</summary>
        public bool IsLiquidPhase(bool waterPresent) => Phase == SubstancePhase.Liquid || IsDissolved(waterPresent);

        public override string ToString() => $"{Name} ({Formula})";
    }
}
