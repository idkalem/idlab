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
        public readonly string Code;
        public readonly string RuleId;

        public ReactionEffect(LabEventType type, double severity, string message, string code = null, string ruleId = null)
        {
            Type = type;
            Severity = severity;
            Message = message;
            Code = code;
            RuleId = ruleId;
        }
    }

    public sealed class ReactionOutcome
    {
        public double HeatReleasedJ { get; set; }
        public List<ReactionEffect> Effects { get; } = new List<ReactionEffect>();
        public List<string> AppliedRules { get; } = new List<string>();

        /// <summary>Gas released this call (formula → mol). The gas leaves the container.</summary>
        public Dictionary<string, double> GasReleased { get; } = new Dictionary<string, double>();

        /// <summary>A slow (kinetic) reaction still has reactants left: run again next tick.</summary>
        public bool Ongoing { get; set; }

        public void AddGas(string formula, double moles)
        {
            GasReleased.TryGetValue(formula, out double n);
            GasReleased[formula] = n + moles;
        }
    }

    public interface IReactionRule
    {
        string Id { get; }

        /// <summary>Applies the reaction for a step of <paramref name="dt"/> seconds. Returns true if anything changed.</summary>
        bool TryApply(Mixture mixture, ReactionOutcome outcome, double dt);
    }

    /// <summary>
    /// Runs reaction rules over a mixture until nothing changes. Fast reactions (neutralisation, precipitation)
    /// complete within a tick; slow ones (metal + acid, displacement) follow first-order kinetics and keep the
    /// container "reacting" over many ticks so bubbles and colour changes develop over time.
    /// </summary>
    public sealed class ReactionEngine
    {
        private const int MaxPasses = 8;
        private readonly List<IReactionRule> _rules = new List<IReactionRule>();

        public IReadOnlyList<IReactionRule> Rules => _rules;

        public void AddRule(IReactionRule rule) => _rules.Add(rule);

        public ReactionOutcome Process(Mixture mixture, double dt = 0.02)
        {
            var outcome = new ReactionOutcome();
            var appliedKinetic = new HashSet<IReactionRule>();
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                bool changed = false;
                foreach (var rule in _rules)
                {
                    if (appliedKinetic.Contains(rule))
                    {
                        continue; // a kinetic rule advances once per tick
                    }

                    if (rule.TryApply(mixture, outcome, dt))
                    {
                        if (!outcome.AppliedRules.Contains(rule.Id)) outcome.AppliedRules.Add(rule.Id);
                        if ((rule is ReactionRule r && r.RateConstant > 0) || rule is IOncePerTickRule) appliedKinetic.Add(rule);
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

        public static ReactionEngine CreateDefault(SubstanceDatabase db) => ReactionLibrary.Create();
    }

    /// <summary>
    /// Σ aᵢ·Reactantᵢ → Σ bⱼ·Productⱼ (+ gas↑). Extent ξ is limited by the scarcest reactant; ΔH is per ξ.
    /// <see cref="RateConstant"/> &gt; 0 makes it first-order in the limiting reagent: Δξ = ξmax·(1 − e^(−k·dt)).
    /// </summary>
    public sealed class ReactionRule : IReactionRule
    {
        private const double MinExtent = 1e-10;

        public string Id { get; set; }
        public (string id, double coeff)[] Reactants { get; set; }
        public (string id, double coeff)[] Products { get; set; } = new (string, double)[0];

        /// <summary>J per unit extent (negative = exothermic).</summary>
        public double Enthalpy { get; set; }

        /// <summary>1/s; 0 = instantaneous.</summary>
        public double RateConstant { get; set; }

        /// <summary>Must be present but is not consumed (KI for H₂O₂).</summary>
        public string Catalyst { get; set; }

        public string Gas { get; set; }
        public double GasPerExtent { get; set; }
        public bool RequiresWater { get; set; } = true;

        public LabEventType EffectType { get; set; } = LabEventType.Reaction;
        public string EffectCode { get; set; }
        public string EffectMessage { get; set; }

        /// <summary>Extent (mol) that counts as severity 1.</summary>
        public double SeverityScale { get; set; } = 0.02;

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            if (RequiresWater && !m.HasWater) return false;
            if (Catalyst != null && !m.Contains(Catalyst)) return false;

            double max = double.MaxValue;
            foreach (var (id, coeff) in Reactants)
            {
                max = Math.Min(max, m.Moles(id) / coeff);
            }

            if (max <= MinExtent) return false;

            double extent = RateConstant > 0 ? max * (1 - Math.Exp(-RateConstant * Math.Max(dt, 1e-4))) : max;
            if (RateConstant > 0 && max - extent < MinExtent * 10) extent = max; // finish the tail
            if (extent <= 0) return false;

            foreach (var (id, coeff) in Reactants) m.Remove(id, extent * coeff);
            foreach (var (id, coeff) in Products) m.Add(id, extent * coeff);
            outcome.HeatReleasedJ += -Enthalpy * extent;
            if (Gas != null) outcome.AddGas(Gas, extent * GasPerExtent);
            if (RateConstant > 0 && max - extent > MinExtent) outcome.Ongoing = true;
            if (EffectMessage != null)
            {
                outcome.Effects.Add(new ReactionEffect(EffectType, Math.Min(1.0, max / SeverityScale), EffectMessage, EffectCode, Id));
            }

            return true;
        }
    }

    /// <summary>2 Na + 2 H₂O → 2 NaOH + H₂↑ — violently exothermic; large amounts ignite the hydrogen.</summary>
    public sealed class SodiumWaterRule : IReactionRule
    {
        private const double EnthalpyPerMolNa = -184_000.0;
        private const double ExplosionThresholdMol = 0.02; // ≈ 0.5 g of sodium

        public string Id => "hazard.sodium.water";

        public bool TryApply(Mixture mixture, ReactionOutcome outcome, double dt)
        {
            double extent = Math.Min(mixture.Moles(SubstanceIds.Sodium), mixture.Moles(SubstanceIds.Water));
            if (extent <= 1e-9) return false;

            mixture.Remove(SubstanceIds.Sodium, extent);
            mixture.Remove(SubstanceIds.Water, extent);
            mixture.Add(SubstanceIds.SodiumHydroxide, extent);
            outcome.HeatReleasedJ += -EnthalpyPerMolNa * extent;
            outcome.AddGas("H₂", extent / 2);

            outcome.Effects.Add(extent >= ExplosionThresholdMol
                ? new ReactionEffect(LabEventType.Explosion, Math.Min(1.0, extent / 0.1),
                    "Sodium reacted violently with water — hydrogen gas ignited!", "hazard.sodium.explosion", Id)
                : new ReactionEffect(LabEventType.Fizz, Math.Min(1.0, extent / ExplosionThresholdMol),
                    "Sodium fizzes and skates across the water, releasing hydrogen.", "hazard.sodium.fizz", Id));
            return true;
        }
    }

    /// <summary>All reactions the lab knows about.</summary>
    public static class ReactionLibrary
    {
        private const double StrongNeutralization = -57_100.0;

        public static ReactionEngine Create()
        {
            var e = new ReactionEngine();
            const string W = SubstanceIds.Water;

            // ---------------------------------------------------------- hazards first
            e.AddRule(new SodiumWaterRule());
            e.AddRule(new ReactionRule
            {
                Id = "hazard.bleach.acid",
                Reactants = new[] { (SubstanceIds.SodiumHypochlorite, 1.0), (SubstanceIds.HydrochloricAcid, 2.0) },
                Products = new[] { (SubstanceIds.SodiumChloride, 1.0), (W, 1.0) },
                Gas = "Cl₂", GasPerExtent = 1,
                EffectType = LabEventType.ToxicGas, EffectCode = "hazard.chlorine",
                EffectMessage = "Toxic chlorine gas released! Bleach must never be mixed with acids.",
            });
            e.AddRule(new ReactionRule
            {
                Id = "hazard.bleach.ammonia",
                Reactants = new[] { (SubstanceIds.SodiumHypochlorite, 1.0), (SubstanceIds.Ammonia, 1.0) },
                Products = new[] { (SubstanceIds.SodiumHydroxide, 1.0) },
                Gas = "NH₂Cl", GasPerExtent = 1,
                EffectType = LabEventType.ToxicGas, EffectCode = "hazard.chloramine",
                EffectMessage = "Toxic chloramine vapour! Never mix bleach with ammonia cleaners.",
            });

            // ---------------------------------------------------------- neutralisation (acid + base → salt + water)
            void Neutral(string acid, double a, string @base, double b, string salt, double water, double dh) =>
                e.AddRule(new ReactionRule
                {
                    Id = $"neutralize.{acid}.{@base}",
                    Reactants = new[] { (acid, a), (@base, b) },
                    Products = new[] { (salt, 1.0), (W, water) },
                    Enthalpy = dh,
                });

            const string HCl = SubstanceIds.HydrochloricAcid, H2SO4 = SubstanceIds.SulfuricAcid, HNO3 = SubstanceIds.NitricAcid,
                HAc = SubstanceIds.AceticAcid, NaOH = SubstanceIds.SodiumHydroxide, KOH = SubstanceIds.PotassiumHydroxide,
                NH3 = SubstanceIds.Ammonia;
            Neutral(HCl, 1, NaOH, 1, SubstanceIds.SodiumChloride, 1, StrongNeutralization);
            Neutral(HCl, 1, KOH, 1, SubstanceIds.PotassiumChloride, 1, StrongNeutralization);
            Neutral(HNO3, 1, NaOH, 1, SubstanceIds.SodiumNitrate, 1, StrongNeutralization);
            Neutral(HNO3, 1, KOH, 1, SubstanceIds.PotassiumNitrate, 1, StrongNeutralization);
            Neutral(H2SO4, 1, NaOH, 2, SubstanceIds.SodiumSulfate, 2, 2 * StrongNeutralization);
            Neutral(H2SO4, 1, KOH, 2, SubstanceIds.PotassiumSulfate, 2, 2 * StrongNeutralization);
            Neutral(HAc, 1, NaOH, 1, SubstanceIds.SodiumAcetate, 1, -56_100);
            Neutral(HAc, 1, KOH, 1, SubstanceIds.PotassiumAcetate, 1, -56_100);
            Neutral(HCl, 1, NH3, 1, SubstanceIds.AmmoniumChloride, 0, -52_200);
            Neutral(HNO3, 1, NH3, 1, SubstanceIds.AmmoniumNitrate, 0, -52_200);
            Neutral(H2SO4, 1, NH3, 2, SubstanceIds.AmmoniumSulfate, 0, -104_400);

            // ---------------------------------------------------------- acid + carbonate → salt + water + CO₂↑
            void Carbonate(string id, string acid, double a, string carbonate, double c, string salt, double s, double water, double co2, double dh, double k) =>
                e.AddRule(new ReactionRule
                {
                    Id = id, Reactants = new[] { (acid, a), (carbonate, c) }, Products = new[] { (salt, s), (W, water) },
                    Enthalpy = dh, RateConstant = k, Gas = "CO₂", GasPerExtent = co2,
                    EffectType = LabEventType.Fizz, EffectCode = "gas.co2", EffectMessage = "Fizzing! Carbon dioxide gas is released.",
                });
            const string NaHCO3 = SubstanceIds.SodiumBicarbonate, CaCO3 = SubstanceIds.CalciumCarbonate;
            Carbonate("co2.hcl.nahco3", HCl, 1, NaHCO3, 1, SubstanceIds.SodiumChloride, 1, 1, 1, 12_000, 2.5);
            Carbonate("co2.hac.nahco3", HAc, 1, NaHCO3, 1, SubstanceIds.SodiumAcetate, 1, 1, 1, 15_000, 1.5);
            Carbonate("co2.hno3.nahco3", HNO3, 1, NaHCO3, 1, SubstanceIds.SodiumNitrate, 1, 1, 1, 12_000, 2.5);
            Carbonate("co2.h2so4.nahco3", H2SO4, 1, NaHCO3, 2, SubstanceIds.SodiumSulfate, 1, 2, 2, 24_000, 2.5);
            Carbonate("co2.hcl.caco3", HCl, 2, CaCO3, 1, SubstanceIds.CalciumChloride, 1, 1, 1, -15_000, 0.08);

            // ---------------------------------------------------------- metal + acid → salt + H₂↑ (activity series)
            void MetalAcid(string metal, string acid, double a, string salt, double dh, double k) =>
                e.AddRule(new ReactionRule
                {
                    Id = $"h2.{metal}.{acid}", Reactants = new[] { (metal, 1.0), (acid, a) }, Products = new[] { (salt, 1.0) },
                    Enthalpy = dh, RateConstant = k, Gas = "H₂", GasPerExtent = 1,
                    EffectType = LabEventType.Fizz, EffectCode = "gas.h2", EffectMessage = "Bubbles of hydrogen gas form on the metal.",
                });
            MetalAcid(SubstanceIds.Magnesium, HCl, 2, SubstanceIds.MagnesiumChloride, -467_000, 0.25);
            MetalAcid(SubstanceIds.Zinc, HCl, 2, SubstanceIds.ZincChloride, -153_000, 0.05);
            MetalAcid(SubstanceIds.Iron, HCl, 2, SubstanceIds.IronIIChloride, -87_000, 0.012);
            MetalAcid(SubstanceIds.Magnesium, H2SO4, 1, SubstanceIds.MagnesiumSulfate, -467_000, 0.25);
            MetalAcid(SubstanceIds.Zinc, H2SO4, 1, SubstanceIds.ZincSulfate, -153_000, 0.05);
            MetalAcid(SubstanceIds.Iron, H2SO4, 1, SubstanceIds.IronIISulfate, -87_000, 0.012);
            // Copper and silver do not react with non-oxidising acids — a teaching point, so no rule.

            // ---------------------------------------------------------- precipitation (instant)
            void Precip(string id, (string, double)[] reactants, (string, double)[] products, string code, string msg) =>
                e.AddRule(new ReactionRule
                {
                    Id = id, Reactants = reactants, Products = products, Enthalpy = -20_000,
                    EffectType = LabEventType.Precipitate, EffectCode = code, EffectMessage = msg, SeverityScale = 0.005,
                });
            const string AgNO3 = SubstanceIds.SilverNitrate, CuSO4 = SubstanceIds.CopperSulfate, FeCl3 = SubstanceIds.IronIIIChloride;
            Precip("precip.agcl.nacl", new[] { (AgNO3, 1.0), (SubstanceIds.SodiumChloride, 1.0) }, new[] { (SubstanceIds.SilverChloride, 1.0), (SubstanceIds.SodiumNitrate, 1.0) }, "precip.agcl", "A white precipitate of silver chloride forms.");
            Precip("precip.agcl.kcl", new[] { (AgNO3, 1.0), (SubstanceIds.PotassiumChloride, 1.0) }, new[] { (SubstanceIds.SilverChloride, 1.0), (SubstanceIds.PotassiumNitrate, 1.0) }, "precip.agcl", "A white precipitate of silver chloride forms.");
            Precip("precip.agcl.hcl", new[] { (AgNO3, 1.0), (HCl, 1.0) }, new[] { (SubstanceIds.SilverChloride, 1.0), (HNO3, 1.0) }, "precip.agcl", "A white precipitate of silver chloride forms.");
            Precip("precip.cuoh2.naoh", new[] { (CuSO4, 1.0), (NaOH, 2.0) }, new[] { (SubstanceIds.CopperHydroxide, 1.0), (SubstanceIds.SodiumSulfate, 1.0) }, "precip.cuoh2", "A pale blue precipitate of copper(II) hydroxide forms.");
            Precip("precip.cuoh2.koh", new[] { (CuSO4, 1.0), (KOH, 2.0) }, new[] { (SubstanceIds.CopperHydroxide, 1.0), (SubstanceIds.PotassiumSulfate, 1.0) }, "precip.cuoh2", "A pale blue precipitate of copper(II) hydroxide forms.");
            Precip("precip.feoh3.naoh", new[] { (FeCl3, 1.0), (NaOH, 3.0) }, new[] { (SubstanceIds.IronIIIHydroxide, 1.0), (SubstanceIds.SodiumChloride, 3.0) }, "precip.feoh3", "A rust-brown precipitate of iron(III) hydroxide forms.");
            Precip("precip.feoh3.koh", new[] { (FeCl3, 1.0), (KOH, 3.0) }, new[] { (SubstanceIds.IronIIIHydroxide, 1.0), (SubstanceIds.PotassiumChloride, 3.0) }, "precip.feoh3", "A rust-brown precipitate of iron(III) hydroxide forms.");

            // ---------------------------------------------------------- displacement (more active metal pushes out less active)
            void Displace(string metal, string salt, double s, string newSalt, string deposit, double d, double k, string code, string msg) =>
                e.AddRule(new ReactionRule
                {
                    Id = $"displace.{metal}.{salt}", Reactants = new[] { (metal, 1.0), (salt, s) }, Products = new[] { (newSalt, 1.0), (deposit, d) },
                    Enthalpy = -150_000, RateConstant = k, EffectCode = code, EffectMessage = msg, SeverityScale = 0.005,
                });
            Displace(SubstanceIds.Iron, CuSO4, 1, SubstanceIds.IronIISulfate, SubstanceIds.Copper, 1, 0.02, "displace.cu", "Copper metal coats the iron; the blue colour fades.");
            Displace(SubstanceIds.Zinc, CuSO4, 1, SubstanceIds.ZincSulfate, SubstanceIds.Copper, 1, 0.05, "displace.cu", "Copper metal coats the zinc; the blue colour fades.");
            Displace(SubstanceIds.Magnesium, CuSO4, 1, SubstanceIds.MagnesiumSulfate, SubstanceIds.Copper, 1, 0.2, "displace.cu", "Copper metal forms on the magnesium; the blue colour fades.");
            Displace(SubstanceIds.Copper, AgNO3, 2, SubstanceIds.CopperNitrate, SubstanceIds.Silver, 2, 0.02, "displace.ag", "Silver crystals grow on the copper and the solution turns blue.");

            // ---------------------------------------------------------- catalytic decomposition: 2 H₂O₂ → 2 H₂O + O₂↑ (KI)
            e.AddRule(new ReactionRule
            {
                Id = "decompose.h2o2.ki",
                Reactants = new[] { (SubstanceIds.HydrogenPeroxide, 2.0) }, Products = new[] { (W, 2.0) },
                Catalyst = SubstanceIds.PotassiumIodide, Enthalpy = -196_000, RateConstant = 0.6, Gas = "O₂", GasPerExtent = 1,
                EffectType = LabEventType.Foam, EffectCode = "foam.o2", EffectMessage = "Foam erupts as oxygen is released (elephant toothpaste).",
                SeverityScale = 0.01,
            });

            // ---------------------------------------------------------- generic, ion-based rules for the whole catalogue
            // Pairs already covered by a hand-tuned rule above are skipped so they are not counted twice.
            var handled = new HashSet<(string, string)>();
            foreach (var rule in e.Rules)
            {
                if (!(rule is ReactionRule rr)) continue;
                foreach (var (a, _) in rr.Reactants)
                {
                    foreach (var (b, _) in rr.Reactants)
                    {
                        if (a != b) handled.Add((a, b));
                    }
                }
            }

            e.AddRule(new GenericMetalWaterRule());
            e.AddRule(new GenericOxideWaterRule());
            e.AddRule(new GenericCombinationRule());
            e.AddRule(new GenericAcidBaseRule(handled));
            e.AddRule(new GenericPrecipitationRule(handled));
            e.AddRule(new GenericMetalAcidRule(handled));
            e.AddRule(new GenericDisplacementRule(handled));
            return e;
        }
    }
}
