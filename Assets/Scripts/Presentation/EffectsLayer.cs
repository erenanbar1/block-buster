using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlast.Presentation
{
    /// <summary>
    /// Pooled confetti, floating score popups and screen shake. Everything lives on one
    /// non-interactive full-screen layer that sits above the board.
    /// </summary>
    public sealed class EffectsLayer : MonoBehaviour
    {
        class Particle
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Velocity;
            public float Spin;
            public float Life;
            public float MaxLife;
            public float Size;
        }

        readonly List<Particle> active = new List<Particle>();
        readonly Stack<Particle> pool = new Stack<Particle>();
        readonly List<TextMeshProUGUI> textPool = new List<TextMeshProUGUI>();

        RectTransform root;
        RectTransform shakeTarget;
        Vector2 shakeBase;
        float shakeAmount;
        float shakeDecay = 6f;

        const int MaxParticles = 220;

        public static EffectsLayer Create(Transform parent)
        {
            var go = new GameObject("Effects", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Ui.Fill(rt);
            return go.AddComponent<EffectsLayer>();
        }

        void Awake() => root = (RectTransform)transform;

        public void BindShakeTarget(RectTransform target)
        {
            shakeTarget = target;
            shakeBase = target != null ? target.anchoredPosition : Vector2.zero;
        }

        // ---- confetti ---------------------------------------------------------

        public void Burst(Vector3 worldPos, Color color, int count = 7)
        {
            Vector2 local = root.InverseTransformPoint(worldPos);
            for (int i = 0; i < count; i++)
            {
                if (active.Count >= MaxParticles) break;
                var p = Rent();
                p.Rt.anchoredPosition = local + Random.insideUnitCircle * 18f;
                p.Size = Random.Range(14f, 30f);
                p.Rt.sizeDelta = new Vector2(p.Size, p.Size);
                p.Rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                p.Rt.localScale = Vector3.one;
                p.Img.color = Color.Lerp(color, Color.white, Random.Range(0f, 0.45f));
                p.Img.enabled = true;

                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float speed = Random.Range(320f, 900f);
                p.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                p.Velocity.x += Random.Range(-160f, 160f);
                p.Spin = Random.Range(-540f, 540f);
                p.MaxLife = Random.Range(0.45f, 0.85f);
                p.Life = p.MaxLife;
                active.Add(p);
            }
        }

        Particle Rent()
        {
            if (pool.Count > 0) return pool.Pop();
            var img = Ui.Image("P", root, SpriteFactory.RoundedRect(32, 8), Color.white, Image.Type.Sliced);
            return new Particle { Rt = img.rectTransform, Img = img };
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Img.enabled = false;
                    active.RemoveAt(i);
                    pool.Push(p);
                    continue;
                }
                p.Velocity.y -= 2600f * dt;               // gravity
                p.Velocity *= 1f - Mathf.Min(1f, 1.6f * dt); // drag
                p.Rt.anchoredPosition += p.Velocity * dt;
                p.Rt.Rotate(0, 0, p.Spin * dt);

                float t = p.Life / p.MaxLife;
                var c = p.Img.color;
                c.a = Mathf.Clamp01(t * 1.6f);
                p.Img.color = c;
                p.Rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, t);
            }

            if (shakeTarget != null)
            {
                if (shakeAmount > 0.01f)
                {
                    shakeAmount = Mathf.Max(0f, shakeAmount - shakeDecay * shakeAmount * dt - 8f * dt);
                    shakeTarget.anchoredPosition = shakeBase + Random.insideUnitCircle * shakeAmount;
                }
                else if (shakeTarget.anchoredPosition != shakeBase)
                {
                    shakeTarget.anchoredPosition = shakeBase;
                }
            }
        }

        public void Shake(float amount) => shakeAmount = Mathf.Max(shakeAmount, amount);

        // ---- floating text ----------------------------------------------------

        public void Popup(Vector3 worldPos, string message, Color color, float size = 62f, float rise = 150f)
        {
            var tmp = RentText();
            tmp.text = message;
            tmp.color = color;
            tmp.fontSize = size;
            tmp.gameObject.SetActive(true);
            var rt = tmp.rectTransform;
            rt.anchoredPosition = root.InverseTransformPoint(worldPos);
            StartCoroutine(FloatText(tmp, rt.anchoredPosition, rise));
        }

        TextMeshProUGUI RentText()
        {
            foreach (var t in textPool)
                if (!t.gameObject.activeSelf) return t;
            var tmp = Ui.Text("Popup", root, string.Empty, 62f, Color.white);
            tmp.rectTransform.sizeDelta = new Vector2(600f, 100f);
            textPool.Add(tmp);
            return tmp;
        }

        IEnumerator FloatText(TextMeshProUGUI tmp, Vector2 start, float rise)
        {
            var rt = tmp.rectTransform;
            Color baseColor = tmp.color;
            yield return Tween.Run(0.85f, null, t =>
            {
                if (tmp == null) return;
                rt.anchoredPosition = start + new Vector2(0f, Tween.EaseOutCubic(t) * rise);
                float scale = t < 0.25f ? Mathf.Lerp(0.6f, 1.12f, t / 0.25f) : Mathf.Lerp(1.12f, 1f, (t - 0.25f) / 0.75f);
                rt.localScale = Vector3.one * scale;
                var c = baseColor;
                c.a = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                tmp.color = c;
            });
            if (tmp != null) tmp.gameObject.SetActive(false);
        }
    }
}
