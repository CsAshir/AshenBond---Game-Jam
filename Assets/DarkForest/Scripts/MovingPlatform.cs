using UnityEngine;

/// <summary>Kinematic platform that glides between two points and carries whoever stands on it.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour, IRewindable
{
    public Vector2 pointA;
    public Vector2 pointB;
    public float period = 4f;
    public float topY = 0.25f;   // local height of the walking surface

    Rigidbody2D _rb;
    Rigidbody2D _rider;
    float _t;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void FixedUpdate()
    {
        _t += TimeFreeze.FixedDelta;
        float k = 0.5f - 0.5f * Mathf.Cos(_t / period * Mathf.PI * 2f);
        Vector2 next = Vector2.Lerp(pointA, pointB, k);
        Vector2 delta = next - _rb.position;
        _rb.MovePosition(next);
        if (_rider) _rider.position += delta;
        Sfx.Loop(this, "gear", delta.sqrMagnitude > 0f, transform.position, 0.5f);
    }

    public object CaptureState() => _t;

    public void RestoreState(object state)
    {
        _t = (float)state;
        _rider = null;
        float k = 0.5f - 0.5f * Mathf.Cos(_t / period * Mathf.PI * 2f);
        _rb.position = Vector2.Lerp(pointA, pointB, k);
        transform.position = _rb.position;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.rigidbody && collision.transform.position.y >= transform.position.y + topY - 0.15f)
            _rider = collision.rigidbody;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.rigidbody == _rider) _rider = null;
    }
}
