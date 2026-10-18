using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared gothic UI for every menu (title, pause, settings, how to play, credits, confirm dialogs): iron double
/// frames with diamond caps, ember-glow on the selected button, the red crosshair ornament under titles, the
/// Pixelify Sans font, hover / click sounds, keyboard + gamepad + mouse navigation.
/// Art lives in Resources/UI (pixel 9-slices), the font in Resources/Fonts.
/// </summary>
public static class MenuUI
{
    public static readonly Color Ink = new Color(0.03f, 0.02f, 0.06f, 0.9f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.5f);
    public static readonly Color Ember = new Color(1f, 0.55f, 0.2f);
    public static readonly Color Cyan = new Color(0.45f, 0.92f, 1f);
    public static readonly Color Bone = new Color(0.86f, 0.82f, 0.78f);
    public static readonly Color Dim = new Color(0.62f, 0.6f, 0.68f);

    public static float S => Screen.height / 1080f;

    // ------------------------------------------------------------------ assets
    static Font _regular, _bold;
    static Texture2D _panel, _btn, _btnOn, _divider, _ornament, _knob, _togOn, _togOff, _key;

    static Texture2D Tex(string n)
    {
        var t = Resources.Load<Texture2D>("UI/" + n);
        if (t) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
        return t;
    }

    static void Load()
    {
        if (_panel) return;
        _regular = Resources.Load<Font>("Fonts/Pixelify-Regular");
        _bold = Resources.Load<Font>("Fonts/Pixelify-Bold");
        _panel = Tex("panel"); _btn = Tex("btn"); _btnOn = Tex("btn_on"); _divider = Tex("divider"); _ornament = Tex("ornament");
        _knob = Tex("knob"); _togOn = Tex("toggle_on"); _togOff = Tex("toggle_off"); _key = Tex("key");
    }

    public static Font Font { get { Load(); return _regular; } }
    public static Font BoldFont { get { Load(); return _bold; } }

    /// <summary>Call at the top of any OnGUI: the whole game uses Pixelify Sans.</summary>
    public static void ApplyFont()
    {
        Load();
        if (_regular && GUI.skin.font != _regular) GUI.skin.font = _regular;
    }

