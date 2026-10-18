using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Noise-driven flicker for fire lights (campfire, torches).</summary>
[RequireComponent(typeof(Light2D))]
public class FlickerLight2D : MonoBehaviour
{
    [Range(0f, 1f)] public float flickerAmount = 0.35f;
    public float speed = 7f;
    [Range(0f, 0.5f)] public float radiusJitter = 0.08f;
    [Tooltip("Scales the light on top of the flicker (e.g. the lantern fading in the tower).")]
    public float strength = 1f;
    public float radiusScale = 1f;

    Light2D _light;
    float _baseIntensity;
    float _baseRadius;
    float _seed;

    void Awake()
    {
        _light = GetComponent<Light2D>();
        _baseIntensity = _light.intensity;
        _baseRadius = _light.pointLightOuterRadius;
        _seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(_seed, Time.time * speed) * 2f - 1f;
        float n2 = Mathf.PerlinNoise(_seed + 31f, Time.time * speed * 2.3f) * 2f - 1f;
        float f = n * 0.7f + n2 * 0.3f;
        _light.intensity = _baseIntensity * strength * (1f + f * flickerAmount);
        _light.pointLightOuterRadius = _baseRadius * radiusScale * (1f + f * radiusJitter);
    }
}
