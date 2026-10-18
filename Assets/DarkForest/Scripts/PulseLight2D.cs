using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Slow sine "breathing" for neon / rune / magic lights.</summary>
[RequireComponent(typeof(Light2D))]
public class PulseLight2D : MonoBehaviour
{
    [Range(0f, 1f)] public float pulseAmount = 0.35f;
    public float speed = 1.6f;

    Light2D _light;
    float _baseIntensity;
    float _phase;

    void Awake()
    {
        _light = GetComponent<Light2D>();
        _baseIntensity = _light.intensity;
        _phase = transform.position.x * 0.7f;
    }

    void Update()
    {
        _light.intensity = _baseIntensity * (1f + Mathf.Sin(Time.time * speed + _phase) * pulseAmount);
    }
}
