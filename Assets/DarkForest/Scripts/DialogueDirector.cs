using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// In-engine cutscene beats: the game pauses, black bars slide in, the camera eases in on Ilyan and white lines
/// type out at the bottom (speaker name tinted). Space / Enter / click / gamepad South continues, Esc skips.
/// Lines are "Speaker|Text" (Speaker empty = narration). Used before fights, at area starts and at exits.
/// </summary>
[DefaultExecutionOrder(-500)]
public class DialogueDirector : MonoBehaviour
{
    public static bool Active { get; private set; }
    /// <summary>Unscaled time the last dialogue closed (inputs wait a moment so the "continue" click doesn't fire).</summary>
    public static float LastEnd { get; private set; } = -10f;
    public static bool Blocking => Active || Time.unscaledTime - LastEnd < 0.3f;
    static DialogueDirector _inst;

    public float zoomSize = 6.2f;
    public float charsPerSecond = 42f;

    readonly Queue<(string speaker, string text)> _lines = new();
    Action _onDone;
    string _speaker, _text;
    float _shown, _bars, _zoom;
    float _baseSize;
    Vector3 _baseCam;
    Camera _cam;
    CameraFollow2D _follow;
    Transform _focus;
    bool _closing;
    float _prevScale = 1f;
    bool _wasDown, _escWas = true;

    public static DialogueDirector Instance
    {
        get
        {
            if (!_inst) _inst = new GameObject("Dialogue Director").AddComponent<DialogueDirector>();
            return _inst;
        }
    }

    void Awake() { _inst = this; Active = false; }

    void OnDestroy()
    {
        if (_inst == this) { Active = false; if (Time.timeScale == 0f) Time.timeScale = 1f; }
    }

    /// <summary>Pause the game and play these lines; onDone runs when they finish (or are skipped).</summary>
    public static void Play(string[] lines, Action onDone = null, Transform focus = null)
    {
        if (lines == null || lines.Length == 0) { onDone?.Invoke(); return; }
        Instance.Begin(lines, onDone, focus);
    }

    void Begin(string[] lines, Action onDone, Transform focus)
    {
        foreach (var l in lines)
        {
            int bar = l.IndexOf('|');
            _lines.Enqueue(bar < 0 ? ("", l) : (l[..bar], l[(bar + 1)..]));
        }
        _onDone += onDone;
        if (Active) return;
        Active = true;
        _closing = false;
        _cam = Camera.main;
        _follow = _cam ? _cam.GetComponent<CameraFollow2D>() : null;
        if (_follow) _follow.enabled = false;
        if (_cam) { _baseSize = _cam.orthographicSize; _baseCam = _cam.transform.position; }
        var p = GameObject.Find("Player");
        _focus = focus ? focus : p ? p.transform : null;
        _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        _wasDown = true;   // the key that started this must be released first
        _escWas = true;
        Next();
    }

    AudioSource _voice;
    float _voiceEnd = -1f;
    float _cps;

    void Next()
    {
        if (_voice) _voice.Stop();
        _voiceEnd = -1f;
        if (_lines.Count == 0) { _closing = true; return; }
        (_speaker, _text) = _lines.Dequeue();
        _shown = 0f;
        _cps = charsPerSecond;
        var clip = VoiceBank.Find(_text);
        if (clip)
        {
            if (!_voice) { _voice = gameObject.AddComponent<AudioSource>(); _voice.ignoreListenerPause = true; }
            _voice.clip = clip;
            _voice.volume = MenuUI.VoiceVolume;
            _voice.Play();
            _voiceEnd = Time.unscaledTime + clip.length;
            _cps = Mathf.Max(18f, _text.Length / Mathf.Max(0.5f, clip.length * 0.9f));   // the words appear as they are spoken
        }
    }

