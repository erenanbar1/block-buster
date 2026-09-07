using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast.Core
{
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
        public int CellCount => Cells.Length;

        public PieceShape(string id, int weight, params Vector2Int[] cells)
        {
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
            return new PieceShape(id, weight, cells.ToArray());
        }

        public override string ToString() => $"{Id}({Width}x{Height}, {CellCount} cells)";
    }
}
