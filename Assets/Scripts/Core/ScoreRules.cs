using UnityEngine;

namespace BlockBlast.Core
{
    /// <summary>
    /// Scoring maths, kept separate from the game loop so the numbers can be tuned and
    /// unit tested without touching presentation.
    /// </summary>
    public static class ScoreRules
    {
        public const int PerfectClearBonus = 300;
        public const int MaxComboMultiplier = 5;

        /// <summary>One point per block dropped, before any line clears.</summary>
        public static int PlacementScore(int placedCells) => Mathf.Max(0, placedCells);

        /// <summary>
        /// Clearing several lines at once is worth far more than clearing them one at a
        /// time: 1 line = 10, 2 = 30, 3 = 60, 4 = 100 ...
        /// </summary>
        public static int LineBaseScore(int linesCleared)
        {
            if (linesCleared <= 0) return 0;
            return 10 * linesCleared * (linesCleared + 1) / 2;
        }

        /// <summary>Combo 1-2 = x1, 3-4 = x2, 5-6 = x3 ... capped at x5.</summary>
        public static int ComboMultiplier(int combo)
        {
            if (combo < 1) combo = 1;
            int multiplier = 1 + (combo - 1) / 2;
            return Mathf.Min(multiplier, MaxComboMultiplier);
        }

        /// <summary>Total points awarded for a single placement, combo included.</summary>
        public static int ScoreFor(PlacementResult result, int comboAfterPlacement)
        {
            int score = PlacementScore(result.PlacedCells);
            int lines = result.LinesCleared;
            if (lines > 0)
                score += LineBaseScore(lines) * ComboMultiplier(comboAfterPlacement);
            if (result.PerfectClear)
                score += PerfectClearBonus;
            return score;
        }

        /// <summary>A clear extends the combo, a placement that clears nothing ends it.</summary>
        public static int NextCombo(int currentCombo, PlacementResult result)
            => result.LinesCleared > 0 ? currentCombo + 1 : 0;
    }
}
