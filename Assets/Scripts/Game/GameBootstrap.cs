using BlockBlast.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BlockBlast.Game
{
    /// <summary>
    /// Single entry point: builds the camera, canvas, every view and the controller at
    /// runtime. The scene itself only needs one GameObject carrying this component, which
    /// keeps the whole game reviewable as code instead of as scene diffs.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const float BoardCentreY = 60f;

        public GameController Controller { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform PlayRoot { get; private set; }
        public RectTransform DragLayer { get; private set; }

        public static GameBootstrap Instance { get; private set; }

        Camera uiCamera;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            uiCamera = EnsureCamera();
            EnsureEventSystem();
            BuildUi();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Camera EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera));
                go.tag = "MainCamera";
                go.transform.SetParent(transform, false);
                cam = go.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.BackdropBottom;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            return cam;
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
            go.AddComponent<InputSystemUIInputModule>();
        }

        void BuildUi()
        {
            // ---- canvas ---------------------------------------------------------
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas = canvasGo.GetComponent<Canvas>();
            // Rendered through the camera rather than as an overlay, so post effects and
            // screen captures see the game exactly as the player does.
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = uiCamera;
            Canvas.planeDistance = 10f;
            Canvas.pixelPerfect = false;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Theme.RefWidth, Theme.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            // ---- backdrop (fills the whole screen, outside the safe area) ---------
            var backdrop = Ui.Image("Backdrop", canvasGo.transform,
                SpriteFactory.VerticalGradient(Theme.BackdropTop, Theme.BackdropBottom), Color.white,
                Image.Type.Sliced);
            Ui.Fill(backdrop.rectTransform);

            var glow = Ui.Image("Glow", canvasGo.transform, SpriteFactory.Circle(256),
                Theme.Hex(0x6A3BFF, 0.30f));
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.72f);
            glow.rectTransform.sizeDelta = new Vector2(1700f, 1700f);

            // ---- safe area + fixed design frame -----------------------------------
            var safe = Ui.Rect("SafeArea", canvasGo.transform);
            Ui.Fill(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            PlayRoot = Ui.Rect("Play", safe);
            PlayRoot.sizeDelta = new Vector2(Theme.RefWidth, Theme.RefHeight);
            PlayRoot.gameObject.AddComponent<DesignFitter>();

            // ---- views --------------------------------------------------------------
            var effects = EffectsLayer.Create(PlayRoot);

            var board = BoardView.Create(PlayRoot, Theme.BoardSize, effects);
            ((RectTransform)board.transform).anchoredPosition = new Vector2(0f, BoardCentreY);

            DragLayer = Ui.Rect("DragLayer", PlayRoot);
            Ui.Fill(DragLayer);

            var tray = TrayView.Create(PlayRoot, DragLayer, uiCamera);
            var hud = HudView.Create(PlayRoot);

            // Draw order, bottom to top: board, confetti, tray, dragged piece, hud.
            board.transform.SetSiblingIndex(0);
            effects.transform.SetSiblingIndex(1);
            tray.transform.SetSiblingIndex(2);
            DragLayer.SetSiblingIndex(3);
            hud.transform.SetSiblingIndex(4);

            effects.BindShakeTarget((RectTransform)board.transform);

            var sfx = SfxPlayer.Create(transform);

            Controller = gameObject.AddComponent<GameController>();
            Controller.Initialise(board, tray, hud, effects, sfx);
        }
    }
}
