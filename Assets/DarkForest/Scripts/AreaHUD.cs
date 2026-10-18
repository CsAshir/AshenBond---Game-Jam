using UnityEngine;

/// <summary>
/// Jam HUD on a dark panel (top left) so it never melts into the backgrounds:
///   Ilyan's live portrait, LIFE (lanterns on an animated bar with a lag trail), EMBERS, TIME (3 rewind
///   hourglasses, the active one drains while Q is held), DASH, and Mira's portrait that fills from the bottom
///   over 7 s - the shadow strike (P) only works when she is full and glowing.
/// Plus heal hints, the control line and the area title card.
/// </summary>
public class AreaHUD : MonoBehaviour
{
    public string areaTitle = "Area A";
    public string areaSubtitle = "The Burning Village";
    public float titleTime = 3.5f;
    public Texture2D heartIcon;     // lantern
    public Texture2D sisterIcon;
    public PlayerHealth health;
    public ShadowSummon shadow;
    [Tooltip("Optional painted art for the rewind watch (see Docs/Menu_Art_Prompts.md). A pixel dial is drawn when empty.")]
    public Texture2D rewindDialArt;

    PlayerController2D _pc;
    SpriteRenderer _portrait;
    TimeRewind _rewind;
    float _life = 1f, _lifeLag = 1f, _time = 1f, _mira;
    int _lastHearts = -1;
    float _hurtFlash, _readyFlash;
    bool _wasReady;

    static readonly Color Panel = new Color(0.04f, 0.03f, 0.08f, 0.78f);
    static readonly Color Gold = new Color(1f, 0.78f, 0.42f, 1f);
    static readonly Color Ember = new Color(1f, 0.55f, 0.2f, 1f);
    static readonly Color Cyan = new Color(0.45f, 0.92f, 1f, 1f);

    Texture2D _dial;

