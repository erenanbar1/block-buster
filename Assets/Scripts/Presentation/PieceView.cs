using System;
using System.Collections;
using BlockBlast.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlockBlast.Presentation
{
    /// <summary>
    /// A draggable piece sitting in a tray slot. Handles its own grab/drag/return visuals
    /// and reports drag events to the controller, which owns snapping and placement rules.
    /// </summary>
    public sealed class PieceView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PieceInstance Piece { get; private set; }
        public PieceShape Shape => Piece.Shape;
        public int ColorIndex => Piece.ColorIndex;
        public int SlotIndex { get; private set; }
        public bool IsDragging { get; private set; }
        public bool Interactable { get; set; } = true;

        public event Action<PieceView> DragBegan;
        public event Action<PieceView> Dragged;
        public event Action<PieceView> DragEnded;

        RectTransform rt;
        RectTransform cells;
        RectTransform homeParent;
        RectTransform dragLayer;
        CanvasGroup group;
        Camera uiCamera;
        Vector2 pointerLocal;

        public RectTransform Rect => rt;

        public static PieceView Create(PieceInstance piece, int slotIndex, RectTransform slot,
            RectTransform dragLayer, Camera uiCamera)
        {
            var go = new GameObject("Piece_" + piece.Shape.Id, typeof(RectTransform), typeof(CanvasGroup));
            var view = go.AddComponent<PieceView>();
            view.Build(piece, slotIndex, slot, dragLayer, uiCamera);
            return view;
        }

        void Build(PieceInstance piece, int slotIndex, RectTransform slot, RectTransform layer, Camera cam)
        {
            Piece = piece;
            SlotIndex = slotIndex;
            homeParent = slot;
            dragLayer = layer;
            uiCamera = cam;

            rt = (RectTransform)transform;
            rt.SetParent(slot, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = FullSize(piece.Shape);
            rt.localScale = Vector3.one * Theme.TrayScale;
            group = GetComponent<CanvasGroup>();

            // Generous invisible grab area so 1x1 pieces are still easy to pick up.
            var hit = Ui.Image("Hit", rt, null, new Color(0, 0, 0, 0));
            hit.raycastTarget = true;
            Ui.Fill(hit.rectTransform, -46f);

            cells = Ui.Rect("Cells", rt);
            cells.sizeDelta = rt.sizeDelta;

            var sprite = SpriteFactory.Block();
            var color = Theme.Block(piece.ColorIndex);
            foreach (var c in piece.Shape.Cells)
            {
                var img = Ui.Image("Cell", cells, sprite, color);
                img.rectTransform.sizeDelta = new Vector2(Theme.CellSize, Theme.CellSize);
                img.rectTransform.anchoredPosition = CellOffset(piece.Shape, c.x, c.y);
            }
        }

        public static Vector2 FullSize(PieceShape shape) => new Vector2(
            shape.Width * Theme.CellSize + (shape.Width - 1) * Theme.CellSpacing,
            shape.Height * Theme.CellSize + (shape.Height - 1) * Theme.CellSpacing);

        /// <summary>Offset of a shape cell from the piece centre, at full board scale.</summary>
        public static Vector2 CellOffset(PieceShape shape, int x, int y)
        {
            var size = FullSize(shape);
            return new Vector2(
                -size.x * 0.5f + Theme.CellSize * 0.5f + x * Theme.CellPitch,
                -size.y * 0.5f + Theme.CellSize * 0.5f + y * Theme.CellPitch);
        }

        /// <summary>
        /// World position the shape's bounding-box bottom-left cell centre currently occupies.
        /// The controller converts this into a board origin for snapping.
        /// </summary>
        public Vector3 BottomLeftCellWorld()
        {
            var size = FullSize(Shape);
            var local = new Vector2(-size.x * 0.5f + Theme.CellSize * 0.5f, -size.y * 0.5f + Theme.CellSize * 0.5f);
            return cells.TransformPoint(local);
        }

        // ---- drag handling -----------------------------------------------------

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Interactable) return;
            IsDragging = true;
            rt.SetParent(dragLayer, true);
            rt.SetAsLastSibling();
            group.blocksRaycasts = false;
            StopAllCoroutines();
            StartCoroutine(Tween.Scale(rt, rt.localScale, Vector3.one, 0.10f));
            UpdatePosition(eventData);
            DragBegan?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;
            UpdatePosition(eventData);
            Dragged?.Invoke(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;
            IsDragging = false;
            group.blocksRaycasts = true;
            DragEnded?.Invoke(this);
        }

        void UpdatePosition(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, eventData.position, uiCamera, out var local))
            {
                rt.anchoredPosition = local + new Vector2(0f, Theme.DragLift);
            }
        }

        /// <summary>Snaps the dragged piece so its cells line up exactly with a board origin.</summary>
        public void SnapTo(BoardView board, Vector2Int origin)
        {
            var size = FullSize(Shape);
            Vector3 targetWorld = board.CellToWorld(origin.x, origin.y);
            Vector3 currentWorld = BottomLeftCellWorld();
            Vector3 delta = targetWorld - currentWorld;
            rt.position += delta;
        }

        public IEnumerator ReturnHome()
        {
            Interactable = false;
            rt.SetParent(homeParent, true);
            var from = rt.anchoredPosition;
            var fromScale = rt.localScale;
            float duration = Theme.ReturnDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Tween.EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                rt.anchoredPosition = Vector2.Lerp(from, Vector2.zero, t);
                rt.localScale = Vector3.Lerp(fromScale, Vector3.one * Theme.TrayScale, t);
                yield return null;
            }
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one * Theme.TrayScale;
            Interactable = true;
        }

        /// <summary>
        /// Pops the piece into its slot. Deliberately leaves the piece grabbable while it
        /// animates: OnBeginDrag cancels this tween, so an impatient player is never locked
        /// out for the length of the flourish.
        /// </summary>
        public IEnumerator SpawnIntoTray(float delay)
        {
            rt.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            yield return Tween.Scale(rt, Vector3.zero, Vector3.one * Theme.TrayScale, 0.24f, Tween.EaseOutBack);
        }

        public void SetDimmed(bool dimmed)
        {
            group.alpha = dimmed ? 0.34f : 1f;
        }
    }
}
