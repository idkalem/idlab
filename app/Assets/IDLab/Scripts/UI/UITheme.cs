using UnityEngine;

namespace IDLab.App.UI
{
    /// <summary>Dark lab theme tokens (single source of truth for the HUD palette).</summary>
    public static class UITheme
    {
        public static readonly Color SceneBackground = Hex(0x0B0F17);
        public static readonly Color Panel = Hex(0x111827, 0.92f);
        public static readonly Color PanelRaised = Hex(0x1A2333, 0.96f);
        public static readonly Color PanelBorder = Hex(0x2A3A52);
        public static readonly Color TopBar = Hex(0x0D131E, 0.97f);
        public static readonly Color Text = Hex(0xE6EDF7);
        public static readonly Color TextMuted = Hex(0x8A9BB4);
        public static readonly Color Accent = Hex(0x35C4FF);
        public static readonly Color AccentDim = Hex(0x1C4E6B);
        public static readonly Color Button = Hex(0x1F2A3C);
        public static readonly Color ButtonHover = Hex(0x2A3A52);
        public static readonly Color Danger = Hex(0xFF5A4F);
        public static readonly Color Warning = Hex(0xFFB347);
        public static readonly Color Success = Hex(0x4FE07A);
        public static readonly Color GraphGrid = Hex(0x26324A);

        /// <summary>Series colours for live graphs, in assignment order.</summary>
        public static readonly Color[] SeriesPalette =
        {
            Hex(0xFF7A45), Hex(0x35C4FF), Hex(0x4FE07A), Hex(0xC07BFF), Hex(0xFFD24F), Hex(0xFF5A9E),
        };

        public const int FontSmall = 13;
        public const int FontBody = 15;
        public const int FontTitle = 18;
        public const int FontValue = 26;

        public static Color Hex(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
    }
}
