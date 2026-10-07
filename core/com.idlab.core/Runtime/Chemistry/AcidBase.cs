using System;
using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    /// <summary>
    /// pH of an aqueous mixture. Handles excess strong acid/base (with water autoionisation), weak acids and
    /// weak bases (exact quadratic), buffers (Henderson–Hasselbalch) and hydrolysis of conjugate ions
    /// (acetate is basic, ammonium is acidic). Assumes neutralisation has already been applied by the
    /// <see cref="ReactionEngine"/>.
    /// </summary>
    public static class AcidBase
    {
        private const double Kw = PhysicalConstants.WaterIonProduct;
        private const double Tiny = 1e-12;

        private sealed class Pair
        {
            public double K;      // Ka (acid pairs) or Kb (base pairs)
            public double Weak;   // mol of the weak acid / weak base
            public double Conj;   // mol of its conjugate ion
        }

        public static double ComputePH(Mixture mixture)
        {
            double v = mixture.LiquidVolumeLiters;
            if (v < 1e-7 || !mixture.Contains(SubstanceIds.Water))
            {
                return double.NaN; // no aqueous phase — pH undefined
            }

            double strongH = 0, strongOH = 0;
            var acids = new Dictionary<string, Pair>();
            var bases = new Dictionary<string, Pair>();

            foreach (var kv in mixture.Components)
            {
                var s = mixture.Database.Get(kv.Key);
                switch (s.Kind)
                {
                    case SubstanceKind.StrongAcid:
                        strongH += kv.Value * s.Protons;
                        break;
                    case SubstanceKind.StrongBase:
                        strongOH += kv.Value * s.Hydroxides;
                        break;
                    case SubstanceKind.WeakAcid:
                        PairFor(acids, s.Id, s.Ka).Weak += kv.Value * s.Protons;
                        break;
                    case SubstanceKind.WeakBase:
                        PairFor(bases, s.Id, s.Kb).Weak += kv.Value * s.Hydroxides;
                        break;
                }

                if (s.ConjugateId != null && mixture.Database.TryGet(s.ConjugateId, out var parent) && s.IsDissolved(true))
                {
                    if (parent.Kind == SubstanceKind.WeakAcid)
                    {
                        PairFor(acids, parent.Id, parent.Ka).Conj += kv.Value * s.ConjugateCount;
                    }
                    else if (parent.Kind == SubstanceKind.WeakBase)
                    {
                        PairFor(bases, parent.Id, parent.Kb).Conj += kv.Value * s.ConjugateCount;
                    }
                }
            }

            double net = (strongH - strongOH) / v;
            if (net > Tiny)
            {
                // Strong acid in excess; weak acids barely dissociate (common-ion effect).
                double h = net;
                foreach (var a in acids.Values)
                {
                    double ca = a.Weak / v;
                    if (ca > Tiny) h += Quadratic(1, net + a.K, -a.K * ca);
                }

                return -Math.Log10(WithWater(h));
            }

            if (net < -Tiny)
            {
                return 14.0 + Math.Log10(WithWater(-net));
            }

            var phs = new List<double>();
            foreach (var a in acids.Values)
            {
                double ca = a.Weak / v, cb = a.Conj / v;
                if (ca > Tiny && cb > Tiny) phs.Add(-Math.Log10(a.K) + Math.Log10(cb / ca));
                else if (ca > Tiny) phs.Add(-Math.Log10(WithWater(Quadratic(1, a.K, -a.K * ca))));
                else if (cb > Tiny)
                {
                    double kb = Kw / a.K;
                    phs.Add(14.0 + Math.Log10(WithWater(Quadratic(1, kb, -kb * cb))));
                }
            }

            foreach (var b in bases.Values)
            {
                double cb = b.Weak / v, ca = b.Conj / v;
                if (cb > Tiny && ca > Tiny) phs.Add(14.0 - (-Math.Log10(b.K) + Math.Log10(ca / cb)));
                else if (cb > Tiny) phs.Add(14.0 + Math.Log10(WithWater(Quadratic(1, b.K, -b.K * cb))));
                else if (ca > Tiny)
                {
                    double ka = Kw / b.K;
                    phs.Add(-Math.Log10(WithWater(Quadratic(1, ka, -ka * ca))));
                }
            }

            if (phs.Count == 0)
            {
                return 7.0;
            }

            double sum = 0;
            foreach (var p in phs) sum += p;
            return sum / phs.Count;
        }

        private static Pair PairFor(Dictionary<string, Pair> map, string id, double k)
        {
            if (!map.TryGetValue(id, out var p))
            {
                p = new Pair { K = k };
                map[id] = p;
            }

            return p;
        }

        /// <summary>Adds water autoionisation to a strong-ion concentration c: [X] = (c + √(c² + 4Kw)) / 2.</summary>
        private static double WithWater(double c) => (c + Math.Sqrt(c * c + 4 * Kw)) / 2.0;

        /// <summary>Positive root of a·x² + b·x + c = 0.</summary>
        private static double Quadratic(double a, double b, double c) => (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a);

        /// <summary>Fraction of a weak acid dissociated (α = Ka / (Ka + [H⁺])) — for the micro view.</summary>
        public static double WeakAcidDissociation(Substance acid, Mixture mixture)
        {
            double ph = ComputePH(mixture);
            if (double.IsNaN(ph) || acid.Ka <= 0) return 0;
            double h = Math.Pow(10, -ph);
            return acid.Ka / (acid.Ka + h);
        }

        /// <summary>Fraction of a weak base protonated (α = Kb / (Kb + [OH⁻])) — for the micro view.</summary>
        public static double WeakBaseProtonation(Substance @base, Mixture mixture)
        {
            double ph = ComputePH(mixture);
            if (double.IsNaN(ph) || @base.Kb <= 0) return 0;
            double oh = Math.Pow(10, ph - 14);
            return @base.Kb / (@base.Kb + oh);
        }
    }
}
