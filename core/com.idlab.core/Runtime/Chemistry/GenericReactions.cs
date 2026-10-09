using System;
using System.Collections.Generic;
using IDLab.Core.Events;

namespace IDLab.Core.Chemistry
{
    /// <summary>
    /// Standard electrode potentials used as the metal activity series (V). A metal with E° &lt; 0 releases H₂ from
    /// non-oxidising acids; a metal displaces the ion of any metal with a higher E° from its salt solution.
    /// </summary>
    public static class ActivitySeries
    {
        public sealed class Entry
        {
            public string Symbol;
            public double Potential;   // E° (V)
            public string CationKey;   // ion formed in acid / displacement
            public double Rate;        // 1/s first-order rate with 1 M acid (0 = does not react)
            public bool ReactsWithWater;
        }

        private static readonly Dictionary<string, Entry> BySymbol = new Dictionary<string, Entry>();

        /// <summary>Metal ion (cation key) → metal symbol deposited when it is displaced.</summary>
        private static readonly Dictionary<string, string> DepositOf = new Dictionary<string, string>();

        static ActivitySeries()
        {
            void E(string sym, double e0, string cation, double rate, bool water = false)
            {
                BySymbol[sym] = new Entry { Symbol = sym, Potential = e0, CationKey = cation, Rate = rate, ReactsWithWater = water };
                if (cation != null && !DepositOf.ContainsKey(cation)) DepositOf[cation] = sym;
            }

            E("Li", -3.04, "Li", 0.6, true);
            E("Cs", -3.03, "Cs", 5, true);
            E("Rb", -2.98, "Rb", 5, true);
            E("K", -2.93, "K", 5, true);
            E("Ba", -2.91, "Ba", 0.8, true);
            E("Sr", -2.89, "Sr", 0.5, true);
            E("Ca", -2.87, "Ca", 0.3, true);
            E("Na", -2.71, "Na", 3, true);
            E("Mg", -2.37, "Mg", 0.25);
            E("Be", -1.85, "Be", 0.02);
            E("Al", -1.66, "Al", 0.03);
            E("Mn", -1.18, "Mn2", 0.08);
            E("Zn", -0.76, "Zn", 0.05);
            E("Cr", -0.74, "Cr3", 0.01);
            E("Fe", -0.44, "Fe2", 0.012);
            E("Cd", -0.40, "Cd", 0.01);
            E("Co", -0.28, "Co2", 0.006);
            E("Ni", -0.25, "Ni2", 0.005);
            E("Sn", -0.14, "Sn2", 0.004);
            E("Pb", -0.13, "Pb2", 0.002);
            E("Cu", 0.34, "Cu2", 0);
            E("Ag", 0.80, "Ag", 0);
            E("Hg", 0.85, "Hg2", 0);
            E("Pt", 1.18, null, 0);
            E("Au", 1.50, null, 0);
            DepositOf["Cu1"] = "Cu";
        }

        public static Entry Get(string symbol) => symbol != null && BySymbol.TryGetValue(symbol, out var e) ? e : null;

        /// <summary>The metal that plates out when this cation is reduced, with its potential.</summary>
        public static Entry ForCation(string cationKey) =>
            cationKey != null && DepositOf.TryGetValue(cationKey, out var sym) ? Get(sym) : null;
    }

    /// <summary>Marks a rule that advances at most once per tick (its own rates are per-second, or it handles all pairs in one call).</summary>
    public interface IOncePerTickRule
    {
    }

    /// <summary>Shared helpers for the ion-based rules.</summary>
    internal static class Ionic
    {
        public static bool Dissolved(Mixture m, Substance s) => s.IsDissolved(m.HasWater);

        public static List<Substance> DissolvedIonic(Mixture m)
        {
            var list = new List<Substance>();
            foreach (var kv in m.Components)
            {
                if (kv.Value <= 1e-10) continue;
                var s = m.Database.Get(kv.Key);
                if (s.IsIonic && Dissolved(m, s)) list.Add(s);
            }

            return list;
        }

        public static bool IsInsolubleSolid(Substance s) => s.Phase == SubstancePhase.Solid && !s.Soluble;
    }

