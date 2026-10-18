using UnityEngine;

/// <summary>Collapsed village gate: walking through fades the screen and announces the next area.</summary>
[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    public string title = "Area B";
    public string subtitle = "The Dark Forest";
    public float fadeTime = 1.5f;
    [Tooltip("Scene to load after the title (must be in Build Settings). Empty = stay on the title.")]
    public string nextScene;
    public float holdTitle = 2.2f;
    [Tooltip("Lines spoken before leaving (\"Speaker|Text\").")]
    public string[] outroLines = new string[0];

    bool _active, _talking;
    float _t;
    Texture2D _black;

    void Awake()
    {
        _black = new Texture2D(1, 1);
        _black.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.06f));
        _black.Apply();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_active || _talking || !other.GetComponentInParent<PlayerController2D>()) return;
        _talking = true;
        DialogueDirector.Play(outroLines, () => { _active = true; Sfx.Play("level_change"); });
    }

    void Update()
    {
        if (!_active) return;
        _t += Time.deltaTime;
        if (!string.IsNullOrEmpty(nextScene) && _t > fadeTime + holdTitle)
        {
            _active = false;
            LoadingScreen.Load(nextScene, title, subtitle);
        }
    }

    void OnGUI()
    {
        MenuUI.ApplyFont();
        if (!_active) return;
        float a = Mathf.Clamp01(_t / fadeTime);
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _black);

        float ta = Mathf.Clamp01((_t - fadeTime) / 1f);
        var big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.07f) };
        var small = new GUIStyle(big) { fontSize = Mathf.RoundToInt(Screen.height * 0.035f) };
        GUI.color = new Color(1f, 0.75f, 0.45f, ta);
        GUI.Label(new Rect(0, Screen.height * 0.38f, Screen.width, Screen.height * 0.12f), title, big);
        GUI.color = new Color(0.55f, 0.75f, 1f, ta);
        GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, Screen.height * 0.08f), subtitle, small);
    }
}
