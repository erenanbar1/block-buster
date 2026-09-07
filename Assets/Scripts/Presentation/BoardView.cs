using System.Collections;
using System.Collections.Generic;
using BlockBlast.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlast.Presentation
{
    /// <summary>
    /// Draws the playfield and owns all board-space maths (cell to local position and back).
    /// The visuals are updated explicitly by the controller rather than rebuilt every frame,
    /// so blocks can be animated on their way out.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public int Size { get; private set; }
        public RectTransform Grid { get; private set; }

        RectTransform frame;
        Image[] slots;
        Image[] blocks;
        Image[] hints;      // ghost of the piece being dragged
        Image[] lineHints;  // "this whole line will pop" wash
        EffectsLayer effects;

        readonly List<int> previewRows = new List<int>();
        readonly List<int> previewCols = new List<int>();
        readonly HashSet<int> litHints = new HashSet<int>();
        readonly HashSet<int> litLines = new HashSet<int>();

        public static BoardView Create(Transform parent, int size, EffectsLayer effects)
        {
            var go = new GameObject("Board", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view.Build(size, effects);
            return view;
        }

        void Build(int size, EffectsLayer fx)
        {
            Size = size;
            effects = fx;
            frame = (RectTransform)transform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(Theme.FrameExtent, Theme.FrameExtent);

            var frameImage = Ui.Image("Frame", frame, SpriteFactory.RoundedRect(96, 30), Theme.FrameColor,
                Image.Type.Sliced);
            Ui.Fill(frameImage.rectTransform);

            Grid = Ui.Rect("Grid", frame);
            Grid.sizeDelta = new Vector2(Theme.GridExtent, Theme.GridExtent);

            // Line hints wash over the blocks, not under them: the cells that are about to
            // pop are mostly filled already, so a hint below the blocks would be invisible.
            var slotLayer = Ui.Rect("Slots", Grid);
            var blockLayer = Ui.Rect("Blocks", Grid);
            var lineLayer = Ui.Rect("LineHints", Grid);
            var hintLayer = Ui.Rect("Hints", Grid);
            foreach (var layer in new[] { slotLayer, blockLayer, lineLayer, hintLayer })
                layer.sizeDelta = Grid.sizeDelta;

            int count = size * size;
            slots = new Image[count];
            blocks = new Image[count];
            hints = new Image[count];
            lineHints = new Image[count];

            var blockSprite = SpriteFactory.Block();
            var slotSprite = SpriteFactory.RoundedRect(64, 18);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = Index(x, y);
                    var pos = CellToLocal(x, y);

                    slots[i] = MakeCell(slotLayer, "Slot", slotSprite, Theme.SlotColor, pos, Image.Type.Sliced);
                    lineHints[i] = MakeCell(lineLayer, "Line", blockSprite, Theme.LineHintColor, pos);
                    lineHints[i].enabled = false;
                    blocks[i] = MakeCell(blockLayer, "Block", blockSprite, Color.white, pos);
                    blocks[i].enabled = false;
                    hints[i] = MakeCell(hintLayer, "Hint", blockSprite, Theme.HintColor, pos);
                    hints[i].enabled = false;
                }
        }

        Image MakeCell(Transform parent, string name, Sprite sprite, Color color, Vector2 localPos,
            Image.Type type = Image.Type.Simple)
        {
            var img = Ui.Image(name, parent, sprite, color, type);
            img.rectTransform.sizeDelta = new Vector2(Theme.CellSize, Theme.CellSize);
            img.rectTransform.anchoredPosition = localPos;
            return img;
        }

        int Index(int x, int y) => y * Size + x;

        /// <summary>Centre of a cell in Grid local space.</summary>
        public Vector2 CellToLocal(int x, int y)
        {
            float start = -Theme.GridExtent * 0.5f + Theme.CellSize * 0.5f;
            return new Vector2(start + x * Theme.CellPitch, start + y * Theme.CellPitch);
        }

        /// <summary>Nearest cell coordinate for a point in Grid local space (may be off-board).</summary>
        public Vector2Int LocalToCell(Vector2 local)
        {
            float start = -Theme.GridExtent * 0.5f + Theme.CellSize * 0.5f;
            return new Vector2Int(
                Mathf.RoundToInt((local.x - start) / Theme.CellPitch),
                Mathf.RoundToInt((local.y - start) / Theme.CellPitch));
        }

        public Vector3 CellToWorld(int x, int y) => Grid.TransformPoint(CellToLocal(x, y));

        /// <summary>The block image for a cell, so callers can animate an individual block.</summary>
        public RectTransform BlockAt(int x, int y)
            => InBounds(x, y) ? blocks[Index(x, y)].rectTransform : null;

        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

        // ---- block visuals ---------------------------------------------------

        public void Refresh(BoardModel model)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = Index(x, y);
                    int color = model.ColorAt(x, y);
                    var img = blocks[i];
                    img.enabled = color != BoardModel.Empty;
                    img.color = color == BoardModel.Empty ? Color.white : Theme.Block(color);
                    img.rectTransform.localScale = Vector3.one;
                    img.rectTransform.anchoredPosition = CellToLocal(x, y);
                }
        }

        public void SetCell(int x, int y, int colorIndex)
        {
            var img = blocks[Index(x, y)];
            img.enabled = colorIndex != BoardModel.Empty;
            if (colorIndex != BoardModel.Empty) img.color = Theme.Block(colorIndex);
            img.rectTransform.localScale = Vector3.one;
            img.rectTransform.anchoredPosition = CellToLocal(x, y);
        }

        /// <summary>Drops a piece onto the board with a staggered pop per cell.</summary>
        public IEnumerator AnimatePlacement(PieceShape shape, Vector2Int origin, int colorIndex)
        {
            foreach (var c in shape.Cells)
            {
                int x = origin.x + c.x, y = origin.y + c.y;
                SetCell(x, y, colorIndex);
                var rt = blocks[Index(x, y)].rectTransform;
                rt.localScale = Vector3.one * 0.55f;
                StartCoroutine(Tween.Scale(rt, Vector3.one * 0.55f, Vector3.one, Theme.PopInDuration,
                    Tween.EaseOutBack));
            }
            yield return new WaitForSecondsRealtime(Theme.PopInDuration * 0.5f);
        }

        /// <summary>Flash, blow up and fade every cleared cell, radiating out from the drop point.</summary>
        public IEnumerator AnimateClear(List<Vector2Int> cleared, Vector2Int epicentre)
        {
            var routines = new List<Coroutine>();
            foreach (var cell in cleared)
            {
                float delay = Vector2Int.Distance(cell, epicentre) * 0.018f;
                routines.Add(StartCoroutine(ClearCell(cell, delay)));
            }
            foreach (var r in routines) yield return r;
        }

        IEnumerator ClearCell(Vector2Int cell, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            int i = Index(cell.x, cell.y);
            var img = blocks[i];
            var rt = img.rectTransform;
            Color from = img.color;
            effects?.Burst(CellToWorld(cell.x, cell.y), from);

            yield return Tween.Run(Theme.ClearDuration, Tween.EaseOutCubic, t =>
            {
                if (img == null) return;
                float flash = Mathf.Clamp01(t * 4f);
                var c = Color.Lerp(from, Color.white, Mathf.Clamp01(1f - t * 3f));
                c.a = 1f - Tween.EaseInCubic(t);
                img.color = c;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.45f, flash * t);
            });

            img.enabled = false;
            img.color = Color.white;
            rt.localScale = Vector3.one;
        }

        // ---- drag preview -----------------------------------------------------

        public void ShowPreview(BoardModel model, PieceShape shape, Vector2Int origin, int colorIndex)
        {
            HidePreview();
            if (!model.CanPlace(shape, origin)) return;

            var ghost = Theme.WithAlpha(Theme.Block(colorIndex), 0.42f);
            foreach (var c in shape.Cells)
            {
                int i = Index(origin.x + c.x, origin.y + c.y);
                hints[i].enabled = true;
                hints[i].color = ghost;
                litHints.Add(i);
            }

            model.PreviewClears(shape, origin.x, origin.y, previewRows, previewCols);
            foreach (int y in previewRows)
                for (int x = 0; x < Size; x++) LightLine(x, y);
            foreach (int x in previewCols)
                for (int y = 0; y < Size; y++) LightLine(x, y);
        }

        void LightLine(int x, int y)
        {
            int i = Index(x, y);
            lineHints[i].enabled = true;
            litLines.Add(i);
        }

        public void HidePreview()
        {
            foreach (int i in litHints) hints[i].enabled = false;
            foreach (int i in litLines) lineHints[i].enabled = false;
            litHints.Clear();
            litLines.Clear();
        }

        /// <summary>Sweeps the board clean with a diagonal wave. Used on game over / restart.</summary>
        public IEnumerator AnimateWipe(BoardModel model)
        {
            for (int d = 0; d < Size * 2; d++)
            {
                for (int y = 0; y < Size; y++)
                {
                    int x = d - y;
                    if (x < 0 || x >= Size) continue;
                    int i = Index(x, y);
                    if (!blocks[i].enabled) continue;
                    effects?.Burst(CellToWorld(x, y), blocks[i].color, 4);
                    blocks[i].enabled = false;
                }
                yield return new WaitForSecondsRealtime(0.03f);
            }
            model.Clear();
            Refresh(model);
        }
    }
}