    /// <summary>
    /// Double displacement: two dissolved ionic compounds swap partners when one combination is insoluble
    /// (AgNO₃ + NaCl → AgCl↓ + NaNO₃, Pb(NO₃)₂ + 2 KI → PbI₂↓ + 2 KNO₃) or forms a coloured complex
    /// (FeCl₃ + 3 KSCN → Fe(SCN)₃ + 3 KCl). Stoichiometry follows from the ion counts of the four compounds.
    /// </summary>
    public sealed class GenericPrecipitationRule : IReactionRule, IOncePerTickRule
    {
        private readonly HashSet<(string, string)> _explicit;

        public GenericPrecipitationRule(HashSet<(string, string)> explicitPairs) => _explicit = explicitPairs;

        public string Id => "generic.precipitation";

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            if (!m.HasWater) return false;
            var ions = Ionic.DissolvedIonic(m);
            if (ions.Count < 2) return false;

            bool changed = false;
            var db = m.Database;
            foreach (var s1 in ions)
            {
                foreach (var s2 in ions)
                {
                    if (s1 == s2 || s1.CationKey == s2.CationKey || s1.AnionKey == s2.AnionKey) continue;
                    if (_explicit.Contains((s1.Id, s2.Id)) || _explicit.Contains((s2.Id, s1.Id))) continue;
                    var p = db.FindIonic(s1.CationKey, s2.AnionKey);
                    var q = db.FindIonic(s2.CationKey, s1.AnionKey);
                    if (p == null || q == null || p == s1 || p == s2) continue;
                    if (!Ionic.IsInsolubleSolid(p) && !p.FormsComplex) continue;
                    // A complex only forms from its own ions; never "un-precipitate" an insoluble partner.
                    if (p.FormsComplex && (Ionic.IsInsolubleSolid(q) || q.FormsComplex)) continue;

                    double n1 = m.Moles(s1.Id), n2 = m.Moles(s2.Id);
                    if (n1 <= 1e-10 || n2 <= 1e-10) continue;
                    double xi = Math.Min(n1 * s1.CationCount / p.CationCount, n2 * s2.AnionCount / p.AnionCount);
                    if (xi <= 1e-10) continue;

                    double used1 = p.CationCount * xi / s1.CationCount;
                    double used2 = p.AnionCount * xi / s2.AnionCount;
                    double qMoles = s2.CationCount * used2 / q.CationCount;
                    m.Remove(s1.Id, used1);
                    m.Remove(s2.Id, used2);
                    m.Add(p.Id, xi);
                    m.Add(q.Id, qMoles);
                    outcome.HeatReleasedJ += 20_000 * xi;
                    outcome.Effects.Add(new ReactionEffect(p.FormsComplex ? LabEventType.Reaction : LabEventType.Precipitate,
                        Math.Min(1.0, xi / 0.005), $"{p.Name} forms.", (p.FormsComplex ? "complex:" : "precip:") + p.Id, Id));
                    changed = true;
                }
            }

