using System;

namespace IDLab.Core.Chemistry
{
    /// <summary>
    /// pH of an aqueous mixture. Handles excess strong acid/base (with water autoionisation),
    /// weak acids (exact quadratic), weak-acid buffers (Henderson–Hasselbalch) and conjugate-base
    /// hydrolysis (e.g. sodium acetate at the equivalence point). Assumes neutralisation has already
    /// been applied by the <see cref="ReactionEngine"/>.
    /// </summary>
    public static class AcidBase
    {
        private const double Kw = PhysicalConstants.WaterIonProduct;
        private const double Tiny = 1e-12;

        public static double ComputePH(Mixture mixture)
        {
            double v = mixture.VolumeLiters;
            if (v < 1e-7 || !mixture.Contains(SubstanceIds.Water))
            {
                return double.NaN; // no aqueous phase — pH undefined
            }

            double strongH = 0, strongOH = 0;
            double weakAcidMoles = 0, conjugateMoles = 0, weakKa = 0;

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
                        weakAcidMoles += kv.Value * s.Protons;
                        weakKa = s.Ka;
                        if (s.ConjugateBaseId != null)
                        {
                            conjugateMoles += mixture.Moles(s.ConjugateBaseId);
                        }

                        break;
                }
            }

            // A conjugate base can exist with no acid left (e.g. after full neutralisation).
            if (weakKa == 0)
            {
                foreach (var s in mixture.Database.All)
                {
                    if (s.Kind == SubstanceKind.WeakAcid && s.ConjugateBaseId != null && mixture.Contains(s.ConjugateBaseId))
                    {
                        conjugateMoles += mixture.Moles(s.ConjugateBaseId);
                        weakKa = s.Ka;
                    }
                }
            }

            double net = (strongH - strongOH) / v;
            double ca = weakAcidMoles / v;
            double cb = conjugateMoles / v;

            if (net > Tiny)
            {
                // Strong acid in excess, weak acid dissociation suppressed (common-ion effect).
                double x = ca > 0 ? Quadratic(1, net + weakKa, -weakKa * ca) : 0;
                return -Math.Log10(WithWater(net + x));
            }

            if (net < -Tiny)
            {
                return 14.0 + Math.Log10(WithWater(-net));
            }

            if (ca > Tiny && cb > Tiny)
            {
                return -Math.Log10(weakKa) + Math.Log10(cb / ca); // buffer
            }

            if (ca > Tiny)
            {
                double x = Quadratic(1, weakKa, -weakKa * ca);
                return -Math.Log10(WithWater(x));
            }

            if (cb > Tiny)
            {
                double kb = Kw / weakKa;
                double oh = Quadratic(1, kb, -kb * cb);
                return 14.0 + Math.Log10(WithWater(oh));
            }

            return 7.0;
        }

        /// <summary>Adds water autoionisation to a strong-ion concentration c: [X] = (c + √(c² + 4Kw)) / 2.</summary>
        private static double WithWater(double c) => (c + Math.Sqrt(c * c + 4 * Kw)) / 2.0;

        /// <summary>Positive root of a·x² + b·x + c = 0.</summary>
        private static double Quadratic(double a, double b, double c) => (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a);

        /// <summary>Fraction of a weak acid dissociated in the given mixture (for the micro view).</summary>
        public static double WeakAcidDissociation(Substance acid, Mixture mixture)
        {
            double v = mixture.VolumeLiters;
            double c = v > 0 ? mixture.Moles(acid.Id) / v : 0;
            if (c <= Tiny || acid.Ka <= 0)
            {
                return 0;
            }

            double ph = ComputePH(mixture);
            if (double.IsNaN(ph))
            {
                return 0;
            }

            double h = Math.Pow(10, -ph);
            return acid.Ka / (acid.Ka + h); // α = Ka / (Ka + [H⁺])
        }
    }
}
