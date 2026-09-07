using System.Collections.Generic;
using BlockBlast.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlockBlast.Tests
{
    public class PieceShapeTests
    {
        [Test]
        public void FromRowsPutsTheFirstRowAtTheTop()
        {
            var shape = PieceShape.FromRows("test", 1, "#.", "##");

            Assert.AreEqual(2, shape.Width);
            Assert.AreEqual(2, shape.Height);
            Assert.AreEqual(3, shape.CellCount);
            Assert.IsTrue(shape.Contains(0, 1), "top-left cell sits at y = 1");
            Assert.IsTrue(shape.Contains(0, 0));
            Assert.IsTrue(shape.Contains(1, 0));
            Assert.IsFalse(shape.Contains(1, 1));
        }

        [Test]
        public void CellsAreNormalisedToTheOrigin()
        {
            var shape = new PieceShape("offset", 1,
                new Vector2Int(5, 7), new Vector2Int(6, 7), new Vector2Int(5, 8));

            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in shape.Cells)
            {
                minX = Mathf.Min(minX, c.x);
                minY = Mathf.Min(minY, c.y);
            }
            Assert.AreEqual(0, minX);
            Assert.AreEqual(0, minY);
            Assert.AreEqual(2, shape.Width);
            Assert.AreEqual(2, shape.Height);
        }

        [Test]
        public void DuplicateCellsAreRejected()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new PieceShape("dupe", 1, new Vector2Int(0, 0), new Vector2Int(0, 0)));
        }

        [Test]
        public void EmptyShapeIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => new PieceShape("empty", 1));
        }
    }

    public class PieceLibraryTests
    {
        [Test]
        public void LibraryIsPopulatedAndWeighted()
        {
            Assert.Greater(PieceLibrary.All.Count, 30);
            Assert.Greater(PieceLibrary.TotalWeight, 0);
        }

        [Test]
        public void EveryShapeIdIsUnique()
        {
            var seen = new HashSet<string>();
            foreach (var s in PieceLibrary.All)
                Assert.IsTrue(seen.Add(s.Id), "duplicate piece id: " + s.Id);
        }

        [Test]
        public void EveryShapeFitsOnAnEmptyBoard()
        {
            var board = new BoardModel(8);
            foreach (var s in PieceLibrary.All)
            {
                Assert.LessOrEqual(s.Width, 8, s.Id + " is too wide");
                Assert.LessOrEqual(s.Height, 8, s.Id + " is too tall");
                Assert.IsTrue(board.HasAnyPlacement(s), s.Id + " does not fit an empty board");
            }
        }

        [Test]
        public void LookupByIdWorks()
        {
            Assert.IsNotNull(PieceLibrary.ById("square2"));
            Assert.IsNull(PieceLibrary.ById("no-such-piece"));
        }
    }

    public class PieceGeneratorTests
    {
        [Test]
        public void TrioAlwaysContainsAPlayablePieceOnAnEmptyBoard()
        {
            var board = new BoardModel(8);
            var gen = new PieceGenerator(1234);
            for (int i = 0; i < 200; i++)
            {
                var trio = gen.NextTrio(board);
                Assert.AreEqual(3, trio.Length);
                foreach (var p in trio) Assert.IsNotNull(p);
                Assert.IsTrue(AnyFits(board, trio));
            }
        }

        [Test]
        public void TrioStaysPlayableOnACrowdedBoard()
        {
            // Only a handful of scattered holes left.
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (!(x == 1 && y == 1) && !(x == 6 && y == 6) && !(x == 3 && y == 5))
                        board.SetCell(x, y, 0);

            var gen = new PieceGenerator(99);
            for (int i = 0; i < 100; i++)
            {
                var trio = gen.NextTrio(board);
                Assert.IsTrue(AnyFits(board, trio), "generator dealt an unplayable trio while holes remain");
            }
        }

        [Test]
        public void TrioOnADeadBoardStillReturnsThreePieces()
        {
            var board = new BoardModel(8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    board.SetCell(x, y, 0);

            var trio = new PieceGenerator(7).NextTrio(board);
            Assert.AreEqual(3, trio.Length);
            foreach (var p in trio) Assert.IsNotNull(p);
            Assert.IsFalse(AnyFits(board, trio), "nothing can fit, and that is a real game over");
        }

        [Test]
        public void SameSeedProducesTheSameDeal()
        {
            var a = new PieceGenerator(4242).NextTrio(new BoardModel(8));
            var b = new PieceGenerator(4242).NextTrio(new BoardModel(8));
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Shape.Id, b[i].Shape.Id);
                Assert.AreEqual(a[i].ColorIndex, b[i].ColorIndex);
            }
        }

        [Test]
        public void ColoursStayInsidePaletteRange()
        {
            var board = new BoardModel(8);
            var gen = new PieceGenerator(5, 8);
            for (int i = 0; i < 100; i++)
                foreach (var p in gen.NextTrio(board))
                {
                    Assert.GreaterOrEqual(p.ColorIndex, 0);
                    Assert.Less(p.ColorIndex, 8);
                }
        }

        [Test]
        public void CrowdedBoardsFavourSmallerPieces()
        {
            var empty = new BoardModel(8);
            var crowded = new BoardModel(8);
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 8; x++)
                    if ((x * 3 + y * 5) % 4 != 0) crowded.SetCell(x, y, 0);

            Assert.Greater(AverageCells(empty, 1), AverageCells(crowded, 1),
                "pieces dealt on a packed board should be smaller on average");
        }

        static float AverageCells(BoardModel board, int seed)
        {
            var gen = new PieceGenerator(seed);
            int total = 0, count = 0;
            for (int i = 0; i < 400; i++)
                foreach (var p in gen.NextTrio(board))
                {
                    total += p.Shape.CellCount;
                    count++;
                }
            return (float)total / count;
        }

        static bool AnyFits(BoardModel board, PieceInstance[] trio)
        {
            foreach (var p in trio)
                if (board.HasAnyPlacement(p.Shape)) return true;
            return false;
        }
    }
}
