using System;
using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    public static class SubstanceIds
    {
        public const string Water = "water";
        public const string HydrochloricAcid = "hcl";
        public const string SodiumHydroxide = "naoh";
        public const string SodiumChloride = "nacl";
        public const string AceticAcid = "acetic_acid";
        public const string SodiumAcetate = "sodium_acetate";
        public const string Sodium = "sodium";
        public const string SodiumHypochlorite = "naocl";
        public const string UniversalIndicator = "universal_indicator";
    }

    /// <summary>Micro-view particle species ids (the visual vocabulary of the atomic inset).</summary>
    public static class SpeciesIds
    {
        public const string Water = "H2O";
        public const string Hydronium = "H3O+";
        public const string Hydroxide = "OH-";
        public const string SodiumIon = "Na+";
        public const string ChlorideIon = "Cl-";
        public const string AcetateIon = "CH3COO-";
        public const string AceticAcid = "CH3COOH";
        public const string HypochloriteIon = "OCl-";
        public const string SodiumAtom = "Na";
        public const string Indicator = "Ind";
    }

    public sealed class SubstanceDatabase
    {
        private readonly Dictionary<string, Substance> _byId = new Dictionary<string, Substance>();

        public IEnumerable<Substance> All => _byId.Values;

        public void Register(Substance substance) => _byId[substance.Id] = substance;

        public Substance Get(string id)
        {
            if (_byId.TryGetValue(id, out var s))
            {
                return s;
            }

            throw new KeyNotFoundException($"Unknown substance '{id}'.");
        }

        public bool TryGet(string id, out Substance substance) => _byId.TryGetValue(id, out substance);

        public static SubstanceDatabase CreateDefault()
        {
            var db = new SubstanceDatabase();

            db.Register(new Substance
            {
                Id = SubstanceIds.Water, Name = "Water", Formula = "H₂O", Kind = SubstanceKind.Solvent,
                MolarMass = 18.015, Density = 1.0, SpecificHeat = 4184,
                BoilingPointC = 100, LatentHeatVaporization = 2.257e6,
                MolecularSpeciesId = SpeciesIds.Water,
                Color = new ColorRGBA(0.70f, 0.85f, 1.0f, 0.25f),
            });

            var hcl = new Substance
            {
                Id = SubstanceIds.HydrochloricAcid, Name = "Hydrochloric acid", Formula = "HCl", Kind = SubstanceKind.StrongAcid,
                MolarMass = 36.46, Density = 1.19, SpecificHeat = 2500, Protons = 1,
                Color = new ColorRGBA(0.95f, 0.97f, 0.85f, 0.25f),
            };
            hcl.Ions.Add(new IonPart(SpeciesIds.Hydronium));
            hcl.Ions.Add(new IonPart(SpeciesIds.ChlorideIon));
            db.Register(hcl);

            var naoh = new Substance
            {
                Id = SubstanceIds.SodiumHydroxide, Name = "Sodium hydroxide", Formula = "NaOH", Kind = SubstanceKind.StrongBase,
                MolarMass = 40.0, Density = 2.13, SpecificHeat = 1490, Hydroxides = 1,
                Color = new ColorRGBA(0.92f, 0.95f, 1.0f, 0.25f),
            };
            naoh.Ions.Add(new IonPart(SpeciesIds.SodiumIon));
            naoh.Ions.Add(new IonPart(SpeciesIds.Hydroxide));
            db.Register(naoh);

            var nacl = new Substance
            {
                Id = SubstanceIds.SodiumChloride, Name = "Sodium chloride", Formula = "NaCl", Kind = SubstanceKind.Salt,
                MolarMass = 58.44, Density = 2.16, SpecificHeat = 880,
                Color = new ColorRGBA(0.95f, 0.95f, 0.95f, 0.2f),
            };
            nacl.Ions.Add(new IonPart(SpeciesIds.SodiumIon));
            nacl.Ions.Add(new IonPart(SpeciesIds.ChlorideIon));
            db.Register(nacl);

            var acetic = new Substance
            {
                Id = SubstanceIds.AceticAcid, Name = "Acetic acid", Formula = "CH₃COOH", Kind = SubstanceKind.WeakAcid,
                MolarMass = 60.05, Density = 1.05, SpecificHeat = 2050, Protons = 1, Ka = 1.8e-5,
                ConjugateBaseId = SubstanceIds.SodiumAcetate, MolecularSpeciesId = SpeciesIds.AceticAcid,
                Color = new ColorRGBA(1.0f, 0.98f, 0.9f, 0.25f),
            };
            acetic.Ions.Add(new IonPart(SpeciesIds.Hydronium));
            acetic.Ions.Add(new IonPart(SpeciesIds.AcetateIon));
            db.Register(acetic);

            var acetate = new Substance
            {
                Id = SubstanceIds.SodiumAcetate, Name = "Sodium acetate", Formula = "CH₃COONa", Kind = SubstanceKind.Salt,
                MolarMass = 82.03, Density = 1.53, SpecificHeat = 1600,
                Color = new ColorRGBA(0.95f, 0.95f, 0.95f, 0.2f),
            };
            acetate.Ions.Add(new IonPart(SpeciesIds.SodiumIon));
            acetate.Ions.Add(new IonPart(SpeciesIds.AcetateIon));
            db.Register(acetate);

            db.Register(new Substance
            {
                Id = SubstanceIds.Sodium, Name = "Sodium metal", Formula = "Na", Kind = SubstanceKind.ReactiveMetal,
                MolarMass = 22.99, Density = 0.97, SpecificHeat = 1230,
                MolecularSpeciesId = SpeciesIds.SodiumAtom,
                Color = new ColorRGBA(0.75f, 0.75f, 0.72f, 1.0f),
            });

            var naocl = new Substance
            {
                Id = SubstanceIds.SodiumHypochlorite, Name = "Sodium hypochlorite (bleach)", Formula = "NaOCl", Kind = SubstanceKind.Oxidizer,
                MolarMass = 74.44, Density = 1.11, SpecificHeat = 3000,
                Color = new ColorRGBA(0.92f, 0.98f, 0.75f, 0.3f),
            };
            naocl.Ions.Add(new IonPart(SpeciesIds.SodiumIon));
            naocl.Ions.Add(new IonPart(SpeciesIds.HypochloriteIon));
            db.Register(naocl);

            db.Register(new Substance
            {
                Id = SubstanceIds.UniversalIndicator, Name = "Universal indicator", Formula = "Ind", Kind = SubstanceKind.Indicator,
                MolarMass = 300, Density = 1.0, SpecificHeat = 2000,
                MolecularSpeciesId = SpeciesIds.Indicator,
                Color = new ColorRGBA(0.2f, 0.7f, 0.2f, 0.8f),
            });

            return db;
        }

        internal static double Mass(Substance s, double moles) => Math.Max(0, moles) * s.MolarMass / 1000.0; // kg
    }
}
