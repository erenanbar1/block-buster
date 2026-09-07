using System.Collections.Generic;
using BlockBlast.Core;
using NUnit.Framework;
using UnityEngine;

namespace BlockBlast.Tests
{
    /// <summary>
    /// Plays complete games headlessly with a simple greedy bot. This is the closest thing
    /// to a soak test the rules can get: it exercises generator, board and scoring together
    /// over tens of thousands of placements and asserts the invariants hold throughout.
    /// </summary>
    public class GameSimulationTests
    {
        class GameRun
        {
            public int Score;
            public int Moves;
            public int Clears;
            public int BestCombo;
        }

        [Test]
        public void SimulatedGamesTerminateAndKeepTheirInvariants()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var run = Play(seed, verify: true);
                Assert.Greater(run.Moves, 0, "seed " + seed + " ended before a single move");
                Assert.GreaterOrEqual(run.Score, run.Moves, "every placement scores at least one point per block");
            }
        }

        [Test]
        public void GreedyBotSurvivesAReasonableNumberOfMoves()
        {
            var lengths = new List<int>();
            var scores = new List<int>();
            for (int seed = 0; seed < 60; seed++)
            {
                var run = Play(seed, verify: false);
                lengths.Add(run.Moves);
                scores.Add(run.Score);
            }
            lengths.Sort();
            scores.Sort();
            int medianMoves = lengths[lengths.Count / 2];
            int medianScore = scores[scores.Count / 2];

            Debug.Log($"[sim] median moves {medianMoves}, median score {medianScore}, " +
                      $"longest {lengths[lengths.Count - 1]}, best {scores[scores.Count - 1]}");

            Assert.Greater(medianMoves, 25,
                "a competent player should get well past a couple of trays before dying");
        }

        [Test]
        public void NoCompletedLineIsEverLeftStanding()
        {
            var board = new BoardModel(8);
            var gen = new PieceGenerator(2024);
            var origins = new List<Vector2Int>();

            for (int move = 0; move < 4000; move++)
            {
                var trio = gen.NextTrio(board);
                bool placedAny = false;
                foreach (var piece in trio)
                {
                    board.GetValidOrigins(piece.Shape, origins);
                    if (origins.Count == 0) continue;
                    board.Place(piece.Shape, origins[move % origins.Count], piece.ColorIndex);
                    placedAny = true;

                    for (int i = 0; i < board.Size; i++)
                    {
                        Assert.IsFalse(board.IsRowFull(i), "row " + i + " survived a placement");
                        Assert.IsFalse(board.IsColumnFull(i), "column " + i + " survived a placement");
                    }
                }
                if (!placedAny) board.Clear();
            }
        }

        [Test]
        public void OccupiedCountAlwaysMatchesTheGrid()
        {
            var board = new BoardModel(8);
            var gen = new PieceGenerator(77);
            var origins = new List<Vector2Int>();

            for (int move = 0; move < 2000; move++)
            {
                var trio = gen.NextTrio(board);
                bool placedAny = false;
                foreach (var piece in trio)
                {
                    board.GetValidOrigins(piece.Shape, origins);
                    if (origins.Count == 0) continue;
                    board.Place(piece.Shape, origins[0], piece.ColorIndex);
                    placedAny = true;
                    Assert.AreEqual(CountOccupied(board), board.OccupiedCount);
                }
                if (!placedAny) board.Clear();
            }
        }

        static int CountOccupied(BoardModel board)
        {
            int n = 0;
            for (int y = 0; y < board.Size; y++)
                for (int x = 0; x < board.Size; x++)
                    if (board.IsOccupied(x, y)) n++;
            return n;
        }

        // ---- the bot -----------------------------------------------------------------

        static GameRun Play(int seed, bool verify)
        {
            var board = new BoardModel(8);
            var gen = new PieceGenerator(seed);
            var run = new GameRun();
            int combo = 0;
            var origins = new List<Vector2Int>();

            for (int tray = 0; tray < 500; tray++)
            {
                var pieces = new List<PieceInstance>(gen.NextTrio(board));

                while (pieces.Count > 0)
                {
                    int bestPiece = -1;
                    var bestOrigin = Vector2Int.zero;
                    float bestValue = float.NegativeInfinity;

                    for (int i = 0; i < pieces.Count; i++)
                    {
                        board.GetValidOrigins(pieces[i].Shape, origins);
                        foreach (var origin in origins)
                        {
                            float value = Evaluate(board, pieces[i].Shape, origin);
                            if (value <= bestValue) continue;
                            bestValue = value;
                            bestPiece = i;
                            bestOrigin = origin;
                        }
                    }

                    if (bestPiece < 0) return run; // nothing in the tray fits: game over

                    var chosen = pieces[bestPiece];
                    int before = board.OccupiedCount;
                    var result = board.Place(chosen.Shape, bestOrigin, chosen.ColorIndex);
                    pieces.RemoveAt(bestPiece);

                    combo = ScoreRules.NextCombo(combo, result);
                    run.Score += ScoreRules.ScoreFor(result, combo);
                    run.Moves++;
                    if (result.LinesCleared > 0) run.Clears++;
                    run.BestCombo = Mathf.Max(run.BestCombo, combo);

                    if (verify)
                    {
                        Assert.AreEqual(before + chosen.Shape.CellCount - result.ClearedCellCount,
                            board.OccupiedCount, "occupancy drifted after a placement");
                        Assert.AreEqual(CountOccupied(board), board.OccupiedCount);
                        for (int i = 0; i < board.Size; i++)
                        {
                            Assert.IsFalse(board.IsRowFull(i));
                            Assert.IsFalse(board.IsColumnFull(i));
                        }
                    }
                }
            }
            return run;
        }

        /// <summary>Prefer clearing lines, then keeping the board flat and hole-free.</summary>
        static float Evaluate(BoardModel board, PieceShape shape, Vector2Int origin)
        {
            var probe = board.Clone();
            var result = probe.Place(shape, origin, 0);

            float value = result.LinesCleared * 260f;
            value -= probe.OccupiedCount * 1.4f;
            value -= IsolatedHoles(probe) * 12f;
            value += EdgeContact(board, shape, origin) * 2.5f;
            return value;
        }

        /// <summary>Empty cells with no empty neighbour: dead space that needs a 1x1 to fix.</summary>
        static int IsolatedHoles(BoardModel board)
        {
            int holes = 0;
            for (int y = 0; y < board.Size; y++)
                for (int x = 0; x < board.Size; x++)
                {
                    if (board.IsOccupied(x, y)) continue;
                    bool free = (board.InBounds(x - 1, y) && !board.IsOccupied(x - 1, y))
                             || (board.InBounds(x + 1, y) && !board.IsOccupied(x + 1, y))
                             || (board.InBounds(x, y - 1) && !board.IsOccupied(x, y - 1))
                             || (board.InBounds(x, y + 1) && !board.IsOccupied(x, y + 1));
                    if (!free) holes++;
                }
            return holes;
        }

        /// <summary>How much of the piece touches existing blocks or the board edge.</summary>
        static int EdgeContact(BoardModel board, PieceShape shape, Vector2Int origin)
        {
            int contact = 0;
            foreach (var c in shape.Cells)
            {
                int x = origin.x + c.x, y = origin.y + c.y;
                contact += Neighbour(board, shape, origin, x - 1, y);
                contact += Neighbour(board, shape, origin, x + 1, y);
                contact += Neighbour(board, shape, origin, x, y - 1);
                contact += Neighbour(board, shape, origin, x, y + 1);
            }
            return contact;
        }

        static int Neighbour(BoardModel board, PieceShape shape, Vector2Int origin, int x, int y)
        {
            if (!board.InBounds(x, y)) return 1;                                  // wall
            if (shape.Contains(x - origin.x, y - origin.y)) return 0;             // own body
            return board.IsOccupied(x, y) ? 1 : 0;
        }
    }
}
