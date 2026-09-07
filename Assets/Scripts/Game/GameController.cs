using System;
using System.Collections;
using System.Collections.Generic;
using BlockBlast.Core;
using BlockBlast.Presentation;
using UnityEngine;

namespace BlockBlast.Game
{
    /// <summary>
    /// The game loop: owns the model, drives the views, and decides when a run is over.
    /// Presentation is animated but the model is always updated up-front, so the state the
    /// player sees can never drift from the state the rules use.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public BoardModel Board { get; private set; }
        public int Score { get; private set; }
        public int Best { get; private set; }
        public int Combo { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsBusy { get; private set; }
        public TrayView Tray => tray;
        public BoardView BoardView => boardView;

        /// <summary>Raised after a placement has been fully resolved (model + score).</summary>
        public event Action<PlacementResult, int> PiecePlaced;
        public event Action GameOver;

        BoardView boardView;
        TrayView tray;
        HudView hud;
        EffectsLayer effects;
        SfxPlayer sfx;
        PieceGenerator generator;
        int seed;

        readonly List<PieceInstance> trayModel = new List<PieceInstance>(TrayView.SlotCount);
        Vector2Int? currentSnap;

        public void Initialise(BoardView board, TrayView tray, HudView hud, EffectsLayer effects, SfxPlayer sfx)
        {
            boardView = board;
            this.tray = tray;
            this.hud = hud;
            this.effects = effects;
            this.sfx = sfx;

            Board = new BoardModel(Theme.BoardSize);
            Best = SaveSystem.LoadBest();
            hud.SetBest(Best);
            hud.RestartRequested += Restart;
            hud.SoundToggleRequested += ToggleSound;

            if (sfx != null) sfx.Muted = SaveSystem.LoadMuted();
            hud.SetMuted(SaveSystem.LoadMuted());

            for (int i = 0; i < TrayView.SlotCount; i++) trayModel.Add(null);

            if (!TryResume()) StartFreshRun();
        }

        // ---- run lifecycle -------------------------------------------------------

        void StartFreshRun()
        {
            seed = Environment.TickCount;
            generator = new PieceGenerator(seed, Theme.Blocks.Length);
            Board.Clear();
            Score = 0;
            Combo = 0;
            IsGameOver = false;
            boardView.Refresh(Board);
            hud.SetScore(0, animate: false);
            hud.HideGameOver();
            hud.HideCombo();
            RefillTray();
            Persist();
        }

        bool TryResume()
        {
            var data = SaveSystem.LoadGame();
            if (data == null || data.boardSize != Board.Size) return false;

            seed = data.generatorSeed;
            generator = new PieceGenerator(seed, Theme.Blocks.Length);
            Board.Restore(data.cells);
            Score = data.score;
            Combo = data.combo;
            IsGameOver = false;
            boardView.Refresh(Board);
            hud.SetScore(Score, animate: false);
            hud.HideGameOver();

            var restored = new List<PieceInstance>();
            bool anyPiece = false;
            for (int i = 0; i < TrayView.SlotCount; i++)
            {
                string id = i < data.trayShapeIds.Length ? data.trayShapeIds[i] : null;
                var shape = string.IsNullOrEmpty(id) ? null : PieceLibrary.ById(id);
                restored.Add(shape == null ? null : new PieceInstance(shape, data.trayColors[i]));
                anyPiece |= shape != null;
            }

            if (anyPiece) SetTray(restored, animate: false);
            else RefillTray();

            EvaluateGameOver();
            return true;
        }

        public void ToggleSound()
        {
            bool muted = !SaveSystem.LoadMuted();
            SaveSystem.SaveMuted(muted);
            if (sfx != null) sfx.Muted = muted;
            hud.SetMuted(muted);
        }

        public void Restart()
        {
            if (IsBusy && !IsGameOver) return;
            StopAllCoroutines();
            StartCoroutine(RestartRoutine());
        }

        IEnumerator RestartRoutine()
        {
            IsBusy = true;
            hud.HideGameOver();
            tray.SetInteractable(false);
            yield return boardView.AnimateWipe(Board);
            tray.ClearAll();
            for (int i = 0; i < trayModel.Count; i++) trayModel[i] = null;
            SaveSystem.ClearGame();
            StartFreshRun();
            IsBusy = false;
        }

        // ---- tray ------------------------------------------------------------------

        void RefillTray() => SetTray(generator.NextTrio(Board, TrayView.SlotCount));

        /// <summary>
        /// Replaces the whole tray. Used when refilling, when resuming a saved run, and by
        /// the play-mode tests to deal a known hand.
        /// </summary>
        public void SetTray(IList<PieceInstance> pieces, bool animate = true)
        {
            tray.ClearAll();
            for (int i = 0; i < TrayView.SlotCount; i++)
            {
                var piece = i < pieces.Count ? pieces[i] : null;
                trayModel[i] = piece;
                if (piece == null) continue;

                var view = tray.Fill(i, piece);
                Bind(view);
                if (animate) view.StartCoroutine(view.SpawnIntoTray(i * 0.06f));
            }
            tray.RefreshPlayability(Board);
        }

        void Bind(PieceView view)
        {
            view.DragBegan += OnDragBegan;
            view.Dragged += OnDragged;
            view.DragEnded += OnDragEnded;
        }

        // ---- dragging ---------------------------------------------------------------

        void OnDragBegan(PieceView view) => UpdateSnap(view);

        void OnDragged(PieceView view) => UpdateSnap(view);

        void UpdateSnap(PieceView view)
        {
            currentSnap = ResolveSnap(view);
            if (currentSnap.HasValue)
                boardView.ShowPreview(Board, view.Shape, currentSnap.Value, view.ColorIndex);
            else
                boardView.HidePreview();
        }

        /// <summary>Board origin the dragged piece would land on, or null if it cannot land.</summary>
        public Vector2Int? ResolveSnap(PieceView view)
        {
            Vector3 world = view.BottomLeftCellWorld();
            Vector2 local = boardView.Grid.InverseTransformPoint(world);
            var origin = boardView.LocalToCell(local);
            return Board.CanPlace(view.Shape, origin) ? origin : (Vector2Int?)null;
        }

        void OnDragEnded(PieceView view)
        {
            boardView.HidePreview();
            var snap = currentSnap;
            currentSnap = null;

            if (IsGameOver || IsBusy || !snap.HasValue)
            {
                sfx?.PlayInvalid();
                view.StartCoroutine(view.ReturnHome());
                return;
            }
            StartCoroutine(PlaceRoutine(view, snap.Value));
        }

        /// <summary>Places a tray piece directly. Used by the automated tests.</summary>
        public bool PlaceFromSlot(int slotIndex, Vector2Int origin)
        {
            var view = tray.Pieces[slotIndex];
            if (view == null || IsBusy || IsGameOver) return false;
            if (!Board.CanPlace(view.Shape, origin)) return false;
            StartCoroutine(PlaceRoutine(view, origin));
            return true;
        }

        IEnumerator PlaceRoutine(PieceView view, Vector2Int origin)
        {
            IsBusy = true;
            tray.SetInteractable(false);

            var shape = view.Shape;
            int colorIndex = view.ColorIndex;
            int slot = view.SlotIndex;

            tray.Consume(view);
            trayModel[slot] = null;

            var result = Board.Place(shape, origin, colorIndex);
            Combo = ScoreRules.NextCombo(Combo, result);
            int gained = ScoreRules.ScoreFor(result, Combo);
            Score += gained;

            yield return boardView.AnimatePlacement(shape, origin, colorIndex);
            sfx?.PlayPlace();

            Vector3 centre = boardView.CellToWorld(
                Mathf.Clamp(origin.x + shape.Width / 2, 0, Board.Size - 1),
                Mathf.Clamp(origin.y + shape.Height / 2, 0, Board.Size - 1));

            if (result.LinesCleared > 0)
            {
                sfx?.PlayClear(Combo);
                effects.Shake(9f + result.LinesCleared * 7f);
                if (Combo >= 2) hud.ShowCombo(Combo);
                effects.Popup(centre, "+" + gained, Theme.Hex(0xFFD400), 76f);
                yield return boardView.AnimateClear(result.ClearedCells, origin);

                if (result.PerfectClear)
                {
                    sfx?.PlayPerfect();
                    effects.Popup(boardView.CellToWorld(Board.Size / 2, Board.Size / 2), "PERFECT!",
                        Theme.Hex(0x2FD86B), 92f, 210f);
                    effects.Shake(26f);
                }
            }
            else
            {
                effects.Popup(centre, "+" + gained, Theme.WithAlpha(Theme.TextColor, 0.9f), 52f, 110f);
            }

            hud.SetScore(Score);
            if (Score > Best)
            {
                Best = Score;
                hud.SetBest(Best);
                SaveSystem.SaveBest(Best);
            }

            if (tray.IsEmpty) RefillTray();
            else tray.RefreshPlayability(Board);

            tray.SetInteractable(true);
            IsBusy = false;

            PiecePlaced?.Invoke(result, gained);
            Persist();
            EvaluateGameOver();
        }

        // ---- end of run --------------------------------------------------------------

        public void EvaluateGameOver()
        {
            if (IsGameOver) return;
            var shapes = tray.RemainingShapes();
            if (shapes.Count == 0 || Board.HasAnyPlacement(shapes)) return;

            IsGameOver = true;
            tray.SetInteractable(false);
            sfx?.PlayGameOver();
            bool newBest = Score >= Best && Score > 0;
            hud.ShowGameOver(Score, Best, newBest);
            SaveSystem.ClearGame();
            GameOver?.Invoke();
        }

        void Persist()
        {
            if (IsGameOver) return;
            SaveSystem.SaveGame(SaveSystem.Capture(Board, trayModel, Score, Combo, seed));
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Persist();
        }

        void OnApplicationQuit() => Persist();
    }
}
