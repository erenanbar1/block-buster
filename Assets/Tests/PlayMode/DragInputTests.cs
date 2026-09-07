using System.Collections;
using System.Collections.Generic;
using BlockBlast.Core;
using BlockBlast.Game;
using BlockBlast.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace BlockBlast.Tests
{
    /// <summary>
    /// Drives pieces through the actual uGUI drag handlers rather than calling the
    /// controller directly, so the pointer-to-board maths and the drag-layer reparenting
    /// are covered too.
    /// </summary>
    public class DragInputTests
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

        /// <summary>
        /// Screen point the finger must be at for <paramref name="view"/> to land on
        /// <paramref name="origin"/>, accounting for the lift that keeps the piece visible
        /// above the fingertip.
        /// </summary>
        Vector2 PointerForOrigin(PieceView view, Vector2Int origin)
        {
            var layer = bootstrap.DragLayer;
            var size = PieceView.FullSize(view.Shape);
            var cellOffset = new Vector2(-size.x * 0.5f + Theme.CellSize * 0.5f,
                                         -size.y * 0.5f + Theme.CellSize * 0.5f);

            Vector2 targetLocal = layer.InverseTransformPoint(Controller.BoardView.CellToWorld(origin.x, origin.y));
            Vector2 pointerLocal = targetLocal - cellOffset - new Vector2(0f, Theme.DragLift);
            Vector3 world = layer.TransformPoint(pointerLocal);
            return RectTransformUtility.WorldToScreenPoint(bootstrap.Canvas.worldCamera, world);
        }

        IEnumerator DragTo(PieceView view, Vector2Int origin)
        {
            var data = new PointerEventData(EventSystem.current) { position = PointerForOrigin(view, origin) };
            var go = view.gameObject;

            ExecuteEvents.Execute(go, data, ExecuteEvents.beginDragHandler);
            yield return null;

            // A couple of move events, the way a real finger would arrive.
            data.position = PointerForOrigin(view, origin);
            ExecuteEvents.Execute(go, data, ExecuteEvents.dragHandler);
            yield return null;
            ExecuteEvents.Execute(go, data, ExecuteEvents.dragHandler);
            yield return null;

            ExecuteEvents.Execute(go, data, ExecuteEvents.endDragHandler);
        }

        IEnumerator WaitUntilIdle()
        {
            float timeout = Time.realtimeSinceStartup + 8f;
            while (Controller.IsBusy && Time.realtimeSinceStartup < timeout) yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingAPieceOntoTheBoardPlacesItThere()
        {
            yield return Boot();

            var dot = new PieceInstance(PieceLibrary.ById("dot"), 3);
            Controller.SetTray(new List<PieceInstance> { dot }, animate: false);
            yield return null;

            var view = Controller.Tray.Pieces[0];
            var target = new Vector2Int(4, 5);
            yield return DragTo(view, target);
            yield return WaitUntilIdle();

            Assert.IsTrue(Controller.Board.IsOccupied(target.x, target.y),
                "the dragged piece should have landed on the cell under it");
            Assert.AreEqual(1, Controller.Board.OccupiedCount);
            Assert.AreEqual(3, Controller.Board.ColorAt(target.x, target.y), "colour should survive the drop");
        }

        [UnityTest]
        public IEnumerator DraggingAMultiCellPieceLandsEveryCell()
        {
            yield return Boot();

            var shape = PieceLibrary.ById("corner3_a");
            Controller.SetTray(new List<PieceInstance> { new PieceInstance(shape, 5) }, animate: false);
            yield return null;

            var origin = new Vector2Int(2, 2);
            yield return DragTo(Controller.Tray.Pieces[0], origin);
            yield return WaitUntilIdle();

            foreach (var c in shape.Cells)
                Assert.IsTrue(Controller.Board.IsOccupied(origin.x + c.x, origin.y + c.y),
                    "cell " + c + " of the piece did not land");
            Assert.AreEqual(shape.CellCount, Controller.Board.OccupiedCount);
        }

        [UnityTest]
        public IEnumerator DroppingOntoAnOccupiedCellReturnsThePieceToItsSlot()
        {
            yield return Boot();

            var dot = PieceLibrary.ById("dot");
            Controller.SetTray(new List<PieceInstance>
            {
                new PieceInstance(dot, 0), new PieceInstance(dot, 1)
            }, animate: false);
            yield return null;

            var blocked = new Vector2Int(3, 3);
            Controller.Board.SetCell(blocked.x, blocked.y, 7);
            Controller.BoardView.Refresh(Controller.Board);

            var view = Controller.Tray.Pieces[1];
            yield return DragTo(view, blocked);
            yield return WaitUntilIdle();
            yield return new WaitForSecondsRealtime(Theme.ReturnDuration + 0.1f);

            Assert.AreEqual(1, Controller.Board.OccupiedCount, "nothing should have been placed");
            Assert.IsNotNull(Controller.Tray.Pieces[1], "the piece should still be in the tray");
            Assert.AreEqual(Controller.Tray.Slot(1), Controller.Tray.Pieces[1].Rect.parent,
                "the piece should have flown back to its own slot");
            Assert.AreEqual(0, Controller.Score);
        }

        [UnityTest]
        public IEnumerator DragBeginLiftsThePieceToFullSizeOnTheDragLayer()
        {
            yield return Boot();

            Controller.SetTray(new List<PieceInstance> { new PieceInstance(PieceLibrary.ById("rect23"), 6) },
                animate: false);
            yield return null;

            var view = Controller.Tray.Pieces[0];
            var data = new PointerEventData(EventSystem.current)
            {
                position = PointerForOrigin(view, new Vector2Int(3, 3))
            };

            ExecuteEvents.Execute(view.gameObject, data, ExecuteEvents.beginDragHandler);
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.IsTrue(view.IsDragging);
            Assert.AreEqual(bootstrap.DragLayer, view.Rect.parent, "a dragged piece belongs on the drag layer");
            Assert.AreEqual(1f, view.Rect.localScale.x, 0.05f, "a dragged piece grows to board scale");

            ExecuteEvents.Execute(view.gameObject, data, ExecuteEvents.endDragHandler);
        }

        [UnityTest]
        public IEnumerator DragPreviewShowsTheLinesThatWouldPop()
        {
            yield return Boot();

            var board = Controller.Board;
            for (int x = 0; x < 7; x++) board.SetCell(x, 0, 2);
            Controller.BoardView.Refresh(board);
            Controller.SetTray(new List<PieceInstance> { new PieceInstance(PieceLibrary.ById("dot"), 4) },
                animate: false);
            yield return null;

            var view = Controller.Tray.Pieces[0];
            var data = new PointerEventData(EventSystem.current)
            {
                position = PointerForOrigin(view, new Vector2Int(7, 0))
            };
            ExecuteEvents.Execute(view.gameObject, data, ExecuteEvents.beginDragHandler);
            yield return null;

            var snap = Controller.ResolveSnap(view);
            Assert.IsTrue(snap.HasValue, "hovering a valid cell should resolve a snap");
            Assert.AreEqual(new Vector2Int(7, 0), snap.Value);

            var rows = new List<int>();
            var cols = new List<int>();
            board.PreviewClears(view.Shape, snap.Value.x, snap.Value.y, rows, cols);
            Assert.AreEqual(new[] { 0 }, rows.ToArray(), "the bottom row should be flagged as about to clear");

            ExecuteEvents.Execute(view.gameObject, data, ExecuteEvents.endDragHandler);
            yield return WaitUntilIdle();

            Assert.AreEqual(0, board.OccupiedCount, "dropping there should clear the row");
        }
    }
}
