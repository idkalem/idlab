using System;
using System.Collections.Generic;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;

namespace IDLab.Core.Micro
{
    public enum MicroViewMode
    {
        Idle,
        ThermalMotion,
        IonDissociation,
        ElectronFlow,
    }

    public enum MatterPhase
    {
        Solid,
        Liquid,
        Boiling,
    }

    /// <summary>How one particle species should look in the atomic inset.</summary>
    public readonly struct MicroSpecies
    {
        public readonly string Id;
        public readonly int Count;
        public readonly int Charge;
        public readonly float Radius;   // relative, H₂O = 1
        public readonly ColorRGBA Color;

        public MicroSpecies(string id, int count, int charge, float radius, ColorRGBA color)
        {
            Id = id;
            Count = count;
            Charge = charge;
            Radius = radius;
            Color = color;
        }
    }

    /// <summary>Everything the micro-view renderer needs; it never inspects chemistry directly.</summary>
    public sealed class MicroViewState
    {
        public MicroViewMode Mode { get; set; }
        public MatterPhase Phase { get; set; } = MatterPhase.Liquid;
        public double TemperatureC { get; set; }

        /// <summary>RMS particle speed relative to 20 °C (∝ √T in kelvin).</summary>
        public double SpeedFactor { get; set; } = 1.0;

        public List<MicroSpecies> Species { get; } = new List<MicroSpecies>();
        public string Caption { get; set; } = "";
    }

    /// <summary>Maps macroscopic state (a container's mixture and temperature) onto a particle picture.</summary>
    public static class MicroScaleModel
    {
        private const double ReferenceKelvin = 293.15;
        private const double SoluteDisplayShare = 0.45; // solutes are ~0.1% of particles in reality; exaggerate so they're visible

        private static readonly Dictionary<string, (int charge, float radius, ColorRGBA color)> Visuals =
            new Dictionary<string, (int, float, ColorRGBA)>
            {
                { SpeciesIds.Water, (0, 1.0f, ColorRGBA.FromHex(0x4FA3FF)) },
                { SpeciesIds.Hydronium, (1, 1.05f, ColorRGBA.FromHex(0xFF4F4F)) },
                { SpeciesIds.Hydroxide, (-1, 0.95f, ColorRGBA.FromHex(0x7B61FF)) },
                { SpeciesIds.SodiumIon, (1, 0.8f, ColorRGBA.FromHex(0xB06BFF)) },
                { SpeciesIds.PotassiumIon, (1, 1.0f, ColorRGBA.FromHex(0xD08CFF)) },
                { SpeciesIds.ChlorideIon, (-1, 1.3f, ColorRGBA.FromHex(0x4FE07A)) },
                { SpeciesIds.NitrateIon, (-1, 1.35f, ColorRGBA.FromHex(0x5FA8FF)) },
                { SpeciesIds.SulfateIon, (-2, 1.5f, ColorRGBA.FromHex(0xFFE14F)) },
                { SpeciesIds.AmmoniumIon, (1, 1.1f, ColorRGBA.FromHex(0x5FD3C8)) },
                { SpeciesIds.Ammonia, (0, 1.05f, ColorRGBA.FromHex(0x9EE8E0)) },
                { SpeciesIds.MagnesiumIon, (2, 0.75f, ColorRGBA.FromHex(0xC9CCD2)) },
                { SpeciesIds.ZincIon, (2, 0.8f, ColorRGBA.FromHex(0xA7B0BF)) },
                { SpeciesIds.IronIIIon, (2, 0.85f, ColorRGBA.FromHex(0x8FCB86)) },
                { SpeciesIds.IronIIIIon, (3, 0.8f, ColorRGBA.FromHex(0xE8901A)) },
                { SpeciesIds.CopperIon, (2, 0.82f, ColorRGBA.FromHex(0x1F6FE5)) },
                { SpeciesIds.SilverIon, (1, 1.0f, ColorRGBA.FromHex(0xD8DADF)) },
                { SpeciesIds.CalciumIon, (2, 0.95f, ColorRGBA.FromHex(0xF0E6D2)) },
                { SpeciesIds.BicarbonateIon, (-1, 1.35f, ColorRGBA.FromHex(0xF3F3F3)) },
                { SpeciesIds.PermanganateIon, (-1, 1.4f, ColorRGBA.FromHex(0x9B1FB0)) },
                { SpeciesIds.IodideIon, (-1, 1.6f, ColorRGBA.FromHex(0xC28B3A)) },
                { SpeciesIds.AcetateIon, (-1, 1.5f, ColorRGBA.FromHex(0xFFB347)) },
                { SpeciesIds.AceticAcid, (0, 1.6f, ColorRGBA.FromHex(0xE8D9A8)) },
                { SpeciesIds.HypochloriteIon, (-1, 1.2f, ColorRGBA.FromHex(0xE6E14A)) },
                { SpeciesIds.Ethanol, (0, 1.4f, ColorRGBA.FromHex(0xFFB37A)) },
                { SpeciesIds.Sucrose, (0, 2.2f, ColorRGBA.FromHex(0xFFF2CC)) },
                { SpeciesIds.HydrogenPeroxide, (0, 1.05f, ColorRGBA.FromHex(0xBFE6FF)) },
                { SpeciesIds.SodiumAtom, (0, 1.4f, ColorRGBA.FromHex(0xBFBFB8)) },
                { SpeciesIds.Indicator, (0, 2.0f, ColorRGBA.FromHex(0x3CB43C)) },
                { "Mg", (0, 1.3f, ColorRGBA.FromHex(0xC9CCD2)) },
                { "Zn", (0, 1.25f, ColorRGBA.FromHex(0xAEB4BE)) },
                { "Fe", (0, 1.2f, ColorRGBA.FromHex(0x6A6D73)) },
                { "Cu", (0, 1.2f, ColorRGBA.FromHex(0xC8703A)) },
                { "Ag", (0, 1.3f, ColorRGBA.FromHex(0xE0E2E6)) },
            };

