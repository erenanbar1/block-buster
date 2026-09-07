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
    /// Deals the trio of pieces shown in the tray, according to the stage the run has
    /// reached.
    ///
    /// The deal escalates: each stage unlocks a meaner <see cref="PieceTier"/> and
    /// raises <see cref="StageProfile.BigPieceBias"/>, so bulky shapes get likelier as
    /// the run goes on. A single mercy rule pulls the other way — once the board is
    /// nearly full, oversized shapes are damped again, because being handed a 3x3 into
    /// a packed board is unfair rather than hard.
    ///
    /// A trio is never dealt in which nothing at all can be placed, unless the board
    /// genuinely admits no piece in the pool (a real, unavoidable game over).
    /// </summary>
    public sealed class PieceGenerator
    {
        /// <summary>Above this fill ratio, big shapes get damped no matter the stage.</summary>
        const float MercyFill = 0.75f;

        readonly System.Random rng;
        readonly int colorCount;
        readonly List<PieceShape> pool = new List<PieceShape>();
        readonly List<int> weights = new List<int>();

        public PieceGenerator(int seed, int colorCount = 8)
        {
            rng = new System.Random(seed);
            this.colorCount = Mathf.Max(1, colorCount);
        }

        public PieceInstance[] NextTrio(BoardModel board, int count = 3)
            => NextTrio(board, DifficultyCurve.ForStage(1), count);

        public PieceInstance[] NextTrio(BoardModel board, StageProfile profile, int count)
        {
            var trio = new PieceInstance[count];
            const int maxAttempts = 24;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                for (int i = 0; i < count; i++)
                    trio[i] = new PieceInstance(PickShape(board, profile), rng.Next(colorCount));

                if (AnyPlaceable(board, trio)) return trio;
            }

            // Random draws kept missing: force a fitting shape into a random slot.
            var rescue = FindFittingShape(board, profile);
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

        /// <summary>Weighted pick from the tiers this stage has unlocked.</summary>
        PieceShape PickShape(BoardModel board, StageProfile profile)
        {
            float fill = board.CellCount == 0 ? 0f : (float)board.OccupiedCount / board.CellCount;
            float mercy = Mathf.Clamp01((fill - MercyFill) / (1f - MercyFill));

            pool.Clear();
            weights.Clear();
            int total = 0;

            foreach (var shape in PieceLibrary.All)
            {
                if (shape.Tier > profile.MaxTier) continue;

                int w = Mathf.RoundToInt(shape.Weight * SizeBias(shape, profile.BigPieceBias, mercy));
                if (w < 1) w = 1;
                pool.Add(shape);
                weights.Add(w);
                total += w;
            }

            if (pool.Count == 0) return PieceLibrary.All[0];

            int roll = rng.Next(total);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        /// <summary>
        /// Bulky shapes get commoner as the stage bias rises, then get damped again once
        /// the board is nearly full so the endgame stays winnable.
        /// </summary>
        static float SizeBias(PieceShape shape, float stageBias, float mercy)
        {
            int bulk = shape.Bulk;
            if (bulk == 0) return 1f;

            float escalated = 1f + stageBias * bulk;
            float damped = 1f / (1f + bulk);
            return Mathf.Lerp(escalated, damped, mercy);
        }

        /// <summary>Any shape in this stage's pool that still fits, or null when the board is dead.</summary>
        PieceShape FindFittingShape(BoardModel board, StageProfile profile)
        {
            var order = PieceLibrary.UpToTier(profile.MaxTier);
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
