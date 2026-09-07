using System.Collections.Generic;
using BlockBlast.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlockBlast.Tests
{
    public class RunProgressTests
    {
        static PlacementResult Result(int placed, int rows, int cols, bool perfect = false)
        {
            var r = new PlacementResult
            {
                PlacedCells = placed,
                ClearedRows = new List<int>(),
                ClearedColumns = new List<int>(),
                ClearedCells = new List<Vector2Int>(),
                PerfectClear = perfect
            };
            for (int i = 0; i < rows; i++) r.ClearedRows.Add(i);
            for (int i = 0; i < cols; i++) r.ClearedColumns.Add(i);
            return r;
        }

        static PlacementResult Clear(int lines = 1) => Result(4, lines, 0);
        static PlacementResult Dry() => Result(4, 0, 0);

        // ---- combo grace: the change that makes chains reachable at all ----------

        [Test]
        public void ClearingExtendsTheCombo()
        {
            var run = new RunProgress();
            run.RegisterPlacement(Clear());
            Assert.AreEqual(1, run.Combo);
            run.RegisterPlacement(Clear());
            Assert.AreEqual(2, run.Combo);
        }

        [Test]
        public void ASingleDryPlacementDoesNotBreakTheCombo()
        {
            var run = new RunProgress();
            run.RegisterPlacement(Clear());
            run.RegisterPlacement(Clear());

            var events = run.RegisterPlacement(Dry());

            Assert.AreEqual(2, run.Combo, "one dry move is forgiven");
            Assert.IsFalse(events.ComboBroken);
        }

        [Test]
        public void TwoDryPlacementsInARowBreakTheCombo()
        {
            var run = new RunProgress();
            run.RegisterPlacement(Clear());
            run.RegisterPlacement(Clear());
            run.RegisterPlacement(Dry());

            var events = run.RegisterPlacement(Dry());

            Assert.AreEqual(0, run.Combo);
            Assert.IsTrue(events.ComboBroken);
        }

        [Test]
        public void ClearingAfterAGraceMissContinuesTheSameChain()
        {
            var run = new RunProgress();
            run.RegisterPlacement(Clear());
            run.RegisterPlacement(Dry());
            run.RegisterPlacement(Clear());

            Assert.AreEqual(2, run.Combo, "the chain resumes rather than restarting");
            Assert.AreEqual(0, run.ComboMisses);
        }

        [Test]
        public void GraceIsTheOnlyReasonHighCombosAreReachable()
        {
            // A realistic tray rhythm: clear, dry, clear, dry, clear...
            var run = new RunProgress();
            for (int i = 0; i < 5; i++)
            {
                run.RegisterPlacement(Clear());
                run.RegisterPlacement(Dry());
            }
            Assert.GreaterOrEqual(run.BestCombo, 5,
                "alternating clear/dry should build a chain, not reset every other move");
            Assert.GreaterOrEqual(ScoreRules.ComboMultiplier(run.BestCombo), 3);
        }

        [Test]
        public void BestComboRemembersThePeak()
        {
            var run = new RunProgress();
            run.RegisterPlacement(Clear());
            run.RegisterPlacement(Clear());
            run.RegisterPlacement(Clear());
            run.RegisterPlacement(Dry());
            run.RegisterPlacement(Dry());

            Assert.AreEqual(0, run.Combo);
            Assert.AreEqual(3, run.BestCombo);
        }

        // ---- stages ---------------------------------------------------------------

        [Test]
        public void RunStartsAtStageOne()
        {
            var run = new RunProgress();
            Assert.AreEqual(1, run.Stage);
            Assert.AreEqual(0, run.TotalLines);
            Assert.AreEqual(0f, run.StageProgress);
        }

        [Test]
        public void ClearingEnoughLinesAdvancesTheStage()
        {
            var run = new RunProgress();
            int needed = DifficultyCurve.ForStage(1).LinesToAdvance;

            RunEvents last = default;
            for (int i = 0; i < needed; i++) last = run.RegisterPlacement(Clear());

            Assert.IsTrue(last.StageAdvanced);
            Assert.AreEqual(2, run.Stage);
            Assert.AreEqual(needed, run.TotalLines);
        }

        [Test]
        public void OneHugeClearCanSkipStraightPastAStage()
        {
            var run = new RunProgress();
            // Far more lines at once than stage 1 needs.
            var events = run.RegisterPlacement(Result(8, 8, 8));
            Assert.IsTrue(events.StageAdvanced);
            Assert.Greater(run.Stage, 2, "a huge clear should carry through several stages");
        }

        [Test]
        public void StageProgressTracksLinesTowardTheNextStage()
        {
            var run = new RunProgress();
            int needed = DifficultyCurve.ForStage(1).LinesToAdvance;
            run.RegisterPlacement(Clear());

            Assert.AreEqual(1f / needed, run.StageProgress, 0.001f);
        }

        [Test]
        public void StageNeverGoesBackwards()
        {
            var run = new RunProgress();
            int previous = run.Stage;
            for (int i = 0; i < 200; i++)
            {
                run.RegisterPlacement(i % 3 == 0 ? Clear() : Dry());
                Assert.GreaterOrEqual(run.Stage, previous);
                previous = run.Stage;
            }
        }

        [Test]
        public void ScoreUsesTheStageMultiplierOfTheStageThePlacementHappenedIn()
        {
            var run = new RunProgress();
            var events = run.RegisterPlacement(Dry());
            // Stage 1 multiplier is 1, so a 4-block dry placement is worth exactly 4.
            Assert.AreEqual(4, events.PointsScored);
            Assert.AreEqual(4, run.Score);
        }

        [Test]
        public void ScoreGrowsWithStage()
        {
            var early = new RunProgress();
            int earlyPoints = early.RegisterPlacement(Dry()).PointsScored;

            var late = new RunProgress();
            late.Restore(stage: 5, linesThisStage: 0, totalLines: 40, score: 0,
                combo: 0, comboMisses: 0, bestCombo: 0, placementsSinceJunk: 0, placements: 0);
            int latePoints = late.RegisterPlacement(Dry()).PointsScored;

            Assert.Greater(latePoints, earlyPoints, "the same move should pay more later in a run");
        }

        // ---- junk cadence ------------------------------------------------------------

        [Test]
        public void EarlyStagesNeverAskForJunk()
        {
            var run = new RunProgress();
            for (int i = 0; i < 60; i++)
                Assert.IsFalse(run.RegisterPlacement(Dry()).JunkDue,
                    "stage 1 should never seed junk");
        }

        [Test]
        public void LaterStagesAskForJunkOnCadence()
        {
            var run = new RunProgress();
            run.Restore(stage: 4, linesThisStage: 0, totalLines: 30, score: 0,
                combo: 0, comboMisses: 0, bestCombo: 0, placementsSinceJunk: 0, placements: 0);

            var profile = DifficultyCurve.ForStage(4);
            Assert.IsTrue(profile.SpawnsJunk);

            bool sawJunk = false;
            for (int i = 0; i < profile.PlacementsPerJunk; i++)
                sawJunk |= run.RegisterPlacement(Dry()).JunkDue;

            Assert.IsTrue(sawJunk, "junk should be due within one cadence window");
        }

        [Test]
        public void JunkWaitsForAQuietBeatRatherThanLandingOnAClear()
        {
            var run = new RunProgress();
            run.Restore(stage: 4, linesThisStage: 0, totalLines: 30, score: 0,
                combo: 0, comboMisses: 0, bestCombo: 0, placementsSinceJunk: 0, placements: 0);

            var profile = DifficultyCurve.ForStage(4);
            for (int i = 0; i < profile.PlacementsPerJunk - 1; i++) run.RegisterPlacement(Dry());

            // The drop is now due, but this placement clears - junk must not step on it.
            Assert.IsFalse(run.RegisterPlacement(Clear()).JunkDue,
                "junk landed on the same beat as a clear");

            // It arrives on the next quiet move instead: deferred, not cancelled.
            Assert.IsTrue(run.RegisterPlacement(Dry()).JunkDue,
                "the deferred drop never arrived");
        }

        [Test]
        public void APlayerWhoKeepsClearingStillEventuallyMeetsJunk()
        {
            // The failure mode this guards against: pressure that only accrues on wasted
            // moves, which a strong player simply never makes.
            var run = new RunProgress();
            run.Restore(stage: 5, linesThisStage: 0, totalLines: 40, score: 0,
                combo: 0, comboMisses: 0, bestCombo: 0, placementsSinceJunk: 0, placements: 0);

            bool sawJunk = false;
            for (int i = 0; i < 60 && !sawJunk; i++)
            {
                // A strong rhythm: clear two moves out of every three.
                sawJunk |= run.RegisterPlacement(Clear()).JunkDue;
                sawJunk |= run.RegisterPlacement(Clear()).JunkDue;
                sawJunk |= run.RegisterPlacement(Dry()).JunkDue;
            }

            Assert.IsTrue(sawJunk, "a clear-heavy player never saw junk, so pressure never rises");
        }

        [Test]
        public void JunkNeverArrivesOnTheSameBeatAsAClear()
        {
            var run = new RunProgress();
            run.Restore(stage: 6, linesThisStage: 0, totalLines: 60, score: 0,
                combo: 0, comboMisses: 0, bestCombo: 0, placementsSinceJunk: 99, placements: 0);

            Assert.IsFalse(run.RegisterPlacement(Clear()).JunkDue);
        }

        [Test]
        public void ResetReturnsAFreshRun()
        {
            var run = new RunProgress();
            for (int i = 0; i < 30; i++) run.RegisterPlacement(Clear());
            run.Reset();

            Assert.AreEqual(1, run.Stage);
            Assert.AreEqual(0, run.Score);
            Assert.AreEqual(0, run.Combo);
            Assert.AreEqual(0, run.TotalLines);
            Assert.AreEqual(0, run.Placements);
        }
    }
}