            return changed;
        }
    }

    /// <summary>
    /// Acid + basic compound → salt (+ water, + gas): hydroxides and oxides are neutralised, carbonates and
    /// hydrogencarbonates fizz (CO₂), sulfides give H₂S, sulfites SO₂ and cyanides HCN (toxic — a safety lesson).
    /// Works for any acid H_qX and any compound M_aY_b in the catalogue whose salt MX exists.
    /// </summary>
    public sealed class GenericAcidBaseRule : IReactionRule, IOncePerTickRule
    {
        private readonly HashSet<(string, string)> _explicit;

        public GenericAcidBaseRule(HashSet<(string, string)> explicitPairs) => _explicit = explicitPairs;

        public string Id => "generic.acid-base";

        private struct Basic
        {
            public double Water, Gas, Enthalpy; // per H⁺ consumed
            public string GasFormula;
            public LabEventType Effect;
            public string Code;
            public double RateSoluble, RateInsoluble;
        }

        private static bool TryBasic(string anion, out Basic b)
        {
            b = default;
            switch (anion)
            {
                case "OH": b = new Basic { Water = 1, Enthalpy = -57_100, RateSoluble = 0, RateInsoluble = 0.15 }; return true;
                case "O": b = new Basic { Water = 0.5, Enthalpy = -60_000, RateSoluble = 0.4, RateInsoluble = 0.08 }; return true;
                case "CO3": b = new Basic { Water = 0.5, Gas = 0.5, GasFormula = "CO₂", Enthalpy = -6_000, Effect = LabEventType.Fizz, Code = "gas.co2", RateSoluble = 2.5, RateInsoluble = 0.08 }; return true;
                case "HCO3": b = new Basic { Water = 1, Gas = 1, GasFormula = "CO₂", Enthalpy = 12_000, Effect = LabEventType.Fizz, Code = "gas.co2", RateSoluble = 2.5, RateInsoluble = 0.5 }; return true;
                case "S": b = new Basic { Water = 0, Gas = 0.5, GasFormula = "H₂S", Enthalpy = -10_000, Effect = LabEventType.ToxicGas, Code = "hazard.h2s", RateSoluble = 1.5, RateInsoluble = 0.05 }; return true;
                case "SO3": b = new Basic { Water = 0.5, Gas = 0.5, GasFormula = "SO₂", Enthalpy = -5_000, Effect = LabEventType.ToxicGas, Code = "hazard.so2", RateSoluble = 1.5, RateInsoluble = 0.1 }; return true;
                case "CN": b = new Basic { Water = 0, Gas = 1, GasFormula = "HCN", Enthalpy = -5_000, Effect = LabEventType.ToxicGas, Code = "hazard.hcn", RateSoluble = 1.5, RateInsoluble = 0.1 }; return true;
                default: return false;
            }
        }

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            if (!m.HasWater) return false;
            var db = m.Database;
            var acids = new List<Substance>();
            var bases = new List<Substance>();
            foreach (var kv in m.Components)
            {
                if (kv.Value <= 1e-10) continue;
                var s = db.Get(kv.Key);
                if (!s.IsIonic) continue;
                if (s.CationKey == "H" && s.IsAcid && Ionic.Dissolved(m, s)) acids.Add(s);
                else if (s.CationKey != "H" && TryBasic(s.AnionKey, out _)) bases.Add(s);
            }

            if (acids.Count == 0 || bases.Count == 0) return false;
            bool changed = false;
            foreach (var acid in acids)
            {
                foreach (var b in bases)
                {
                    if (_explicit.Contains((acid.Id, b.Id)) || _explicit.Contains((b.Id, acid.Id))) continue;
                    var salt = db.FindIonic(b.CationKey, acid.AnionKey);
                    if (salt == null || salt == b) continue;
                    TryBasic(b.AnionKey, out var info);

                    double nAcid = m.Moles(acid.Id), nBase = m.Moles(b.Id);
                    if (nAcid <= 1e-10 || nBase <= 1e-10) continue;

                    // Extent in mol of salt: limited by the metal (from the base) and the anion (from the acid).
                    double max = Math.Min(nBase * b.CationCount / salt.CationCount, nAcid * acid.AnionCount / salt.AnionCount);
                    if (max <= 1e-10) continue;
                    bool soluble = Ionic.Dissolved(m, b);
                    double k = soluble ? info.RateSoluble : info.RateInsoluble;
                    double xi = k > 0 ? max * (1 - Math.Exp(-k * Math.Max(dt, 1e-4))) : max;
                    if (k > 0 && max - xi < 1e-9) xi = max;
                    if (xi <= 1e-12) continue;

                    double protons = salt.CationCount * salt.CationCharge * xi; // H⁺ consumed = positive charge of the salt formed
                    m.Remove(b.Id, salt.CationCount * xi / b.CationCount);
                    m.Remove(acid.Id, salt.AnionCount * xi / acid.AnionCount);
                    m.Add(salt.Id, xi);
                    if (info.Water > 0) m.Add(SubstanceIds.Water, info.Water * protons);
                    if (info.GasFormula != null) outcome.AddGas(info.GasFormula, info.Gas * protons);
                    outcome.HeatReleasedJ += -info.Enthalpy * protons;
                    if (k > 0 && max - xi > 1e-9) outcome.Ongoing = true;

                    if (info.Code != null)
                    {
                        outcome.Effects.Add(new ReactionEffect(info.Effect, Math.Min(1.0, max / 0.02), $"{info.GasFormula} is released.", info.Code, Id));
                    }
                    else if (!soluble)
                    {
                        outcome.Effects.Add(new ReactionEffect(LabEventType.Reaction, Math.Min(1.0, max / 0.01), $"{b.Name} dissolves in the acid.", "dissolve:" + b.Id, Id));
                    }

                    changed = true;
                }
            }

            return changed;
        }
    }

    /// <summary>Metal above hydrogen + non-oxidising acid → salt + H₂↑ (rate from the activity series).</summary>
    public sealed class GenericMetalAcidRule : IReactionRule, IOncePerTickRule
    {
        private readonly HashSet<(string, string)> _explicit;

        public GenericMetalAcidRule(HashSet<(string, string)> explicitPairs) => _explicit = explicitPairs;

        public string Id => "generic.metal-acid";

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            if (!m.HasWater) return false;
            var db = m.Database;
            var metals = new List<(Substance s, ActivitySeries.Entry e)>();
            var acids = new List<Substance>();
            foreach (var kv in m.Components)
            {
                if (kv.Value <= 1e-10) continue;
                var s = db.Get(kv.Key);
                if (s.ElementSymbol != null)
                {
                    var e = ActivitySeries.Get(s.ElementSymbol);
                    if (e != null && e.Potential < 0 && !e.ReactsWithWater && e.Rate > 0) metals.Add((s, e));
                }
                else if (s.IsIonic && s.CationKey == "H" && s.IsAcid && Ionic.Dissolved(m, s) && s.AnionKey != "NO3")
                {
                    acids.Add(s);
                }
            }

            bool changed = false;
            foreach (var (metal, e) in metals)
            {
                foreach (var acid in acids)
                {
                    if (_explicit.Contains((metal.Id, acid.Id)) || _explicit.Contains((acid.Id, metal.Id))) continue;
                    var salt = db.FindIonic(e.CationKey, acid.AnionKey);
                    if (salt == null) continue;
                    double nMetal = m.Moles(metal.Id), nAcid = m.Moles(acid.Id);
                    double max = Math.Min(nMetal / salt.CationCount, nAcid * acid.AnionCount / salt.AnionCount);
                    if (max <= 1e-10) continue;
                    double rate = acid.Kind == SubstanceKind.WeakAcid ? e.Rate * 0.15 : e.Rate;
                    double xi = max * (1 - Math.Exp(-rate * Math.Max(dt, 1e-4)));
                    if (max - xi < 1e-9) xi = max;
                    double electrons = salt.CationCount * salt.CationCharge * xi;
                    m.Remove(metal.Id, salt.CationCount * xi);
                    m.Remove(acid.Id, salt.AnionCount * xi / acid.AnionCount);
                    m.Add(salt.Id, xi);
                    outcome.AddGas("H₂", electrons / 2);
                    outcome.HeatReleasedJ += 80_000 * electrons / 2;
                    if (max - xi > 1e-9) outcome.Ongoing = true;
                    outcome.Effects.Add(new ReactionEffect(LabEventType.Fizz, Math.Min(1.0, max / 0.02), "Hydrogen bubbles form on the metal.", "gas.h2", Id));
                    changed = true;
                }
            }

            return changed;
        }
    }

    /// <summary>A more active metal displaces a less active metal's ion from solution (Zn + CuSO₄ → ZnSO₄ + Cu).</summary>
    public sealed class GenericDisplacementRule : IReactionRule, IOncePerTickRule
    {
        private readonly HashSet<(string, string)> _explicit;

        public GenericDisplacementRule(HashSet<(string, string)> explicitPairs) => _explicit = explicitPairs;

        public string Id => "generic.displacement";

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            if (!m.HasWater) return false;
            var db = m.Database;
            var metals = new List<(Substance s, ActivitySeries.Entry e)>();
            foreach (var kv in m.Components)
            {
                if (kv.Value <= 1e-10) continue;
                var s = db.Get(kv.Key);
                var e = ActivitySeries.Get(s.ElementSymbol);
                if (e != null && !e.ReactsWithWater && e.CationKey != null) metals.Add((s, e));
            }

            if (metals.Count == 0) return false;
            bool changed = false;
            foreach (var salt in Ionic.DissolvedIonic(m))
            {
                var target = ActivitySeries.ForCation(salt.CationKey);
                if (target == null) continue;
                foreach (var (metal, e) in metals)
                {
                    if (e.Potential >= target.Potential || e.Symbol == target.Symbol) continue;
                    if (_explicit.Contains((metal.Id, salt.Id)) || _explicit.Contains((salt.Id, metal.Id))) continue;
                    var product = db.FindIonic(e.CationKey, salt.AnionKey);
                    var deposit = db.FindElement(target.Symbol);
                    if (product == null || deposit == null) continue;

                    double nMetal = m.Moles(metal.Id), nSalt = m.Moles(salt.Id);
                    double max = Math.Min(nMetal / product.CationCount, nSalt * salt.AnionCount / product.AnionCount);
                    if (max <= 1e-10) continue;
                    double rate = Math.Max(0.004, Math.Min(0.2, (e.Rate > 0 ? e.Rate : 0.02) * 0.8));
                    double xi = max * (1 - Math.Exp(-rate * Math.Max(dt, 1e-4)));
                    if (max - xi < 1e-9) xi = max;
                    double saltUsed = product.AnionCount * xi / salt.AnionCount;
                    m.Remove(metal.Id, product.CationCount * xi);
                    m.Remove(salt.Id, saltUsed);
                    m.Add(product.Id, xi);
                    m.Add(deposit.Id, salt.CationCount * saltUsed);
                    outcome.HeatReleasedJ += 60_000 * xi;
                    if (max - xi > 1e-9) outcome.Ongoing = true;
                    outcome.Effects.Add(new ReactionEffect(LabEventType.Reaction, Math.Min(1.0, max / 0.005),
                        $"{deposit.Name} metal deposits on the {metal.Name}.", "displace:" + target.Symbol + ":" + e.Symbol, Id));
                    changed = true;
                }
            }

            return changed;
        }
    }

    /// <summary>Alkali and alkaline-earth metals + water → hydroxide + H₂ (K, Rb, Cs explode; Li, Ca fizz).</summary>
    public sealed class GenericMetalWaterRule : IReactionRule, IOncePerTickRule
    {
        public string Id => "generic.metal-water";

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            double water = m.Moles(SubstanceIds.Water);
            if (water <= 1e-9) return false;
            var db = m.Database;
            bool changed = false;
            foreach (var kv in new List<KeyValuePair<string, double>>(m.Components))
            {
                if (kv.Value <= 1e-10) continue;
                var s = db.Get(kv.Key);
                var e = ActivitySeries.Get(s.ElementSymbol);
                if (e == null || !e.ReactsWithWater || s.Id == SubstanceIds.Sodium) continue;
                var hydroxide = db.FindIonic(e.CationKey, "OH");
                if (hydroxide == null) continue;
                int charge = hydroxide.CationCharge;
                double max = Math.Min(kv.Value, m.Moles(SubstanceIds.Water) / charge);
                if (max <= 1e-10) continue;
                double xi = max * (1 - Math.Exp(-e.Rate * Math.Max(dt, 1e-4)));
                if (max - xi < 1e-9) xi = max;
                m.Remove(s.Id, xi);
                m.Remove(SubstanceIds.Water, charge * xi);
                m.Add(hydroxide.Id, xi / hydroxide.CationCount);
                outcome.AddGas("H₂", charge * xi / 2);
                outcome.HeatReleasedJ += 180_000 * xi;
                if (max - xi > 1e-9) outcome.Ongoing = true;
                bool violent = e.Rate >= 3 && max >= 0.004;
                outcome.Effects.Add(violent
                    ? new ReactionEffect(LabEventType.Explosion, Math.Min(1.0, 0.45 + max / 0.05), $"{s.Name} reacts explosively with water!", "metal.water.boom:" + e.Symbol, Id + ":boom:" + e.Symbol)
                    : new ReactionEffect(LabEventType.Fizz, Math.Min(1.0, max / 0.02), $"{s.Name} fizzes in water, releasing hydrogen.", "metal.water:" + e.Symbol, Id));
                changed = true;
            }

            return changed;
        }
    }

    /// <summary>Basic oxides of alkali / alkaline-earth metals + water → hydroxide (quicklime slaking: CaO + H₂O → Ca(OH)₂).</summary>
    public sealed class GenericOxideWaterRule : IReactionRule, IOncePerTickRule
    {
        public string Id => "generic.oxide-water";

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            if (m.Moles(SubstanceIds.Water) <= 1e-9) return false;
            var db = m.Database;
            bool changed = false;
            foreach (var kv in new List<KeyValuePair<string, double>>(m.Components))
            {
                if (kv.Value <= 1e-10) continue;
                var s = db.Get(kv.Key);
                if (!s.IsIonic || s.AnionKey != "O") continue;
                var hydroxide = db.FindIonic(s.CationKey, "OH");
                if (hydroxide == null || hydroxide.Kind != SubstanceKind.StrongBase) continue;
                double max = Math.Min(kv.Value, m.Moles(SubstanceIds.Water) / Math.Max(1, s.AnionCount));
                double xi = max * (1 - Math.Exp(-0.5 * Math.Max(dt, 1e-4)));
                if (max - xi < 1e-9) xi = max;
                if (xi <= 1e-12) continue;
                m.Remove(s.Id, xi);
                m.Remove(SubstanceIds.Water, s.AnionCount * xi);
                m.Add(hydroxide.Id, s.CationCount * xi / hydroxide.CationCount);
                outcome.HeatReleasedJ += 65_000 * xi * s.AnionCount;
                if (max - xi > 1e-9) outcome.Ongoing = true;
                outcome.Effects.Add(new ReactionEffect(LabEventType.Reaction, Math.Min(1.0, max / 0.02), $"{s.Name} reacts with water and heats up.", "oxide.water:" + s.Id, Id));
                changed = true;
            }

            return changed;
        }
    }

    /// <summary>
    /// A metal heated with sulfur, iodine or bromine combines with it directly (Fe + S → FeS glows red;
    /// Zn + S → ZnS; mercury and sulfur react even at room temperature, which is how spilt mercury is cleaned up).
    /// </summary>
    public sealed class GenericCombinationRule : IReactionRule, IOncePerTickRule
    {
        public string Id => "generic.combine";

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            var db = m.Database;
            Substance nonMetal = null;
            var metals = new List<(Substance s, ActivitySeries.Entry e)>();
            foreach (var kv in m.Components)
            {
                if (kv.Value <= 1e-10) continue;
                var s = db.Get(kv.Key);
                if (s.ElementSymbol == null) continue;
                if (Synthesis.CombineWith.ContainsKey(s.ElementSymbol)) nonMetal = s;
                else
                {
                    var e = ActivitySeries.Get(s.ElementSymbol);
                    if (e != null) metals.Add((s, e));
                }
            }

            if (nonMetal == null || metals.Count == 0) return false;
            bool changed = false;
            foreach (var (metal, e) in metals)
            {
                double needC = metal.ElementSymbol == "Hg" && nonMetal.ElementSymbol == "S" ? 0 : Synthesis.CombineWith[nonMetal.ElementSymbol];
                if (m.TemperatureC < needC) continue;
                var product = db.FindIonic(e.CationKey, nonMetal.ElementSymbol);
                if (product == null) continue;
                int atomsX = Combustion.AtomsOf(nonMetal.Formula, nonMetal.ElementSymbol);
                double perProductX = (double)product.AnionCount / Math.Max(1, atomsX);
                double max = Math.Min(m.Moles(metal.Id) / product.CationCount, m.Moles(nonMetal.Id) / perProductX);
                if (max <= 1e-10) continue;
                double xi = max * (1 - Math.Exp(-0.4 * Math.Max(dt, 1e-4)));
                if (max - xi < 1e-9) xi = max;
                m.Remove(metal.Id, product.CationCount * xi);
                m.Remove(nonMetal.Id, perProductX * xi);
                m.Add(product.Id, xi);
                outcome.HeatReleasedJ += 100_000 * xi;
                if (max - xi > 1e-9) outcome.Ongoing = true;
                outcome.Effects.Add(new ReactionEffect(LabEventType.Reaction, Math.Min(1.0, max / 0.02), $"{product.Name} forms.", "make:" + product.Id, Id));
                changed = true;
            }

            return changed;
        }
    }

    /// <summary>
    /// Thermite: aluminium powder and rust heated strongly react with a blinding flash and molten iron
    /// (2Al + Fe₂O₃ → Al₂O₃ + 2Fe, ΔH ≈ −850 kJ).
    /// </summary>
    public sealed class ThermiteRule : IReactionRule, IOncePerTickRule
    {
        public const double IgnitionC = 450;

        public string Id => "generic.thermite";

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            if (m.TemperatureC < IgnitionC) return false;
            var db = m.Database;
            var al = db.FindElement("Al");
            var fe2o3 = db.FindIonic("Fe3", "O");
            var fe = db.FindElement("Fe");
            var al2o3 = db.FindIonic("Al", "O");
            if (al == null || fe2o3 == null || fe == null || al2o3 == null) return false;
            double max = Math.Min(m.Moles(al.Id) / 2, m.Moles(fe2o3.Id));
            if (max <= 1e-8) return false;
            double xi = max * (1 - Math.Exp(-3.0 * Math.Max(dt, 1e-4)));
            if (max - xi < 1e-8) xi = max;
            m.Remove(al.Id, 2 * xi);
            m.Remove(fe2o3.Id, xi);
            m.Add(al2o3.Id, xi);
            m.Add(fe.Id, 2 * xi);
            outcome.HeatReleasedJ += 850_000 * xi * 0.1; // most of the heat leaves as light and sparks
            if (max - xi > 1e-8) outcome.Ongoing = true;
            outcome.Effects.Add(new ReactionEffect(LabEventType.Explosion, Math.Min(1.0, 0.55 + max / 0.02),
                "Thermite ignites: molten iron and a blinding flash!", "boom.thermite", Id));
            return true;
        }
    }

    /// <summary>
    /// Yeast contains catalase, an enzyme that splits hydrogen peroxide: 2H₂O₂ → 2H₂O + O₂↑. Like every enzyme it is
    /// fastest near body temperature, slow in the cold, and stops working above ~60–75 °C (the protein is denatured).
    /// With dish detergent in the mixture the oxygen is trapped as a column of foam: "elephant toothpaste".
    /// </summary>
    public sealed class YeastCatalaseRule : IReactionRule, IOncePerTickRule
    {
        public const double DenatureC = 62;

        public string Id => "enzyme.catalase";

        /// <summary>Relative enzyme activity at a temperature (1 at 37 °C, 0 once denatured).</summary>
        public static double Activity(double temperatureC)
        {
            if (temperatureC >= DenatureC) return Math.Max(0, 0.4 - (temperatureC - DenatureC) * 0.035); // falls to 0 by ~74 °C
            double x = (temperatureC - 37) / 20.0;
            return Math.Max(0.12, Math.Exp(-x * x));
        }

        public bool TryApply(Mixture m, ReactionOutcome outcome, double dt)
        {
            double yeast = m.Moles(SubstanceIds.DryYeast), peroxide = m.Moles(SubstanceIds.HydrogenPeroxide);
            if (yeast <= 1e-6 || peroxide <= 1e-7 || !m.HasWater) return false;
            double grams = yeast * m.Database.Get(SubstanceIds.DryYeast).MolarMass;
            double k = 2.2 * Math.Min(1.0, grams / 1.5) * Activity(m.TemperatureC);
            if (k <= 0) return false;
            double xi = peroxide * (1 - Math.Exp(-k * Math.Max(dt, 1e-4)));
            if (peroxide - xi < 1e-7) xi = peroxide;
            m.Remove(SubstanceIds.HydrogenPeroxide, xi);
            m.Add(SubstanceIds.Water, xi);
            outcome.AddGas("O₂", xi / 2);
            outcome.HeatReleasedJ += 98_000 * xi * 0.55; // the rest leaves with the hot foam and steam
            if (peroxide - xi > 1e-7) outcome.Ongoing = true;
            bool soap = m.Moles(SubstanceIds.Detergent) > 1e-5;
            outcome.Effects.Add(new ReactionEffect(LabEventType.Foam, Math.Min(1.0, peroxide / 0.05),
                soap ? "Elephant toothpaste: a column of foam erupts!" : "Yeast splits the hydrogen peroxide; oxygen bubbles out.",
                soap ? "foam.elephant" : "foam.o2.yeast", Id));
            return true;
        }
    }
}
