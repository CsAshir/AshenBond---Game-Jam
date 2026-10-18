using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loading screen between scenes: ink-black card, area title, a pixel lantern flame that grows with the load,
/// a progress bar and a line of story. Survives the scene switch, then fades out over the new area.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    static readonly string[] Lines =
    {
        "Mira always said the lantern was heavier than it looked.",
        "The bell stopped at one minute to midnight.",
        "Fire exposes them. Her shadow finishes them.",
        "Hold Q and the world remembers the last few seconds.",
        "A shadow that never hurts you is not a monster.",
        "Ember beasts flee when they cannot reach you.",
        "Carry the light to the clock tower, Ilyan.",
    };

    string _scene, _title, _sub, _line;
    float _progress, _shown, _fade = 0f;
    bool _fadingOut;

    public static void Load(string scene, string title = "", string subtitle = "")
    {
        var go = new GameObject("Loading Screen");
        DontDestroyOnLoad(go);
        var ls = go.AddComponent<LoadingScreen>();
        ls._scene = scene;
        ls._title = title;
        ls._sub = subtitle;
        ls._line = Lines[Random.Range(0, Lines.Length)];
    }

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        while (_fade < 1f) { _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime * 3f); yield return null; }
        var op = SceneManager.LoadSceneAsync(_scene);
        op.allowSceneActivation = false;
        float t = 0f;
        while (op.progress < 0.9f || t < 1.6f)       // a short minimum so the card can be read
        {
            t += Time.unscaledDeltaTime;
            _progress = Mathf.Min(op.progress / 0.9f, t / 1.6f);
            yield return null;
        }
        _progress = 1f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        _fadingOut = true;
        while (_fade > 0f) { _fade = Mathf.MoveTowards(_fade, 0f, Time.unscaledDeltaTime * 2f); yield return null; }
        Destroy(gameObject);
    }

    void Update() => _shown = Mathf.MoveTowards(_shown, _progress, Time.unscaledDeltaTime * 1.5f);

    void OnGUI()
    {
        MenuUI.ApplyFont();
        GUI.depth = -1000;
        float s = MenuUI.S;
        MenuUI.Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.015f, 0.04f, _fade));
        if (_fadingOut || _fade < 0.99f) return;

        // pixel lantern flame (grows with the load)
        float cx = Screen.width / 2f, cy = Screen.height * 0.42f, px = 8 * s;
        float grow = 0.5f + 0.5f * _shown;
        for (int i = 0; i < 9; i++)
        {
            float w = (5 - Mathf.Abs(i - 3)) * grow * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f + i));
            if (w <= 0f) continue;
            var col = Color.Lerp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.35f, 0.1f), i / 8f);
            MenuUI.Box(new Rect(cx - w * px, cy - i * px * grow * 1.4f, w * 2 * px, px * 1.4f * grow), col);
        }
        MenuUI.Frame(new Rect(cx - 7 * px, cy + 2 * px, 14 * px, 9 * px), MenuUI.Gold, 3 * s);   // lantern cage
        MenuUI.Box(new Rect(cx - 7 * px, cy + 2 * px, 14 * px, 9 * px), new Color(1f, 0.6f, 0.2f, 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f)));

        MenuUI.Text(new Rect(0, Screen.height * 0.56f, Screen.width, 80 * s), _title, 60, MenuUI.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        MenuUI.Text(new Rect(0, Screen.height * 0.56f + 70 * s, Screen.width, 50 * s), _sub, 30, new Color(0.6f, 0.75f, 1f), TextAnchor.MiddleCenter);
        var bar = new Rect(Screen.width * 0.3f, Screen.height * 0.78f, Screen.width * 0.4f, 10 * s);
        MenuUI.Box(bar, new Color(0f, 0f, 0f, 0.7f));
        MenuUI.Box(new Rect(bar.x, bar.y, bar.width * _shown, bar.height), MenuUI.Ember);
        MenuUI.Frame(bar, new Color(1f, 0.78f, 0.42f, 0.5f), 2 * s);
        MenuUI.Text(new Rect(0, bar.yMax + 24 * s, Screen.width, 40 * s), $"<i>{_line}</i>", 24, new Color(0.8f, 0.8f, 0.9f), TextAnchor.MiddleCenter);
    }
}
