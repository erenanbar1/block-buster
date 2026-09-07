using UnityEngine;

namespace BlockBlast.Core
{
    /// <summary>
    /// Scoring maths, kept separate from the game loop so the numbers can be tuned and
    /// unit tested without touching presentation.
    ///
    /// Combo state lives in <see cref="RunProgress"/>, which owns the grace rule that
    /// lets a chain survive a single non-clearing placement.
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

        /// <summary>
        /// Total points for a single placement: blocks dropped, plus line value scaled
        /// by the combo, all multiplied by the stage so late-game numbers feel big.
        /// </summary>
        public static int ScoreFor(PlacementResult result, int comboAfterPlacement, int stageMultiplier = 1)
        {
            if (stageMultiplier < 1) stageMultiplier = 1;

            int score = PlacementScore(result.PlacedCells);
            int lines = result.LinesCleared;
            if (lines > 0)
                score += LineBaseScore(lines) * ComboMultiplier(comboAfterPlacement);
            if (result.PerfectClear)
                score += PerfectClearBonus;
            return score * stageMultiplier;
        }

        /// <summary>Name for a multi-line clear, shown as a popup. Null when there is nothing to shout about.</summary>
        public static string ClearTitle(int linesCleared)
        {
            switch (linesCleared)
            {
                case 2: return "DOUBLE!";
                case 3: return "TRIPLE!";
                case 0:
                case 1: return null;
                default: return "BLAST!";
            }
        }
    }
}
