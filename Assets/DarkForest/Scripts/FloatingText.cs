using UnityEngine;

/// <summary>Small world-space text that pops above a character ("!", "!!", taunts) and fades.</summary>
public class FloatingText : MonoBehaviour
{
    static Font _font;

    Transform _target;
    Vector3 _offset;
    float _life, _t;
    TextMesh _text;
    Color _color;

    public static FloatingText Show(Transform target, Vector3 offset, string text, Color color, float life = 1.4f, float size = 1f)
    {
        _font ??= MenuUI.BoldFont ? MenuUI.BoldFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Floating Text");
        var tm = go.AddComponent<TextMesh>();
        tm.font = _font;
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = 0.06f * size;
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _font.material;
        mr.sortingOrder = 60;
        var ft = go.AddComponent<FloatingText>();
        ft._target = target;
        ft._offset = offset;
        ft._life = life;
        ft._text = tm;
        ft._color = color;
        go.transform.position = (target ? target.position : Vector3.zero) + offset;
        return ft;
    }

    void LateUpdate()
    {
        _t += Time.unscaledDeltaTime;
        if (_target) transform.position = _target.position + _offset + Vector3.up * Mathf.Min(_t, 0.3f) * 0.8f;
        float pop = _t < 0.12f ? Mathf.Lerp(0.4f, 1.25f, _t / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((_t - 0.12f) / 0.1f));
        transform.localScale = Vector3.one * pop;
        var c = _color;
        c.a *= Mathf.Clamp01((_life - _t) / 0.35f);
        _text.color = c;
        if (_t >= _life || (!_target && _t > 0.1f)) Destroy(gameObject);
    }
}
