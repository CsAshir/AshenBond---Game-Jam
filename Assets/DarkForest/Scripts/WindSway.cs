using UnityEngine;

/// <summary>Gentle wind: rocks a sprite around its base (pivot at the bottom). Shared gusts keep everything in sync.</summary>
public class WindSway : MonoBehaviour
{
    [Tooltip("Max lean in degrees.")]
    public float amount = 1.2f;
    public float speed = 1.3f;
    [Range(0f, 1f)] public float gustInfluence = 0.7f;

    float _phase;
    Quaternion _base;

    /// <summary>0..1 global gust strength (slow noise), also used by particles that want to follow the wind.</summary>
    public static float Gust => Mathf.PerlinNoise(Time.time * 0.15f, 0.37f);

    void Start()
    {
        _phase = transform.position.x * 0.35f;
        _base = transform.localRotation;
    }

    void Update()
    {
        float gust = Gust;
        float sway = Mathf.Sin(Time.time * speed + _phase) * (1f - gustInfluence * 0.5f)
                   + gust * gustInfluence * 1.6f
                   + Mathf.Sin(Time.time * speed * 2.7f + _phase * 2f) * 0.25f * gust;
        transform.localRotation = _base * Quaternion.Euler(0f, 0f, -sway * amount);
    }
}
