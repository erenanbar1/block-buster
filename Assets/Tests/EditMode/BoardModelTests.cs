using System.Collections.Generic;
using BlockBlast.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlockBlast.Tests
{
    public class BoardModelTests
    {
        static PieceShape Dot => PieceLibrary.ById("dot");
        static PieceShape Bar2H => PieceLibrary.ById("bar2h");
        static PieceShape Square2 => PieceLibrary.ById("square2");

        [Test]
        public void NewBoardIsEmpty()
        {
            var board = new BoardModel(8);
            Assert.AreEqual(64, board.CellCount);
            Assert.AreEqual(0, board.OccupiedCount);
            Assert.IsTrue(board.IsEmpty);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    Assert.IsFalse(board.IsOccupied(x, y));
        }

        [Test]
        public void PlacingWritesEveryCellOfTheShape()
        {
            var board = new BoardModel(8);
            var result = board.Place(Square2, 2, 3, 5);

            Assert.AreEqual(4, result.PlacedCells);
            Assert.AreEqual(4, board.OccupiedCount);
            Assert.AreEqual(5, board.ColorAt(2, 3));
            Assert.AreEqual(5, board.ColorAt(3, 4));
            Assert.AreEqual(0, result.LinesCleared);
        }

        [Test]
        public void CannotPlaceOverlappingOrOutOfBounds()
        {
            var board = new BoardModel(8);
            board.Place(Square2, 0, 0, 0);

            Assert.IsFalse(board.CanPlace(Dot, 0, 0), "overlaps an occupied cell");
            Assert.IsFalse(board.CanPlace(Dot, 8, 0), "off the right edge");
            Assert.IsFalse(board.CanPlace(Dot, -1, 0), "off the left edge");
            Assert.IsFalse(board.CanPlace(PieceLibrary.ById("bar5h"), 4, 0), "hangs over the edge");
            Assert.IsTrue(board.CanPlace(Dot, 2, 0));
        }

        [Test]
        public void PlacingOnAnOccupiedCellThrows()
        {
            var board = new BoardModel(8);
            board.Place(Dot, 0, 0, 0);
            Assert.Throws<System.InvalidOperationException>(() => board.Place(Dot, 0, 0, 1));
        }

        [Test]
        public void CompletingARowClearsIt()
        {
            var board = BoardModel.Parse(
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "#######.");

            var result = board.Place(Dot, 7, 0, 3);

            Assert.AreEqual(1, result.LinesCleared);
            Assert.AreEqual(1, result.ClearedRows.Count);
            Assert.AreEqual(0, result.ClearedRows[0]);
            Assert.AreEqual(8, result.ClearedCellCount);
            Assert.AreEqual(0, board.OccupiedCount);
        }

        [Test]
        public void CompletingAColumnClearsIt()
        {
            var board = new BoardModel(8);
            for (int y = 1; y < 8; y++) board.SetCell(3, y, 0);

            var result = board.Place(Dot, 3, 0, 2);

            Assert.AreEqual(1, result.ClearedColumns.Count);
            Assert.AreEqual(3, result.ClearedColumns[0]);
            Assert.AreEqual(0, board.OccupiedCount);
        }

        [Test]
        public void RowAndColumnClearedTogetherCountSharedCellOnce()
        {
            var board = new BoardModel(8);
            for (int x = 1; x < 8; x++) board.SetCell(x, 0, 0);   // row 0 missing (0,0)
            for (int y = 1; y < 8; y++) board.SetCell(0, y, 0);   // column 0 missing (0,0)

            var result = board.Place(Dot, 0, 0, 1);

            Assert.AreEqual(2, result.LinesCleared);
            Assert.AreEqual(15, result.ClearedCellCount, "8 + 8 minus the shared corner");
            Assert.AreEqual(0, board.OccupiedCount);
            Assert.IsTrue(result.PerfectClear);
        }

        [Test]
        public void PerfectClearOnlyWhenTheBoardEndsEmpty()
        {
            var board = new BoardModel(8);
            for (int x = 0; x < 7; x++) board.SetCell(x, 0, 0);
            board.SetCell(4, 4, 0); // stray block survives the clear

            var result = board.Place(Dot, 7, 0, 0);

            Assert.AreEqual(1, result.LinesCleared);
            Assert.IsFalse(result.PerfectClear);
            Assert.AreEqual(1, board.OccupiedCount);
        }

        [Test]
        public void HasAnyPlacementDetectsADeadBoard()
        {
            // Checkerboard: no piece bigger than 1x1 fits anywhere.
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if ((x + y) % 2 == 0) board.SetCell(x, y, 0);

            Assert.IsTrue(board.HasAnyPlacement(Dot));
            Assert.IsFalse(board.HasAnyPlacement(Bar2H));
            Assert.IsFalse(board.HasAnyPlacement(Square2));
        }

        [Test]
        public void FullBoardHasNoPlacementAtAll()
        {
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    board.SetCell(x, y, 0);

            foreach (var shape in PieceLibrary.All)
                Assert.IsFalse(board.HasAnyPlacement(shape), shape.Id + " should not fit on a full board");
        }

        [Test]
        public void ValidOriginsMatchesBruteForce()
        {
            var board = new BoardModel(8);
            board.Place(Square2, 3, 3, 0);
            var origins = new List<Vector2Int>();
            board.GetValidOrigins(Bar2H, origins);

            int expected = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 7; x++)
                    if (!board.IsOccupied(x, y) && !board.IsOccupied(x + 1, y)) expected++;

            Assert.AreEqual(expected, origins.Count);
            foreach (var o in origins) Assert.IsTrue(board.CanPlace(Bar2H, o));
        }

        [Test]
        public void PreviewClearsPredictsTheRealClear()
        {
            var board = BoardModel.Parse(
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "######..");

            var rows = new List<int>();
            var cols = new List<int>();
            board.PreviewClears(Bar2H, 6, 0, rows, cols);

            Assert.AreEqual(new[] { 0 }, rows.ToArray());
            Assert.IsEmpty(cols);

            var result = board.Place(Bar2H, 6, 0, 0);
            Assert.AreEqual(1, result.ClearedRows.Count);
            Assert.AreEqual(0, result.ClearedRows[0]);
        }

        [Test]
        public void PreviewClearsDoesNotMutateTheBoard()
        {
            var board = new BoardModel(8);
            for (int x = 0; x < 7; x++) board.SetCell(x, 0, 0);
            var before = board.ToAscii();

            var rows = new List<int>();
            var cols = new List<int>();
            board.PreviewClears(Dot, 7, 0, rows, cols);

            Assert.AreEqual(before, board.ToAscii());
            Assert.AreEqual(7, board.OccupiedCount);
        }

        [Test]
        public void SnapshotAndRestoreRoundTrip()
        {
            var board = new BoardModel(8);
            board.Place(Square2, 1, 1, 4);
            board.Place(Bar2H, 5, 5, 2);
            var snapshot = board.Snapshot();
            string ascii = board.ToAscii();
            int occupied = board.OccupiedCount;

            board.Clear();
            Assert.AreEqual(0, board.OccupiedCount);

            board.Restore(snapshot);
            Assert.AreEqual(ascii, board.ToAscii());
            Assert.AreEqual(occupied, board.OccupiedCount);
            Assert.AreEqual(4, board.ColorAt(1, 1));
            Assert.AreEqual(2, board.ColorAt(5, 5));
        }

        [Test]
        public void ParseReadsTopRowFirst()
        {
            var board = BoardModel.Parse(
                "#.......",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                ".......#");

            Assert.IsTrue(board.IsOccupied(0, 7), "first ascii row is the top of the board");
            Assert.IsTrue(board.IsOccupied(7, 0));
            Assert.AreEqual(2, board.OccupiedCount);
        }

        [Test]
        public void OccupiedCountTracksClearingCells()
        {
            var board = new BoardModel(8);
            board.SetCell(0, 0, 3);
            Assert.AreEqual(1, board.OccupiedCount);
            board.SetCell(0, 0, 4);
            Assert.AreEqual(1, board.OccupiedCount, "recolouring is not a new block");
            board.SetCell(0, 0, BoardModel.Empty);
            Assert.AreEqual(0, board.OccupiedCount);
        }
    }
}
