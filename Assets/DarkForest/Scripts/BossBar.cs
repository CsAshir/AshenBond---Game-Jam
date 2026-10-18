using UnityEngine;

/// <summary>Boss name + health bar along the bottom of the screen while the boss is alive (phase notch at half).</summary>
public class BossBar : MonoBehaviour
{
    public string bossName = "Boss";
    public Color barColor = new Color(0.75f, 0.35f, 1f);
    EnemyHealth _hp;
    float _shown;
    float _lag = 1f;
    Texture2D _white;

    void Awake()
    {
        _hp = GetComponent<EnemyHealth>();
        _white = Texture2D.whiteTexture;
    }

    void OnGUI()
    {
        MenuUI.ApplyFont();
        if (!_hp) return;
        float target = _hp.IsDead ? 0f : 1f;
        _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 2f);
        if (_shown <= 0.01f) return;
        float k = Mathf.Clamp01(_hp.Hp / (float)_hp.maxHp);
        _lag = Mathf.MoveTowards(_lag, k, Time.unscaledDeltaTime * 0.35f);   // white "damage taken" trail

        float w = Screen.width * 0.5f, h = Screen.height * 0.018f;
        float x = (Screen.width - w) / 2f, y = Screen.height * 0.9f;
        GUI.color = new Color(0f, 0f, 0f, 0.6f * _shown);
        GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), _white);
        GUI.color = new Color(1f, 1f, 1f, 0.5f * _shown);
        GUI.DrawTexture(new Rect(x, y, w * _lag, h), _white);
        GUI.color = new Color(barColor.r, barColor.g, barColor.b, _shown);
        GUI.DrawTexture(new Rect(x, y, w * k, h), _white);
        GUI.color = new Color(1f, 1f, 1f, 0.7f * _shown);
        GUI.DrawTexture(new Rect(x + w * 0.5f - 1, y, 2, h), _white);
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.028f), fontStyle = FontStyle.Bold };
        GUI.color = new Color(1f, 0.85f, 0.7f, _shown);
        GUI.Label(new Rect(0, y - Screen.height * 0.05f, Screen.width, Screen.height * 0.045f), bossName, style);
    }
}
