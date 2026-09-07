using System.Collections;
using System.Collections.Generic;
using BlockBlast.Core;
using BlockBlast.Game;
using BlockBlast.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlockBlast.Tests
{
    /// <summary>
    /// Drives the stage system through the real controller, so the wiring between
    /// RunProgress, the generator, the junk spawner and the save file is covered.
    /// </summary>
    public class ProgressionPlayModeTests
    {
        GameBootstrap bootstrap;
        GameController Controller => bootstrap.Controller;

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteAll();

        [TearDown]
        public void TearDown()
        {
            if (bootstrap != null) Object.DestroyImmediate(bootstrap.gameObject);
            bootstrap = null;
            PlayerPrefs.DeleteAll();
        }

        IEnumerator Boot()
        {
            bootstrap = new GameObject("Game").AddComponent<GameBootstrap>();
            yield return null;
            yield return null;
        }

        IEnumerator WaitUntilIdle()
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (Controller.IsBusy && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsFalse(Controller.IsBusy, "controller never became idle");
        }

        /// <summary>Fills row 0 except one cell, then drops a dot into the gap.</summary>
        IEnumerator ClearOneRow()
        {
            var board = Controller.Board;
            for (int x = 0; x < board.Size - 1; x++)
                if (!board.IsOccupied(x, 0)) board.SetCell(x, 0, 2);
            if (board.IsOccupied(board.Size - 1, 0)) board.SetCell(board.Size - 1, 0, BoardModel.Empty);
            Controller.BoardView.Refresh(board);

            Controller.SetTray(new List<PieceInstance> { new PieceInstance(PieceLibrary.ById("dot"), 4) },
                animate: false);
            yield return null;

            Assert.IsTrue(Controller.PlaceFromSlot(0, new Vector2Int(board.Size - 1, 0)));
            yield return WaitUntilIdle();
        }

        [UnityTest]
        public IEnumerator ARunStartsAtStageOne()
        {
            yield return Boot();

            Assert.AreEqual(1, Controller.Stage);
            Assert.AreEqual(0, Controller.Progress.TotalLines);
            Assert.AreEqual("Warm Up", Controller.Progress.Profile.Name);
        }

        [UnityTest]
        public IEnumerator ClearingEnoughLinesAdvancesTheStage()
        {
            yield return Boot();

            int needed = DifficultyCurve.ForStage(1).LinesToAdvance;
            for (int i = 0; i < needed; i++) yield return ClearOneRow();

            Assert.AreEqual(2, Controller.Stage, "the stage did not advance after a full stage of lines");
            Assert.AreEqual(needed, Controller.Progress.TotalLines);
        }

        [UnityTest]
        public IEnumerator ScoreGrowsFasterOnceStagesStack()
        {
            yield return Boot();

            yield return ClearOneRow();
            int firstClear = Controller.Score;

            // Push to a later stage, then clear an identical row.
            Controller.Progress.Restore(stage: 5, linesThisStage: 0, totalLines: 40,
                score: Controller.Score, combo: 0, comboMisses: 0, bestCombo: 0,
                placementsSinceJunk: 0, placements: 10);

            int before = Controller.Score;
            yield return ClearOneRow();
            int lateClear = Controller.Score - before;

            Assert.Greater(lateClear, firstClear,
                "the same clear should pay more at a later stage");
        }

        [UnityTest]
        public IEnumerator JunkLandsOnTheBoardAndIsMarkedAsJunk()
        {
            yield return Boot();

            // Sit one placement away from a junk drop at a stage that spawns it.
            var profile = DifficultyCurve.ForStage(4);
            Assert.IsTrue(profile.SpawnsJunk, "precondition: stage 4 seeds junk");

            Controller.Progress.Restore(stage: 4, linesThisStage: 0, totalLines: 30, score: 0,
                combo: 0, comboMisses: 0, bestCombo: 0,
                placementsSinceJunk: profile.PlacementsPerJunk - 1, placements: 20);

            var dot = PieceLibrary.ById("dot");
            Controller.SetTray(new List<PieceInstance>
            {
                new PieceInstance(dot, 0), new PieceInstance(dot, 1), new PieceInstance(dot, 2)
            }, animate: false);
            yield return null;

            // A placement that clears nothing, so the drop is not deferred.
            Assert.IsTrue(Controller.PlaceFromSlot(0, new Vector2Int(4, 4)));
            yield return WaitUntilIdle();

            int junkCells = 0;
            var board = Controller.Board;
            for (int y = 0; y < board.Size; y++)
                for (int x = 0; x < board.Size; x++)
                    if (board.ColorAt(x, y) == JunkSpawner.JunkColorIndex) junkCells++;

            Assert.Greater(junkCells, 0, "no junk was seeded when it was due");
        }

        [UnityTest]
        public IEnumerator JunkNeverEndsTheRunOnTheSpot()
        {
            yield return Boot();

            var profile = DifficultyCurve.ForStage(6);
            Controller.Progress.Restore(stage: 6, linesThisStage: 0, totalLines: 60, score: 0,
                combo: 0, comboMisses: 0, bestCombo: 0,
                placementsSinceJunk: profile.PlacementsPerJunk - 1, placements: 40);

            var dot = PieceLibrary.ById("dot");
            Controller.SetTray(new List<PieceInstance>
            {
                new PieceInstance(dot, 0), new PieceInstance(dot, 1), new PieceInstance(dot, 2)
            }, animate: false);
            yield return null;

            Assert.IsTrue(Controller.PlaceFromSlot(0, new Vector2Int(4, 4)));
            yield return WaitUntilIdle();

            Assert.IsFalse(Controller.IsGameOver,
                "a junk drop ended the run, which it must never do on its own");
            Assert.IsTrue(Controller.Board.HasAnyPlacement(Controller.Tray.RemainingShapes()),
                "junk left the tray with no legal move");
        }

        [UnityTest]
        public IEnumerator StageSurvivesARelaunch()
        {
            yield return Boot();

            int needed = DifficultyCurve.ForStage(1).LinesToAdvance;
            for (int i = 0; i < needed; i++) yield return ClearOneRow();

            int stage = Controller.Stage;
            int totalLines = Controller.Progress.TotalLines;
            Assert.AreEqual(2, stage);

            Object.DestroyImmediate(bootstrap.gameObject);
            bootstrap = null;
            yield return Boot();

            Assert.AreEqual(stage, Controller.Stage, "stage was lost across a relaunch");
            Assert.AreEqual(totalLines, Controller.Progress.TotalLines);
        }

        [UnityTest]
        public IEnumerator RestartReturnsToStageOne()
        {
            yield return Boot();

            int needed = DifficultyCurve.ForStage(1).LinesToAdvance;
            for (int i = 0; i < needed; i++) yield return ClearOneRow();
            Assert.AreEqual(2, Controller.Stage);

            Controller.Restart();
            float timeout = Time.realtimeSinceStartup + 10f;
            while (Controller.IsBusy && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;

            Assert.AreEqual(1, Controller.Stage);
            Assert.AreEqual(0, Controller.Progress.TotalLines);
            Assert.AreEqual(0, Controller.Score);
        }
    }
}
