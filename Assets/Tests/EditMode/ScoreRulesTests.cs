using System.Collections.Generic;
using BlockBlast.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlockBlast.Tests
{
    public class ScoreRulesTests
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

        [Test]
        public void PlacementScoresOnePointPerBlock()
        {
            Assert.AreEqual(4, ScoreRules.PlacementScore(4));
            Assert.AreEqual(0, ScoreRules.PlacementScore(0));
            Assert.AreEqual(0, ScoreRules.PlacementScore(-3));
        }

        [Test]
        public void MoreLinesAtOnceIsWorthMoreThanTheSameLinesApart()
        {
            Assert.AreEqual(0, ScoreRules.LineBaseScore(0));
            Assert.AreEqual(10, ScoreRules.LineBaseScore(1));
            Assert.AreEqual(30, ScoreRules.LineBaseScore(2));
            Assert.AreEqual(60, ScoreRules.LineBaseScore(3));
            Assert.AreEqual(100, ScoreRules.LineBaseScore(4));
            Assert.Greater(ScoreRules.LineBaseScore(2), ScoreRules.LineBaseScore(1) * 2);
        }

        [Test]
        public void ComboMultiplierRampsThenCaps()
        {
            Assert.AreEqual(1, ScoreRules.ComboMultiplier(0));
            Assert.AreEqual(1, ScoreRules.ComboMultiplier(1));
            Assert.AreEqual(1, ScoreRules.ComboMultiplier(2));
            Assert.AreEqual(2, ScoreRules.ComboMultiplier(3));
            Assert.AreEqual(3, ScoreRules.ComboMultiplier(5));
            Assert.AreEqual(ScoreRules.MaxComboMultiplier, ScoreRules.ComboMultiplier(99));
        }

        // Combo sequencing moved to RunProgress, which owns the grace rule.
        // See RunProgressTests.

        [Test]
        public void StageMultiplierScalesTheWholePlacement()
        {
            var result = Result(5, 1, 0);
            int atStage1 = ScoreRules.ScoreFor(result, 1, 1);
            Assert.AreEqual(atStage1 * 4, ScoreRules.ScoreFor(result, 1, 4));
        }

        [Test]
        public void StageMultiplierIsNeverBelowOne()
        {
            var result = Result(5, 1, 0);
            int baseline = ScoreRules.ScoreFor(result, 1, 1);
            Assert.AreEqual(baseline, ScoreRules.ScoreFor(result, 1, 0));
            Assert.AreEqual(baseline, ScoreRules.ScoreFor(result, 1, -7));
        }

        [Test]
        public void MultiLineClearsGetALabelAndSingleLinesDoNot()
        {
            Assert.IsNull(ScoreRules.ClearTitle(0));
            Assert.IsNull(ScoreRules.ClearTitle(1));
            Assert.AreEqual("DOUBLE!", ScoreRules.ClearTitle(2));
            Assert.AreEqual("TRIPLE!", ScoreRules.ClearTitle(3));
            Assert.AreEqual("BLAST!", ScoreRules.ClearTitle(4));
            Assert.AreEqual("BLAST!", ScoreRules.ClearTitle(9));
        }

        [Test]
        public void ScoreForCombinesPlacementLinesAndCombo()
        {
            // 5 blocks + one line (10) at combo 3 => multiplier 2
            var result = Result(5, 1, 0);
            Assert.AreEqual(5 + 10 * 2, ScoreRules.ScoreFor(result, 3));
        }

        [Test]
        public void PerfectClearAddsItsBonus()
        {
            var plain = Result(4, 1, 0);
            var perfect = Result(4, 1, 0, perfect: true);
            Assert.AreEqual(ScoreRules.ScoreFor(plain, 1) + ScoreRules.PerfectClearBonus,
                ScoreRules.ScoreFor(perfect, 1));
        }

        [Test]
        public void PlacementWithoutClearsScoresOnlyItsBlocks()
        {
            Assert.AreEqual(4, ScoreRules.ScoreFor(Result(4, 0, 0), 0));
        }

        [Test]
        public void ScoreIsNeverNegative()
        {
            for (int combo = 0; combo < 20; combo++)
                for (int lines = 0; lines < 16; lines++)
                    Assert.GreaterOrEqual(ScoreRules.ScoreFor(Result(5, lines, 0), combo), 0);
        }
    }
}
