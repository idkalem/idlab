namespace IDLab.Core
{
    /// <summary>Engine-independent linear colour (0..1 per channel).</summary>
    public readonly struct ColorRGBA
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public ColorRGBA(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static ColorRGBA FromHex(uint rgb, float a = 1f) =>
            new ColorRGBA(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static ColorRGBA Lerp(ColorRGBA x, ColorRGBA y, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new ColorRGBA(
                x.R + (y.R - x.R) * t,
                x.G + (y.G - x.G) * t,
                x.B + (y.B - x.B) * t,
                x.A + (y.A - x.A) * t);
        }

        public ColorRGBA WithAlpha(float a) => new ColorRGBA(R, G, B, a);

        public override string ToString() => $"RGBA({R:0.00}, {G:0.00}, {B:0.00}, {A:0.00})";
    }
}
