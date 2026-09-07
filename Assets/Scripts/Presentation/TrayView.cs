using System.Collections.Generic;
using BlockBlast.Core;
using UnityEngine;

namespace BlockBlast.Presentation
{
    /// <summary>The three-slot rack the player drags pieces out of.</summary>
    public sealed class TrayView : MonoBehaviour
    {
        public const int SlotCount = 3;

        readonly RectTransform[] slots = new RectTransform[SlotCount];
        readonly PieceView[] pieces = new PieceView[SlotCount];

        RectTransform dragLayer;
        Camera uiCamera;

        public IReadOnlyList<PieceView> Pieces => pieces;
        public bool IsEmpty
        {
            get
            {
                foreach (var p in pieces)
                    if (p != null) return false;
                return true;
            }
        }

        public static TrayView Create(Transform parent, RectTransform dragLayer, Camera uiCamera)
        {
            var go = new GameObject("Tray", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, Theme.TrayY);
            rt.sizeDelta = new Vector2(Theme.TraySlotWidth * SlotCount, 320f);

            var view = go.AddComponent<TrayView>();
            view.Build(dragLayer, uiCamera);
            return view;
        }

        void Build(RectTransform layer, Camera cam)
        {
            dragLayer = layer;
            uiCamera = cam;
            var rt = (RectTransform)transform;

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = Ui.Rect("Slot" + i, rt);
                slot.sizeDelta = new Vector2(Theme.TraySlotWidth, 300f);
                slot.anchoredPosition = new Vector2((i - 1) * Theme.TraySlotWidth, 0f);
                slots[i] = slot;
            }
        }

        public RectTransform Slot(int index) => slots[index];

        public PieceView Fill(int index, PieceInstance piece)
        {
            Clear(index);
            var view = PieceView.Create(piece, index, slots[index], dragLayer, uiCamera);
            pieces[index] = view;
            return view;
        }

        public void Clear(int index)
        {
            if (pieces[index] != null)
            {
                Destroy(pieces[index].gameObject);
                pieces[index] = null;
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < SlotCount; i++) Clear(i);
        }

        public void Consume(PieceView view)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (pieces[i] != view) continue;
                pieces[i] = null;
                Destroy(view.gameObject);
                return;
            }
        }

        public List<PieceShape> RemainingShapes()
        {
            var list = new List<PieceShape>(SlotCount);
            foreach (var p in pieces)
                if (p != null) list.Add(p.Shape);
            return list;
        }

        /// <summary>Greys out pieces that no longer fit anywhere, the way the real game does.</summary>
        public void RefreshPlayability(BoardModel board)
        {
            foreach (var p in pieces)
            {
                if (p == null) continue;
                p.SetDimmed(!board.HasAnyPlacement(p.Shape));
            }
        }

        public void SetInteractable(bool value)
        {
            foreach (var p in pieces)
                if (p != null) p.Interactable = value;
        }
    }
}
