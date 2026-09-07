using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlast.Presentation
{
    /// <summary>Score readouts, combo badge, restart button and the game-over card.</summary>
    public sealed class HudView : MonoBehaviour
    {
        TextMeshProUGUI scoreText;
        TextMeshProUGUI bestText;
        TextMeshProUGUI comboText;
        RectTransform comboBadge;
        CanvasGroup comboGroup;

        CanvasGroup gameOverGroup;
        RectTransform gameOverCard;
        TextMeshProUGUI gameOverScore;
        TextMeshProUGUI gameOverBest;
        TextMeshProUGUI gameOverTitle;

        Button soundButton;
        TextMeshProUGUI soundLabel;

        int displayedScore;
        Coroutine scoreRoutine;
        Coroutine comboRoutine;

        public event Action RestartRequested;
        public event Action SoundToggleRequested;

        public static HudView Create(Transform parent)
        {
            var go = new GameObject("Hud", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Ui.Fill(rt);
            var hud = go.AddComponent<HudView>();
            hud.Build(rt);
            return hud;
        }

        void Build(RectTransform root)
        {
            // ---- top bar --------------------------------------------------------
            var top = Ui.Rect("TopBar", root);
            Ui.AnchorTo(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                new Vector2(Theme.RefWidth, 280f));

            // Row one: best-score pill on the left, restart on the right.
            var bestPill = Ui.Image("BestPill", top, SpriteFactory.RoundedRect(96, 42),
                Theme.WithAlpha(Color.white, 0.08f), Image.Type.Sliced);
            Ui.AnchorTo(bestPill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -6f),
                new Vector2(276f, 78f));

            bestText = Ui.Text("Best", bestPill.rectTransform, "BEST  0", 36f, Theme.MutedTextColor);
            Ui.Fill(bestText.rectTransform);

            var restart = Ui.Button("Restart", top, "NEW", new Vector2(98f, 78f),
                Theme.WithAlpha(Color.white, 0.10f), 34f);
            Ui.AnchorTo(restart.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-46f, -6f), new Vector2(98f, 78f));
            restart.onClick.AddListener(() => RestartRequested?.Invoke());

            soundButton = Ui.Button("Sound", top, "SFX", new Vector2(98f, 78f),
                Theme.WithAlpha(Color.white, 0.10f), 30f);
            Ui.AnchorTo(soundButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-156f, -6f), new Vector2(98f, 78f));
            soundLabel = soundButton.GetComponentInChildren<TextMeshProUGUI>();
            soundButton.onClick.AddListener(() => SoundToggleRequested?.Invoke());

            var title = Ui.Text("Title", top, "BLOCK BLAST", 34f, Theme.WithAlpha(Theme.TextColor, 0.45f));
            Ui.AnchorTo(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f),
                new Vector2(420f, 48f));
            title.characterSpacing = 12f;

            // Row two: the score, the only thing that needs to be readable at a glance.
            scoreText = Ui.Text("Score", top, "0", 136f, Theme.TextColor);
            Ui.AnchorTo(scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f),
                new Vector2(900f, 160f));

            // ---- combo badge -----------------------------------------------------
            comboBadge = Ui.Rect("ComboBadge", root);
            Ui.AnchorTo(comboBadge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 620f),
                new Vector2(420f, 90f));
            comboGroup = comboBadge.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0f;

            var comboBg = Ui.Image("Bg", comboBadge, SpriteFactory.RoundedRect(96, 42), Theme.Hex(0xFF7A18, 0.92f),
                Image.Type.Sliced);
            Ui.Fill(comboBg.rectTransform);
            comboText = Ui.Text("Label", comboBadge, "COMBO x2", 52f, Color.white);
            Ui.Fill(comboText.rectTransform);

            // ---- game over card ---------------------------------------------------
            var overlay = Ui.Rect("GameOver", root, raycast: true);
            Ui.Fill(overlay);
            gameOverGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            Ui.SetAlpha(gameOverGroup, 0f);

            var dim = Ui.Image("Dim", overlay, null, new Color(0.02f, 0.01f, 0.08f, 0.82f));
            Ui.Fill(dim.rectTransform);

            gameOverCard = Ui.Rect("Card", overlay);
            gameOverCard.sizeDelta = new Vector2(820f, 780f);
            var cardBg = Ui.Image("CardBg", gameOverCard, SpriteFactory.RoundedRect(128, 44), Theme.PanelColor,
                Image.Type.Sliced);
            Ui.Fill(cardBg.rectTransform);

            gameOverTitle = Ui.Text("Title", gameOverCard, "NO MOVES LEFT", 68f, Theme.TextColor);
            Ui.AnchorTo(gameOverTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -80f), new Vector2(760f, 90f));

            var scoreLabel = Ui.Text("ScoreLabel", gameOverCard, "SCORE", 40f, Theme.MutedTextColor);
            Ui.AnchorTo(scoreLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -215f), new Vector2(600f, 60f));

            gameOverScore = Ui.Text("ScoreValue", gameOverCard, "0", 140f, Theme.TextColor);
            Ui.AnchorTo(gameOverScore.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -270f), new Vector2(700f, 160f));

            gameOverBest = Ui.Text("BestValue", gameOverCard, "BEST  0", 48f, Theme.MutedTextColor);
            Ui.AnchorTo(gameOverBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -450f), new Vector2(700f, 70f));

            var again = Ui.Button("PlayAgain", gameOverCard, "PLAY AGAIN", new Vector2(560f, 132f),
                Theme.Hex(0x2FD86B), 54f);
            Ui.AnchorTo(again.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 90f), new Vector2(560f, 132f));
            again.onClick.AddListener(() => RestartRequested?.Invoke());
        }

        // ---- score ---------------------------------------------------------------

        public void SetScore(int value, bool animate = true)
        {
            if (!animate || !isActiveAndEnabled)
            {
                displayedScore = value;
                scoreText.text = value.ToString();
                return;
            }
            if (scoreRoutine != null) StopCoroutine(scoreRoutine);
            scoreRoutine = StartCoroutine(CountTo(value));
        }

        IEnumerator CountTo(int target)
        {
            int from = displayedScore;
            yield return Tween.Run(0.32f, Tween.EaseOutCubic, t =>
            {
                displayedScore = Mathf.RoundToInt(Mathf.Lerp(from, target, t));
                scoreText.text = displayedScore.ToString();
            });
            displayedScore = target;
            scoreText.text = target.ToString();
            yield return Tween.Pop(scoreText.rectTransform, 1.10f, 0.16f);
        }

        public void SetBest(int value) => bestText.text = "BEST  " + value;

        /// <summary>Muted reads as a struck-through, faded button rather than a second icon.</summary>
        public void SetMuted(bool muted)
        {
            if (soundLabel == null) return;
            soundLabel.fontStyle = muted ? FontStyles.Bold | FontStyles.Strikethrough : FontStyles.Bold;
            soundLabel.color = muted ? Theme.WithAlpha(Theme.MutedTextColor, 0.55f) : Color.white;
            var image = soundButton.targetGraphic as Image;
            if (image != null) image.color = Theme.WithAlpha(Color.white, muted ? 0.05f : 0.10f);
        }

        // ---- combo ----------------------------------------------------------------

        public void ShowCombo(int combo)
        {
            if (comboRoutine != null) StopCoroutine(comboRoutine);
            comboText.text = "COMBO x" + ScoreRulesMultiplier(combo);
            comboRoutine = StartCoroutine(ComboFlash());
        }

        static int ScoreRulesMultiplier(int combo) => Core.ScoreRules.ComboMultiplier(combo);

        IEnumerator ComboFlash()
        {
            comboGroup.alpha = 1f;
            comboBadge.localScale = Vector3.one * 0.6f;
            yield return Tween.Scale(comboBadge, Vector3.one * 0.6f, Vector3.one, 0.22f, Tween.EaseOutBack);
            yield return new WaitForSecondsRealtime(0.75f);
            yield return Tween.Run(0.3f, null, t => comboGroup.alpha = 1f - t);
            comboGroup.alpha = 0f;
        }

        public void HideCombo()
        {
            if (comboRoutine != null) StopCoroutine(comboRoutine);
            comboGroup.alpha = 0f;
        }

        // ---- game over -------------------------------------------------------------

        public void ShowGameOver(int score, int best, bool newBest)
        {
            gameOverTitle.text = newBest ? "NEW BEST!" : "NO MOVES LEFT";
            gameOverTitle.color = newBest ? Theme.Hex(0xFFD400) : Theme.TextColor;
            gameOverScore.text = score.ToString();
            gameOverBest.text = "BEST  " + best;
            StartCoroutine(ShowGameOverRoutine());
        }

        IEnumerator ShowGameOverRoutine()
        {
            gameOverCard.localScale = Vector3.one * 0.7f;
            StartCoroutine(Tween.Scale(gameOverCard, Vector3.one * 0.7f, Vector3.one, 0.35f, Tween.EaseOutBack));
            yield return Tween.Fade(gameOverGroup, 0f, 1f, 0.25f);
            Ui.SetAlpha(gameOverGroup, 1f);
        }

        public void HideGameOver() => Ui.SetAlpha(gameOverGroup, 0f);

        public bool IsGameOverVisible => gameOverGroup.alpha > 0.5f;
    }
}