        public static double SpeedFactor(double temperatureC) =>
            Math.Sqrt(Math.Max(1.0, temperatureC + PhysicalConstants.KelvinOffset) / ReferenceKelvin);

        public static MicroViewState Idle(string caption = "Select a container to look inside it.")
        {
            return new MicroViewState { Mode = MicroViewMode.Idle, Caption = caption };
        }

        public static MicroViewState Describe(LiquidContainer container, int particleBudget = 60)
        {
            if (container == null || container.Contents.IsEmpty)
            {
                return Idle(container == null ? "Select a container to look inside it." : "The container is empty.");
            }

            var mixture = container.Contents;
            var state = new MicroViewState
            {
                TemperatureC = container.TemperatureC,
                SpeedFactor = SpeedFactor(container.TemperatureC),
                Phase = container.IsBoiling ? MatterPhase.Boiling : MatterPhase.Liquid,
            };

            // 1) Formula units: the dissociated and molecular fraction of each solute. Allocating whole units
            //    (rather than individual ions) keeps the picture stoichiometric and charge balanced.
            double waterMoles = 0;
            var units = new Dictionary<string, double>();
            var unitParts = new Dictionary<string, IReadOnlyList<IonPart>>();
            bool aqueous = mixture.Contains(SubstanceIds.Water);
            foreach (var kv in mixture.Components)
            {
                var s = mixture.Database.Get(kv.Key);
                if (s.Id == SubstanceIds.Water)
                {
                    waterMoles = kv.Value;
                    continue;
                }

                double dissociated = !s.IsDissolved(aqueous) || s.Ions.Count == 0
                    ? 0.0
                    : s.Kind == SubstanceKind.WeakAcid ? AcidBase.WeakAcidDissociation(s, mixture)
                    : s.Kind == SubstanceKind.WeakBase ? AcidBase.WeakBaseProtonation(s, mixture)
                    : 1.0;

                if (dissociated > 0)
                {
                    units[s.Id + ":ions"] = kv.Value * dissociated;
                    unitParts[s.Id + ":ions"] = s.Ions;
                }

                if (dissociated < 1.0 && s.MolecularSpeciesId != null)
                {
                    units[s.Id + ":mol"] = kv.Value * (1.0 - dissociated);
                    unitParts[s.Id + ":mol"] = new[] { new IonPart(s.MolecularSpeciesId) };
                }
            }

            // 2) Allocate the particle budget: solutes are exaggerated, solvent fills the rest.
            double unitMoles = 0, particleMoles = 0;
            foreach (var kv in units)
            {
                unitMoles += kv.Value;
                particleMoles += kv.Value * ParticlesPerUnit(unitParts[kv.Key]);
            }

            var speciesCounts = new Dictionary<string, int>();
            int soluteParticles = 0;
            if (unitMoles > 0)
            {
                int soluteBudget = waterMoles > 0 ? (int)Math.Round(particleBudget * SoluteDisplayShare) : particleBudget;
                double particlesPerUnit = particleMoles / unitMoles;
                int unitBudget = Math.Max(units.Count, (int)Math.Round(soluteBudget / particlesPerUnit));
                foreach (var kv in LargestRemainder(units, unitBudget, unitMoles))
                {
                    foreach (var part in unitParts[kv.Key])
                    {
                        speciesCounts.TryGetValue(part.SpeciesId, out int existing);
                        speciesCounts[part.SpeciesId] = existing + kv.Value * part.Count;
                        soluteParticles += kv.Value * part.Count;
                    }
                }
            }

            var speciesIds = new List<string>(speciesCounts.Keys);
            speciesIds.Sort(string.CompareOrdinal);
            foreach (var id in speciesIds)
            {
                AddSpecies(state, id, speciesCounts[id]);
            }

            if (waterMoles > 0)
            {
                AddSpecies(state, SpeciesIds.Water, Math.Max(0, particleBudget - soluteParticles));
            }

            bool hasIons = false;
            foreach (var sp in state.Species)
            {
                hasIons |= sp.Charge != 0;
            }

            state.Mode = hasIons ? MicroViewMode.IonDissociation : MicroViewMode.ThermalMotion;
            state.Caption = BuildCaption(state, container);
            return state;
        }

