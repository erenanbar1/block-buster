using UnityEngine;

namespace BlockBlast.Presentation
{
    /// <summary>
    /// Synthesises every sound effect at startup, so the game needs no audio assets.
    /// Short blips built from a detuned square/sine mix with a percussive envelope.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        const int SampleRate = 44100;

        AudioSource source;
        AudioClip place;
        AudioClip invalid;
        AudioClip gameOver;
        AudioClip[] clears;   // pitch rises with the combo
        AudioClip perfect;

        public bool Muted { get; set; }

        public static SfxPlayer Create(Transform parent)
        {
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            return go.AddComponent<SfxPlayer>();
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.55f;

            place = Blip("sfx_place", new[] { 330f, 494f }, 0.09f, 0.55f, 0.35f);
            invalid = Blip("sfx_invalid", new[] { 120f, 90f }, 0.13f, 0.5f, 0.9f);
            gameOver = Arpeggio("sfx_gameover", new[] { 523f, 415f, 330f, 247f }, 0.16f);
            perfect = Arpeggio("sfx_perfect", new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.10f);

            clears = new AudioClip[5];
            for (int i = 0; i < clears.Length; i++)
            {
                float root = 440f * Mathf.Pow(1.1225f, i); // ~a whole tone per combo step
                clears[i] = Arpeggio("sfx_clear" + i, new[] { root, root * 1.26f, root * 1.5f }, 0.075f);
            }
        }

        public void PlayPlace() => Play(place, Random.Range(0.96f, 1.06f));
        public void PlayInvalid() => Play(invalid, 1f);
        public void PlayGameOver() => Play(gameOver, 1f);
        public void PlayPerfect() => Play(perfect, 1f);

        public void PlayClear(int combo)
        {
            int i = Mathf.Clamp(combo - 1, 0, clears.Length - 1);
            Play(clears[i], 1f);
        }

        void Play(AudioClip clip, float pitch)
        {
            if (Muted || clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip);
        }

        // ---- synthesis ----------------------------------------------------------

        /// <summary>One percussive tone: a couple of partials with an exponential decay.</summary>
        static AudioClip Blip(string name, float[] partials, float duration, float decay, float squareMix)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t / (duration * decay));
                float v = 0f;
                foreach (float f in partials)
                {
                    float phase = 2f * Mathf.PI * f * t;
                    float sine = Mathf.Sin(phase);
                    float square = Mathf.Sign(sine);
                    v += Mathf.Lerp(sine, square, squareMix);
                }
                v /= Mathf.Max(1, partials.Length);
                // tiny fade-in kills the click at sample zero
                v *= Mathf.Min(1f, t * 400f) * env;
                data[i] = v * 0.6f;
            }
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A sequence of blips concatenated into one clip.</summary>
        static AudioClip Arpeggio(string name, float[] notes, float noteDuration)
        {
            int perNote = Mathf.CeilToInt(SampleRate * noteDuration);
            var data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            {
                for (int i = 0; i < perNote; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-t / (noteDuration * 0.5f));
                    float phase = 2f * Mathf.PI * notes[n] * t;
                    float v = Mathf.Lerp(Mathf.Sin(phase), Mathf.Sin(phase * 2f) * 0.5f, 0.3f);
                    v *= Mathf.Min(1f, t * 400f) * env;
                    data[n * perNote + i] = v * 0.55f;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
