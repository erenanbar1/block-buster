using System.Collections.Generic;

namespace BlockBlast.Core
{
    /// <summary>
    /// Every shape the generator can hand out. Each rotation is its own entry because
    /// pieces cannot be rotated by the player.
    ///
    /// Shapes are grouped by <see cref="PieceTier"/>: the difficulty curve unlocks one
    /// tier at a time, so the opening minutes deal only forgiving shapes and the late
    /// game deals everything.
    /// </summary>
    public static class PieceLibrary
    {
        public static readonly IReadOnlyList<PieceShape> All;
        /// <summary>Sum of every shape weight, cached for weighted picking.</summary>
        public static readonly int TotalWeight;

        static PieceLibrary()
        {
            const PieceTier basic = PieceTier.Basic;
            const PieceTier tetro = PieceTier.Tetromino;
            const PieceTier awkward = PieceTier.Awkward;
            const PieceTier nasty = PieceTier.Nasty;

            var list = new List<PieceShape>
            {
                // --- Basic: short, forgiving, always available -----------------------
                PieceShape.FromRows("dot",    10, basic, "#"),
                PieceShape.FromRows("bar2h",  12, basic, "##"),
                PieceShape.FromRows("bar2v",  12, basic, "#", "#"),
                PieceShape.FromRows("bar3h",  11, basic, "###"),
                PieceShape.FromRows("bar3v",  11, basic, "#", "#", "#"),
                PieceShape.FromRows("square2", 12, basic, "##", "##"),
                PieceShape.FromRows("corner2_a", 9, basic, "#.", "##"),
                PieceShape.FromRows("corner2_b", 9, basic, ".#", "##"),
                PieceShape.FromRows("corner2_c", 9, basic, "##", "#."),
                PieceShape.FromRows("corner2_d", 9, basic, "##", ".#"),

                // --- Tetromino: still friendly, but they leave gaps -------------------
                PieceShape.FromRows("bar4h",   7, tetro, "####"),
                PieceShape.FromRows("bar4v",   7, tetro, "#", "#", "#", "#"),
                PieceShape.FromRows("L_a", 4, tetro, "#.", "#.", "##"),
                PieceShape.FromRows("L_b", 4, tetro, ".#", ".#", "##"),
                PieceShape.FromRows("L_c", 4, tetro, "##", "#.", "#."),
                PieceShape.FromRows("L_d", 4, tetro, "##", ".#", ".#"),
                PieceShape.FromRows("L_e", 4, tetro, "###", "#.."),
                PieceShape.FromRows("L_f", 4, tetro, "###", "..#"),
                PieceShape.FromRows("L_g", 4, tetro, "#..", "###"),
                PieceShape.FromRows("L_h", 4, tetro, "..#", "###"),
                PieceShape.FromRows("T_a", 4, tetro, "###", ".#."),
                PieceShape.FromRows("T_b", 4, tetro, ".#.", "###"),
                PieceShape.FromRows("T_c", 4, tetro, "#.", "##", "#."),
                PieceShape.FromRows("T_d", 4, tetro, ".#", "##", ".#"),
                PieceShape.FromRows("S_a", 3, tetro, ".##", "##."),
                PieceShape.FromRows("S_b", 3, tetro, "##.", ".##"),
                PieceShape.FromRows("S_c", 3, tetro, "#.", "##", ".#"),
                PieceShape.FromRows("S_d", 3, tetro, ".#", "##", "#."),

                // --- Awkward: bulky shapes that eat a lot of board --------------------
                PieceShape.FromRows("rect23",   4, awkward, "##", "##", "##"),
                PieceShape.FromRows("rect32",   4, awkward, "###", "###"),
                PieceShape.FromRows("corner3_a", 5, awkward, "#..", "#..", "###"),
                PieceShape.FromRows("corner3_b", 5, awkward, "..#", "..#", "###"),
                PieceShape.FromRows("corner3_c", 5, awkward, "###", "#..", "#.."),
                PieceShape.FromRows("corner3_d", 5, awkward, "###", "..#", "..#"),
                PieceShape.FromRows("bar5h",   3, awkward, "#####"),
                PieceShape.FromRows("bar5v",   3, awkward, "#", "#", "#", "#", "#"),
                PieceShape.FromRows("square3",  2, awkward, "###", "###", "###"),

                // --- Nasty: punish an untidy board ------------------------------------
                PieceShape.FromRows("plus",   2, nasty, ".#.", "###", ".#."),
                PieceShape.FromRows("diag2a", 2, nasty, ".#", "#."),
                PieceShape.FromRows("diag2b", 2, nasty, "#.", ".#"),
                PieceShape.FromRows("diag3a", 1, nasty, "..#", ".#.", "#.."),
                PieceShape.FromRows("diag3b", 1, nasty, "#..", ".#.", "..#"),
            };

            All = list;
            int total = 0;
            foreach (var s in list) total += s.Weight;
            TotalWeight = total;
        }

        public static PieceShape ById(string id)
        {
            foreach (var s in All)
                if (s.Id == id) return s;
            return null;
        }

        /// <summary>Every shape at or below a tier — the pool a given stage deals from.</summary>
        public static List<PieceShape> UpToTier(PieceTier tier)
        {
            var pool = new List<PieceShape>();
            foreach (var s in All)
                if (s.Tier <= tier) pool.Add(s);
            return pool;
        }
    }
}