    public static GUIStyle Style(int size, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        ApplyFont();
        var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * S), alignment = anchor, wordWrap = true, richText = true };
        if (bold && _bold) st.font = _bold;
        return st;
    }

    // ------------------------------------------------------------------ drawing primitives
    public static void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    public static void Frame(Rect r, Color c, float t)
    {
        Box(new Rect(r.x, r.y, r.width, t), c);
        Box(new Rect(r.x, r.yMax - t, r.width, t), c);
        Box(new Rect(r.x, r.y, t, r.height), c);
        Box(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    /// <summary>9-slice a pixel texture with crisp, scaled borders.</summary>
    public static void Nine(Rect r, Texture2D t, int border, float scale, Color tint)
    {
        if (!t) { Box(r, Ink); return; }
        GUI.color = tint;
        float b = Mathf.Min(border * scale, Mathf.Min(r.width, r.height) / 2f);
        float w = t.width, h = t.height, u = border / w, v = border / h;
        float[] xs = { r.x, r.x + b, r.xMax - b, r.xMax };
        float[] ys = { r.y, r.y + b, r.yMax - b, r.yMax };
        float[] us = { 0f, u, 1f - u, 1f };
        float[] vs = { 1f, 1f - v, v, 0f };   // GUI y goes down, texture v goes up
        for (int iy = 0; iy < 3; iy++)
            for (int ix = 0; ix < 3; ix++)
            {
                var dst = new Rect(xs[ix], ys[iy], xs[ix + 1] - xs[ix], ys[iy + 1] - ys[iy]);
                if (dst.width <= 0f || dst.height <= 0f) continue;
                var uv = new Rect(us[ix], vs[iy + 1], us[ix + 1] - us[ix], vs[iy] - vs[iy + 1]);
                GUI.DrawTextureWithTexCoords(dst, t, uv);
            }
        GUI.color = Color.white;
    }

    public static void Panel(Rect r, float alpha = 1f) { Load(); Nine(r, _panel, 16, 3f * S, new Color(1f, 1f, 1f, alpha)); }

    public static void Text(Rect r, string text, int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        var st = Style(size, style == FontStyle.Bold, anchor);
        GUI.color = c;
        GUI.Label(r, text, st);
        GUI.color = Color.white;
    }

    /// <summary>Big title with a dark-red drop shadow and the red crosshair ornament behind it (as in the mockups).</summary>
    public static void Title(Rect r, string text, int size, float alpha = 1f)
    {
        Load();
        if (_ornament)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.85f * alpha);
            float ow = r.width * 0.95f, oh = ow * _ornament.height / _ornament.width;
            GUI.DrawTexture(new Rect(r.center.x - ow / 2f, r.center.y - oh / 2f + 4 * S, ow, oh), _ornament);
        }
        var st = Style(size, true, TextAnchor.MiddleCenter);
        GUI.color = new Color(0.5f, 0.08f, 0.05f, 0.85f * alpha);
        GUI.Label(new Rect(r.x + 4 * S, r.y + 5 * S, r.width, r.height), text, st);
        GUI.color = new Color(Bone.r, Bone.g, Bone.b, alpha);
        GUI.Label(r, text, st);
        GUI.color = Color.white;
    }

    public static void Divider(Rect r)
    {
        Box(new Rect(r.x, r.center.y - S, r.width, 2 * S), new Color(0.38f, 0.35f, 0.42f));
        float d = 12 * S;
        GUIUtility.RotateAroundPivot(45f, r.center);
        Box(new Rect(r.center.x - d / 2f, r.center.y - d / 2f, d, d), new Color(0.6f, 0.55f, 0.62f));
        Box(new Rect(r.center.x - d / 4f, r.center.y - d / 4f, d / 2f, d / 2f), Ember);
        GUI.matrix = Matrix4x4.identity;
    }

    public static void Key(Rect r, string key)
    {
        Load();
        Nine(r, _key, 4, 2f * S, Color.white);
        Text(r, key, 18, Bone, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    // ------------------------------------------------------------------ sound
    static AudioSource _ui;
    static AudioClip _hover, _click;

    public static void Sound(bool click)
    {
        if (!_ui)
        {
            var go = new GameObject("UI Audio");
            Object.DontDestroyOnLoad(go);
            _ui = go.AddComponent<AudioSource>();
            _ui.ignoreListenerPause = true;
            _hover = Resources.Load<AudioClip>("Audio/ui_hover");
            _click = Resources.Load<AudioClip>("Audio/ui_click");
        }
        var c = click ? _click : _hover;
        if (!c) return;
        _ui.pitch = click ? 1.1f : 1.7f;
        _ui.PlayOneShot(c, (click ? 0.55f : 0.22f) * SfxVolume);
    }

    // ------------------------------------------------------------------ navigation
    static bool Edge(ref bool was, bool now) { bool p = now && !was; was = now; return p; }

    /// <summary>Framed button column. Returns the clicked index or -1. Text stays centred inside its frame.
    /// A null item is a row drawn by the caller (slider, toggle): it can be selected but has no button.</summary>
    public class List
    {
        public int selected;
        int _lastSelected = -1;
        readonly float[] _hover = new float[16];
        bool _wasUp = true, _wasDown = true, _wasOk = true;

        public int Draw(Rect first, float spacing, string[] items, int size = 32, int count = -1)
        {
            Load();
            int n = count < 0 ? items.Length : count;
            int clicked = -1;
            var k = Keyboard.current; var g = Gamepad.current;
            bool up = (k != null && (k.wKey.isPressed || k.upArrowKey.isPressed)) || (g != null && (g.dpad.up.isPressed || g.leftStick.y.ReadValue() > 0.6f));
            bool down = (k != null && (k.sKey.isPressed || k.downArrowKey.isPressed)) || (g != null && (g.dpad.down.isPressed || g.leftStick.y.ReadValue() < -0.6f));
            bool ok = (k != null && (k.enterKey.isPressed || k.spaceKey.isPressed)) || (g != null && g.buttonSouth.isPressed);
            if (Event.current.type == EventType.Repaint)
            {
                if (Edge(ref _wasUp, up)) selected = (selected + n - 1) % n;
                if (Edge(ref _wasDown, down)) selected = (selected + 1) % n;
                if (Edge(ref _wasOk, ok)) clicked = selected;
            }
            for (int i = 0; i < items.Length; i++)
            {
                var r = new Rect(first.x, first.y + i * spacing, first.width, first.height);
                if (r.Contains(Event.current.mousePosition))
                {
                    if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.Repaint) selected = i;
                    if (items[i] != null && Event.current.type == EventType.MouseDown && Event.current.button == 0) { clicked = i; Event.current.Use(); }
                }
                if (items[i] == null) continue;
                bool on = i == selected;
                if (Event.current.type == EventType.Repaint) _hover[i] = Mathf.MoveTowards(_hover[i], on ? 1f : 0f, Time.unscaledDeltaTime * 7f);
                float h = _hover[i];
                var grow = new Rect(r.x - 6 * S * h, r.y - 2 * S * h, r.width + 12 * S * h, r.height + 4 * S * h);
                Nine(grow, _btn, 8, 3f * S, new Color(1f, 1f, 1f, 1f - h));
                if (h > 0.01f)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
                    Nine(grow, _btnOn, 8, 3f * S, new Color(1f, 1f, 1f, h * pulse));
                }
                Text(r, items[i], size, Color.Lerp(Dim, Gold, h), TextAnchor.MiddleCenter, on ? FontStyle.Bold : FontStyle.Normal);
            }
            if (selected != _lastSelected) { if (_lastSelected >= 0) Sound(false); _lastSelected = selected; }
            if (clicked >= 0) Sound(true);
            return clicked;
        }
    }

    /// <summary>Slider row (0..1): label, ember track, diamond knob, value. Left/right adjusts when selected.</summary>
    public static float Slider(Rect r, string label, float value, bool selected)
    {
        Load();
        if (selected) Box(new Rect(r.x, r.y + 4 * S, r.width, r.height - 8 * S), new Color(1f, 0.55f, 0.2f, 0.08f));
        Text(new Rect(r.x + 20 * S, r.y, r.width * 0.4f, r.height), label, 26, selected ? Gold : Bone);
        var bar = new Rect(r.x + r.width * 0.45f, r.center.y - 5 * S, r.width * 0.42f, 10 * S);
        Box(bar, new Color(0f, 0f, 0f, 0.7f));
        Box(new Rect(bar.x, bar.y + 2 * S, bar.width * value, bar.height - 4 * S), selected ? Ember : new Color(0.75f, 0.45f, 0.2f));
        Frame(bar, new Color(0.4f, 0.37f, 0.44f), Mathf.Max(1f, S));
        if (_knob)
        {
            float ks = 30 * S;
            GUI.DrawTexture(new Rect(bar.x + bar.width * value - ks / 2f, bar.center.y - ks / 2f, ks, ks), _knob);
        }
        Text(new Rect(bar.xMax + 14 * S, r.y, 70 * S, r.height), Mathf.RoundToInt(value * 100).ToString(), 22, Bone);
        var e = Event.current;
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && new Rect(bar.x - 10 * S, bar.y - 14 * S, bar.width + 20 * S, bar.height + 28 * S).Contains(e.mousePosition))
        {
            value = Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width);
            e.Use();
        }
        if (selected && e.type == EventType.Repaint)
        {
            var k = Keyboard.current;
            if (k != null && (k.aKey.isPressed || k.leftArrowKey.isPressed)) value = Mathf.Clamp01(value - Time.unscaledDeltaTime * 0.6f);
            if (k != null && (k.dKey.isPressed || k.rightArrowKey.isPressed)) value = Mathf.Clamp01(value + Time.unscaledDeltaTime * 0.6f);
        }
        return value;
    }

    // ------------------------------------------------------------------ settings (saved)
    public static float Master { get => PlayerPrefs.GetFloat("ab_master", 0.8f); set { PlayerPrefs.SetFloat("ab_master", value); AudioListener.volume = value; } }
    public static float MusicVolume { get => PlayerPrefs.GetFloat("ab_music", 0.7f); set => PlayerPrefs.SetFloat("ab_music", value); }
    public static float SfxVolume { get => PlayerPrefs.GetFloat("ab_sfx", 0.85f); set => PlayerPrefs.SetFloat("ab_sfx", value); }
    public static float VoiceVolume { get => PlayerPrefs.GetFloat("ab_voice", 1f); set => PlayerPrefs.SetFloat("ab_voice", value); }
    public static float ShakeAmount { get => PlayerPrefs.GetFloat("ab_shake", 1f); set { PlayerPrefs.SetFloat("ab_shake", value); CameraFollow2D.ShakeScale = value; } }

    public static void ApplySettings()
    {
        AudioListener.volume = Master;
        CameraFollow2D.ShakeScale = ShakeAmount;
    }

    static readonly Vector2Int[] Resolutions = { new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160) };
    static int _resIndex = -1;
    static bool _fullscreenPending;

    /// <summary>Settings page (title + framed panel). Returns true when Back is chosen.</summary>
    public static bool Settings(Rect area, List list)
    {
        Load();
        if (_resIndex < 0)
        {
            _fullscreenPending = Screen.fullScreen;
            _resIndex = 2;
            for (int i = 0; i < Resolutions.Length; i++) if (Resolutions[i].x == Screen.width) _resIndex = i;
        }
        Title(new Rect(area.x, area.y, area.width, 110 * S), "SETTINGS", 76);
        var panel = new Rect(area.x, area.y + 130 * S, area.width, 552 * S);
        Panel(panel);
        float x = panel.x + 34 * S, w = panel.width - 68 * S, y = panel.y + 30 * S, row = 56 * S;
        // rows: 0-4 sliders, 5 fullscreen, 6 resolution, 7 Apply, 8 Back
        Master = Slider(new Rect(x, y, w, row), "Master Volume", Master, list.selected == 0);
        MusicVolume = Slider(new Rect(x, y + row, w, row), "Music Volume", MusicVolume, list.selected == 1);
        SfxVolume = Slider(new Rect(x, y + row * 2, w, row), "SFX Volume", SfxVolume, list.selected == 2);
        VoiceVolume = Slider(new Rect(x, y + row * 3, w, row), "Voice Volume", VoiceVolume, list.selected == 3);
        ShakeAmount = Slider(new Rect(x, y + row * 4, w, row), "Screen Shake", ShakeAmount, list.selected == 4);
        Divider(new Rect(x, y + row * 5 + 2 * S, w, 10 * S));
        float y2 = y + row * 5 + 14 * S;
        var tr = new Rect(x, y2, w, row);
        if (list.selected == 5) Box(new Rect(tr.x, tr.y + 4 * S, tr.width, tr.height - 8 * S), new Color(1f, 0.55f, 0.2f, 0.08f));
        Text(new Rect(tr.x + 20 * S, tr.y, w * 0.4f, row), "Fullscreen", 26, list.selected == 5 ? Gold : Bone);
        var pill = new Rect(tr.x + w * 0.45f, tr.center.y - 16 * S, 72 * S, 32 * S);
        if (_togOn) GUI.DrawTexture(pill, _fullscreenPending ? _togOn : _togOff);
        Text(new Rect(pill.xMax + 12 * S, tr.y, 80 * S, row), _fullscreenPending ? "ON" : "OFF", 22, Bone);
        var rr = new Rect(x, y2 + row, w, row);
        if (list.selected == 6) Box(new Rect(rr.x, rr.y + 4 * S, rr.width, rr.height - 8 * S), new Color(1f, 0.55f, 0.2f, 0.08f));
        Text(new Rect(rr.x + 20 * S, rr.y, w * 0.4f, row), "Resolution", 26, list.selected == 6 ? Gold : Bone);
        var box = new Rect(rr.x + w * 0.45f, rr.center.y - 20 * S, w * 0.5f, 40 * S);
        Nine(box, _btn, 8, 3f * S, Color.white);
        var res = Resolutions[_resIndex];
        Text(box, $"<   {res.x} x {res.y}   >", 22, Bone, TextAnchor.MiddleCenter);
        var e = Event.current;
        if (e.type == EventType.MouseDown && pill.Contains(e.mousePosition)) { _fullscreenPending = !_fullscreenPending; Sound(true); e.Use(); }
        if (e.type == EventType.MouseDown && box.Contains(e.mousePosition))
        {
            _resIndex = (_resIndex + (e.mousePosition.x < box.center.x ? Resolutions.Length - 1 : 1)) % Resolutions.Length;
            Sound(true);
            e.Use();
        }
        var k = Keyboard.current;
        if (e.type == EventType.Repaint && k != null && list.selected == 6)
        {
            if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) _resIndex = (_resIndex + Resolutions.Length - 1) % Resolutions.Length;
            if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) _resIndex = (_resIndex + 1) % Resolutions.Length;
        }
        Divider(new Rect(x, y2 + row * 2 + 6 * S, w, 10 * S));
        // Selection list over all rows (sliders / toggle / resolution rows are caller-drawn = null), then Apply / Back.
        float rowsTop = y;
        float[] rowY = { y, y + row, y + row * 2, y + row * 3, y + row * 4, y2, y2 + row };
        int c = -1;
        for (int i = 0; i < rowY.Length; i++)
        {
            var rr2 = new Rect(x, rowY[i], w, row);
            if (rr2.Contains(e.mousePosition) && (e.type == EventType.MouseMove || e.type == EventType.Repaint)) list.selected = i;
        }
        float by = y2 + row * 2 + 28 * S, bw = (w - 30 * S) / 2f;
        var apply = new Rect(x, by, bw, 54 * S);
        var back = new Rect(x + bw + 30 * S, by, bw, 54 * S);
        if (apply.Contains(e.mousePosition) && (e.type == EventType.MouseMove || e.type == EventType.Repaint)) list.selected = 7;
        if (back.Contains(e.mousePosition) && (e.type == EventType.MouseMove || e.type == EventType.Repaint)) list.selected = 8;
        Nine(apply, list.selected == 7 ? _btnOn : _btn, 8, 3f * S, Color.white);
        Text(apply, "Apply", 28, list.selected == 7 ? Gold : Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
        Nine(back, list.selected == 8 ? _btnOn : _btn, 8, 3f * S, Color.white);
        Text(back, "Back", 28, list.selected == 8 ? Gold : Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (apply.Contains(e.mousePosition)) { c = 7; e.Use(); }
            else if (back.Contains(e.mousePosition)) { c = 8; e.Use(); }
        }
        // keyboard: up/down moves through the 9 rows, Enter activates
        if (e.type == EventType.Repaint && k != null)
        {
            int prev = list.selected;
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) list.selected = (list.selected + 8) % 9;
            if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) list.selected = (list.selected + 1) % 9;
            if (list.selected >= 7 && (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame)) list.selected = 8;
            if (list.selected >= 7 && (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)) list.selected = 7;
            if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) c = list.selected;
            if (prev != list.selected) Sound(false);
        }
        _ = rowsTop;
        if (c == 5) { _fullscreenPending = !_fullscreenPending; Sound(true); }
        if (c == 6) { _resIndex = (_resIndex + 1) % Resolutions.Length; Sound(true); }
        if (c == 7)
        {
            Sound(true);
            var rz = Resolutions[_resIndex];
            Screen.SetResolution(rz.x, rz.y, _fullscreenPending ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.Save();
        }
        if (c == 8) { Sound(true); _resIndex = -1; PlayerPrefs.Save(); return true; }
        return false;
    }

    // ------------------------------------------------------------------ content pages

    public static void HowToPlay(Rect area)
    {
        Title(new Rect(area.x, area.y, area.width, 110 * S), "HOW TO PLAY", 68);
        var panel = new Rect(area.x, area.y + 130 * S, area.width, 640 * S);
        Panel(panel);
        float x = panel.x + 40 * S, y = panel.y + 32 * S, w = panel.width - 80 * S;
        (string key, string what)[] rows =
        {
            ("A / D", "Walk   (hold Shift to run)"),
            ("Space", "Jump. Press again in the air to double jump"),
            ("K", "Dash. Nothing can hurt you while you dash"),
            ("J", "Lantern fire. Hold to charge a flare"),
            ("P", "Mira's shadow strike (refills in 7 seconds)"),
            ("E", "Mira heals one heart (costs 2 embers)"),
            ("Q", "Hold to rewind time, up to 3 seconds"),
            ("Esc", "Pause"),
        };
        foreach (var (key, what) in rows)
        {
            Key(new Rect(x, y + 5 * S, 110 * S, 40 * S), key);
            Text(new Rect(x + 132 * S, y, w - 132 * S, 50 * S), what, 24, Bone);
            y += 54 * S;
        }
        Divider(new Rect(x, y + 6 * S, w, 10 * S));
        Text(new Rect(x, y + 22 * S, w, 130 * S),
             "<b>Fire, then shadow.</b> Lantern fire makes an enemy glow cyan for a moment. Strike it with Mira while it glows for triple damage. Souls of fallen enemies fly into your lantern and feed Mira's strike.",
             22, new Color(0.75f, 0.9f, 1f));
    }

    public static readonly (string name, string role)[] People =
    {
        ("Muhammad Ashir", "Game Design & Development: Gameplay programming, game and level design, combat systems and mechanics, lighting, visual effects and overall art direction."),
        ("Sarah Khan", "Asset Research & Level Design: Sourcing and curating visual assets, sound effects, enemies, props, and environments, with a focus on map creation and level design."),
        ("Javeria Ismail", "AI-Generated Assets & Cinematic Production: Creating AI-generated artwork, cutscenes, cinematic shots, and production-ready visual content for the game."),
    };

    public const string ThirdParty = "Third-party art: Cainos Pixel Art Platformer, Monster Creatures Fantasy, Pixel Art Lantern Pack, Magic Pack 9, Impact and Dash FX, Undead Executioner, Dark Fantasy Big Boss. Font: Pixelify Sans (OFL). Built with Unity 6.";

    public static void Credits(Rect area)
    {
        Title(new Rect(area.x, area.y, area.width, 110 * S), "CREDITS", 76);
        var panel = new Rect(area.x, area.y + 130 * S, area.width, 600 * S);
        Panel(panel);
        float x = panel.x + 40 * S, y = panel.y + 34 * S, w = panel.width - 80 * S;
        foreach (var (name, role) in People)
        {
            Text(new Rect(x, y, w, 40 * S), name, 32, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text(new Rect(x, y + 40 * S, w, 80 * S), role, 20, Bone, TextAnchor.UpperLeft);
            y += 132 * S;
        }
        Divider(new Rect(x, y, w, 10 * S));
        Text(new Rect(x, y + 16 * S, w, 120 * S), ThirdParty, 17, Dim);
    }

    /// <summary>Framed yes/no dialog. Returns 1 = yes, 0 = no, -1 = still open.</summary>
    public static int Confirm(string title, string question, List list)
    {
        float w = 640 * S, h = 400 * S;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
        Panel(r);
        Title(new Rect(r.x, r.y + 26 * S, r.width, 90 * S), title, 54);
        Text(new Rect(r.x, r.y + 130 * S, r.width, 50 * S), question, 26, Bone, TextAnchor.MiddleCenter);
        int c = list.Draw(new Rect(r.x + 110 * S, r.y + 200 * S, r.width - 220 * S, 58 * S), 74 * S, new[] { "Yes", "No" }, 28);
        if (c == 0) return 1;
        if (c == 1) return 0;
        return -1;
    }
}
