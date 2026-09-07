using UnityEngine;

namespace BlockBlast.Presentation
{
    /// <summary>Colours, sizes and timings for the whole game, in one place.</summary>
    public static class Theme
    {
        // ---- reference resolution (portrait phone) ---------------------------
        public const float RefWidth = 1080f;
        public const float RefHeight = 1920f;

        // ---- board metrics ----------------------------------------------------
        public const int BoardSize = 8;
        public const float CellSize = 116f;
        public const float CellSpacing = 8f;
        public const float BoardPadding = 20f;
        /// <summary>Width/height of the grid itself, excluding the frame padding.</summary>
        public static float GridExtent => BoardSize * CellSize + (BoardSize - 1) * CellSpacing;
        public static float FrameExtent => GridExtent + BoardPadding * 2f;
        public static float CellPitch => CellSize + CellSpacing;

        // ---- tray -------------------------------------------------------------
        public const float TrayScale = 0.5f;
        public const float TraySlotWidth = 330f;
        public const float TrayY = -768f;
        /// <summary>How far above the finger the piece floats while dragging.</summary>
        public const float DragLift = 210f;

        // ---- timings ----------------------------------------------------------
        public const float PopInDuration = 0.16f;
        public const float ClearDuration = 0.34f;
        public const float ReturnDuration = 0.16f;

        // ---- palette ----------------------------------------------------------
        public static readonly Color BackdropTop = Hex(0x1B1140);
        public static readonly Color BackdropBottom = Hex(0x0C0722);
        public static readonly Color FrameColor = Hex(0x120A2E, 0.65f);
        public static readonly Color SlotColor = Hex(0x2E2264);
        public static readonly Color HintColor = new Color(1f, 1f, 1f, 0.22f);
        public static readonly Color LineHintColor = Hex(0xFFF0A0, 0.34f);
        public static readonly Color TextColor = Hex(0xF3EEFF);
        public static readonly Color MutedTextColor = new Color(0.72f, 0.68f, 0.88f, 1f);
        public static readonly Color PanelColor = Hex(0x241A52);

        /// <summary>
        /// Junk blocks are deliberately grey and desaturated. They must never be mistaken
        /// for a piece the player placed, or the pressure reads as the game cheating.
        /// </summary>
        public static readonly Color JunkColor = Hex(0x6B6480);
        public static readonly Color StageBarColor = Hex(0x18C7E8);
        public static readonly Color StageBarTrack = new Color(1f, 1f, 1f, 0.10f);

        /// <summary>Block colours. Index into this from BoardModel colour indices.</summary>
        public static readonly Color[] Blocks =
        {
            Hex(0xFF4D6D), // red
            Hex(0xFF9F1C), // orange
            Hex(0xFFD400), // yellow
            Hex(0x2FD86B), // green
            Hex(0x18C7E8), // cyan
            Hex(0x4C7DFF), // blue
            Hex(0xA85CFF), // violet
            Hex(0xFF6FC1), // pink
        };

        public static Color Block(int index)
        {
            if (index == Core.JunkSpawner.JunkColorIndex) return JunkColor;
            if (Blocks.Length == 0) return Color.white;
            int i = ((index % Blocks.Length) + Blocks.Length) % Blocks.Length;
            return Blocks[i];
        }

        public static Color Hex(int rgb, float a = 1f)
            => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
