using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast.Core
{
    /// <summary>A concrete piece handed to the player: a shape plus the colour it was dealt in.</summary>
    public sealed class PieceInstance
    {
        public PieceShape Shape { get; }
        public int ColorIndex { get; }

        public PieceInstance(PieceShape shape, int colorIndex)
        {
            Shape = shape;
            ColorIndex = colorIndex;
        }

        public override string ToString() => Shape.Id + "#" + ColorIndex;
    }

    /// <summary>
    /// Deals the trio of pieces shown in the tray.
    ///
    /// Two pieces of fairness are baked in:
    ///  * as the board fills up, huge pieces become progressively less likely;
    ///  * a trio is never dealt in which nothing at all can be placed, unless the board
    ///    genuinely admits no piece in the library (a real, unavoidable game over).
    /// </summary>
    public sealed class PieceGenerator
    {
        readonly System.Random rng;
        readonly int colorCount;
        readonly List<PieceShape> scratch = new List<PieceShape>();

        public PieceGenerator(int seed, int colorCount = 8)
        {
            rng = new System.Random(seed);
            this.colorCount = Mathf.Max(1, colorCount);
        }

        public PieceInstance[] NextTrio(BoardModel board, int count = 3)
        {
            var trio = new PieceInstance[count];
            const int maxAttempts = 24;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                for (int i = 0; i < count; i++)
                    trio[i] = new PieceInstance(PickShape(board), rng.Next(colorCount));

                if (AnyPlaceable(board, trio)) return trio;
            }

            // Random draws kept missing: force a fitting shape into a random slot.
            var rescue = FindFittingShape(board);
            if (rescue != null)
                trio[rng.Next(count)] = new PieceInstance(rescue, rng.Next(colorCount));

            return trio;
        }

        static bool AnyPlaceable(BoardModel board, PieceInstance[] pieces)
        {
            foreach (var p in pieces)
                if (p != null && board.HasAnyPlacement(p.Shape)) return true;
            return false;
        }

        /// <summary>Weighted pick, with big shapes damped once the board gets crowded.</summary>
        PieceShape PickShape(BoardModel board)
        {
            float fill = board.CellCount == 0 ? 0f : (float)board.OccupiedCount / board.CellCount;
            float crowding = Mathf.Clamp01((fill - 0.35f) / 0.5f); // 0 when roomy, 1 when packed

            int total = 0;
            scratch.Clear();
            var weights = new List<int>(PieceLibrary.All.Count);
            foreach (var shape in PieceLibrary.All)
            {
                int w = Mathf.RoundToInt(shape.Weight * SizeBias(shape, crowding));
                if (w < 1) w = 1;
                scratch.Add(shape);
                weights.Add(w);
                total += w;
            }

            int roll = rng.Next(total);
            for (int i = 0; i < scratch.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return scratch[i];
            }
            return scratch[scratch.Count - 1];
        }

        static float SizeBias(PieceShape shape, float crowding)
        {
            int span = Mathf.Max(shape.Width, shape.Height);
            // Pieces of 4+ cells or spanning 4+ tiles get rarer as the board fills.
            float bulk = Mathf.Max(shape.CellCount - 3, span - 3, 0);
            return Mathf.Lerp(1f, 1f / (1f + bulk), crowding);
        }

        /// <summary>Any shape that still fits somewhere, or null when the board is dead.</summary>
        PieceShape FindFittingShape(BoardModel board)
        {
            var order = new List<PieceShape>(PieceLibrary.All);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (var shape in order)
                if (board.HasAnyPlacement(shape)) return shape;
            return null;
        }
    }
}
