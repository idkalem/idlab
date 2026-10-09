using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IDLab.Core.Chemistry
{
    /// <summary>How a compound can be made in the lab (each kind is backed by a reaction rule of the simulation).</summary>
    public enum SynthesisKind
    {
        Burn,            // element burnt in air: 2Mg + O₂ → 2MgO
        Combine,         // elements heated together: Fe + S → FeS
        MetalAcid,       // Zn + 2HCl → ZnCl₂ + H₂
        Neutralise,      // NaOH + HCl → NaCl + H₂O
        OxideAcid,       // CuO + H₂SO₄ → CuSO₄ + H₂O
        CarbonateAcid,   // CaCO₃ + 2HCl → CaCl₂ + CO₂ + H₂O
        Precipitate,     // AgNO₃ + NaCl → AgCl↓ + NaNO₃
        Displace,        // Zn + CuSO₄ → ZnSO₄ + Cu
        MetalWater,      // 2Na + 2H₂O → 2NaOH + H₂
        OxideWater,      // CaO + H₂O → Ca(OH)₂
    }

    /// <summary>One way to make <see cref="Product"/>: reactants to put on the bench and the balanced equation.</summary>
    public sealed class SynthesisRoute
    {
        public SynthesisKind Kind { get; set; }
        public Substance Product { get; set; }

        /// <summary>Reactants to take from the shelves (water and air are not listed).</summary>
        public List<Substance> Reagents { get; } = new List<Substance>();

        /// <summary>Needs water (a beaker of water) besides the reagents.</summary>
        public bool NeedsWater { get; set; }

        /// <summary>Needs heating (Bunsen burner).</summary>
        public bool Heat { get; set; }

        /// <summary>Balanced equation, e.g. "Zn + 2HCl → ZnCl₂ + H₂".</summary>
        public string Equation { get; set; }
    }

    /// <summary>
    /// Finds lab routes to a compound from the reaction rules the simulation actually runs (so following a route on
    /// the bench really makes the product), and balances their equations.
    /// </summary>
    public static class Synthesis
    {
        /// <summary>Dissolved salts used as the "other half" of a precipitation, in order of preference.</summary>
        private static readonly string[] PartnerCations = { "Na", "K", "NH4" };
        private static readonly string[] PartnerAnions = { "NO3", "Cl", "SO4", "CH3COO" };

        /// <summary>Non-metals that combine directly with a heated metal, and the temperature (°C) needed.</summary>
        public static readonly Dictionary<string, double> CombineWith = new Dictionary<string, double>
        {
            ["S"] = 150, ["I"] = 120, ["Br"] = 25, ["Se"] = 250,
        };

        public static List<SynthesisRoute> RoutesTo(SubstanceDatabase db, Substance target)
        {
            var routes = new List<SynthesisRoute>();
            if (target == null || !target.IsIonic || target.Hidden) return routes;
            string cat = target.CationKey, an = target.AnionKey;
            var metal = MetalFor(db, cat);
            var acid = an == "OH" || an == "O" ? null : db.FindIonic("H", an);
            bool soluble = Dissolves(target);

            // Element burnt in air.
            if (an == "O" && metal != null)
            {
                var (oxide, _) = Combustion.OxideOf(db, metal);
                if (oxide == target && (!double.IsNaN(Combustion.SolidIgnitionC(metal)) || !double.IsNaN(Combustion.TarnishC(metal))))
                {
                    routes.Add(Route(SynthesisKind.Burn, target, heat: true, reagents: new[] { metal },
                        reactants: new[] { metal.Formula, "O₂" }, products: new[] { target.Formula }));
                }
            }

            // Metal heated with sulfur / iodine / bromine.
            if (metal != null && CombineWith.ContainsKey(an))
            {
                var nonMetal = db.FindElement(an);
                var e = ActivitySeries.Get(metal.ElementSymbol);
                if (nonMetal != null && e != null && e.CationKey == cat)
                {
                    routes.Add(Route(SynthesisKind.Combine, target, heat: an != "Br", reagents: new[] { metal, nonMetal },
                        reactants: new[] { metal.Formula, nonMetal.Formula }, products: new[] { target.Formula }));
                }
            }

            // Active metal + acid → salt + hydrogen (the simulation uses non-oxidising acids).
            if (metal != null && acid != null && an != "NO3")
            {
                var e = ActivitySeries.Get(metal.ElementSymbol);
                if (e != null && e.Potential < 0 && e.Rate > 0 && !e.ReactsWithWater && e.CationKey == cat && soluble)
                {
                    routes.Add(Route(SynthesisKind.MetalAcid, target, heat: false, reagents: new[] { metal, acid },
                        reactants: new[] { metal.Formula, acid.Formula }, products: new[] { target.Formula, "H₂" }));
                }
            }

            if (acid != null)
            {
                var hydroxide = db.FindIonic(cat, "OH");
                if (hydroxide != null && hydroxide != target)
                {
                    routes.Add(Route(SynthesisKind.Neutralise, target, heat: false, reagents: new[] { hydroxide, acid },
                        reactants: new[] { hydroxide.Formula, acid.Formula }, products: new[] { target.Formula, "H₂O" }));
                }

                var oxide = db.FindIonic(cat, "O");
                if (oxide != null && oxide != target && soluble)
                {
                    routes.Add(Route(SynthesisKind.OxideAcid, target, heat: false, reagents: new[] { oxide, acid },
                        reactants: new[] { oxide.Formula, acid.Formula }, products: new[] { target.Formula, "H₂O" }));
                }

                var carbonate = db.FindIonic(cat, "CO3");
                if (carbonate != null && carbonate != target && soluble && an != "CO3")
                {
                    routes.Add(Route(SynthesisKind.CarbonateAcid, target, heat: false, reagents: new[] { carbonate, acid },
                        reactants: new[] { carbonate.Formula, acid.Formula }, products: new[] { target.Formula, "CO₂", "H₂O" }));
                }
            }

            // An insoluble compound precipitates from two solutions.
            if (!soluble && target.Phase == SubstancePhase.Solid)
            {
                var cationSalt = PartnerAnions.Select(a => db.FindIonic(cat, a)).FirstOrDefault(s => s != null && Dissolves(s) && s != target);
                var anionSalt = PartnerCations.Select(c => db.FindIonic(c, an)).FirstOrDefault(s => s != null && Dissolves(s) && s != target);
                if (cationSalt != null && anionSalt != null)
                {
                    var byproduct = db.FindIonic(anionSalt.CationKey, cationSalt.AnionKey);
                    if (byproduct != null)
                    {
                        routes.Add(Route(SynthesisKind.Precipitate, target, heat: false, reagents: new[] { cationSalt, anionSalt },
                            reactants: new[] { cationSalt.Formula, anionSalt.Formula }, products: new[] { target.Formula, byproduct.Formula }));
                    }
                }
            }

            // A more active metal displaces copper from its salt: Zn + CuSO₄ → ZnSO₄ + Cu.
            if (metal != null && soluble && an != "OH" && an != "O")
            {
                var e = ActivitySeries.Get(metal.ElementSymbol);
                var copperSalt = db.FindIonic("Cu2", an);
                var copper = db.FindElement("Cu");
                if (e != null && e.CationKey == cat && !e.ReactsWithWater && e.Potential < 0.2 && copperSalt != null && Dissolves(copperSalt) && copper != null && metal != copper)
                {
                    routes.Add(Route(SynthesisKind.Displace, target, heat: false, reagents: new[] { metal, copperSalt },
                        reactants: new[] { metal.Formula, copperSalt.Formula }, products: new[] { target.Formula, copper.Formula }));
                }
            }

            if (an == "OH" && metal != null)
            {
                var e = ActivitySeries.Get(metal.ElementSymbol);
                if (e != null && e.ReactsWithWater && e.CationKey == cat)
                {
                    routes.Add(Route(SynthesisKind.MetalWater, target, heat: false, reagents: new[] { metal }, water: true,
                        reactants: new[] { metal.Formula, "H₂O" }, products: new[] { target.Formula, "H₂" }));
                }

                var oxide = db.FindIonic(cat, "O");
                if (oxide != null && target.Kind == SubstanceKind.StrongBase)
                {
                    routes.Add(Route(SynthesisKind.OxideWater, target, heat: false, reagents: new[] { oxide }, water: true,
                        reactants: new[] { oxide.Formula, "H₂O" }, products: new[] { target.Formula }));
                }
            }

            routes.RemoveAll(r => r.Equation == null);
            return routes;
        }

        /// <summary>Compounds that can be made from an element, each with the route that uses it.</summary>
        public static List<SynthesisRoute> FromElement(SubstanceDatabase db, Substance element)
        {
            var result = new List<SynthesisRoute>();
            string sym = element?.ElementSymbol;
            if (sym == null) return result;
            var candidates = new List<Substance>();
            var (oxide, _) = Combustion.OxideOf(db, element);
            if (oxide != null) candidates.Add(oxide);
            var e = ActivitySeries.Get(sym);
            if (e != null)
            {
                foreach (var an in new[] { "Cl", "SO4", "CH3COO", "S", "I", "Br", "OH", "NO3" })
                {
                    var s = db.FindIonic(e.CationKey, an);
                    if (s != null) candidates.Add(s);
                }
            }

            if (CombineWith.ContainsKey(sym))
            {
                // A non-metal: its compounds with the common metals.
                foreach (var m in new[] { "Fe", "Zn", "Cu", "Mg", "Al", "Ag", "Hg" })
                {
                    var me = ActivitySeries.Get(m);
                    var s = me != null ? db.FindIonic(me.CationKey, sym) : null;
                    if (s != null) candidates.Add(s);
                }
            }

            foreach (var c in candidates.Distinct())
            {
                var route = RoutesTo(db, c).FirstOrDefault(r => r.Reagents.Contains(element));
                if (route != null) result.Add(route);
            }

            return result;
        }

        private static bool Dissolves(Substance s) => s.Soluble || s.Phase == SubstancePhase.Aqueous;

        private static Substance MetalFor(SubstanceDatabase db, string cationKey)
        {
            if (cationKey == null) return null;
            int i = 0;
            while (i < cationKey.Length && char.IsLetter(cationKey[i])) i++;
            string sym = cationKey.Substring(0, i);
            if (sym == "NH" || sym == "H") return null;
            var el = db.FindElement(sym);
            return el != null && el.Phase == SubstancePhase.Solid || el?.ElementSymbol == "Hg" ? el : null;
        }

        private static SynthesisRoute Route(SynthesisKind kind, Substance product, bool heat, Substance[] reagents, string[] reactants, string[] products,
            bool water = false)
        {
            var r = new SynthesisRoute { Kind = kind, Product = product, Heat = heat, NeedsWater = water, Equation = Balance(reactants, products) };
            r.Reagents.AddRange(reagents.Where(x => x != null));
            return r;
        }

        // ------------------------------------------------------------------ formulas and balancing

        /// <summary>Atoms in a formula such as "CuSO₄·5H₂O", "Ca(OH)₂" or "(NH₄)₂SO₄".</summary>
        public static Dictionary<string, int> Atoms(string formula)
        {
            var total = new Dictionary<string, int>();
            foreach (var part in formula.Replace('.', '·').Split('·'))
            {
                int pos = 0, coef = ReadNumber(part, ref pos);
                var counts = ParseGroup(part, ref pos);
                foreach (var kv in counts) Add(total, kv.Key, kv.Value * Math.Max(1, coef));
            }

            return total;
        }

        private static Dictionary<string, int> ParseGroup(string f, ref int pos)
        {
            var counts = new Dictionary<string, int>();
            while (pos < f.Length)
            {
                char c = f[pos];
                if (c == '(' || c == '[')
                {
                    pos++;
                    var inner = ParseGroup(f, ref pos);
                    int n = Math.Max(1, ReadNumber(f, ref pos));
                    foreach (var kv in inner) Add(counts, kv.Key, kv.Value * n);
                }
                else if (c == ')' || c == ']')
                {
                    pos++;
                    return counts;
                }
                else if (char.IsUpper(c))
                {
                    int start = pos++;
                    while (pos < f.Length && char.IsLower(f[pos])) pos++;
                    string el = f.Substring(start, pos - start);
                    Add(counts, el, Math.Max(1, ReadNumber(f, ref pos)));
                }
                else
                {
                    pos++; // charges, arrows, spaces
                }
            }

            return counts;
        }

        private static int ReadNumber(string f, ref int pos)
        {
            int n = 0;
            bool any = false;
            while (pos < f.Length)
            {
                char c = f[pos];
                int d = c >= '0' && c <= '9' ? c - '0' : c >= '₀' && c <= '₉' ? c - '₀' : -1;
                if (d < 0) break;
                n = n * 10 + d;
                any = true;
                pos++;
            }

            return any ? n : 0;
        }

        private static void Add(Dictionary<string, int> d, string k, int v)
        {
            d.TryGetValue(k, out int n);
            d[k] = n + v;
        }

        /// <summary>
        /// Smallest whole-number coefficients (up to 12) that balance the equation, written as
        /// "2Na + 2H₂O → 2NaOH + H₂"; null when it cannot be balanced.
        /// </summary>
        public static string Balance(string[] reactants, string[] products)
        {
            var species = reactants.Concat(products).Select(Atoms).ToArray();
            var elements = species.SelectMany(s => s.Keys).Distinct().ToArray();
            int n = species.Length, nr = reactants.Length;
            var coef = new int[n];
            int[] best = null;
            int bestSum = int.MaxValue;

            void Search(int i, int sum)
            {
                if (sum >= bestSum) return;
                if (i == n)
                {
                    foreach (var el in elements)
                    {
                        int left = 0, right = 0;
                        for (int k = 0; k < n; k++)
                        {
                            species[k].TryGetValue(el, out int a);
                            if (k < nr) left += a * coef[k];
                            else right += a * coef[k];
                        }

                        if (left != right) return;
                    }

                    best = (int[])coef.Clone();
                    bestSum = sum;
                    return;
                }

                for (int c = 1; c <= 12; c++)
                {
                    coef[i] = c;
                    Search(i + 1, sum + c);
                }
            }

            Search(0, 0);
            if (best == null) return null;
            string Side(int from, int to, string[] names)
            {
                var sb = new StringBuilder();
                for (int k = from; k < to; k++)
                {
                    if (sb.Length > 0) sb.Append(" + ");
                    if (best[k] > 1) sb.Append(best[k]);
                    sb.Append(names[k - from]);
                }

                return sb.ToString();
            }

            return Side(0, nr, reactants) + " → " + Side(nr, n, products);
        }
    }
}
