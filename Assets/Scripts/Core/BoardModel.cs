using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BlockBlast.Core
{
    /// <summary>Result of committing a piece to the board.</summary>
    public struct PlacementResult
    {
        public int PlacedCells;
        public List<int> ClearedRows;
        public List<int> ClearedColumns;
        /// <summary>Every distinct cell removed by the clear (row/column overlaps counted once).</summary>
        public List<Vector2Int> ClearedCells;
        public bool PerfectClear;

        public int LinesCleared => (ClearedRows?.Count ?? 0) + (ClearedColumns?.Count ?? 0);
        public int ClearedCellCount => ClearedCells?.Count ?? 0;
    }

    /// <summary>
    /// The 8x8 playfield. Pure logic apart from Vector2Int, so it can be unit tested and
    /// simulated headlessly. Origin (0,0) is the bottom-left cell.
    /// </summary>
    public sealed class BoardModel
    {
        public const int Empty = -1;
        public const int DefaultSize = 8;

        readonly int[] cells;
        public int Size { get; }
        public int CellCount => cells.Length;
        public int OccupiedCount { get; private set; }
        public bool IsEmpty => OccupiedCount == 0;

        public BoardModel(int size = DefaultSize)
        {
            if (size < 2) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            cells = new int[size * size];
            Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < cells.Length; i++) cells[i] = Empty;
            OccupiedCount = 0;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

        public int ColorAt(int x, int y)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Cell outside board");
            return cells[y * Size + x];
        }

        public bool IsOccupied(int x, int y) => InBounds(x, y) && cells[y * Size + x] != Empty;

        public void SetCell(int x, int y, int colorIndex)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Cell outside board");
            int i = y * Size + x;
            bool wasOccupied = cells[i] != Empty;
            bool willBeOccupied = colorIndex != Empty;
            if (wasOccupied && !willBeOccupied) OccupiedCount--;
            else if (!wasOccupied && willBeOccupied) OccupiedCount++;
            cells[i] = colorIndex;
        }

        public bool CanPlace(PieceShape shape, int originX, int originY)
        {
            if (shape == null) return false;
            if (originX < 0 || originY < 0) return false;
            if (originX + shape.Width > Size || originY + shape.Height > Size) return false;
            foreach (var c in shape.Cells)
                if (cells[(originY + c.y) * Size + originX + c.x] != Empty) return false;
            return true;
        }

        public bool CanPlace(PieceShape shape, Vector2Int origin) => CanPlace(shape, origin.x, origin.y);

        /// <summary>Commits a piece and resolves any completed rows/columns.</summary>
        public PlacementResult Place(PieceShape shape, int originX, int originY, int colorIndex)
        {
            if (!CanPlace(shape, originX, originY))
                throw new InvalidOperationException("Cannot place " + shape + " at (" + originX + "," + originY + ")");

            foreach (var c in shape.Cells)
                SetCell(originX + c.x, originY + c.y, colorIndex);

            var result = new PlacementResult
            {
                PlacedCells = shape.CellCount,
                ClearedRows = new List<int>(),
                ClearedColumns = new List<int>(),
                ClearedCells = new List<Vector2Int>()
            };

            for (int y = 0; y < Size; y++)
                if (IsRowFull(y)) result.ClearedRows.Add(y);
            for (int x = 0; x < Size; x++)
                if (IsColumnFull(x)) result.ClearedColumns.Add(x);

            // Collect before clearing so cells shared by a row and a column count once.
            var doomed = new HashSet<Vector2Int>();
            foreach (int y in result.ClearedRows)
                for (int x = 0; x < Size; x++) doomed.Add(new Vector2Int(x, y));
            foreach (int x in result.ClearedColumns)
                for (int y = 0; y < Size; y++) doomed.Add(new Vector2Int(x, y));

            foreach (var c in doomed)
            {
                result.ClearedCells.Add(c);
                SetCell(c.x, c.y, Empty);
            }

            result.PerfectClear = result.ClearedCells.Count > 0 && OccupiedCount == 0;
            return result;
        }

        public PlacementResult Place(PieceShape shape, Vector2Int origin, int colorIndex)
            => Place(shape, origin.x, origin.y, colorIndex);

        public bool IsRowFull(int y)
        {
            for (int x = 0; x < Size; x++)
                if (cells[y * Size + x] == Empty) return false;
            return true;
        }

        public bool IsColumnFull(int x)
        {
            for (int y = 0; y < Size; y++)
                if (cells[y * Size + x] == Empty) return false;
            return true;
        }

        public bool HasAnyPlacement(PieceShape shape)
        {
            if (shape == null) return false;
            for (int y = 0; y + shape.Height <= Size; y++)
                for (int x = 0; x + shape.Width <= Size; x++)
                    if (CanPlace(shape, x, y)) return true;
            return false;
        }

        public bool HasAnyPlacement(IEnumerable<PieceShape> shapes)
        {
            if (shapes == null) return false;
            foreach (var s in shapes)
                if (s != null && HasAnyPlacement(s)) return true;
            return false;
        }

        public void GetValidOrigins(PieceShape shape, List<Vector2Int> results)
        {
            results.Clear();
            if (shape == null) return;
            for (int y = 0; y + shape.Height <= Size; y++)
                for (int x = 0; x + shape.Width <= Size; x++)
                    if (CanPlace(shape, x, y)) results.Add(new Vector2Int(x, y));
        }

        /// <summary>
        /// Rows/columns that would complete if the piece were placed here, without mutating
        /// anything. Drives the "these lines will pop" drag preview.
        /// </summary>
        public void PreviewClears(PieceShape shape, int originX, int originY, List<int> rows, List<int> columns)
        {
            rows.Clear();
            columns.Clear();
            if (!CanPlace(shape, originX, originY)) return;

            for (int y = 0; y < Size; y++)
            {
                bool full = true;
                for (int x = 0; x < Size && full; x++)
                    if (cells[y * Size + x] == Empty && !shape.Contains(x - originX, y - originY)) full = false;
                if (full) rows.Add(y);
            }
            for (int x = 0; x < Size; x++)
            {
                bool full = true;
                for (int y = 0; y < Size && full; y++)
                    if (cells[y * Size + x] == Empty && !shape.Contains(x - originX, y - originY)) full = false;
                if (full) columns.Add(x);
            }
        }

        public int[] Snapshot()
        {
            var copy = new int[cells.Length];
            Array.Copy(cells, copy, cells.Length);
            return copy;
        }

        public void Restore(int[] snapshot)
        {
            if (snapshot == null || snapshot.Length != cells.Length)
                throw new ArgumentException("Snapshot size mismatch", nameof(snapshot));
            Array.Copy(snapshot, cells, cells.Length);
            OccupiedCount = 0;
            foreach (int c in cells)
                if (c != Empty) OccupiedCount++;
        }

        public BoardModel Clone()
        {
            var b = new BoardModel(Size);
            b.Restore(cells);
            return b;
        }

        /// <summary>Top row first, so it reads the way the board looks on screen.</summary>
        public string ToAscii()
        {
            var sb = new StringBuilder();
            for (int y = Size - 1; y >= 0; y--)
            {
                for (int x = 0; x < Size; x++) sb.Append(cells[y * Size + x] == Empty ? '.' : '#');
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Builds a board from ASCII rows, top row first. Used by the tests.</summary>
        public static BoardModel Parse(params string[] rows)
        {
            var b = new BoardModel(rows.Length);
            for (int r = 0; r < rows.Length; r++)
            {
                int y = rows.Length - 1 - r;
                for (int x = 0; x < rows[r].Length; x++)
                    if (rows[r][x] != '.') b.SetCell(x, y, 0);
            }
            return b;
        }
    }
}
