using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Esc (gamepad Start) pauses: PAUSED over the red crosshair, framed buttons Resume / How to Play / Settings /
/// Main Menu / Quit. Main Menu and Quit ask first ("Return to Main Menu?" / "Leave Ashen Bond?").
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool Paused { get; private set; }
    public string mainMenuScene = "MainMenu";

    enum Page { Main, HowTo, Settings, ConfirmMenu, ConfirmQuit }
    Page _page;
    readonly MenuUI.List _list = new();
    readonly MenuUI.List _settingsList = new();
    readonly MenuUI.List _confirm = new() { selected = 1 };
    float _fade;
    bool _escWas = true;

    void Awake() { Paused = false; MenuUI.ApplySettings(); }

    void OnDestroy() { if (Paused) { Time.timeScale = 1f; AudioListener.pause = false; } Paused = false; }

    void Update()
    {
        var k = Keyboard.current; var g = Gamepad.current;
        bool held = (k != null && k.escapeKey.isPressed) || (g != null && g.startButton.isPressed);
        bool esc = held && !_escWas;                           // own edge detection
        _escWas = held;
        if (esc && !DialogueDirector.Active && !TimeRewind.Rewinding && !GameOverPanel.Open)
        {
            if (Paused && _page != Page.Main) { _page = Page.Main; MenuUI.Sound(true); }
            else Set(!Paused);
        }
        _fade = Mathf.MoveTowards(_fade, Paused ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (Paused) Time.timeScale = 0f;
    }

    void Set(bool on)
    {
        Paused = on;
        _page = Page.Main;
        _list.selected = 0;
        Time.timeScale = on ? 0f : 1f;
        AudioListener.pause = on;
        MenuUI.Sound(true);
    }

    void OnGUI()
    {
        if (_fade <= 0f) return;
        MenuUI.ApplyFont();
        GUI.depth = -500;
        float s = MenuUI.S;
        MenuUI.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.01f, 0.03f, 0.7f * _fade));
        if (!Paused) return;
        float cx = Screen.width / 2f;
        var page = new Rect(cx - 400 * s, 60 * s, 800 * s, Screen.height - 120 * s);
        switch (_page)
        {
            case Page.Main:
            case Page.ConfirmMenu:
            case Page.ConfirmQuit:
                MenuUI.Title(new Rect(cx - 380 * s, 120 * s, 760 * s, 160 * s), "PAUSED", 120, _fade);
                if (_page == Page.Main)
                {
                    int c = _list.Draw(new Rect(cx - 210 * s, 340 * s, 420 * s, 64 * s), 82 * s,
                                       new[] { "Resume", "How to Play", "Settings", "Main Menu", "Quit" }, 32);
                    if (c == 0) Set(false);
                    else if (c == 1) _page = Page.HowTo;
                    else if (c == 2) { _page = Page.Settings; _settingsList.selected = 0; }
                    else if (c == 3) { _page = Page.ConfirmMenu; _confirm.selected = 1; }
                    else if (c == 4) { _page = Page.ConfirmQuit; _confirm.selected = 1; }
                }
                else
                {
                    bool toMenu = _page == Page.ConfirmMenu;
                    int q = MenuUI.Confirm(toMenu ? "MAIN MENU?" : "QUIT GAME?", toMenu ? "Return to Main Menu?" : "Leave Ashen Bond?", _confirm);
                    if (q == 0) _page = Page.Main;
                    if (q == 1)
                    {
                        if (toMenu) { Set(false); LoadingScreen.Load(mainMenuScene, "Ashen Bond", ""); }
                        else
                        {
#if UNITY_EDITOR
                            UnityEditor.EditorApplication.isPlaying = false;
#else
                            Application.Quit();
#endif
                        }
                    }
                }
                break;
            case Page.HowTo:
                MenuUI.HowToPlay(page);
                break;
            case Page.Settings:
                if (MenuUI.Settings(new Rect(page.x, page.y, page.width, 800 * s), _settingsList)) _page = Page.Main;
                break;
        }
        GUI.color = Color.white;
    }
}
