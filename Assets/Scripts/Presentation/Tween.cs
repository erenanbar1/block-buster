using System;
using System.Collections;
using UnityEngine;

namespace BlockBlast.Presentation
{
    /// <summary>Tiny coroutine tween helpers, so the project needs no tweening package.</summary>
    public static class Tween
    {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInCubic(float t) => t * t * t;
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.9f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public static IEnumerator Run(float duration, Func<float, float> ease, Action<float> apply)
        {
            if (duration <= 0f)
            {
                apply(1f);
                yield break;
            }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                apply(ease != null ? ease(t) : t);
                yield return null;
            }
            apply(1f);
        }

        public static IEnumerator Scale(Transform target, Vector3 from, Vector3 to, float duration,
            Func<float, float> ease = null)
        {
            return Run(duration, ease ?? EaseOutCubic, t =>
            {
                if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, t);
            });
        }

        public static IEnumerator Move(RectTransform target, Vector2 from, Vector2 to, float duration,
            Func<float, float> ease = null)
        {
            return Run(duration, ease ?? EaseOutCubic, t =>
            {
                if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            });
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration,
            Func<float, float> ease = null)
        {
            return Run(duration, ease, t =>
            {
                if (group != null) Ui.SetAlpha(group, Mathf.Lerp(from, to, t));
            });
        }

        /// <summary>Quick squash-and-stretch pop used whenever something lands.</summary>
        public static IEnumerator Pop(Transform target, float overshoot = 1.18f, float duration = 0.18f)
        {
            if (target == null) yield break;
            Vector3 baseScale = Vector3.one;
            yield return Run(duration, null, t =>
            {
                if (target == null) return;
                float s = t < 0.4f
                    ? Mathf.Lerp(1f, overshoot, t / 0.4f)
                    : Mathf.Lerp(overshoot, 1f, EaseOutCubic((t - 0.4f) / 0.6f));
                target.localScale = baseScale * s;
            });
            if (target != null) target.localScale = baseScale;
        }
    }
}
