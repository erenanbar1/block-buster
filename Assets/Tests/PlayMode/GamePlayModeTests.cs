using System.Collections;
using System.Collections.Generic;
using BlockBlast.Core;
using BlockBlast.Game;
using BlockBlast.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BlockBlast.Tests
{
    /// <summary>
    /// Boots the real game the way the scene does and drives it through the controller,
    /// so the wiring between model, views and save system is covered end to end.
    /// </summary>
    public class GamePlayModeTests
    {
        GameBootstrap bootstrap;

        GameController Controller => bootstrap.Controller;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
        }

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
            float timeout = Time.realtimeSinceStartup + 8f;
            while (Controller.IsBusy && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsFalse(Controller.IsBusy, "controller never became idle");
        }

        [UnityTest]
        public IEnumerator BootstrapBuildsAPlayableScene()
        {
            yield return Boot();

            Assert.IsNotNull(Controller, "no controller was created");
            Assert.IsNotNull(bootstrap.Canvas, "no canvas was created");
            Assert.AreEqual(Theme.BoardSize, Controller.Board.Size);
            Assert.AreEqual(0, Controller.Score);
            Assert.IsFalse(Controller.IsGameOver);
            Assert.IsTrue(Controller.Board.IsEmpty);

            int dealt = 0;
            foreach (var p in Controller.Tray.Pieces)
                if (p != null) dealt++;
            Assert.AreEqual(TrayView.SlotCount, dealt, "the tray should start with three pieces");

            Assert.IsNotNull(Object.FindAnyObjectByType<GraphicRaycaster>(), "UI cannot receive input");
            Assert.IsNotNull(Camera.main, "no camera to render with");
        }

        [UnityTest]
        public IEnumerator PlacingAPieceFillsTheBoardAndScores()
        {
            yield return Boot();

            var piece = Controller.Tray.Pieces[0];
            var origins = new List<Vector2Int>();
            Controller.Board.GetValidOrigins(piece.Shape, origins);
            Assert.IsNotEmpty(origins);

            int cells = piece.Shape.CellCount;
            Assert.IsTrue(Controller.PlaceFromSlot(0, origins[0]));
            yield return WaitUntilIdle();

            Assert.AreEqual(cells, Controller.Board.OccupiedCount);
            Assert.GreaterOrEqual(Controller.Score, cells);
            Assert.IsNull(Controller.Tray.Pieces[0], "the placed piece should have left the tray");
        }

        [UnityTest]
        public IEnumerator CompletingARowClearsItAndPaysTheLineBonus()
        {
            yield return Boot();

            var board = Controller.Board;
            for (int x = 0; x < 7; x++) board.SetCell(x, 0, 2);
            Controller.BoardView.Refresh(board);
            Controller.SetTray(new List<PieceInstance> { new PieceInstance(PieceLibrary.ById("dot"), 4) },
                animate: false);

            int scoreBefore = Controller.Score;
            Assert.IsTrue(Controller.PlaceFromSlot(0, new Vector2Int(7, 0)));
            yield return WaitUntilIdle();

            Assert.AreEqual(0, board.OccupiedCount, "the completed row should have been cleared");
            Assert.GreaterOrEqual(Controller.Score - scoreBefore, 1 + ScoreRules.LineBaseScore(1));
            Assert.AreEqual(1, Controller.Combo);
        }

        [UnityTest]
        public IEnumerator RunEndsWhenNothingInTheTrayFits()
        {
            yield return Boot();

            var board = Controller.Board;
            for (int y = 0; y < board.Size; y++)
                for (int x = 0; x < board.Size; x++)
                    if ((x + y) % 2 == 0) board.SetCell(x, y, 1); // checkerboard: only 1x1 fits
            Controller.BoardView.Refresh(board);

            var bar = PieceLibrary.ById("bar2h");
            Controller.SetTray(new List<PieceInstance>
            {
                new PieceInstance(bar, 0), new PieceInstance(bar, 1), new PieceInstance(bar, 2)
            }, animate: false);

            bool raised = false;
            Controller.GameOver += () => raised = true;
            Controller.EvaluateGameOver();
            yield return null;

            Assert.IsTrue(Controller.IsGameOver, "no piece fits, the run should be over");
            Assert.IsTrue(raised, "the GameOver event should have fired");
            Assert.IsFalse(SaveSystem.HasSavedGame, "a finished run should not be resumable");
        }

        [UnityTest]
        public IEnumerator DraggedPieceSnapsBackToTheCellItCovers()
        {
            yield return Boot();

            // Deal the hand explicitly: the spawn animation would otherwise still be
            // scaling the piece while we measure where its cells sit.
            Controller.SetTray(new List<PieceInstance> { new PieceInstance(PieceLibrary.ById("square2"), 0) },
                animate: false);
            yield return null;

            var view = Controller.Tray.Pieces[0];
            var target = new Vector2Int(2, 3);
            view.Rect.localScale = Vector3.one;
            view.SnapTo(Controller.BoardView, target);
            yield return null;

            var snap = Controller.ResolveSnap(view);
            Assert.IsTrue(snap.HasValue, "a piece sitting exactly on the grid must resolve to a cell");
            Assert.AreEqual(target, snap.Value);
        }

        [UnityTest]
        public IEnumerator PieceDroppedOffTheBoardDoesNotResolve()
        {
            yield return Boot();

            Controller.SetTray(new List<PieceInstance> { new PieceInstance(PieceLibrary.ById("square2"), 0) },
                animate: false);
            yield return null;

            var view = Controller.Tray.Pieces[0];
            view.Rect.localScale = Vector3.one;
            view.SnapTo(Controller.BoardView, new Vector2Int(0, 0));
            view.Rect.anchoredPosition += new Vector2(0f, -4000f);
            yield return null;

            Assert.IsFalse(Controller.ResolveSnap(view).HasValue);
        }

        [UnityTest]
        public IEnumerator RestartClearsTheBoardAndTheScore()
        {
            yield return Boot();

            var origins = new List<Vector2Int>();
            Controller.Board.GetValidOrigins(Controller.Tray.Pieces[0].Shape, origins);
            Controller.PlaceFromSlot(0, origins[0]);
            yield return WaitUntilIdle();
            Assert.Greater(Controller.Score, 0);

            Controller.Restart();
            float timeout = Time.realtimeSinceStartup + 8f;
            while (Controller.IsBusy && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;

            Assert.AreEqual(0, Controller.Score);
            Assert.IsTrue(Controller.Board.IsEmpty);
            Assert.IsFalse(Controller.IsGameOver);
        }

        [UnityTest]
        public IEnumerator AnInterruptedRunIsResumedOnNextLaunch()
        {
            yield return Boot();

            var origins = new List<Vector2Int>();
            Controller.Board.GetValidOrigins(Controller.Tray.Pieces[0].Shape, origins);
            Controller.PlaceFromSlot(0, origins[0]);
            yield return WaitUntilIdle();

            int score = Controller.Score;
            string board = Controller.Board.ToAscii();
            Assert.IsTrue(SaveSystem.HasSavedGame, "an in-progress run should be saved");

            Object.DestroyImmediate(bootstrap.gameObject);
            bootstrap = null;
            yield return Boot();

            Assert.AreEqual(score, Controller.Score, "score should survive a relaunch");
            Assert.AreEqual(board, Controller.Board.ToAscii(), "board should survive a relaunch");
        }

        [UnityTest]
        public IEnumerator BestScoreIsRememberedAcrossRuns()
        {
            yield return Boot();

            var origins = new List<Vector2Int>();
            Controller.Board.GetValidOrigins(Controller.Tray.Pieces[0].Shape, origins);
            Controller.PlaceFromSlot(0, origins[0]);
            yield return WaitUntilIdle();

            int score = Controller.Score;
            Assert.AreEqual(score, SaveSystem.LoadBest());

            Object.DestroyImmediate(bootstrap.gameObject);
            bootstrap = null;
            yield return Boot();

            Assert.AreEqual(score, Controller.Best);
        }
    }
}
