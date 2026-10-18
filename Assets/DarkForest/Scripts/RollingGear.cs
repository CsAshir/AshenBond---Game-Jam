using UnityEngine;

/// <summary>
/// A loose saw-gear rolling back and forth along a stretch of walkway, spitting sparks. Jump it as it passes.
/// It spins in step with its travel (it really rolls), stops with the bell and runs backwards with the rewind.
/// </summary>
public class RollingGear : MonoBehaviour, IRewindable
{
    public float minX, maxX;
    public float speed = 2.6f;
    public float radius = 0.9f;
    public Transform art;
    public ParticleSystem sparks;

    float _x;
    float _dir = 1f;

    void Start() => _x = transform.position.x;

    void Update()
    {
        float dt = TimeFreeze.Delta;
        _x += _dir * speed * dt;
        if (_x > maxX) { _x = maxX; _dir = -1f; Bump(); }
        if (_x < minX) { _x = minX; _dir = 1f; Bump(); }
        Pose(dt);
        Sfx.Loop(this, "gear", dt > 0f, transform.position, 0.7f);
        if (sparks) sparks.transform.localPosition = new Vector3(-_dir * radius * 0.6f, -radius * 0.85f, 0f);

        if (TimeRewind.Rewinding) return;
        foreach (var c in Physics2D.OverlapCircleAll((Vector2)transform.position, radius * 0.85f))
        {
            var hp = c.GetComponentInParent<PlayerHealth>();
            if (hp) { hp.Damage(1, transform.position); break; }
        }
    }

    void Pose(float dt)
    {
        transform.position = new Vector3(_x, transform.position.y, transform.position.z);
        if (art) art.Rotate(0f, 0f, -_dir * speed * dt / radius * Mathf.Rad2Deg);   // rolls, never slides
    }

    void Bump()
    {
        float d = Camera.main ? Mathf.Abs(Camera.main.transform.position.x - _x) : 0f;
        if (d < 12f) CameraFollow2D.Shake(0.06f, 0.08f);
    }

    public object CaptureState() => new Vector2(_x, _dir);

    public void RestoreState(object state)
    {
        var v = (Vector2)state;
        float dx = v.x - _x;
        _x = v.x;
        _dir = v.y;
        transform.position = new Vector3(_x, transform.position.y, transform.position.z);
        if (art) art.Rotate(0f, 0f, -dx / radius * Mathf.Rad2Deg);   // spins back as it rolls back
    }
}