    static bool Down()
    {
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        return (k != null && (k.spaceKey.isPressed || k.enterKey.isPressed || k.jKey.isPressed)) || (m != null && m.leftButton.isPressed)
               || (g != null && g.buttonSouth.isPressed);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _bars = Mathf.MoveTowards(_bars, Active && !_closing ? 1f : 0f, dt * 3f);
        if (!Active) return;

        Time.timeScale = 0f;
        _zoom = Mathf.MoveTowards(_zoom, _closing ? 0f : 1f, dt * 1.6f);
        float e = _zoom * _zoom * (3f - 2f * _zoom);
        if (_cam)
        {
            _cam.orthographicSize = Mathf.Lerp(_baseSize, zoomSize, e);
            var target = _focus ? new Vector3(_focus.position.x, _focus.position.y + (_focus.CompareTag("Player") || _focus.name == "Player" ? 0.6f : 0.4f), _baseCam.z) : _baseCam;
            _cam.transform.position = Vector3.Lerp(_baseCam, target, e);
        }

        if (_closing)
        {
            if (_zoom <= 0f) End();
            return;
        }

        _shown += dt * _cps;
        // A spoken line moves on by itself shortly after the voice ends.
        if (_voiceEnd > 0f && Time.unscaledTime > _voiceEnd + 0.7f && _shown >= _text.Length) { Next(); return; }
        var k = Keyboard.current;
        bool esc = k != null && k.escapeKey.isPressed;
        bool escPressed = esc && !_escWas;                     // own edge detection: never misses a frame
        _escWas = esc;
        if (escPressed) { _lines.Clear(); if (_voice) _voice.Stop(); _closing = true; return; }
        bool down = Down();
        bool pressed = down && !_wasDown;
        _wasDown = down;
        if (pressed)
        {
            if (_shown < _text.Length) _shown = _text.Length;   // first press completes the line
            else Next();
        }
    }

    void End()
    {
        Active = false;
        LastEnd = Time.unscaledTime;
        if (_cam) { _cam.orthographicSize = _baseSize; }
        if (_follow) _follow.enabled = true;
        Time.timeScale = _prevScale;
        foreach (var e in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) e.Grace(0.8f);   // a breath before anyone attacks
        var done = _onDone;
        _onDone = null;
        done?.Invoke();
    }

    void OnGUI()
    {
        MenuUI.ApplyFont();
        if (_bars <= 0f) return;
        float s = Screen.height / 1080f;
        float bh = Screen.height * 0.13f * _bars;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, bh), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - bh, Screen.width, bh), Texture2D.whiteTexture);
        if (!Active || _closing || _text == null) { GUI.color = Color.white; return; }

        var name = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var body = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(32 * s), alignment = TextAnchor.UpperCenter, wordWrap = true };
        float y = Screen.height - Screen.height * 0.13f;
        if (!string.IsNullOrEmpty(_speaker))
        {
            GUI.color = _speaker == "Mira" ? new Color(0.5f, 0.9f, 1f) : _speaker == "Ilyan" ? new Color(1f, 0.78f, 0.45f) : new Color(1f, 0.45f, 0.4f);
            GUI.Label(new Rect(0, y - 2 * s, Screen.width, 32 * s), _speaker.ToUpperInvariant(), name);
        }
        GUI.color = string.IsNullOrEmpty(_speaker) ? new Color(0.85f, 0.85f, 0.9f) : Color.white;
        int n = Mathf.Clamp((int)_shown, 0, _text.Length);
        var text = string.IsNullOrEmpty(_speaker) ? $"<i>{_text[..n]}</i>" : _text[..n];
        body.richText = true;
        GUI.Label(new Rect(Screen.width * 0.15f, y + 30 * s, Screen.width * 0.7f, Screen.height * 0.1f), text, body);
        if (n >= _text.Length)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
            var hint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * s), alignment = TextAnchor.LowerRight };
            GUI.Label(new Rect(0, Screen.height - 34 * s, Screen.width - 30 * s, 28 * s), "Space ▶     Esc skip", hint);
        }
        GUI.color = Color.white;
    }
}
