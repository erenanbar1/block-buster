using UnityEngine;

namespace BlockBlast.Core
{
    /// <summary>Everything that makes one stage feel different from the last.</summary>
    public readonly struct StageProfile
    {
        public readonly int Stage;
        public readonly string Name;
        /// <summary>Lines that must be cleared to reach the next stage.</summary>
        public readonly int LinesToAdvance;
        /// <summary>Highest piece tier this stage deals.</summary>
        public readonly PieceTier MaxTier;
        /// <summary>Extra weight given to bulky shapes. 0 = neutral, higher = meaner deals.</summary>
        public readonly float BigPieceBias;
        /// <summary>Junk blocks seeded per drop. 0 disables junk for this stage.</summary>
        public readonly int JunkBlocks;
        /// <summary>Placements between junk drops. 0 disables junk for this stage.</summary>
        public readonly int PlacementsPerJunk;
        /// <summary>Multiplies every point scored during this stage.</summary>
        public readonly int ScoreMultiplier;

        public StageProfile(int stage, string name, int linesToAdvance, PieceTier maxTier,
            float bigPieceBias, int junkBlocks, int placementsPerJunk, int scoreMultiplier)
        {
            Stage = stage;
            Name = name;
            LinesToAdvance = linesToAdvance;
            MaxTier = maxTier;
            BigPieceBias = bigPieceBias;
            JunkBlocks = junkBlocks;
            PlacementsPerJunk = placementsPerJunk;
            ScoreMultiplier = scoreMultiplier;
        }

        public bool SpawnsJunk => JunkBlocks > 0 && PlacementsPerJunk > 0;
    }

    /// <summary>
    /// The shape of a run. Every balance number lives in this one table, so tuning the
    /// game is a single-file edit and the sim in GameSimulationTests can measure the
    /// result.
    ///
    /// Two things escalate together: the piece pool gets meaner (new tiers unlock and
    /// bulky shapes get likelier) and the board gets seeded with junk. Skill alone
    /// cannot hold the line forever, so a run builds to a crescendo instead of
    /// wandering until an unlucky trio ends it.
    /// </summary>
    public static class DifficultyCurve
    {
        /// <summary>Stages past this point all use <see cref="Endless"/> pacing.</summary>
        public const int LastAuthoredStage = 6;

        static readonly StageProfile[] Stages =
        {
            //             stage name            lines  maxTier             bias junk per  xScore
            new StageProfile(1, "Warm Up",     4, PieceTier.Basic,     0.00f, 0,  0, 1),
            new StageProfile(2, "Rolling",     5, PieceTier.Tetromino, 0.15f, 0,  0, 2),
            new StageProfile(3, "Heating Up",  6, PieceTier.Awkward,   0.35f, 1, 12, 3),
            new StageProfile(4, "Pressure",    7, PieceTier.Awkward,   0.55f, 1,  8, 4),
            new StageProfile(5, "Squeeze",     8, PieceTier.Nasty,     0.75f, 2,  8, 5),
            new StageProfile(6, "Overload",    9, PieceTier.Nasty,     1.00f, 2,  6, 6),
        };

        public static StageProfile ForStage(int stage)
        {
            if (stage < 1) stage = 1;
            if (stage <= LastAuthoredStage) return Stages[stage - 1];
            return Endless(stage);
        }

        /// <summary>
        /// Past the authored stages the pressure keeps creeping but stops short of
        /// impossible, so a great player still gets a long tail rather than a wall.
        /// </summary>
        static StageProfile Endless(int stage)
        {
            int past = stage - LastAuthoredStage;
            return new StageProfile(
                stage,
                "Overload " + (past + 1),
                linesToAdvance: 9 + past * 2,
                maxTier: PieceTier.Nasty,
                bigPieceBias: Mathf.Min(1.0f + past * 0.15f, 1.75f),
                junkBlocks: Mathf.Min(2 + past / 2, 3),
                placementsPerJunk: Mathf.Max(6 - past, 5),
                scoreMultiplier: 6 + past);
        }

        public static string NameOf(int stage) => ForStage(stage).Name;
    }
}