    /// <summary>Pixel pocket-watch face: gold rim, 12 ticks, dark glass.</summary>
    static Texture2D MakeDial()
    {
        const int N = 48;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var c0 = new Color(0, 0, 0, 0);
        var rim = new Color(1f, 0.78f, 0.42f, 1f);
        var rimDark = new Color(0.55f, 0.36f, 0.18f, 1f);
        var glass = new Color(0.06f, 0.05f, 0.12f, 0.92f);
        float c = (N - 1) / 2f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                var col = d < 19.5f ? glass : d < 21.5f ? rim : d < 23.5f ? rimDark : c0;
                float a = Mathf.Atan2(y - c, x - c) * Mathf.Rad2Deg;
                float m = Mathf.Repeat(a + 3f, 30f);
                if (d > 15.5f && d < 19.5f && m < 6f) col = rim;               // hour ticks
                t.SetPixel(x, y, col);
            }
        for (int y = N - 4; y < N; y++) for (int x = (int)c - 2; x <= (int)c + 2; x++) t.SetPixel(x, y, rim);   // the crown
        t.Apply();
        return t;
    }

    void Start()
    {
        _dial = MakeDial();
        if (health) { _pc = health.GetComponent<PlayerController2D>(); _portrait = health.GetComponent<SpriteRenderer>(); }
        _rewind = FindFirstObjectByType<TimeRewind>();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (health)
        {
            if (_lastHearts >= 0 && health.Hearts < _lastHearts) _hurtFlash = 1f;
            _lastHearts = health.Hearts;
            float target = health.Hearts / (float)health.maxHearts;
            _life = Mathf.MoveTowards(_life, target, dt * (_life > target ? 0.9f : 1.6f));     // drains / refills visibly
            _lifeLag = _lifeLag > _life ? Mathf.MoveTowards(_lifeLag, _life, dt * 0.35f) : _life;   // white trail drains after a hit
        }
        if (_rewind) _time = Mathf.MoveTowards(_time, (_rewind.Charges + (TimeRewind.Rewinding ? _rewind.CurrentCharge01 : 0f)) / _rewind.maxCharges, dt * 3f);
        if (shadow)
        {
            _mira = Mathf.MoveTowards(_mira, shadow.AttackCharge01, dt * 2f);
            if (shadow.AttackReady && !_wasReady) _readyFlash = 1f;
            _wasReady = shadow.AttackReady;
        }
        _hurtFlash = Mathf.Max(0f, _hurtFlash - dt * 2.5f);
        _readyFlash = Mathf.Max(0f, _readyFlash - dt * 1.5f);
    }

    static readonly (string key, string what)[] Controls =
    {
        ("A D", "move"), ("Shift", "run"), ("Space", "jump x2"), ("K", "dash"), ("J", "fire"),
        ("P", "Mira"), ("E", "heal"), ("Q", "rewind"), ("Esc", "pause"),
    };

    /// <summary>Bottom-left control strip: key caps with a word each, on a soft ink band; fades while moving.</summary>
    void DrawControls(float s)
    {
        float x = 24 * s, y = Screen.height - 54 * s, h = 34 * s;
        float a = _pc && Mathf.Abs(_pc.Velocity.x) > 0.5f ? 0.55f : 0.9f;
        float total = 0f;
        var lab = MenuUI.Style(17);
        foreach (var (key, what) in Controls)
            total += Mathf.Max(34 * s, lab.CalcSize(new GUIContent(key)).x + 18 * s) + lab.CalcSize(new GUIContent(what)).x + 26 * s;
        MenuUI.Box(new Rect(x - 12 * s, y - 8 * s, total + 16 * s, h + 16 * s), new Color(0.02f, 0.015f, 0.04f, 0.55f * a));
        foreach (var (key, what) in Controls)
        {
            float kw = Mathf.Max(34 * s, lab.CalcSize(new GUIContent(key)).x + 18 * s);
            GUI.color = new Color(1f, 1f, 1f, a);
            MenuUI.Key(new Rect(x, y, kw, h), key);
            float ww = lab.CalcSize(new GUIContent(what)).x;
            MenuUI.Text(new Rect(x + kw + 6 * s, y, ww + 6 * s, h), what, 17, new Color(0.8f, 0.82f, 0.92f, a));
            x += kw + ww + 26 * s;
        }
        GUI.color = Color.white;
    }

    static void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
    }

    static void Frame(Rect r, Color c, float t)
    {
        Box(new Rect(r.x, r.y, r.width, t), c);
        Box(new Rect(r.x, r.yMax - t, r.width, t), c);
        Box(new Rect(r.x, r.y, t, r.height), c);
        Box(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    static void DrawSprite(Rect r, Sprite s, Color c, bool flip = false)
    {
        if (!s) return;
        var tr = s.textureRect;
        var t = s.texture;
        var uv = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);
        if (flip) { uv.x += uv.width; uv.width = -uv.width; }
        // keep aspect
        float a = tr.width / tr.height;
        var fit = r.width / r.height > a ? new Rect(r.center.x - r.height * a / 2f, r.y, r.height * a, r.height) : new Rect(r.x, r.center.y - r.width / a / 2f, r.width, r.width / a);
        GUI.color = c;
        GUI.DrawTextureWithTexCoords(fit, t, uv);
    }

    /// <summary>Animated bar: dark trough, lag trail, fill, a glint sweeping across, segment ticks.</summary>
    static void Bar(Rect r, float fill, float lag, Color col, int segments, float s, float glintPhase)
    {
        Box(r, new Color(0f, 0f, 0f, 0.6f));
        var inner = new Rect(r.x + 2 * s, r.y + 2 * s, r.width - 4 * s, r.height - 4 * s);
        Box(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(lag), inner.height), new Color(1f, 0.95f, 0.9f, 0.85f));
        Box(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(fill), inner.height), col);
        Box(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(fill), inner.height * 0.35f), new Color(1f, 1f, 1f, 0.22f));   // top sheen
        float g = Mathf.Repeat(Time.unscaledTime * 0.45f + glintPhase, 1.6f) - 0.3f;
        if (g > 0f && g < Mathf.Clamp01(fill))
            Box(new Rect(inner.x + inner.width * g, inner.y, 6 * s, inner.height), new Color(1f, 1f, 1f, 0.45f));
        for (int i = 1; i < segments; i++)
            Box(new Rect(inner.x + inner.width * i / segments - s, inner.y, 2 * s, inner.height), new Color(0f, 0f, 0f, 0.7f));
        Frame(r, new Color(Gold.r, Gold.g, Gold.b, 0.55f), Mathf.Max(1f, s));
    }

    float _titleT;

    void OnGUI()
    {
        MenuUI.ApplyFont();
        if (DialogueDirector.Active || PauseMenu.Paused) return;   // cinematic: bars and text only
        float s = Screen.height / 1080f;
        var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * s), fontStyle = FontStyle.Bold };
        var big = new GUIStyle(label) { fontSize = Mathf.RoundToInt(30 * s) };
        float x0 = 22 * s, y0 = 18 * s;

        // ---- panel
        var panel = new Rect(x0, y0, 660 * s, 196 * s);
        MenuUI.Panel(new Rect(panel.x - 8 * s, panel.y - 8 * s, panel.width + 16 * s, panel.height + 16 * s), 0.95f);   // same iron frame as the menus

        // ---- Ilyan portrait (live: his current animation frame)
        var port = new Rect(x0 + 12 * s, y0 + 12 * s, 112 * s, 112 * s);
        Box(port, new Color(0.12f, 0.08f, 0.16f, 0.95f));
        Box(port, new Color(1f, 0.2f, 0.15f, 0.5f * _hurtFlash));
        if (_portrait)
        {
            GUI.BeginGroup(port);                                    // clip: the avatar stays inside its frame
            DrawSprite(new Rect(-24 * s, 2 * s, port.width + 48 * s, port.height + 24 * s), _portrait.sprite, Color.white, _portrait.flipX);
            GUI.EndGroup();
        }
        Frame(port, Gold, Mathf.Max(1f, 2 * s));
        GUI.color = Gold;
        GUI.Label(new Rect(port.x, port.yMax + 4 * s, 140 * s, 26 * s), "ILYAN", label);

        float bx = port.xMax + 18 * s, bw = 280 * s;
        // ---- LIFE: lanterns + bar
        GUI.color = new Color(1f, 0.85f, 0.7f, 0.9f);
        GUI.Label(new Rect(bx, y0 + 8 * s, 120 * s, 24 * s), "LIFE", label);
        // health value counts down with the bar
        GUI.color = _life < 0.4f ? new Color(1f, 0.45f, 0.35f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f)) : new Color(1f, 0.9f, 0.8f, 0.95f);
        GUI.Label(new Rect(bx + 62 * s, y0 + 8 * s, 120 * s, 24 * s), Mathf.CeilToInt(_life * 100f).ToString(), label);
        // chances left (lanterns)
        if (heartIcon)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bx + bw - 92 * s, y0 + 4 * s, 28 * s, 28 * s), heartIcon, ScaleMode.ScaleToFit);
        }
        GameRun.EnsureStarted();
        GUI.color = GameRun.Chances <= 1 ? new Color(1f, 0.45f, 0.35f) : Gold;
        GUI.Label(new Rect(bx + bw - 60 * s, y0 + 8 * s, 70 * s, 24 * s), $"x{GameRun.Chances}", label);
        Bar(new Rect(bx, y0 + 38 * s, bw, 20 * s), _life, _lifeLag, Color.Lerp(new Color(0.9f, 0.15f, 0.12f), new Color(1f, 0.55f, 0.22f), _life), 1, s, 0f);

        // ---- TIME: a pocket watch. Gems = charges left; the hand runs backwards while Q is held.
        if (_rewind)
        {
            var dial = new Rect(bx + bw + 12 * s, y0 + 30 * s, 92 * s, 92 * s);
            bool rew = TimeRewind.Rewinding;
            if (rew) MenuUI.Box(new Rect(dial.x - 6 * s, dial.y - 6 * s, dial.width + 12 * s, dial.height + 12 * s), new Color(1f, 0.75f, 0.35f, 0.15f + 0.1f * Mathf.Sin(Time.unscaledTime * 20f)));
            GUI.color = Color.white;
            GUI.DrawTexture(dial, rewindDialArt ? rewindDialArt : _dial, ScaleMode.ScaleToFit);
            var c = dial.center;
            float frac = _rewind.CurrentCharge01;
            var m = GUI.matrix;
            // remaining time of the active charge as a gold wedge of ticks
            for (int i = 0; i < 12; i++)
            {
                if (i / 12f >= frac) break;
                GUIUtility.RotateAroundPivot(i * 30f, c);
                MenuUI.Box(new Rect(c.x - 2 * s, c.y - 30 * s, 4 * s, 9 * s), new Color(1f, 0.85f, 0.5f, rew ? 1f : 0.8f));
                GUI.matrix = m;
            }
            float hand = rew ? -Time.unscaledTime * 900f : Time.unscaledTime * 30f;   // spins backwards while rewinding
            GUIUtility.RotateAroundPivot(hand, c);
            MenuUI.Box(new Rect(c.x - 2 * s, c.y - 26 * s, 4 * s, 26 * s), new Color(1f, 0.95f, 0.85f));
            GUI.matrix = m;
            MenuUI.Box(new Rect(c.x - 4 * s, c.y - 4 * s, 8 * s, 8 * s), Gold);
            for (int i = 0; i < _rewind.maxCharges; i++)
            {
                bool full = i < _rewind.Charges || (rew && i == _rewind.Charges);
                var gem = new Rect(dial.x + 10 * s + i * 24 * s, dial.yMax + 4 * s, 16 * s, 16 * s);
                MenuUI.Box(gem, new Color(0f, 0f, 0f, 0.6f));
                MenuUI.Box(new Rect(gem.x + 3 * s, gem.y + 3 * s, 10 * s, 10 * s), full ? new Color(1f, 0.8f, 0.4f) : new Color(0.25f, 0.22f, 0.3f));
            }
            GUI.color = new Color(1f, 0.9f, 0.65f, 0.9f);
            GUI.Label(new Rect(bx, y0 + 70 * s, 200 * s, 24 * s), "TIME  hold Q", label);
        }

        // ---- DASH + EMBERS
        float dash = _pc ? _pc.DashReady01 : 1f;
        GUI.color = new Color(0.85f, 0.75f, 1f, 0.9f);
        GUI.Label(new Rect(bx, y0 + 112 * s, 120 * s, 24 * s), "DASH  K", label);
        Bar(new Rect(bx + 90 * s, y0 + 116 * s, 90 * s, 12 * s), dash, dash, new Color(0.7f, 0.45f, 1f), 1, s, 0.9f);
        GUI.color = Ember;
        GUI.Label(new Rect(bx + 196 * s, y0 + 106 * s, 140 * s, 34 * s), $"✦ {EmberPickup.Collected}", big);

        // ---- MIRA: fills from the bottom, glows when the strike is ready
        var mira = new Rect(panel.xMax - 108 * s, y0 + 12 * s, 96 * s, 120 * s);
        Box(mira, new Color(0.06f, 0.05f, 0.12f, 0.95f));
        float h = mira.height * _mira;
        bool ready = shadow == null || shadow.AttackReady;
        Box(new Rect(mira.x, mira.yMax - h, mira.width, h), ready ? new Color(0.35f, 0.75f, 1f, 0.55f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f)) : new Color(0.4f, 0.3f, 0.75f, 0.55f));
        if (!ready) Box(new Rect(mira.x, mira.yMax - h - 2 * s, mira.width, 3 * s), new Color(0.8f, 0.9f, 1f, 0.8f));   // rising edge
        if (sisterIcon)
        {
            GUI.color = ready ? Color.white : new Color(0.55f, 0.55f, 0.7f, 0.75f);
            GUI.DrawTexture(new Rect(mira.x + 6 * s, mira.y + 6 * s, mira.width - 12 * s, mira.height - 12 * s), sisterIcon, ScaleMode.ScaleToFit);
        }
        if (_readyFlash > 0f) Frame(new Rect(mira.x - 6 * s * _readyFlash, mira.y - 6 * s * _readyFlash, mira.width + 12 * s * _readyFlash, mira.height + 12 * s * _readyFlash), new Color(Cyan.r, Cyan.g, Cyan.b, _readyFlash), 3 * s);
        Frame(mira, ready ? Cyan : new Color(0.5f, 0.45f, 0.8f, 0.8f), Mathf.Max(1f, 2 * s));
        GUI.color = ready ? Cyan : new Color(0.7f, 0.7f, 0.9f, 0.8f);
        var mid = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(mira.x - 10 * s, mira.yMax + 2 * s, mira.width + 20 * s, 26 * s), ready ? "MIRA  [P]" : $"MIRA  {Mathf.CeilToInt((1f - _mira) * (shadow ? shadow.attackRecharge : 7f))}s", mid);

        // ---- hints, controls, title card
        if (Time.time < ShadowSummon.HealHintUntil && !string.IsNullOrEmpty(ShadowSummon.HealHint))
        {
            var hint = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(26 * s) };
            GUI.color = new Color(0.6f, 0.9f, 1f, 0.9f);
            GUI.Label(new Rect(0, Screen.height * 0.72f, Screen.width, 40 * s), ShadowSummon.HealHint, hint);
        }
        DrawControls(s);

        if (Event.current.type == EventType.Repaint) _titleT += Time.unscaledDeltaTime;   // title card counts only while playing
        float t = _titleT;
        if (t < titleTime)
        {
            float a = Mathf.Clamp01(Mathf.Min(t / 0.6f, (titleTime - t) / 0.8f));
            var title = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(64 * s) };
            var sub = new GUIStyle(title) { fontSize = Mathf.RoundToInt(30 * s), fontStyle = FontStyle.Normal };
            GUI.color = new Color(1f, 0.72f, 0.4f, a);
            GUI.Label(new Rect(0, Screen.height * 0.28f, Screen.width, 90 * s), areaTitle, title);
            GUI.color = new Color(0.6f, 0.75f, 1f, a);
            GUI.Label(new Rect(0, Screen.height * 0.28f + 85 * s, Screen.width, 50 * s), areaSubtitle, sub);
        }
        GUI.color = Color.white;
    }
}
