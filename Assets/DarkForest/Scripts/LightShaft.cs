using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A god-ray falling through a gap in the canopy. It breathes slowly (clouds and leaves passing over the gap),
/// leans a little with the wind, and keeps its ground light and dust in step with its brightness.
/// </summary>
public class LightShaft : MonoBehaviour
{
    public SpriteRenderer[] beams;
    public Light2D groundLight;
    public float breatheSpeed = 0.35f;
    [Range(0f, 1f)] public float breatheAmount = 0.45f;
    public float leanDegrees = 1.2f;

    float[] _alpha;
    float _light;
    float _phase;
    Quaternion _rot;

    void Start()
    {
        _phase = transform.position.x * 0.71f;
        _rot = transform.localRotation;
        _alpha = new float[beams.Length];
        for (int i = 0; i < beams.Length; i++) _alpha[i] = beams[i] ? beams[i].color.a : 0f;
        if (groundLight) _light = groundLight.intensity;
    }

    void Update()
    {
        float t = Time.time * breatheSpeed + _phase;
        // Two slow waves -> irregular "cloud passing" rhythm, never fully off.
        float k = 1f - breatheAmount * (0.5f + 0.5f * Mathf.Sin(t) * Mathf.Sin(t * 0.63f + 2.1f));
        for (int i = 0; i < beams.Length; i++)
        {
            if (!beams[i]) continue;
            var c = beams[i].color;
            c.a = _alpha[i] * k;
            beams[i].color = c;
        }
        if (groundLight) groundLight.intensity = _light * k;
        transform.localRotation = _rot * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.4f + _phase) * leanDegrees);
    }
}
