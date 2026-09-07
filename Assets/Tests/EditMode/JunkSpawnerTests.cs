using System.Collections.Generic;
using BlockBlast.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlockBlast.Tests
{
    /// <summary>
    /// Junk is the one mechanic that can make the game feel like it is cheating, so the
    /// fairness rules get more test coverage than the mechanic itself.
    /// </summary>
    public class JunkSpawnerTests
    {
        static List<PieceShape> Tray(params string[] ids)
        {
            var list = new List<PieceShape>();
            foreach (var id in ids) list.Add(PieceLibrary.ById(id));
            return list;
        }

        [Test]
        public void SpawnsTheRequestedNumberOfBlocksOnAnOpenBoard()
        {
            var board = new BoardModel(8);
            var cells = new JunkSpawner(1).Spawn(board, Tray("dot"), 3);

            Assert.AreEqual(3, cells.Count);
            Assert.AreEqual(3, board.OccupiedCount);
            foreach (var c in cells)
                Assert.AreEqual(JunkSpawner.JunkColorIndex, board.ColorAt(c.x, c.y));
        }

        [Test]
        public void JunkUsesItsOwnColourSoItReadsAsAHazard()
        {
            Assert.AreNotEqual(BoardModel.Empty, JunkSpawner.JunkColorIndex);
            Assert.Less(JunkSpawner.JunkColorIndex, 0,
                "junk must not collide with a real palette index");
        }

        [Test]
        public void JunkCountsAsOccupied()
        {
            var board = new BoardModel(8);
            new JunkSpawner(2).Spawn(board, Tray("dot"), 1);
            Assert.AreEqual(1, board.OccupiedCount);
        }

        [Test]
        public void NeverCompletesALine()
        {
            // Row 0 has a single hole; filling it would gift the player a clear.
            var board = new BoardModel(8);
            for (int x = 0; x < 7; x++) board.SetCell(x, 0, 0);

            new JunkSpawner(3).Spawn(board, Tray("dot"), 4);

            Assert.IsFalse(board.IsRowFull(0), "junk handed the player a free line clear");
        }

        [Test]
        public void NeverLeavesTheTrayWithNoLegalMove()
        {
            // One 2x1 gap left, and the tray holds only a horizontal 2-bar.
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (!(y == 4 && (x == 2 || x == 3))) board.SetCell(x, y, 0);

            var tray = Tray("bar2h");
            Assert.IsTrue(board.HasAnyPlacement(tray[0]), "precondition: the tray can move");

            new JunkSpawner(4).Spawn(board, tray, 2);

            Assert.IsTrue(board.HasAnyPlacement(tray[0]),
                "junk took away the last legal move, which it must never do");
        }

        [Test]
        public void RollsBackEntirelyWhenNoSafeCellExists()
        {
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (!(y == 4 && (x == 2 || x == 3))) board.SetCell(x, y, 0);

            int before = board.OccupiedCount;
            var cells = new JunkSpawner(5).Spawn(board, Tray("bar2h"), 3);

            Assert.IsEmpty(cells, "no safe cell exists, so nothing should be placed");
            Assert.AreEqual(before, board.OccupiedCount, "the board must be left untouched");
        }

        [Test]
        public void DoesNothingOnAFullBoard()
        {
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    board.SetCell(x, y, 0);

            var cells = new JunkSpawner(6).Spawn(board, Tray("dot"), 2);
            Assert.IsEmpty(cells);
        }

        [Test]
        public void HugsExistingBlocksRatherThanScatteringHoles()
        {
            // A block in the corner: junk should cling to it or to the edges.
            var board = new BoardModel(8);
            board.SetCell(0, 0, 0);

            var cells = new JunkSpawner(7).Spawn(board, Tray("dot"), 1);
            Assert.AreEqual(1, cells.Count);

            var c = cells[0];
            bool touchesEdge = c.x == 0 || c.y == 0 || c.x == board.Size - 1 || c.y == board.Size - 1;
            Assert.IsTrue(touchesEdge,
                "junk landed in open space instead of thickening the existing mass");
        }

        [Test]
        public void SpawningIsDeterministicForASeed()
        {
            var a = new BoardModel(8);
            var b = new BoardModel(8);
            a.SetCell(3, 3, 0);
            b.SetCell(3, 3, 0);

            var first = new JunkSpawner(42).Spawn(a, Tray("dot"), 3);
            var second = new JunkSpawner(42).Spawn(b, Tray("dot"), 3);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i], second[i]);
        }

        [Test]
        public void RequestingZeroOrFewerDoesNothing()
        {
            var board = new BoardModel(8);
            Assert.IsEmpty(new JunkSpawner(8).Spawn(board, Tray("dot"), 0));
            Assert.IsEmpty(new JunkSpawner(8).Spawn(board, Tray("dot"), -3));
            Assert.AreEqual(0, board.OccupiedCount);
        }

        [Test]
        public void AnEmptyTrayIsNotTreatedAsUnplayable()
        {
            var board = new BoardModel(8);
            var cells = new JunkSpawner(9).Spawn(board, new List<PieceShape>(), 2);
            Assert.AreEqual(2, cells.Count, "with no tray to protect, junk should still land");
        }
    }
}
