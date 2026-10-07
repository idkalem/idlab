using System;
using System.Collections.Generic;
using IDLab.Core.Events;

namespace IDLab.Core.Chemistry
{
    /// <summary>A visible consequence of a reaction, positioned later by the container that hosts it.</summary>
    public readonly struct ReactionEffect
    {
        public readonly LabEventType Type;
        public readonly double Severity; // 0..1
        public readonly string Message;

        public ReactionEffect(LabEventType type, double severity, string message)
        {
            Type = type;
            Severity = severity;
            Message = message;
        }
    }

    public sealed class ReactionOutcome
    {
        public double HeatReleasedJ { get; set; }
        public List<ReactionEffect> Effects { get; } = new List<ReactionEffect>();
        public List<string> AppliedRules { get; } = new List<string>();
    }

    public interface IReactionRule
    {
        string Id { get; }

        /// <summary>Applies the reaction to <paramref name="mixture"/> if possible. Returns true if anything changed.</summary>
        bool TryApply(Mixture mixture, ReactionOutcome outcome);
    }

    /// <summary>
    /// Runs reaction rules over a mixture until no rule applies. Reactions at this level are treated as
    /// instantaneous (fast compared with the 20 ms tick); their enthalpy is returned as heat for the
    /// container's thermal model, and hazards are returned as effects for the VFX layer.
    /// </summary>
    public sealed class ReactionEngine
    {
        private const int MaxPasses = 8;
        private readonly List<IReactionRule> _rules = new List<IReactionRule>();

        public IReadOnlyList<IReactionRule> Rules => _rules;

        public void AddRule(IReactionRule rule) => _rules.Add(rule);

        public ReactionOutcome Process(Mixture mixture)
        {
            var outcome = new ReactionOutcome();
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                bool changed = false;
                foreach (var rule in _rules)
                {
                    if (rule.TryApply(mixture, outcome))
                    {
                        outcome.AppliedRules.Add(rule.Id);
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            return outcome;
        }

        public static ReactionEngine CreateDefault(SubstanceDatabase db)
        {
            var engine = new ReactionEngine();

            // Hazards first: they consume reagents that would otherwise neutralise quietly.
            engine.AddRule(new SodiumWaterRule());
            engine.AddRule(new BleachAcidRule());

            // Acid + base → salt + water (+ heat).
            engine.AddRule(new NeutralizationRule("neutralize.hcl.naoh",
                SubstanceIds.HydrochloricAcid, SubstanceIds.SodiumHydroxide, SubstanceIds.SodiumChloride,
                PhysicalConstants.NeutralizationEnthalpy));
            engine.AddRule(new NeutralizationRule("neutralize.acetic.naoh",
                SubstanceIds.AceticAcid, SubstanceIds.SodiumHydroxide, SubstanceIds.SodiumAcetate,
                -56_100.0));
            return engine;
        }
    }

    /// <summary>HA + BOH → BA + H₂O, 1:1, ΔH per mole of water formed.</summary>
    public sealed class NeutralizationRule : IReactionRule
    {
        private const double MinExtent = 1e-12;
        private readonly string _acid;
        private readonly string _base;
        private readonly string _salt;
        private readonly double _enthalpyPerMol;

        public string Id { get; }

        public NeutralizationRule(string id, string acid, string @base, string salt, double enthalpyPerMol)
        {
            Id = id;
            _acid = acid;
            _base = @base;
            _salt = salt;
            _enthalpyPerMol = enthalpyPerMol;
        }

        public bool TryApply(Mixture mixture, ReactionOutcome outcome)
        {
            double extent = Math.Min(mixture.Moles(_acid), mixture.Moles(_base));
            if (extent <= MinExtent)
            {
                return false;
            }

            mixture.Remove(_acid, extent);
            mixture.Remove(_base, extent);
            mixture.Add(_salt, extent);
            mixture.Add(SubstanceIds.Water, extent);
            outcome.HeatReleasedJ += -_enthalpyPerMol * extent;
            return true;
        }
    }

    /// <summary>2 Na + 2 H₂O → 2 NaOH + H₂↑ — violently exothermic; large amounts ignite the hydrogen.</summary>
    public sealed class SodiumWaterRule : IReactionRule
    {
        private const double EnthalpyPerMolNa = -184_000.0;
        private const double ExplosionThresholdMol = 0.02; // ≈ 0.5 g of sodium

        public string Id => "hazard.sodium.water";

        public bool TryApply(Mixture mixture, ReactionOutcome outcome)
        {
            double na = mixture.Moles(SubstanceIds.Sodium);
            double water = mixture.Moles(SubstanceIds.Water);
            double extent = Math.Min(na, water);
            if (extent <= 1e-9)
            {
                return false;
            }

            mixture.Remove(SubstanceIds.Sodium, extent);
            mixture.Remove(SubstanceIds.Water, extent);
            mixture.Add(SubstanceIds.SodiumHydroxide, extent);
            outcome.HeatReleasedJ += -EnthalpyPerMolNa * extent;

            if (extent >= ExplosionThresholdMol)
            {
                outcome.Effects.Add(new ReactionEffect(LabEventType.Explosion, Math.Min(1.0, extent / 0.1),
                    "Sodium reacted violently with water — hydrogen gas ignited!"));
            }
            else
            {
                outcome.Effects.Add(new ReactionEffect(LabEventType.Fizz, Math.Min(1.0, extent / ExplosionThresholdMol),
                    "Sodium fizzes and skates across the water, releasing hydrogen."));
            }

            return true;
        }
    }

    /// <summary>NaOCl + 2 HCl → Cl₂↑ + NaCl + H₂O — never mix bleach with acids.</summary>
    public sealed class BleachAcidRule : IReactionRule
    {
        public string Id => "hazard.bleach.acid";

        public bool TryApply(Mixture mixture, ReactionOutcome outcome)
        {
            double extent = Math.Min(mixture.Moles(SubstanceIds.SodiumHypochlorite), mixture.Moles(SubstanceIds.HydrochloricAcid) / 2.0);
            if (extent <= 1e-9)
            {
                return false;
            }

            mixture.Remove(SubstanceIds.SodiumHypochlorite, extent);
            mixture.Remove(SubstanceIds.HydrochloricAcid, 2 * extent);
            mixture.Add(SubstanceIds.SodiumChloride, extent);
            mixture.Add(SubstanceIds.Water, extent);
            // Cl₂ escapes as gas: not added to the mixture.
            outcome.Effects.Add(new ReactionEffect(LabEventType.ToxicGas, Math.Min(1.0, extent / 0.02),
                "Toxic chlorine gas released! Bleach must never be mixed with acids."));
            return true;
        }
    }
}
