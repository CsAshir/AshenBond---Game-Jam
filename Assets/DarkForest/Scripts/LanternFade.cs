using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// GDD: in the tower the lantern visibly weakens (flame drain). Its lights shrink and dim with progress up the
/// tower (start -> arena door) and it gutters on every bell strike. Embers picked up give a little back.
/// </summary>
public class LanternFade : MonoBehaviour
{
    public Transform player;
    public float startX = 4f;
    public float endX = 140f;
    [Range(0f, 1f)] public float minStrength = 0.5f;
    public Light2D[] lights;
    public SpriteRenderer halo;

    float[] _intensity, _radius;
    float _haloA;
    float _gutter;
    int _embers;
    float _boost;

    void Start()
    {
        _intensity = new float[lights.Length];
        _radius = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) { _intensity[i] = lights[i].intensity; _radius[i] = lights[i].pointLightOuterRadius; }
        if (halo) _haloA = halo.color.a;
        _embers = EmberPickup.Collected;
    }

    public void Gutter() => _gutter = 1f;

    void LateUpdate()
    {
        if (!player) return;
        if (EmberPickup.Collected > _embers) _boost = 1f;          // an ember feeds the flame for a moment
        _embers = EmberPickup.Collected;
        _boost = Mathf.Max(0f, _boost - Time.deltaTime * 0.25f);
        _gutter = Mathf.Max(0f, _gutter - Time.deltaTime * 1.4f);

        float progress = Mathf.InverseLerp(startX, endX, player.position.x);
        float strength = Mathf.Lerp(1f, minStrength, progress) + _boost * 0.3f;
        strength *= 1f - _gutter * 0.6f * (0.6f + 0.4f * Mathf.Sin(Time.time * 40f));
        for (int i = 0; i < lights.Length; i++)
        {
            if (!lights[i]) continue;
            var flicker = lights[i].GetComponent<FlickerLight2D>();
            if (flicker) { flicker.strength = strength; flicker.radiusScale = Mathf.Lerp(0.75f, 1f, strength); continue; }
            lights[i].intensity = _intensity[i] * strength;
            lights[i].pointLightOuterRadius = _radius[i] * Mathf.Lerp(0.75f, 1f, strength);
        }
        if (halo) { var c = halo.color; c.a = _haloA * strength; halo.color = c; }
    }
}
