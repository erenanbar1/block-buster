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
            public int Stage = 1;
            public int JunkDropped;
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

        /// <summary>
        /// The tuning instrument for the whole difficulty curve. Plays 60 complete games
        /// and reports the shape of a typical run, so balance is measured rather than
        /// guessed. Target: a run that builds to a crescendo and ends, roughly 5-10
        /// minutes of play.
        /// </summary>
        [Test]
        public void RunsBuildToACrescendoAndEnd()
        {
            var lengths = new List<int>();
            var scores = new List<int>();
            var stages = new List<int>();
            var combos = new List<int>();
            int totalJunk = 0;

            for (int seed = 0; seed < 60; seed++)
            {
                var run = Play(seed, verify: false);
                lengths.Add(run.Moves);
                scores.Add(run.Score);
                stages.Add(run.Stage);
                combos.Add(run.BestCombo);
                totalJunk += run.JunkDropped;
            }
            lengths.Sort();
            scores.Sort();
            stages.Sort();
            combos.Sort();

            int mid = lengths.Count / 2;
            int medianMoves = lengths[mid];
            int medianScore = scores[mid];
            int medianStage = stages[mid];
            int medianCombo = combos[mid];

            Debug.Log($"[sim] median moves {medianMoves}, score {medianScore}, " +
                      $"stage {medianStage}, best combo {medianCombo} | " +
                      $"longest {lengths[lengths.Count - 1]}, top score {scores[scores.Count - 1]}, " +
                      $"deepest stage {stages[stages.Count - 1]}, junk dropped {totalJunk}");

            Assert.Greater(medianMoves, 30,
                "runs are ending too early to feel like a game");
            Assert.Less(medianMoves, 400,
                "runs never build to a crescendo - the pressure curve is too flat");
            Assert.GreaterOrEqual(medianStage, 3,
                "a typical run should get past the opening stages");
            Assert.GreaterOrEqual(medianCombo, 3,
                "combos are the main source of drama and should be reachable in a normal run");
            Assert.Greater(totalJunk, 0, "junk pressure never fired across 60 runs");
        }

        [Test]
        public void EveryRunTerminates()
        {
            for (int seed = 100; seed < 130; seed++)
            {
                var run = Play(seed, verify: false);
                Assert.Less(run.Moves, 1500, "seed " + seed + " never ended");
                Assert.Greater(run.Moves, 0);
            }
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
            var junk = new JunkSpawner(seed);
            var progress = new RunProgress();
            var run = new GameRun();
            var origins = new List<Vector2Int>();

            for (int tray = 0; tray < 500; tray++)
            {
                var pieces = new List<PieceInstance>(gen.NextTrio(board, progress.Profile, 3));

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

                    int stageBefore = progress.Stage;
                    var events = progress.RegisterPlacement(result);

                    run.Score = progress.Score;
                    run.Moves++;
                    if (result.LinesCleared > 0) run.Clears++;
                    run.BestCombo = Mathf.Max(run.BestCombo, progress.Combo);
                    run.Stage = progress.Stage;

                    if (verify)
                    {
                        Assert.AreEqual(before + chosen.Shape.CellCount - result.ClearedCellCount,
                            board.OccupiedCount, "occupancy drifted after a placement");
                        Assert.AreEqual(CountOccupied(board), board.OccupiedCount);
                        Assert.GreaterOrEqual(progress.Stage, stageBefore, "stage went backwards");
                        for (int i = 0; i < board.Size; i++)
                        {
                            Assert.IsFalse(board.IsRowFull(i));
                            Assert.IsFalse(board.IsColumnFull(i));
                        }
                    }

                    // Pressure: the board fills on its own once the run gets going.
                    if (events.JunkDue)
                    {
                        var shapes = new List<PieceShape>();
                        foreach (var p in pieces) shapes.Add(p.Shape);

                        // The tray can already be stuck here - that is a normal game over
                        // on the next iteration. Junk only has to avoid *causing* it.
                        bool playableBefore = shapes.Count > 0 && board.HasAnyPlacement(shapes);

                        var dropped = junk.Spawn(board, shapes, events.JunkBlocks);
                        run.JunkDropped += dropped.Count;

                        if (verify && playableBefore)
                            Assert.IsTrue(board.HasAnyPlacement(shapes),
                                "a junk drop took away the tray's last legal move");
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