        private static string BuildCaption(MicroViewState state, LiquidContainer container)
        {
            string temperature = $"{state.TemperatureC:0} °C";
            if (state.Phase == MatterPhase.Boiling)
            {
                return $"{temperature} · Boiling: molecules gain enough energy to escape as vapour.";
            }

            if (state.Mode == MicroViewMode.IonDissociation)
            {
                return $"{temperature} · pH {container.PH:0.0} · Ions dissociated in solution (not to scale).";
            }

            return $"{temperature} · Molecules move ×{state.SpeedFactor:0.00} faster than at 20 °C.";
        }

        private static void AddSpecies(MicroViewState state, string id, int count)
        {
            if (count <= 0)
            {
                return;
            }

            var visual = Visuals.TryGetValue(id, out var v) ? v : (0, 1.0f, new ColorRGBA(0.8f, 0.8f, 0.8f));
            state.Species.Add(new MicroSpecies(id, count, visual.Item1, visual.Item2, visual.Item3));
        }

        private static int ParticlesPerUnit(IReadOnlyList<IonPart> parts)
        {
            int n = 0;
            foreach (var p in parts)
            {
                n += p.Count;
            }

            return n;
        }

        /// <summary>Integer apportionment that preserves proportions and gives every present species ≥ 1 particle.</summary>
        private static List<KeyValuePair<string, int>> LargestRemainder(Dictionary<string, double> amounts, int budget, double total)
        {
            var result = new List<KeyValuePair<string, int>>();
            if (budget <= 0 || total <= 0)
            {
                return result;
            }

            var keys = new List<string>(amounts.Keys);
            keys.Sort(string.CompareOrdinal); // deterministic
            var counts = new int[keys.Count];
            var remainders = new double[keys.Count];
            int assigned = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                double exact = amounts[keys[i]] / total * budget;
                counts[i] = Math.Max(1, (int)Math.Floor(exact));
                remainders[i] = exact - Math.Floor(exact);
                assigned += counts[i];
            }

            while (assigned < budget)
            {
                int best = 0;
                for (int i = 1; i < keys.Count; i++)
                {
                    if (remainders[i] > remainders[best])
                    {
                        best = i;
                    }
                }

                counts[best]++;
                remainders[best] = -1;
                assigned++;
                if (Array.TrueForAll(remainders, r => r < 0))
                {
                    for (int i = 0; i < remainders.Length; i++)
                    {
                        remainders[i] = 0;
                    }
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                result.Add(new KeyValuePair<string, int>(keys[i], counts[i]));
            }

            return result;
        }
    }
}
