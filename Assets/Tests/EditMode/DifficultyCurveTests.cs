using System.Collections.Generic;
using BlockBlast.Core;
using NUnit.Framework;

namespace BlockBlast.Tests
{
    public class DifficultyCurveTests
    {
        [Test]
        public void EveryStageIsPlayablyDefined()
        {
            for (int stage = 1; stage <= 20; stage++)
            {
                var p = DifficultyCurve.ForStage(stage);
                Assert.AreEqual(stage, p.Stage);
                Assert.IsNotEmpty(p.Name, "stage " + stage + " needs a name for the HUD");
                Assert.Greater(p.LinesToAdvance, 0, "stage " + stage + " must be completable");
                Assert.GreaterOrEqual(p.ScoreMultiplier, 1);
            }
        }

        [Test]
        public void StageBelowOneClampsToTheFirstStage()
        {
            Assert.AreEqual(1, DifficultyCurve.ForStage(0).Stage);
            Assert.AreEqual(1, DifficultyCurve.ForStage(-5).Stage);
        }

        [Test]
        public void DifficultyOnlyEverRises()
        {
            var previous = DifficultyCurve.ForStage(1);
            for (int stage = 2; stage <= 20; stage++)
            {
                var p = DifficultyCurve.ForStage(stage);
                Assert.GreaterOrEqual((int)p.MaxTier, (int)previous.MaxTier,
                    "stage " + stage + " unlocked fewer shapes than the stage before");
                Assert.GreaterOrEqual(p.BigPieceBias, previous.BigPieceBias,
                    "stage " + stage + " deals smaller pieces than the stage before");
                Assert.GreaterOrEqual(p.ScoreMultiplier, previous.ScoreMultiplier);
                Assert.GreaterOrEqual(p.LinesToAdvance, previous.LinesToAdvance);
                previous = p;
            }
        }

        [Test]
        public void TheOpeningStagesAreGentle()
        {
            var first = DifficultyCurve.ForStage(1);
            Assert.AreEqual(PieceTier.Basic, first.MaxTier, "stage 1 should only deal friendly shapes");
            Assert.IsFalse(first.SpawnsJunk, "the opening must not seed junk");
            Assert.IsFalse(DifficultyCurve.ForStage(2).SpawnsJunk,
                "junk should arrive only once the player has settled in");
        }

        [Test]
        public void PressureArrivesByTheMiddleStages()
        {
            Assert.IsTrue(DifficultyCurve.ForStage(3).SpawnsJunk,
                "by stage 3 the board should start filling on its own");
            Assert.AreEqual(PieceTier.Nasty, DifficultyCurve.ForStage(5).MaxTier,
                "the meanest shapes should be in play by stage 5");
        }

        [Test]
        public void JunkPressureRisesThenCapsSoItNeverBecomesAbsurd()
        {
            int previousBlocks = 0;
            for (int stage = 3; stage <= 30; stage++)
            {
                var p = DifficultyCurve.ForStage(stage);
                Assert.GreaterOrEqual(p.JunkBlocks, previousBlocks);
                Assert.LessOrEqual(p.JunkBlocks, 3, "junk per drop must stay survivable");
                Assert.GreaterOrEqual(p.PlacementsPerJunk, 5, "junk must never arrive every move");
                previousBlocks = p.JunkBlocks;
            }
        }

        [Test]
        public void EveryUnlockedTierActuallyHasShapes()
        {
            foreach (PieceTier tier in System.Enum.GetValues(typeof(PieceTier)))
            {
                var pool = PieceLibrary.UpToTier(tier);
                Assert.IsNotEmpty(pool, tier + " unlocks no shapes at all");
            }
        }

        [Test]
        public void EachTierAddsSomethingNew()
        {
            int previous = 0;
            foreach (PieceTier tier in System.Enum.GetValues(typeof(PieceTier)))
            {
                int count = PieceLibrary.UpToTier(tier).Count;
                Assert.Greater(count, previous, tier + " added no new shapes over the tier below");
                previous = count;
            }
            Assert.AreEqual(PieceLibrary.All.Count, previous, "the top tier should unlock everything");
        }

        [Test]
        public void EveryShapeInTheOpeningPoolFitsAnEmptyBoardEasily()
        {
            var board = new BoardModel(8);
            foreach (var shape in PieceLibrary.UpToTier(PieceTier.Basic))
            {
                Assert.LessOrEqual(shape.CellCount, 4, shape.Id + " is too chunky for the opening pool");
                Assert.IsTrue(board.HasAnyPlacement(shape));
            }
        }
    }

    public class StageAwareGeneratorTests
    {
        [Test]
        public void EarlyStagesOnlyDealUnlockedTiers()
        {
            var board = new BoardModel(8);
            var gen = new PieceGenerator(11);
            var profile = DifficultyCurve.ForStage(1);

            for (int i = 0; i < 200; i++)
                foreach (var piece in gen.NextTrio(board, profile, 3))
                    Assert.LessOrEqual((int)piece.Shape.Tier, (int)profile.MaxTier,
                        piece.Shape.Id + " should not appear at stage 1");
        }

        [Test]
        public void LaterStagesDealBulkierPiecesOnAverage()
        {
            float early = AverageBulk(DifficultyCurve.ForStage(1));
            float late = AverageBulk(DifficultyCurve.ForStage(6));

            Assert.Greater(late, early,
                "the curve is inverted if late stages are not meaner than early ones");
        }

        static float AverageBulk(StageProfile profile)
        {
            var board = new BoardModel(8);
            var gen = new PieceGenerator(7);
            int total = 0, count = 0;
            for (int i = 0; i < 400; i++)
                foreach (var piece in gen.NextTrio(board, profile, 3))
                {
                    total += piece.Shape.CellCount;
                    count++;
                }
            return (float)total / count;
        }

        [Test]
        public void ANearlyFullBoardStillGetsMercy()
        {
            // Packed board, hardest stage: the mercy valve should pull piece size back down.
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (!(x >= 6 && y >= 6)) board.SetCell(x, y, 0);

            var gen = new PieceGenerator(3);
            var profile = DifficultyCurve.ForStage(6);

            int total = 0, count = 0;
            for (int i = 0; i < 200; i++)
                foreach (var piece in gen.NextTrio(board, profile, 3))
                {
                    total += piece.Shape.CellCount;
                    count++;
                }

            Assert.Less((float)total / count, 4f,
                "a packed board should not be handed huge shapes, even at the hardest stage");
        }

        [Test]
        public void TrioStaysPlayableAtEveryStage()
        {
            var board = new BoardModel(8);
            for (int stage = 1; stage <= 10; stage++)
            {
                var profile = DifficultyCurve.ForStage(stage);
                var gen = new PieceGenerator(stage * 13);
                for (int i = 0; i < 50; i++)
                {
                    var trio = gen.NextTrio(board, profile, 3);
                    bool any = false;
                    foreach (var p in trio) any |= board.HasAnyPlacement(p.Shape);
                    Assert.IsTrue(any, "stage " + stage + " dealt an unplayable trio on an empty board");
                }
            }
        }
    }
}
