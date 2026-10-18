using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A glowing forest spore carried by the wind. It drifts with the shared gust (WindSway.Gust), bobs and pulses,
/// lights the trees and ground it passes, and wraps around the camera so a handful of wisps fill the whole level.
/// </summary>
public class WindWisp : MonoBehaviour
{
    public Transform cameraTransform;
    public Vector2 range = new Vector2(17f, 5f);       // half-size of the area around the camera
    public float minY = 4f;                            // stay above the walkway mist
    public float speed = 0.9f;
    public Light2D wispLight;
    public SpriteRenderer glow;

    float _baseY, _phase, _lightBase, _glowAlpha, _speedJitter;

    void Start()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        _phase = Random.value * 10f;
        _speedJitter = Random.Range(0.7f, 1.3f);
        if (wispLight) _lightBase = wispLight.intensity;
        if (glow) _glowAlpha = glow.color.a;
        if (cameraTransform)
            transform.position = new Vector3(cameraTransform.position.x + Random.Range(-range.x, range.x), 0f, transform.position.z);
        PickHeight();
    }

    void PickHeight()
    {
        float cy = cameraTransform ? cameraTransform.position.y : 8f;
        _baseY = Mathf.Max(minY, cy + Random.Range(-range.y, range.y * 0.6f));
    }

    void Update()
    {
        float t = Time.time + _phase;
        var p = transform.position;
        p.x += (speed + WindSway.Gust * 1.4f) * _speedJitter * Time.deltaTime;
        p.y = _baseY + Mathf.Sin(t * 0.9f) * 0.6f + Mathf.Sin(t * 2.3f) * 0.15f;

        if (cameraTransform)
        {
            float dx = p.x - cameraTransform.position.x;
            if (dx > range.x) { p.x = cameraTransform.position.x - range.x; PickHeight(); }
            else if (dx < -range.x - 2f) { p.x = cameraTransform.position.x + range.x; PickHeight(); }
        }
        transform.position = p;

        float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.1f);
        if (wispLight) wispLight.intensity = _lightBase * pulse;
        if (glow) { var c = glow.color; c.a = _glowAlpha * pulse; glow.color = c; }
    }
}
