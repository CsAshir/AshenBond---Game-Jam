using UnityEngine;

/// <summary>Gentle up/down hover for floating runes and wisps.</summary>
public class FloatBob : MonoBehaviour
{
    public float amplitude = 0.15f;
    public float speed = 1.2f;

    Vector3 _start;
    float _phase;

    void Start()
    {
        _start = transform.localPosition;
        _phase = _start.x * 1.3f;
    }

    void Update()
    {
        transform.localPosition = _start + Vector3.up * (Mathf.Sin(Time.time * speed + _phase) * amplitude);
    }
}
