using System.Collections.Generic;
using Mleczarka.Core;
using UnityEngine;

namespace Mleczarka.Gameplay
{
    /// <summary>
    /// Dźwięk gry: muzyka (dzień 6–18 / wieczór, płynne przejście), tło wsi (ptaki, wiatr) i efekty czynności.
    /// Klipy (ElevenLabs, licencja komercyjna planu Starter) przypina narzędzie „Mleczarka/Kariera/11. Muzyka i dźwięki”.
    /// Efekty: GameAudio.Play("Monety"), pętle: GameAudio.Loop("Dojenie_Wiadro", true, transform).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public AudioClip musicDay, musicEvening, ambient;
        public AudioClip[] effects;
        [Range(0, 1)] public float musicVolume = 0.32f, ambientVolume = 0.22f, effectsVolume = 0.8f;

        static GameAudio _i;
        public static GameAudio I => _i ? _i : (_i = FindFirstObjectByType<GameAudio>());
        readonly Dictionary<string, AudioClip> _fx = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioSource> _loops = new Dictionary<string, AudioSource>();
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        AudioSource _musA, _musB, _amb, _ui; bool _aIsCurrent = true; AudioClip _wanted;
        double _lastMoney = double.NaN;

        void Awake()
        {
            _i = this;
            foreach (var c in effects) if (c) _fx[c.name] = c;
            _musA = Src(false, true); _musB = Src(false, true); _amb = Src(false, true); _ui = Src(false, false);
            if (ambient) { _amb.clip = ambient; _amb.volume = ambientVolume; _amb.Play(); }
        }

        AudioSource Src(bool spatial, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = loop; s.spatialBlend = spatial ? 1f : 0f; return s;
        }

        void Update()
        {
            var g = GameController.Instance;
            // muzyka wg pory dnia
            AudioClip want = musicDay;
            if (g && g.Sim != null) { var c = g.Sim.Clock(); if ((c.hh >= 18 || c.hh < 6) && musicEvening) want = musicEvening; }
            if (want && want != _wanted)
            {
                _wanted = want; _aIsCurrent = !_aIsCurrent;
                var to = _aIsCurrent ? _musA : _musB; to.clip = want; to.volume = 0; to.Play();
            }
            var cur = _aIsCurrent ? _musA : _musB; var old = _aIsCurrent ? _musB : _musA;
            cur.volume = Mathf.MoveTowards(cur.volume, musicVolume, Time.unscaledDeltaTime * 0.15f);
            old.volume = Mathf.MoveTowards(old.volume, 0, Time.unscaledDeltaTime * 0.15f);
            if (old.volume <= 0 && old.isPlaying) old.Stop();
            // pieniądze przybyły (sprzedaż, dostawa) — brzęk monet
            if (g && g.Sim != null)
            {
                double m = g.Sim.S.players[GameController.Me].money;
                if (!double.IsNaN(_lastMoney) && m > _lastMoney + 0.01) Play("Monety");
                _lastMoney = m;
            }
        }

        /// <summary>Jednorazowy efekt (w świecie, jeśli podano miejsce), najwyżej maxSeconds (dłuższe klipy wyciszane na końcu).
        /// Ten sam efekt nie częściej niż co minGap s.</summary>
        public static void Play(string id, Vector3? at = null, float volume = 1f, float minGap = 0.15f, float maxSeconds = 999f)
        {
            var a = I; if (!a || !a._fx.TryGetValue(id, out var clip)) return;
            if (a._last.TryGetValue(id, out var t) && Time.unscaledTime - t < minGap) return;
            a._last[id] = Time.unscaledTime;
            var go = new GameObject("Dźwięk: " + id);
            var s = go.AddComponent<AudioSource>(); s.clip = clip; s.volume = volume * a.effectsVolume;
            if (at.HasValue) { go.transform.position = at.Value; s.spatialBlend = 1f; s.minDistance = 3f; s.maxDistance = 45f; s.rolloffMode = AudioRolloffMode.Linear; }
            s.Play();
            float len = Mathf.Min(clip.length, maxSeconds);
            if (len < clip.length) a.StartCoroutine(FadeOut(s, len));
            Destroy(go, len + 0.4f);
        }

        static System.Collections.IEnumerator FadeOut(AudioSource s, float after)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0, after - 0.3f));
            float v = s ? s.volume : 0;
            for (float t = 0; t < 0.3f && s; t += Time.unscaledDeltaTime) { s.volume = v * (1 - t / 0.3f); yield return null; }
        }

        /// <summary>Efekt ciągły (dojenie, mieszanie): włącza/wyłącza pętlę przypiętą do obiektu.</summary>
        public static void Loop(string id, bool on, Transform follow = null, float volume = 1f)
        {
            var a = I; if (!a) return;
            if (!a._loops.TryGetValue(id, out var s) || !s)
            {
                if (!on || !a._fx.TryGetValue(id, out var clip)) return;
                var go = new GameObject("Pętla: " + id); s = go.AddComponent<AudioSource>(); s.clip = clip; s.loop = true;
                s.spatialBlend = follow ? 1f : 0f; s.minDistance = 2f; s.maxDistance = 25f; s.rolloffMode = AudioRolloffMode.Linear;
                a._loops[id] = s;
            }
            if (follow) s.transform.position = follow.position;
            s.volume = volume * a.effectsVolume;
            if (on && !s.isPlaying) s.Play(); else if (!on && s.isPlaying) s.Stop();
        }
    }
}
