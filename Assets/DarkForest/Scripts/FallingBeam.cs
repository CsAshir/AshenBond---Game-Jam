using UnityEngine;

/// <summary>
/// A burning roof beam that drops when the player walks under the trigger, hits the ground with a shake,
/// then stays as a burning obstacle to jump over. Touching it while it falls sends the player back.
/// </summary>
public class FallingBeam : MonoBehaviour
{
    [Tooltip("The beam visual + collider that falls.")]
    public Transform beam;
    public Collider2D solidCollider;
    public float groundY;
    public float gravity = 38f;
    public GameObject impactPrefab;
    public ParticleSystem[] burnWhileLying;

    bool _triggered;
    bool _landed;
    float _velocity;

    void Awake()
    {
        if (solidCollider) solidCollider.enabled = false;
        foreach (var ps in burnWhileLying)
            if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered || !other.GetComponentInParent<PlayerController2D>()) return;
        _triggered = true;
        Sfx.Play("wood_crack", transform.position);
    }

    void Update()
    {
        if (!_triggered || _landed || !beam) return;
        _velocity += gravity * Time.deltaTime;
        var p = beam.position;
        p.y -= _velocity * Time.deltaTime;
        if (p.y <= groundY)
        {
            p.y = groundY;
            Land();
        }
        beam.position = p;

        // Crushing check while falling.
        if (_landed) return;
        foreach (var hit in Physics2D.OverlapBoxAll(beam.position + Vector3.up * 0.3f, new Vector2(3f, 0.6f), 0f))
        {
            var player = hit.GetComponentInParent<PlayerController2D>();
            if (player)
            {
                player.Hurt();
                break;
            }
        }
    }

    void Land()
    {
        _landed = true;
        if (solidCollider) solidCollider.enabled = true;
        if (impactPrefab) Instantiate(impactPrefab, beam.position, Quaternion.identity);
        foreach (var ps in burnWhileLying)
            if (ps) ps.Play();
        CameraFollow2D.Shake(0.35f, 0.25f);
        Sfx.Play("wood_fall", beam.position);
    }
}
