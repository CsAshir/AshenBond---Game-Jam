using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives a one-off effect: expanding flash sprites, a sustain phase (looping particles keep emitting),
/// then a fade of lights and sprites, then self-destruct.
/// </summary>
public class VfxLifetime : MonoBehaviour
{
    public float sustain = 2f;
    public float fade = 1f;
    [Tooltip("Sprites that pop: grow quickly and fade out over flashDuration.")]
    public SpriteRenderer[] flashes;
    public float flashDuration = 0.4f;
    public float flashGrow = 3f;
    [Range(0f, 1f)] public float lightFlicker = 0.3f;

    ParticleSystem[] _systems;
    Light2D[] _lights;
    float[] _lightBase;
    SpriteRenderer[] _sprites;
    float[] _spriteAlpha;
    Vector3[] _flashScale;
    float[] _flashAlpha;
    float _t;
    bool _stopped;
    float _seed;

    void Awake()
    {
        _seed = Random.value * 100f;
        _systems = GetComponentsInChildren<ParticleSystem>();
        _lights = GetComponentsInChildren<Light2D>();
        _lightBase = new float[_lights.Length];
        for (int i = 0; i < _lights.Length; i++) _lightBase[i] = _lights[i].intensity;

        flashes ??= new SpriteRenderer[0];
        _flashScale = new Vector3[flashes.Length];
        _flashAlpha = new float[flashes.Length];
        for (int i = 0; i < flashes.Length; i++)
        {
            _flashScale[i] = flashes[i].transform.localScale;
            _flashAlpha[i] = flashes[i].color.a;
        }

        var all = GetComponentsInChildren<SpriteRenderer>();
        _sprites = System.Array.FindAll(all, s => System.Array.IndexOf(flashes, s) < 0);
        _spriteAlpha = new float[_sprites.Length];
        for (int i = 0; i < _sprites.Length; i++) _spriteAlpha[i] = _sprites[i].color.a;
    }

    void Update()
    {
        _t += Time.deltaTime;

        for (int i = 0; i < flashes.Length; i++)
        {
            if (!flashes[i]) continue;
            float k = Mathf.Clamp01(_t / flashDuration);
            float ease = 1f - (1f - k) * (1f - k);
            flashes[i].transform.localScale = _flashScale[i] * (1f + flashGrow * ease);
            var c = flashes[i].color;
            c.a = _flashAlpha[i] * (1f - k);
            flashes[i].color = c;
        }

        if (!_stopped && _t >= sustain)
        {
            _stopped = true;
            foreach (var ps in _systems)
                if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        float fadeK = Mathf.Clamp01((_t - sustain) / Mathf.Max(0.01f, fade));
        float flicker = 1f + (Mathf.PerlinNoise(_seed, _t * 9f) * 2f - 1f) * lightFlicker;
        for (int i = 0; i < _lights.Length; i++)
            if (_lights[i]) _lights[i].intensity = _lightBase[i] * (1f - fadeK) * flicker;

        for (int i = 0; i < _sprites.Length; i++)
        {
            if (!_sprites[i]) continue;
            var c = _sprites[i].color;
            c.a = _spriteAlpha[i] * (1f - fadeK);
            _sprites[i].color = c;
        }

        if (_t >= sustain + fade + 1.5f) Destroy(gameObject);
    }
}
