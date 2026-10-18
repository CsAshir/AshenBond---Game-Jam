using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Title screen in the gothic kit: ASHEN BOND over the red crosshair, framed buttons (Start Game, How to Play,
/// Settings, Credits, Quit) on an ink panel, the living tower + forest scene behind (MenuBackdrop).
/// Start Game plays the opening cutscene, then Area A.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public string firstScene = "Cinematic_Intro";
    [Tooltip("Optional painted art for the side panel (see Docs/Menu_Art_Prompts.md).")]
    public Texture2D panelArt;
    [Tooltip("Optional logo image; the text title is used when empty.")]
    public Texture2D logo;

    enum Page { Main, HowTo, Settings, Credits, Quit, Difficulty }
    Page _page;
    readonly MenuUI.List _list = new();
    readonly MenuUI.List _settingsList = new();
    readonly MenuUI.List _confirm = new() { selected = 1 };
    readonly MenuUI.List _difficulty = new();
    float _t;
    bool _escWas = true;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        MenuUI.ApplySettings();
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime;
        var k = Keyboard.current;
        bool esc = k != null && k.escapeKey.isPressed;
        if (esc && !_escWas && _page != Page.Main) { _page = Page.Main; MenuUI.Sound(true); }
        _escWas = esc;
    }

    void OnGUI()
    {
        MenuUI.ApplyFont();
        float s = MenuUI.S;
        float intro = Mathf.Clamp01(_t / 1.2f);
        float w = Screen.width * 0.4f;

        // ink side panel fading into the scene
        if (panelArt)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.35f * intro);
            GUI.DrawTexture(new Rect(0, 0, w, Screen.height), panelArt, ScaleMode.ScaleAndCrop);
        }
        for (int i = 0; i < 24; i++)
        {
            float k = i / 23f;
            MenuUI.Box(new Rect(w * k, 0, w / 23f + 1, Screen.height), new Color(0.02f, 0.015f, 0.04f, Mathf.Lerp(0.88f, 0f, k * k) * intro));
        }

        float cx = w * 0.5f;
        if (_page == Page.Main || _page == Page.Quit)
        {
            if (logo)
            {
                GUI.color = new Color(1f, 1f, 1f, intro);
                GUI.DrawTexture(new Rect(cx - 330 * s, 90 * s, 660 * s, 240 * s), logo, ScaleMode.ScaleToFit);
            }
            else
            {
                MenuUI.Title(new Rect(cx - 360 * s, 70 * s, 720 * s, 150 * s), "ASHEN", 124, intro);
                var bond = MenuUI.Style(70, true, TextAnchor.MiddleCenter);
                GUI.color = new Color(0.75f, 0.12f, 0.1f, intro);
                GUI.Label(new Rect(cx - 360 * s, 200 * s, 720 * s, 90 * s), "BOND", bond);
                GUI.color = Color.white;
            }
            MenuUI.Text(new Rect(cx - 300 * s, 290 * s, 600 * s, 40 * s), "<i>carry the light. she never left.</i>", 24, new Color(0.6f, 0.8f, 1f, intro), TextAnchor.MiddleCenter);
            if (_page == Page.Main)
            {
                int c = _list.Draw(new Rect(cx - 200 * s, 380 * s, 400 * s, 64 * s), 82 * s,
                                   new[] { "Start Game", "How to Play", "Settings", "Credits", "Quit" }, 32);
                if (c == 0) { _page = Page.Difficulty; _difficulty.selected = GameRun.Difficulty; }
                else if (c == 1) _page = Page.HowTo;
                else if (c == 2) { _page = Page.Settings; _settingsList.selected = 0; }
                else if (c == 3) _page = Page.Credits;
                else if (c == 4) { _page = Page.Quit; _confirm.selected = 1; }
            }
            MenuUI.Text(new Rect(cx - 300 * s, Screen.height - 56 * s, 600 * s, 40 * s), "Game Jam 2026", 18, new Color(0.55f, 0.55f, 0.68f, intro), TextAnchor.MiddleCenter);
        }

        var page = new Rect(Screen.width / 2f - 400 * s, 60 * s, 800 * s, Screen.height - 120 * s);
        switch (_page)
        {
            case Page.HowTo:
                Dim();
                MenuUI.HowToPlay(page);
                BackHint();
                break;
            case Page.Settings:
                Dim();
                if (MenuUI.Settings(new Rect(page.x, page.y, page.width, 800 * s), _settingsList)) _page = Page.Main;
                break;
            case Page.Credits:
                Dim();
                MenuUI.Credits(page);
                BackHint();
                break;
            case Page.Difficulty:
            {
                Dim();
                float w2 = 700 * s, h2 = 560 * s;
                var r = new Rect((Screen.width - w2) / 2f, (Screen.height - h2) / 2f, w2, h2);
                MenuUI.Panel(r);
                MenuUI.Title(new Rect(r.x, r.y + 26 * s, r.width, 90 * s), "DIFFICULTY", 60);
                MenuUI.Text(new Rect(r.x, r.y + 120 * s, r.width, 40 * s), "How many chances does Ilyan get?", 22, MenuUI.Dim, TextAnchor.MiddleCenter);
                int d = _difficulty.Draw(new Rect(r.x + 110 * s, r.y + 180 * s, r.width - 220 * s, 62 * s), 80 * s,
                                         new[] { "Easy   (10 chances)", "Medium   (5 chances)", "Hard   (3 chances)", "Back" }, 28);
                if (d >= 0 && d < 3)
                {
                    GameRun.Difficulty = d;
                    PlayerPrefs.Save();
                    GameRun.NewRun();
                    LoadingScreen.Load(firstScene, "Ashen Bond", "");
                }
                else if (d == 3) _page = Page.Main;
                break;
            }
            case Page.Quit:
                int q = MenuUI.Confirm("QUIT GAME?", "Leave Ashen Bond?", _confirm);
                if (q == 0) _page = Page.Main;
                if (q == 1)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
                break;
        }
        GUI.color = Color.white;
    }

    static void Dim() => MenuUI.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.01f, 0.03f, 0.7f));

    void BackHint()
    {
        float s = MenuUI.S;
        var r = new Rect(Screen.width / 2f - 110 * s, Screen.height - 60 * s, 220 * s, 46 * s);
        MenuUI.Key(new Rect(r.x, r.y + 6 * s, 70 * s, 34 * s), "Esc");
        MenuUI.Text(new Rect(r.x + 84 * s, r.y, 140 * s, r.height), "Back", 22, MenuUI.Bone);
        if (Event.current.type == EventType.MouseDown) { _page = Page.Main; MenuUI.Sound(true); Event.current.Use(); }
    }
}
