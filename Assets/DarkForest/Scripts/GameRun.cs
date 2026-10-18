using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One playthrough: the difficulty chosen at Start Game and the chances (lives) left. Losing all of Ilyan's health
/// costs a chance and sends him back to the checkpoint; with no chances left the Game Over panel appears.
/// Easy 10, Medium 5, Hard 3 chances.
/// </summary>
public static class GameRun
{
    public static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard" };
    static readonly int[] ChancesFor = { 10, 5, 3 };

    public static int Difficulty
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("ab_difficulty", 0), 0, 2);
        set => PlayerPrefs.SetInt("ab_difficulty", Mathf.Clamp(value, 0, 2));
    }

    public static int MaxChances => ChancesFor[Difficulty];
    public static int Chances { get; private set; } = -1;

    /// <summary>Start Game / Restart: full chances.</summary>
    public static void NewRun() => Chances = MaxChances;

    /// <summary>Spend a chance. Returns false when none are left (game over).</summary>
    public static bool LoseChance()
    {
        if (Chances < 0) Chances = MaxChances;      // scene started directly in the editor
        Chances = Mathf.Max(0, Chances - 1);
        return Chances > 0;
    }

    public static void EnsureStarted() { if (Chances < 0) Chances = MaxChances; }
}

/// <summary>GAME OVER: the lantern went out. Restart this area with full chances, or give up to the title screen.</summary>
public class GameOverPanel : MonoBehaviour
{
    readonly MenuUI.List _list = new();
    float _t;

    public static void Show()
    {
        if (FindFirstObjectByType<GameOverPanel>()) return;
        new GameObject("Game Over").AddComponent<GameOverPanel>();
    }

    public static bool Open => FindFirstObjectByType<GameOverPanel>() != null;

    void Start()
    {
        Time.timeScale = 0f;
        Sfx.Play("gasp");
    }

    void Update() => _t += Time.unscaledDeltaTime;

    void OnGUI()
    {
        MenuUI.ApplyFont();
        GUI.depth = -800;
        float s = MenuUI.S, a = Mathf.Clamp01(_t / 1.2f);
        MenuUI.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0f, 0.01f, 0.8f * a));
        if (_t < 0.6f) return;
        float w = 680 * s, h = 470 * s;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        MenuUI.Panel(r);
        MenuUI.Title(new Rect(r.x, r.y + 30 * s, r.width, 100 * s), "GAME OVER", 70);
        MenuUI.Text(new Rect(r.x, r.y + 140 * s, r.width, 44 * s), "<i>The lantern went out.</i>", 26, MenuUI.Bone, TextAnchor.MiddleCenter);
        MenuUI.Text(new Rect(r.x, r.y + 182 * s, r.width, 36 * s), $"Difficulty: {GameRun.DifficultyNames[GameRun.Difficulty]}   ({GameRun.MaxChances} chances)", 20, MenuUI.Dim, TextAnchor.MiddleCenter);
        int c = _list.Draw(new Rect(r.x + 130 * s, r.y + 250 * s, r.width - 260 * s, 62 * s), 80 * s, new[] { "Restart", "Give Up" }, 30);
        if (c == 0)
        {
            GameRun.NewRun();
            Time.timeScale = 1f;
            LoadingScreen.Load(SceneManager.GetActiveScene().name, "Try again", "");
            Destroy(gameObject);
        }
        else if (c == 1)
        {
            Time.timeScale = 1f;
            LoadingScreen.Load("MainMenu", "Ashen Bond", "");
            Destroy(gameObject);
        }
    }
}
