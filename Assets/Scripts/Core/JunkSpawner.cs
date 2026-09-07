using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast.Core
{
    /// <summary>
    /// Seeds stray blocks onto the board so pressure rises even when the player is
    /// placing well. This is what stops a skilled run from plateauing forever.
    ///
    /// The mechanic is easy; making it feel fair is the hard part, so three rules are
    /// enforced here rather than left to the caller:
    ///  * junk hugs existing blocks and the board edges, so the board degrades into a
    ///    readable mass instead of unplayable swiss cheese;
    ///  * junk never completes a line, because a free clear is not pressure;
    ///  * the whole drop is rolled back if it would leave the tray with no legal move.
    ///    Junk raises the pressure, it never lands the killing blow.
    /// </summary>
    public sealed class JunkSpawner
    {
        /// <summary>Colour index used for junk, so the view can tint it as a hazard.</summary>
        public const int JunkColorIndex = -2;

        readonly System.Random rng;
        readonly List<Vector2Int> candidates = new List<Vector2Int>();
        readonly List<Vector2Int> placed = new List<Vector2Int>();

        public JunkSpawner(int seed) => rng = new System.Random(seed);

        /// <summary>
        /// Drops up to <paramref name="count"/> junk blocks. Returns the cells actually
        /// filled, or an empty list when no safe drop exists.
        /// </summary>
        public IReadOnlyList<Vector2Int> Spawn(BoardModel board, IList<PieceShape> trayShapes, int count)
        {
            placed.Clear();
            if (board == null || count <= 0) return placed;

            for (int i = 0; i < count; i++)
            {
                if (!TryPlaceOne(board, trayShapes)) break;
            }
            return placed;
        }

        bool TryPlaceOne(BoardModel board, IList<PieceShape> trayShapes)
        {
            CollectCandidates(board);
            if (candidates.Count == 0) return false;

            // Try candidates best-first; a cell only sticks if the board stays playable.
            while (candidates.Count > 0)
            {
                int index = PickWeighted(board);
                var cell = candidates[index];
                candidates.RemoveAt(index);

                board.SetCell(cell.x, cell.y, JunkColorIndex);

                if (CompletesALine(board, cell) || !StillPlayable(board, trayShapes))
                {
                    board.SetCell(cell.x, cell.y, BoardModel.Empty);
                    continue;
                }

                placed.Add(cell);
                return true;
            }
            return false;
        }

        void CollectCandidates(BoardModel board)
        {
            candidates.Clear();
            for (int y = 0; y < board.Size; y++)
                for (int x = 0; x < board.Size; x++)
                    if (!board.IsOccupied(x, y)) candidates.Add(new Vector2Int(x, y));
        }

        /// <summary>
        /// Prefers cells with more occupied or off-board neighbours, so junk thickens
        /// the existing mass rather than scattering holes across open space.
        /// </summary>
        int PickWeighted(BoardModel board)
        {
            int bestScore = int.MinValue;
            var best = new List<int>();

            for (int i = 0; i < candidates.Count; i++)
            {
                int score = Contact(board, candidates[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                    best.Add(i);
                }
                else if (score == bestScore)
                {
                    best.Add(i);
                }
            }
            return best[rng.Next(best.Count)];
        }

        static int Contact(BoardModel board, Vector2Int cell)
        {
            int contact = 0;
            contact += Neighbour(board, cell.x - 1, cell.y);
            contact += Neighbour(board, cell.x + 1, cell.y);
            contact += Neighbour(board, cell.x, cell.y - 1);
            contact += Neighbour(board, cell.x, cell.y + 1);
            return contact;
        }

        static int Neighbour(BoardModel board, int x, int y)
        {
            if (!board.InBounds(x, y)) return 1;              // board edge counts as support
            return board.IsOccupied(x, y) ? 1 : 0;
        }

        static bool CompletesALine(BoardModel board, Vector2Int cell)
            => board.IsRowFull(cell.y) || board.IsColumnFull(cell.x);

        static bool StillPlayable(BoardModel board, IList<PieceShape> trayShapes)
        {
            if (trayShapes == null || trayShapes.Count == 0) return true;
            foreach (var shape in trayShapes)
                if (shape != null && board.HasAnyPlacement(shape)) return true;
            return false;
        }
    }
}
