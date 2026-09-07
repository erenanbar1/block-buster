using System.Collections.Generic;

namespace BlockBlast.Core
{
    /// <summary>
    /// Every shape the generator can hand out. Each rotation is its own entry because
    /// pieces cannot be rotated by the player.
    /// </summary>
    public static class PieceLibrary
    {
        public static readonly IReadOnlyList<PieceShape> All;
        /// <summary>Sum of every shape weight, cached for weighted picking.</summary>
        public static readonly int TotalWeight;

        static PieceLibrary()
        {
            var list = new List<PieceShape>
            {
                // --- dots and bars -----------------------------------------------
                PieceShape.FromRows("dot",    10, "#"),
                PieceShape.FromRows("bar2h",  12, "##"),
                PieceShape.FromRows("bar2v",  12, "#", "#"),
                PieceShape.FromRows("bar3h",  11, "###"),
                PieceShape.FromRows("bar3v",  11, "#", "#", "#"),
                PieceShape.FromRows("bar4h",   7, "####"),
                PieceShape.FromRows("bar4v",   7, "#", "#", "#", "#"),
                PieceShape.FromRows("bar5h",   3, "#####"),
                PieceShape.FromRows("bar5v",   3, "#", "#", "#", "#", "#"),

                // --- squares ------------------------------------------------------
                PieceShape.FromRows("square2", 12, "##", "##"),
                PieceShape.FromRows("square3",  2, "###", "###", "###"),
                PieceShape.FromRows("rect23",   4, "##", "##", "##"),
                PieceShape.FromRows("rect32",   4, "###", "###"),

                // --- small corners (2x2 minus one) --------------------------------
                PieceShape.FromRows("corner2_a", 9, "#.", "##"),
                PieceShape.FromRows("corner2_b", 9, ".#", "##"),
                PieceShape.FromRows("corner2_c", 9, "##", "#."),
                PieceShape.FromRows("corner2_d", 9, "##", ".#"),

                // --- big corners (3x3 L) ------------------------------------------
                PieceShape.FromRows("corner3_a", 5, "#..", "#..", "###"),
                PieceShape.FromRows("corner3_b", 5, "..#", "..#", "###"),
                PieceShape.FromRows("corner3_c", 5, "###", "#..", "#.."),
                PieceShape.FromRows("corner3_d", 5, "###", "..#", "..#"),

                // --- L / J tetrominoes ---------------------------------------------
                PieceShape.FromRows("L_a", 4, "#.", "#.", "##"),
                PieceShape.FromRows("L_b", 4, ".#", ".#", "##"),
                PieceShape.FromRows("L_c", 4, "##", "#.", "#."),
                PieceShape.FromRows("L_d", 4, "##", ".#", ".#"),
                PieceShape.FromRows("L_e", 4, "###", "#.."),
                PieceShape.FromRows("L_f", 4, "###", "..#"),
                PieceShape.FromRows("L_g", 4, "#..", "###"),
                PieceShape.FromRows("L_h", 4, "..#", "###"),

                // --- T tetrominoes --------------------------------------------------
                PieceShape.FromRows("T_a", 4, "###", ".#."),
                PieceShape.FromRows("T_b", 4, ".#.", "###"),
                PieceShape.FromRows("T_c", 4, "#.", "##", "#."),
                PieceShape.FromRows("T_d", 4, ".#", "##", ".#"),

                // --- S / Z tetrominoes ----------------------------------------------
                PieceShape.FromRows("S_a", 3, ".##", "##."),
                PieceShape.FromRows("S_b", 3, "##.", ".##"),
                PieceShape.FromRows("S_c", 3, "#.", "##", ".#"),
                PieceShape.FromRows("S_d", 3, ".#", "##", "#."),

                // --- exotic ----------------------------------------------------------
                PieceShape.FromRows("plus",   2, ".#.", "###", ".#."),
                PieceShape.FromRows("diag2a", 2, ".#", "#."),
                PieceShape.FromRows("diag2b", 2, "#.", ".#"),
                PieceShape.FromRows("diag3a", 1, "..#", ".#.", "#.."),
                PieceShape.FromRows("diag3b", 1, "#..", ".#.", "..#"),
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
    }
}
