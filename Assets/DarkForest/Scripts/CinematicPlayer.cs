using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

/// <summary>
/// Full-screen story video between levels (opening before Area A, "Scene 02" after the clock tower). Fades in
/// from black, Esc / Space / Enter skips (with a fade), then either loads the next scene or - at the end of the
/// game - rolls the credits upward over black and returns to the title screen.
/// </summary>
public class CinematicPlayer : MonoBehaviour
{
    public VideoClip clip;
    public string nextScene = "AreaA_BurningVillage";
    public string nextTitle = "Area A";
    public string nextSubtitle = "The Burning Village";
    public bool rollCredits;
    public float creditsSpeed = 70f;
    [Tooltip("Ilyan's coughs, gasps and cries laid over the film at these times (seconds).")]
    public float[] cueTimes = new float[0];
    public AudioClip[] cueClips = new AudioClip[0];
    [Range(0f, 1f)] public float cueVolume = 0.85f;

    VideoPlayer _vp;
    AudioSource _audio;
    RenderTexture _rt;
    float _black = 1f;
    bool _skip, _keyWas = true, _showHint;
    float _hintT;
    bool _credits;
    float _scroll;
    float _creditsHeight = 2600f;

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        MenuUI.ApplySettings();
        if (clip)
        {
            _rt = new RenderTexture((int)clip.width, (int)clip.height, 0);
            _vp = gameObject.AddComponent<VideoPlayer>();
            _vp.playOnAwake = false;
            _vp.clip = clip;
            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.targetTexture = _rt;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.volume = Mathf.Max(MenuUI.MusicVolume, MenuUI.VoiceVolume);
            _vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _vp.SetTargetAudioSource(0, _audio);
            _vp.Prepare();
            float wait = 0f;
            while (!_vp.isPrepared && wait < 15f) { wait += Time.unscaledDeltaTime; yield return null; }
            _vp.Play();
            while (_black > 0f && !_skip) { _black = Mathf.MoveTowards(_black, 0f, Time.unscaledDeltaTime * 1.4f); yield return null; }
            // the video (until it ends or the player skips), with the boy's voice cues on top
            var voice = gameObject.AddComponent<AudioSource>();
            int next = 0;
            while (!_skip && (_vp.isPlaying || _vp.time < 0.2f))
            {
                while (next < cueTimes.Length && next < cueClips.Length && _vp.time >= cueTimes[next])
                {
                    if (cueClips[next]) voice.PlayOneShot(cueClips[next], cueVolume * MenuUI.VoiceVolume);
                    next++;
                }
                yield return null;
            }
            voice.Stop();
            while (_black < 1f) { _black = Mathf.MoveTowards(_black, 1f, Time.unscaledDeltaTime * 1.4f); if (_audio) _audio.volume *= 0.92f; yield return null; }
            _vp.Stop();
        }
        if (rollCredits)
        {
            _skip = false;
            _credits = true;
            _scroll = 0f;
            _titleA = 0f;
            var music = gameObject.AddComponent<AudioSource>();
            music.clip = GameAudio.MakeMusic(-5);                 // slow, soft, in F minor
            music.loop = true;
            music.volume = 0f;
            music.Play();
            float mv = 0.55f * MenuUI.MusicVolume;
            while (_black > 0f) { _black = Mathf.MoveTowards(_black, 0f, Time.unscaledDeltaTime * 0.8f); music.volume = mv * (1f - _black); yield return null; }
            while (!_skip && _scroll < _creditsHeight * MenuUI.S + Screen.height * 0.75f)
            {
                _titleA = Mathf.MoveTowards(_titleA, 1f, Time.unscaledDeltaTime * 0.5f);
                if (_titleA >= 1f) _scroll += Time.unscaledDeltaTime * creditsSpeed * MenuUI.S;   // the title settles before names roll
                yield return null;
            }
            while (_black < 1f) { _black = Mathf.MoveTowards(_black, 1f, Time.unscaledDeltaTime * 0.6f); music.volume = mv * (1f - _black); yield return null; }
            _credits = false;
            LoadingScreen.Load("MainMenu", "Ashen Bond", "");
        }
        else LoadingScreen.Load(nextScene, nextTitle, nextSubtitle);
    }

    void Update()
    {
        var k = Keyboard.current; var g = Gamepad.current; var m = Mouse.current;
        bool any = (k != null && k.anyKey.isPressed) || (m != null && (m.leftButton.isPressed || m.delta.ReadValue().sqrMagnitude > 4f)) || (g != null && g.buttonSouth.isPressed);
        bool skipKey = (k != null && (k.escapeKey.isPressed || k.spaceKey.isPressed || k.enterKey.isPressed)) || (g != null && (g.startButton.isPressed || g.buttonSouth.isPressed));
        if (any) { _showHint = true; _hintT = 2.5f; }
        _hintT -= Time.unscaledDeltaTime;
        if (skipKey && !_keyWas) _skip = true;                    // Esc / Space / Enter skip right away
        _keyWas = skipKey;
    }

    void OnDestroy() { if (_rt) _rt.Release(); }

    void OnGUI()
    {
        MenuUI.ApplyFont();
        float s = MenuUI.S;
        MenuUI.Box(new Rect(0, 0, Screen.width, Screen.height), Color.black);
        if (_rt && !_credits)
        {
            float a = (float)_rt.width / _rt.height;
            float w = Screen.width, h = w / a;
            if (h > Screen.height) { h = Screen.height; w = h * a; }
            GUI.DrawTexture(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), _rt);
        }
        if (_credits) Credits(s);
        MenuUI.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, _black));
        if (_showHint && _hintT > 0f)
        {
            float a = Mathf.Clamp01(_hintT);
            var r = new Rect(Screen.width - 250 * s, Screen.height - 66 * s, 220 * s, 44 * s);
            GUI.color = new Color(1f, 1f, 1f, a);
            MenuUI.Key(new Rect(r.x, r.y + 6 * s, 70 * s, 34 * s), "Esc");
            MenuUI.Text(new Rect(r.x + 84 * s, r.y, 140 * s, r.height), "Skip", 22, new Color(0.86f, 0.82f, 0.78f, a));
        }
    }

    float _titleA;
    public string creditsTitle = "Living in the mess, I created with my own hands";

    /// <summary>
    /// Film-style end credits: the title stays at the top; below it, role / name / description cards roll upward
    /// and fade in and out at the edges of the scrolling area.
    /// </summary>
    void Credits(float s)
    {
        float cx = Screen.width / 2f;
        float top = Screen.height * 0.24f;                         // the scrolling area starts under the title
        // ---- pinned title
        var ta = _titleA;
        MenuUI.Text(new Rect(cx - 800 * s, Screen.height * 0.07f, 1600 * s, 90 * s), "<i>" + creditsTitle + "</i>", 46, new Color(0.9f, 0.86f, 0.8f, ta), TextAnchor.MiddleCenter);
        MenuUI.Box(new Rect(cx - 260 * s * ta, Screen.height * 0.07f + 96 * s, 520 * s * ta, 2 * s), new Color(0.75f, 0.15f, 0.1f, ta));
        // ---- rolling cards
        float y = Screen.height - _scroll;
        void Line(string text, int size, Color c, float gap, bool bold = false, float width = 1200f, int lines = 1)
        {
            float a = Mathf.Clamp01((y - top) / (Screen.height * 0.12f)) * Mathf.Clamp01((Screen.height - y) / (Screen.height * 0.15f));
            if (y > top - 20 * s && y < Screen.height + 50 * s)
                MenuUI.Text(new Rect(cx - width / 2f * s, y, width * s, size * 1.6f * lines * s), text, size, new Color(c.r, c.g, c.b, a), TextAnchor.UpperCenter, bold ? FontStyle.Bold : FontStyle.Normal);
            y += gap * s;
        }
        var bone = MenuUI.Bone; var gold = MenuUI.Gold; var dim = MenuUI.Dim; var cyan = MenuUI.Cyan;
        var red = new Color(0.85f, 0.25f, 0.2f);
        Line("ASHEN BOND", 96, bone, 200, true);
        foreach (var (name, role) in MenuUI.People)
        {
            int colon = role.IndexOf(':');
            string title = colon > 0 ? role[..colon] : role;
            string desc = colon > 0 ? role[(colon + 1)..].Trim() : "";
            Line(title.ToUpperInvariant(), 24, red, 46, true);
            Line(name, 58, gold, 92, true);
            Line(desc, 24, dim, 240, false, 900f, 3);
        }
        Line("STARRING", 24, red, 46, true);
        Line("Ilyan", 44, gold, 56, true);
        Line("a boy who dropped a lantern", 24, dim, 100);
        Line("Mira", 44, cyan, 56, true);
        Line("a sister who came back for him anyway", 24, dim, 240);
        Line("THIRD-PARTY ART AND TOOLS", 24, red, 52, true);
        Line("Cainos Pixel Art Platformer   Monster Creatures Fantasy   Pixel Art Lantern Pack", 22, dim, 40);
        Line("Magic Pack 9   Impact and Dash FX   Undead Executioner   Dark Fantasy Big Boss", 22, dim, 40);
        Line("Pixelify Sans (Open Font License)   Unity 6", 22, dim, 300);
        Line("Thank you for playing.", 48, bone, 90, true);
        Line("Carry the light.", 30, cyan, 300);
        _creditsHeight = (y + _scroll - Screen.height) / s;
    }
}
