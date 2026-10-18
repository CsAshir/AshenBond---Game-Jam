using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Neon look for the boy:
///  - rim: a slightly larger additive copy of the sprite drawn behind him = glowing outline.
///    Amber (his lantern) by default, cyan while his sister is out, orange-white while charging.
///  - overlay: a soft full-body flash on top for heals / shots / hits.
///  - the lantern's warm point light, kept on the lantern side.
/// </summary>
public class PlayerGlow : MonoBehaviour
{
    public SpriteRenderer source;
    public SpriteRenderer overlay;
    public SpriteRenderer rim;
    public Light2D lanternLight;
    public Vector2 lanternOffset = new Vector2(0.55f, 1.0f);

    [Header("Rim")]
    public Color rimColor = new Color(1f, 0.6f, 0.25f);
    public Color sisterRimColor = new Color(0.35f, 0.85f, 1f);
    [Range(0f, 1f)] public float rimAlpha = 0.55f;
    public float rimScale = 1.07f;

    Color _flashColor;
    float _flashTime, _flashDuration, _flashPeak;
    float _hold;
    Color _holdColor;
    bool _sister;
    Color _rimCurrent;
    float _overlayAlpha;

    public void Flash(Color color, float duration, float peak = 1f)
    {
        _flashColor = color;
        _flashDuration = Mathf.Max(0.01f, duration);
        _flashTime = _flashDuration;
        _flashPeak = peak;
    }

    /// <summary>Continuous glow while charging; strength 0 turns it off.</summary>
    public void Hold(Color color, float strength)
    {
        _holdColor = color;
        _hold = strength;
    }

    /// <summary>The sister-shadow is out: the rim turns cyan.</summary>
    public void SetSister(bool active) => _sister = active;

    void Start() => _rimCurrent = rimColor;

    void LateUpdate()
    {
        if (!source) return;

        float flash = 0f;
        if (_flashTime > 0f)
        {
            _flashTime -= Time.deltaTime;
            flash = _flashPeak * Mathf.Sin(_flashTime / _flashDuration * Mathf.PI);
        }

        if (rim)
        {
            rim.sprite = source.sprite;
            rim.flipX = source.flipX;
            float h = source.sprite ? source.sprite.bounds.size.y : 3f;
            rim.transform.localScale = new Vector3(rimScale, rimScale, 1f);
            rim.transform.localPosition = new Vector3(0f, -(rimScale - 1f) * h * 0.5f, 0f);

            Color target = _sister ? sisterRimColor : rimColor;
            float a = rimAlpha * (0.85f + 0.15f * Mathf.Sin(Time.time * 3f));
            if (_hold > 0.01f)
            {
                target = Color.Lerp(target, _holdColor, Mathf.Clamp01(_hold * 2f));
                a = Mathf.Max(a, 0.5f + _hold * 0.5f + 0.1f * Mathf.Sin(Time.time * 20f));
            }
            if (flash > 0.05f)
            {
                target = Color.Lerp(target, _flashColor, flash);
                a = Mathf.Max(a, flash);
            }
            _rimCurrent = Color.Lerp(_rimCurrent, target, Time.deltaTime * 10f);
            rim.color = new Color(_rimCurrent.r, _rimCurrent.g, _rimCurrent.b, Mathf.Clamp01(a));
        }

        if (overlay)
        {
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            float target = Mathf.Max(flash * 0.45f, _hold * 0.18f);
            _overlayAlpha = Mathf.Lerp(_overlayAlpha, target, Time.deltaTime * 20f);
            Color c = flash * 0.45f >= _hold * 0.18f ? _flashColor : _holdColor;
            overlay.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(_overlayAlpha));
        }

        if (lanternLight)
        {
            float dir = source.flipX ? -1f : 1f;
            lanternLight.transform.localPosition = new Vector3(lanternOffset.x * dir, lanternOffset.y, 0f);
        }
    }
}
