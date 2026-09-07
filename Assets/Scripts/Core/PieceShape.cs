using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast.Core
{
    /// <summary>
    /// How mean a shape is to place. The difficulty curve unlocks tiers as the run
    /// progresses, so early stages deal only forgiving shapes and late stages deal
    /// everything.
    /// </summary>
    public enum PieceTier
    {
        /// <summary>Dots, short bars, 2x2, small corners. Always available.</summary>
        Basic = 0,
        /// <summary>L/J/T/S/Z and the 4-bars: still friendly, but they leave gaps.</summary>
        Tetromino = 1,
        /// <summary>Bulky rectangles and 3x3 corners that eat a lot of board.</summary>
        Awkward = 2,
        /// <summary>Plus and diagonals: shapes that punish an untidy board.</summary>
        Nasty = 3,
    }

    /// <summary>
    /// Immutable definition of a placeable piece. Cells are normalised so that the
    /// minimum x and minimum y are both zero, i.e. the shape hugs the bottom-left of
    /// its own bounding box. Pieces are never rotated at runtime (Block Blast rules),
    /// every orientation is a separate entry in <see cref="PieceLibrary"/>.
    /// </summary>
    public sealed class PieceShape
    {
        public string Id { get; }
        public Vector2Int[] Cells { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>Relative spawn frequency. Higher = more common.</summary>
        public int Weight { get; }
        /// <summary>Earliest difficulty tier that deals this shape.</summary>
        public PieceTier Tier { get; }
        public int CellCount => Cells.Length;

        /// <summary>How much board this shape demands, used to bias late-game deals.</summary>
        public int Bulk => Mathf.Max(CellCount - 3, Mathf.Max(Width, Height) - 3, 0);

        public PieceShape(string id, int weight, params Vector2Int[] cells)
            : this(id, weight, PieceTier.Basic, cells) { }

        public PieceShape(string id, int weight, PieceTier tier, params Vector2Int[] cells)
        {
            Tier = tier;
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Piece id required", nameof(id));
            if (cells == null || cells.Length == 0) throw new ArgumentException("Piece needs at least one cell", nameof(cells));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be positive");

            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
            }

            var normalised = new Vector2Int[cells.Length];
            var seen = new HashSet<Vector2Int>();
            int maxX = 0, maxY = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                var c = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
                if (!seen.Add(c)) throw new ArgumentException($"Piece '{id}' contains duplicate cell {c}");
                normalised[i] = c;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            Id = id;
            Weight = weight;
            Cells = normalised;
            Width = maxX + 1;
            Height = maxY + 1;
        }

        public bool Contains(int x, int y)
        {
            foreach (var c in Cells)
                if (c.x == x && c.y == y) return true;
            return false;
        }

        /// <summary>Builds a shape from ASCII rows, top row first. '#'/'X'/'O' mark filled cells.</summary>
        public static PieceShape FromRows(string id, int weight, params string[] rows)
            => FromRows(id, weight, PieceTier.Basic, rows);

        /// <summary>Builds a shape from ASCII rows, top row first, at a given difficulty tier.</summary>
        public static PieceShape FromRows(string id, int weight, PieceTier tier, params string[] rows)
        {
            var cells = new List<Vector2Int>();
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                int y = rows.Length - 1 - r; // first row is the top => highest y
                for (int x = 0; x < row.Length; x++)
                {
                    char ch = row[x];
                    if (ch == '#' || ch == 'X' || ch == 'x' || ch == 'O') cells.Add(new Vector2Int(x, y));
                }
            }
            return new PieceShape(id, weight, tier, cells.ToArray());
        }

        public override string ToString() => $"{Id}({Width}x{Height}, {CellCount} cells, {Tier})";
    }
}
