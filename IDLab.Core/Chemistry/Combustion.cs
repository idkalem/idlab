using System;
using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    /// <summary>
    /// What heat does to flammable and explosive substances: flash points of liquids, ignition temperatures of
    /// solids, auto-ignition on a hot surface, and the product of burning an element in air (Mg → MgO, S → SO₂ ...).
    /// The numbers are textbook approximations good enough for a school simulation.
    /// </summary>
    public static class Combustion
    {
        /// <summary>Elements that burn in air when heated in a flame, with an ignition temperature (°C).</summary>
        private static readonly Dictionary<string, double> ElementIgnitionC = new Dictionary<string, double>
        {
            ["Cs"] = 30, ["Rb"] = 40, ["K"] = 80, ["Na"] = 120, ["Li"] = 180, ["P"] = 60, ["S"] = 232, ["Ba"] = 300,
            ["Sr"] = 330, ["Ca"] = 390, ["C"] = 420, ["Fe"] = 440, ["Zn"] = 460, ["Mg"] = 470, ["Al"] = 480, ["Ti"] = 480,
            ["Mn"] = 450, ["Ce"] = 160, ["La"] = 440, ["U"] = 300, ["Th"] = 400, ["Zr"] = 380, ["Hf"] = 450,
        };

        /// <summary>Metals that only tarnish to their oxide (no flame) when heated in air.</summary>
        private static readonly Dictionary<string, double> ElementTarnishC = new Dictionary<string, double>
        {
            ["Cu"] = 300, ["Pb"] = 330, ["Sn"] = 350, ["Ni"] = 400, ["Co"] = 400, ["Cr"] = 450, ["Bi"] = 300, ["Cd"] = 320,
        };

        /// <summary>Product of burning an element in air: formula-lookup preference per element.</summary>
        private static readonly Dictionary<string, string[]> OxidePreference = new Dictionary<string, string[]>
        {
            ["Na"] = new[] { "Na₂O₂", "Na₂O" }, ["K"] = new[] { "KO₂", "K₂O" }, ["Rb"] = new[] { "RbO₂", "Rb₂O" }, ["Cs"] = new[] { "CsO₂", "Cs₂O" },
            ["Fe"] = new[] { "Fe₃O₄", "Fe₂O₃" }, ["P"] = new[] { "P₄O₁₀", "P₂O₅" }, ["S"] = new[] { "SO₂" }, ["C"] = new[] { "CO₂" },
            ["Pb"] = new[] { "PbO" }, ["Cu"] = new[] { "CuO" }, ["Sn"] = new[] { "SnO₂" }, ["Mn"] = new[] { "Mn₃O₄", "MnO₂" },
            ["Co"] = new[] { "Co₃O₄", "CoO" }, ["U"] = new[] { "U₃O₈", "UO₂" },
        };

        /// <summary>Approximate flash point of a flammable liquid (°C), from its boiling point.</summary>
        public static double FlashPointC(Substance s)
        {
            double bp = double.IsNaN(s.BoilingPointC) ? 120 : s.BoilingPointC;
            return bp - 70;
        }

        /// <summary>Approximate auto-ignition temperature (°C): the hot-surface temperature that lights the vapour.</summary>
        public static double AutoIgnitionC(Substance s)
        {
            string n = (s.Name ?? "").ToLowerInvariant();
            if (s.Formula == "CS₂") return 100;
            if (n.Contains("ether")) return 180;
            if (n.Contains("hexane") || n.Contains("heptane") || n.Contains("octane") || n.Contains("decane")) return 230;
            return 365;
        }

        /// <summary>Ignition temperature (°C) of a flammable solid, or NaN if it does not burn in a lab flame.</summary>
        public static double SolidIgnitionC(Substance s)
        {
            if (s.ElementSymbol != null)
            {
                return ElementIgnitionC.TryGetValue(s.ElementSymbol, out double t) ? t : double.NaN;
            }

            return s.IsFlammable ? 260 : double.NaN;
        }

        /// <summary>Temperature (°C) above which a heated metal slowly turns into its oxide without a flame.</summary>
        public static double TarnishC(Substance s) =>
            s.ElementSymbol != null && ElementTarnishC.TryGetValue(s.ElementSymbol, out double t) ? t : double.NaN;

        /// <summary>Temperature (°C) at which an explosive substance detonates / decomposes violently.</summary>
        public static double ExplosionC(Substance s)
        {
            string f = s.Formula ?? "";
            if (f == "NH₄NO₃" || f == "NH₄ClO₄") return 260; // ammonium nitrate / perchlorate decompose violently
            if ((s.Hazards & Hazard.Explosive) == 0) return double.NaN;
            if (f.Contains("CNO")) return 170;   // fulminates
            if (f.Contains("N₃")) return 300;    // azides
            return 240;                           // TNT and other organic explosives
        }

        /// <summary>
        /// Oxide formed when <paramref name="element"/> burns in air and how many moles of it form per mole of the
        /// element. Null when the element has no oxide in the catalogue.
        /// </summary>
        public static (Substance oxide, double perMole) OxideOf(SubstanceDatabase db, Substance element)
        {
            string sym = element.ElementSymbol;
            if (sym == null) return (null, 0);
            Substance found = null;
            if (OxidePreference.TryGetValue(sym, out var formulas))
            {
                foreach (var f in formulas)
                {
                    found = db.FindByFormula(f);
                    if (found != null) break;
                }
            }

            if (found == null)
            {
                // Lowest oxidation-state oxide of the element (MgO, ZnO, Al₂O₃ ...).
                foreach (var s in db.All)
                {
                    if (!s.IsIonic || s.AnionKey != "O" || s.CationKey == null || s.Hidden) continue;
                    string key = s.CationKey;
                    int i = 0;
                    while (i < key.Length && char.IsLetter(key[i])) i++;
                    if (key.Substring(0, i) != sym) continue;
                    if (found == null || s.CationCharge < found.CationCharge) found = s;
                }
            }

            if (found == null) return (null, 0);
            int atoms = AtomsOf(found.Formula, sym);
            return atoms > 0 ? (found, 1.0 / atoms) : (null, 0);
        }

        /// <summary>Number of atoms of <paramref name="symbol"/> in a (pretty, subscripted) formula such as Fe₃O₄.</summary>
        public static int AtomsOf(string formula, string symbol)
        {
            int total = 0;
            for (int i = 0; i < formula.Length; i++)
            {
                if (string.CompareOrdinal(formula, i, symbol, 0, symbol.Length) != 0) continue;
                int j = i + symbol.Length;
                if (j < formula.Length && char.IsLower(formula[j])) continue; // "C" must not match "Cl"
                int n = 0;
                while (j < formula.Length && Subscript(formula[j]) >= 0)
                {
                    n = n * 10 + Subscript(formula[j]);
                    j++;
                }

                total += n == 0 ? 1 : n;
            }

            return total;
        }

        private static int Subscript(char c) => c >= '₀' && c <= '₉' ? c - '₀' : c >= '0' && c <= '9' ? c - '0' : -1;
    }
}
