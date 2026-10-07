namespace IDLab.Core.Chemistry
{
    /// <summary>Colour lookup tables for acid–base indicators.</summary>
    public static class IndicatorColors
    {
        // Universal indicator: red (pH 0-3) → orange → yellow → green (7) → blue → indigo/violet (14).
        private static readonly (double pH, ColorRGBA color)[] Universal =
        {
            (0.0, ColorRGBA.FromHex(0xC81E1E)),
            (3.0, ColorRGBA.FromHex(0xF0461E)),
            (4.5, ColorRGBA.FromHex(0xF59B1E)),
            (6.0, ColorRGBA.FromHex(0xE6D72D)),
            (7.0, ColorRGBA.FromHex(0x3CB43C)),
            (8.5, ColorRGBA.FromHex(0x1E8C8C)),
            (10.0, ColorRGBA.FromHex(0x2350C8)),
            (12.0, ColorRGBA.FromHex(0x3C2896)),
            (14.0, ColorRGBA.FromHex(0x501E78)),
        };

        public static ColorRGBA UniversalIndicator(double pH)
        {
            if (pH <= Universal[0].pH)
            {
                return Universal[0].color;
            }

            for (int i = 1; i < Universal.Length; i++)
            {
                if (pH <= Universal[i].pH)
                {
                    var lo = Universal[i - 1];
                    var hi = Universal[i];
                    return ColorRGBA.Lerp(lo.color, hi.color, (float)((pH - lo.pH) / (hi.pH - lo.pH)));
                }
            }

            return Universal[Universal.Length - 1].color;
        }
    }
}
