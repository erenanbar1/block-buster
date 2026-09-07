namespace BlockBlast.Core
{
    /// <summary>What a single placement changed, so the presentation knows what to play.</summary>
    public struct RunEvents
    {
        public int PointsScored;
        public bool StageAdvanced;
        public int StageReached;
        /// <summary>Combo chain length after this placement (0 when not in a combo).</summary>
        public int Combo;
        public bool ComboExtended;
        public bool ComboBroken;
        /// <summary>The board should be seeded with junk now.</summary>
        public bool JunkDue;
        public int JunkBlocks;
    }

    /// <summary>
    /// Owns the arc of a single run: which stage the player is on, how the combo chain
    /// is doing, and when the next junk drop is due. Pure state, no Unity types, so the
    /// whole progression can be simulated headlessly in the tests.
    /// </summary>
    public sealed class RunProgress
    {
        public int Stage { get; private set; } = 1;
        public int LinesThisStage { get; private set; }
        public int TotalLines { get; private set; }
        public int Score { get; private set; }

        /// <summary>Consecutive clearing placements, allowing for the grace miss.</summary>
        public int Combo { get; private set; }
        /// <summary>Dry placements since the last clear. Two in a row break the chain.</summary>
        public int ComboMisses { get; private set; }
        public int BestCombo { get; private set; }

        public int PlacementsSinceJunk { get; private set; }
        public int Placements { get; private set; }

        /// <summary>
        /// A combo survives this many non-clearing placements. One grace move is what
        /// makes chains reachable at all: a tray holds three pieces and rarely clears
        /// on more than one of them, so an instant reset kept the combo pinned at 1.
        /// </summary>
        public const int ComboGrace = 1;


        public StageProfile Profile => DifficultyCurve.ForStage(Stage);
        public int LinesToAdvance => Profile.LinesToAdvance;
        /// <summary>0..1 progress through the current stage, for the HUD bar.</summary>
        public float StageProgress =>
            LinesToAdvance <= 0 ? 0f : (float)LinesThisStage / LinesToAdvance;

        public void Reset()
        {
            Stage = 1;
            LinesThisStage = 0;
            TotalLines = 0;
            Score = 0;
            Combo = 0;
            ComboMisses = 0;
            BestCombo = 0;
            PlacementsSinceJunk = 0;
            Placements = 0;
        }

        /// <summary>Applies one placement and reports everything that changed.</summary>
        public RunEvents RegisterPlacement(PlacementResult result)
        {
            var events = new RunEvents();
            Placements++;

            int lines = result.LinesCleared;
            if (lines > 0)
            {
                Combo++;
                ComboMisses = 0;
                events.ComboExtended = true;
                if (Combo > BestCombo) BestCombo = Combo;
            }
            else if (Combo > 0)
            {
                ComboMisses++;
                if (ComboMisses > ComboGrace)
                {
                    Combo = 0;
                    ComboMisses = 0;
                    events.ComboBroken = true;
                }
            }
            events.Combo = Combo;

            // Score uses the stage the placement happened in, before any advance.
            int points = ScoreRules.ScoreFor(result, Combo, Profile.ScoreMultiplier);
            Score += points;
            events.PointsScored = points;

            if (lines > 0)
            {
                TotalLines += lines;
                LinesThisStage += lines;
                while (LinesThisStage >= LinesToAdvance)
                {
                    LinesThisStage -= LinesToAdvance;
                    Stage++;
                    events.StageAdvanced = true;
                }
            }
            events.StageReached = Stage;

            // Pressure accrues with every placement, not only with wasted ones. Counting
            // dry moves alone let a strong player out-clear the clock forever, which is
            // the flat curve this whole system exists to fix.
            var profile = Profile;
            if (profile.SpawnsJunk)
            {
                PlacementsSinceJunk++;

                // Clearing earns no discount here on purpose. Any discount large enough
                // to feel generous also lets a strong player out-clear the clock forever,
                // which is the flat curve this system exists to fix. Clearing is already
                // rewarded by the empty board it leaves behind.
                //
                // The drop does wait for a quiet beat, though: landing junk on top of a
                // clear would step on the one moment that is meant to feel like a reward.
                if (PlacementsSinceJunk >= profile.PlacementsPerJunk && lines == 0)
                {
                    PlacementsSinceJunk = 0;
                    events.JunkDue = true;
                    events.JunkBlocks = profile.JunkBlocks;
                }
            }

            return events;
        }

        /// <summary>Restores a saved run. Used by SaveSystem on resume.</summary>
        public void Restore(int stage, int linesThisStage, int totalLines, int score,
            int combo, int comboMisses, int bestCombo, int placementsSinceJunk, int placements)
        {
            Stage = stage < 1 ? 1 : stage;
            LinesThisStage = linesThisStage < 0 ? 0 : linesThisStage;
            TotalLines = totalLines < 0 ? 0 : totalLines;
            Score = score < 0 ? 0 : score;
            Combo = combo < 0 ? 0 : combo;
            ComboMisses = comboMisses < 0 ? 0 : comboMisses;
            BestCombo = bestCombo < 0 ? 0 : bestCombo;
            PlacementsSinceJunk = placementsSinceJunk < 0 ? 0 : placementsSinceJunk;
            Placements = placements < 0 ? 0 : placements;
        }
    }
}
