using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One per area: named sound cues (filled by the builder from the area's SFX folder), an ambience bed, random
/// ambient one-shots (wolf howl, chains, steam...) and owned loops (footsteps, gear platforms).
/// Gameplay code just calls <see cref="Sfx.Play"/> / <see cref="Sfx.Loop"/>; unknown cue ids are silently ignored.
/// Sounds are quieter the farther they are from the camera (2D game: distance on screen, not 3D audio).
/// </summary>
public class GameAudio : MonoBehaviour
{
    [Serializable]
    public class Cue
    {
        public string id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.8f;
        public Vector2 pitch = new Vector2(0.94f, 1.06f);
        [Tooltip("Beyond this many units from the camera the sound is silent (0 = always full volume).")]
        public float range = 22f;
        [Tooltip("Minimum seconds between two plays (stops machine-gun stacking).")]
        public float cooldown = 0.04f;
    }

    [Serializable]
    public class Ambient
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.4f;
        public Vector2 interval = new Vector2(12f, 25f);
        [HideInInspector] public float next;
    }

    public Cue[] cues = new Cue[0];
    public AudioClip ambienceBed;
    [Range(0f, 1f)] public float ambienceVolume = 0.35f;
    public AudioClip ambienceBed2;
    [Range(0f, 1f)] public float ambienceVolume2 = 0.2f;
    public Ambient[] randomAmbient = new Ambient[0];
    [Tooltip("Generate a soft, gusting wind loop as an extra bed (areas with no recorded ambience).")]
    public bool proceduralWind;
    [Tooltip("Generate a slow, dark ambient chord loop (semitones from A; -99 = no music).")]
    public int musicKey = -99;
    [Range(0f, 1f)] public float musicVolume = 0.32f;
    [Range(0f, 1f)] public float windVolume = 0.3f;

    public static GameAudio Instance { get; private set; }

    readonly Dictionary<string, Cue> _map = new();
    readonly Dictionary<string, float> _last = new();
    readonly List<AudioSource> _pool = new();
    readonly Dictionary<object, (AudioSource src, string id)> _loops = new();
    AudioSource _bed, _bed2, _wind, _music;

    /// <summary>
    /// 32 s seamless ambient loop: four minor chords (i - VI - III - VII) swelling and fading into each other over a
    /// low drone, a soft bell note every two seconds, and a wrap-around echo for space. Melancholic, not busy.
    /// </summary>
    public static AudioClip MakeMusic(int key)
    {
        const int rate = 22050, seconds = 32;
        int n = rate * seconds;
        var y = new float[n];
        float root = 110f * Mathf.Pow(2f, key / 12f);
        int[][] chords = { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { 3, 7, 10 }, new[] { -2, 2, 5 } };
        float F(int semi) => root * Mathf.Pow(2f, semi / 12f);
        var rng = new System.Random(key + 99);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float s = 0f;
            for (int c = 0; c < 4; c++)
            {
                float local = Mathf.Repeat(t - c * 8f, seconds);
                if (local > 10f) continue;
                float env = Mathf.SmoothStep(0f, 1f, local / 2.5f) * Mathf.SmoothStep(0f, 1f, (10f - local) / 2.5f);
                foreach (var semi in chords[c])
                {
                    float f = F(semi) * 2f;
                    s += env * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 2.003f * t)) * 0.16f;
                }
                s += env * Mathf.Sin(2f * Mathf.PI * F(chords[c][0]) * t) * 0.22f;                 // low drone
            }
            y[i] = s * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 8f));                          // slow breathing
        }
        // bell: one chord tone, two octaves up, every two seconds, ringing out
        for (int b = 0; b < seconds / 2; b++)
        {
            var ch = chords[(b / 4) % 4];
            float f = F(ch[rng.Next(ch.Length)]) * 4f;
            int start = b * 2 * rate;
            for (int j = 0; j < rate * 2 && start + j < n; j++)
            {
                float lt = j / (float)rate;
                y[start + j] += Mathf.Sin(2f * Mathf.PI * f * lt) * Mathf.Exp(-lt * 2.2f) * 0.12f * Mathf.Min(1f, lt * 200f);
            }
        }
        // wrap-around echo (keeps the loop seamless)
        int d = (int)(rate * 0.43f);
        var wet = (float[])y.Clone();
        for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i < n; i++) wet[i] += 0.32f * wet[(i - d + n) % n];
        float peak = 0.001f;
        for (int i = 0; i < n; i++) { y[i] = y[i] * 0.6f + wet[i] * 0.4f; peak = Mathf.Max(peak, Mathf.Abs(y[i])); }
        for (int i = 0; i < n; i++) y[i] = y[i] / peak * 0.8f;
        var clip = AudioClip.Create("Music (generated)", n, 1, rate, false);
        clip.SetData(y, 0);
        return clip;
    }

    /// <summary>12 s seamless loop of filtered noise with slow gusts: night wind through trees.</summary>
    static AudioClip MakeWind()
    {
        const int rate = 22050, seconds = 12;
        int n = rate * seconds;
        var data = new float[n];
        var rng = new System.Random(5);
        float brown = 0f, lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.998f;
            float t = i / (float)n;
            // gusts: two slow waves that both complete whole cycles in the loop (seamless)
            float gust = 0.45f + 0.35f * Mathf.Sin(t * Mathf.PI * 2f * 2f) * Mathf.Sin(t * Mathf.PI * 2f * 3f + 1f) + 0.2f * Mathf.Sin(t * Mathf.PI * 2f * 5f);
            float cut = Mathf.Lerp(0.02f, 0.09f, gust);
            lp += (white - lp) * cut;                          // breathy hiss whose brightness follows the gust
            hp = lp - hp * 0.0f;
            data[i] = (brown * 0.8f + hp * 0.9f) * Mathf.Clamp01(gust) * 0.6f;
        }
        int fade = rate / 4;                                   // crossfade the seam
        for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = data[i] * k + data[n - fade + i] * (1f - k); }
        var clip = AudioClip.Create("Wind (generated)", n - fade, 1, rate, false);
        var trimmed = new float[n - fade];
        System.Array.Copy(data, trimmed, n - fade);
        clip.SetData(trimmed, 0);
        return clip;
    }
    Transform _cam;

    void Awake()
    {
        Instance = this;
        foreach (var c in cues) if (c != null && !string.IsNullOrEmpty(c.id)) _map[c.id] = c;
        for (int i = 0; i < 20; i++) _pool.Add(NewSource());
        _bed = Bed(ambienceBed, ambienceVolume);
        _bed2 = Bed(ambienceBed2, ambienceVolume2);
        if (proceduralWind) _wind = Bed(MakeWind(), windVolume);
        if (musicKey > -99) _music = Bed(MakeMusic(musicKey), musicVolume);
        foreach (var a in randomAmbient) a.next = Time.time + UnityEngine.Random.Range(a.interval.x * 0.3f, a.interval.y * 0.6f);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    AudioSource NewSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    AudioSource Bed(AudioClip clip, float vol)
    {
        if (!clip) return null;
        var s = NewSource();
        s.clip = clip;
        s.loop = true;
        s.volume = vol;
        s.Play();
        return s;
    }

    float Falloff(Cue c, Vector3? at)
    {
        if (!at.HasValue || c.range <= 0f) return 1f;
        if (!_cam && Camera.main) _cam = Camera.main.transform;
        if (!_cam) return 1f;
        float d = Vector2.Distance(at.Value, _cam.position);
        return Mathf.Clamp01(1f - Mathf.Max(0f, d - 8f) / Mathf.Max(0.01f, c.range - 8f));
    }

    public void PlayCue(string id, Vector3? at, float volumeScale, float pitchScale)
    {
        if (!_map.TryGetValue(id, out var c) || c.clips == null || c.clips.Length == 0) return;
        if (_last.TryGetValue(id, out var t) && Time.unscaledTime - t < c.cooldown) return;
        float vol = c.volume * volumeScale * Falloff(c, at) * MenuUI.SfxVolume;
        if (vol <= 0.01f) return;
        _last[id] = Time.unscaledTime;
        AudioSource src = null;
        foreach (var s in _pool) if (!s.isPlaying) { src = s; break; }
        if (!src) { src = NewSource(); _pool.Add(src); }
        src.clip = c.clips[UnityEngine.Random.Range(0, c.clips.Length)];
        src.loop = false;
        src.volume = vol;
        src.pitch = UnityEngine.Random.Range(c.pitch.x, c.pitch.y) * pitchScale;
        src.Play();
    }

    public void SetLoop(object owner, string id, bool on, Vector3? at, float volumeScale)
    {
        _loops.TryGetValue(owner, out var l);
        if (!on || !_map.TryGetValue(id, out var c) || c.clips == null || c.clips.Length == 0)
        {
            if (l.src) l.src.volume = Mathf.MoveTowards(l.src.volume, 0f, Time.unscaledDeltaTime * 4f);
            if (l.src && l.src.volume <= 0f) l.src.Stop();
            return;
        }
        if (!l.src || l.id != id)
        {
            if (l.src) l.src.Stop();
            l = (l.src ? l.src : NewSource(), id);
            l.src.clip = c.clips[0];
            l.src.loop = true;
            l.src.pitch = (c.pitch.x + c.pitch.y) * 0.5f;
            l.src.volume = 0f;
            l.src.Play();
            _loops[owner] = l;
        }
        if (!l.src.isPlaying) l.src.Play();
        float target = c.volume * volumeScale * Falloff(c, at) * MenuUI.SfxVolume;
        l.src.volume = Mathf.MoveTowards(l.src.volume, target, Time.unscaledDeltaTime * 4f);
    }

    void Update()
    {
        // Muffle everything while the game is paused by a menu; cutscenes keep the ambience but quieter.
        float duck = (PauseMenu.Paused ? 0.25f : DialogueDirector.Active ? 0.6f : 1f) * MenuUI.MusicVolume;
        if (_bed) _bed.volume = Mathf.MoveTowards(_bed.volume, ambienceVolume * duck, Time.unscaledDeltaTime);
        if (_bed2) _bed2.volume = Mathf.MoveTowards(_bed2.volume, ambienceVolume2 * duck, Time.unscaledDeltaTime);
        if (_wind) _wind.volume = Mathf.MoveTowards(_wind.volume, windVolume * duck, Time.unscaledDeltaTime);
        if (_music) _music.volume = Mathf.MoveTowards(_music.volume, musicVolume * duck, Time.unscaledDeltaTime);
        if (PauseMenu.Paused || DialogueDirector.Active) return;
        foreach (var a in randomAmbient)
        {
            if (!a.clip || Time.time < a.next) continue;
            a.next = Time.time + UnityEngine.Random.Range(a.interval.x, a.interval.y);
            AudioSource src = null;
            foreach (var s in _pool) if (!s.isPlaying) { src = s; break; }
            if (!src) continue;
            src.clip = a.clip;
            src.loop = false;
            src.volume = a.volume * MenuUI.MusicVolume;
            src.pitch = UnityEngine.Random.Range(0.92f, 1.05f);
            src.Play();
        }
    }
}

/// <summary>Static front door for sound: Sfx.Play("ember_shot", position).</summary>
public static class Sfx
{
    public static void Play(string id, Vector3? at = null, float volume = 1f, float pitch = 1f)
    {
        if (GameAudio.Instance) GameAudio.Instance.PlayCue(id, at, volume, pitch);
    }

    /// <summary>Keep a looping cue running for this owner while on is true (footsteps, gears); fades in/out.</summary>
    public static void Loop(object owner, string id, bool on, Vector3? at = null, float volume = 1f)
    {
        if (GameAudio.Instance) GameAudio.Instance.SetLoop(owner, id, on, at, volume);
    }
}
