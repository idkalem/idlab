using System;
using System.Globalization;
using System.Text;
using Raylib_cs;

namespace IDLab.Desktop
{
    internal enum ElementCategory
    {
        AlkaliMetal,
        AlkalineEarth,
        Transition,
        PostTransition,
        Metalloid,
        Nonmetal,
        Halogen,
        NobleGas,
        Lanthanide,
        Actinide,
    }

    internal enum ElementPhase
    {
        Solid,
        Liquid,
        Gas,
        Unknown,
    }

    /// <summary>One element of the periodic table (data generated from the mendeleev database).</summary>
    internal sealed class Element
    {
        public readonly int Z;
        public readonly string Symbol, Name, Note, Block, Configuration;
        public readonly double Mass, Electronegativity, MeltingK, BoilingK, Density;
        public readonly ElementCategory Category;
        public readonly int Column, Row, Group, Period, DiscoveryYear, MassNumber;
        public readonly int[] Shells;
        public readonly ElementPhase Phase;

        public Element(int z, string symbol, string name, double mass, ElementCategory category, int column, int row, int group, int period,
            string block, double electronegativity, double meltingK, double boilingK, double density, string configuration, int[] shells,
            int discoveryYear, int massNumber, ElementPhase phase, string note)
        {
            Z = z; Symbol = symbol; Name = name; Mass = mass; Category = category; Column = column; Row = row; Group = group; Period = period;
            Block = block; Electronegativity = electronegativity; MeltingK = meltingK; BoilingK = boilingK; Density = density;
            Configuration = configuration; Shells = shells; DiscoveryYear = discoveryYear; MassNumber = massNumber; Phase = phase; Note = note;
        }

        public int Neutrons => Math.Max(0, MassNumber - Z);
        public bool IsSynthetic => Z == 43 || Z == 61 || Z >= 93;
    }

    internal static partial class Elements
    {
        public static Color CategoryColor(ElementCategory c)
        {
            switch (c)
            {
                case ElementCategory.AlkaliMetal: return Gfx.Rgba(0xFF6B6B);
                case ElementCategory.AlkalineEarth: return Gfx.Rgba(0xFFA94D);
                case ElementCategory.Transition: return Gfx.Rgba(0xF7C948);
                case ElementCategory.PostTransition: return Gfx.Rgba(0x8BD17C);
                case ElementCategory.Metalloid: return Gfx.Rgba(0x3CC9A0);
                case ElementCategory.Nonmetal: return Gfx.Rgba(0x4DB8FF);
                case ElementCategory.Halogen: return Gfx.Rgba(0x7C8CFF);
                case ElementCategory.NobleGas: return Gfx.Rgba(0xB57CFF);
                case ElementCategory.Lanthanide: return Gfx.Rgba(0xFF7EB6);
                default: return Gfx.Rgba(0xE07CC8);
            }
        }

        public static string CategoryName(ElementCategory c)
        {
            switch (c)
            {
                case ElementCategory.AlkaliMetal: return "Alkali metal";
                case ElementCategory.AlkalineEarth: return "Toprak alkali metal";
                case ElementCategory.Transition: return "Geçiş metali";
                case ElementCategory.PostTransition: return "Zayıf metal";
                case ElementCategory.Metalloid: return "Yarı metal";
                case ElementCategory.Nonmetal: return "Ametal";
                case ElementCategory.Halogen: return "Halojen";
                case ElementCategory.NobleGas: return "Soy gaz";
                case ElementCategory.Lanthanide: return "Lantanit";
                default: return "Aktinit";
            }
        }

        public static string PhaseName(ElementPhase p)
        {
            switch (p)
            {
                case ElementPhase.Solid: return "Katı";
                case ElementPhase.Liquid: return "Sıvı";
                case ElementPhase.Gas: return "Gaz";
                default: return "Bilinmiyor";
            }
        }

        /// <summary>"[Ar] 3d6 4s2" → "[Ar] 3d⁶ 4s²" (a missing count means 1).</summary>
        public static string PrettyConfiguration(string config)
        {
            var sb = new StringBuilder();
            foreach (var part in config.Split(' '))
            {
                if (sb.Length > 0) sb.Append(' ');
                if (part.StartsWith("[", StringComparison.Ordinal))
                {
                    sb.Append(part);
                    continue;
                }

                int letter = -1;
                for (int i = 0; i < part.Length; i++)
                {
                    if (char.IsLetter(part[i])) { letter = i; break; }
                }

                if (letter < 0)
                {
                    sb.Append(part);
                    continue;
                }

                sb.Append(part, 0, letter + 1);
                string count = part.Substring(letter + 1);
                sb.Append(Superscript(count.Length == 0 ? "1" : count));
            }

            return sb.ToString();
        }

        private const string Sup = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        private const string Sub = "₀₁₂₃₄₅₆₇₈₉";

        public static string Superscript(string digits)
        {
            var sb = new StringBuilder();
            foreach (char ch in digits) sb.Append(char.IsDigit(ch) ? Sup[ch - '0'] : ch == '+' ? '⁺' : ch == '-' ? '⁻' : ch);
            return sb.ToString();
        }

        /// <summary>Micro-view species id → display text: "SO4^2-" → "SO₄²⁻", "H3O+" → "H₃O⁺".</summary>
        public static string PrettySpecies(string id)
        {
            var sb = new StringBuilder();
            int caret = id.IndexOf('^');
            string body = caret >= 0 ? id.Substring(0, caret) : id.TrimEnd('+', '-');
            string charge = caret >= 0 ? id.Substring(caret + 1) : id.Substring(body.Length);
            foreach (char ch in body) sb.Append(char.IsDigit(ch) ? Sub[ch - '0'] : ch);
            sb.Append(Superscript(charge));
            return sb.ToString();
        }

        public static string Celsius(double kelvin) =>
            double.IsNaN(kelvin) ? "—" : string.Format(CultureInfo.CurrentCulture, "{0:0.#} °C", kelvin - 273.15);
    }
}
