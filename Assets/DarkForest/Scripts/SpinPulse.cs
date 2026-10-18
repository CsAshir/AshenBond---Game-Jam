using UnityEngine;

/// <summary>Rotates and gently pulses a transform (magic circles, runes).</summary>
public class SpinPulse : MonoBehaviour
{
    public float degreesPerSecond = 90f;
    public float pulseAmount = 0.06f;
    public float pulseSpeed = 6f;
    [Tooltip("Grow from 0 to full size over this many seconds when spawned.")]
    public float growTime = 0.25f;

    Vector3 _scale;
    float _t;

    void Awake() => _scale = transform.localScale;

    void Update()
    {
        _t += Time.deltaTime;
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
        float grow = growTime > 0f ? Mathf.SmoothStep(0f, 1f, _t / growTime) : 1f;
        transform.localScale = _scale * grow * (1f + Mathf.Sin(_t * pulseSpeed) * pulseAmount);
    }
}
