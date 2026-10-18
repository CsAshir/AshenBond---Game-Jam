using UnityEngine;

/// <summary>
/// Clock pendulum with a spinning saw-gear bob. Swings across the walkway on a steady beat (readable timing);
/// touching the gear costs a heart and throws the boy back. The rod is a tiled chain stretched to the bob.
/// </summary>
public class Pendulum : MonoBehaviour, IRewindable
{
    public float length = 6f;
    public float maxAngle = 55f;
    public float period = 2.6f;
    public float phase;
    public Transform bob;                 // spinning gear at the end of the rod
    public SpriteRenderer rod;            // tiled chain, pivot at the top
    public float spinSpeed = 360f;
    public float hitRadius = 0.9f;
    public ParticleSystem sparks;         // sparks thrown off at the bottom of each swing

    float _lastSign;
    float _clock;

    void Update()
    {
        _clock += TimeFreeze.Delta;          // stops with the bell
        float a = maxAngle * Mathf.Sin((_clock / period + phase) * Mathf.PI * 2f);
        transform.localRotation = Quaternion.Euler(0f, 0f, a);
        if (rod) rod.size = new Vector2(rod.size.x, length);
        if (bob)
        {
            bob.localPosition = new Vector3(0f, -length, 0f);
            bob.Rotate(0f, 0f, -spinSpeed * TimeFreeze.Delta * Mathf.Sign(a + 0.001f), Space.Self);
        }

        float s = Mathf.Sign(a);
        if (s != _lastSign && sparks) { sparks.Play(); Sfx.Play("chains", bob ? bob.position : transform.position, 0.5f); }   // whoosh through the bottom
        _lastSign = s;

        if (!bob || TimeRewind.Rewinding) return;
        foreach (var c in Physics2D.OverlapCircleAll(bob.position, hitRadius))
        {
            var pc = c.GetComponentInParent<PlayerController2D>();
            if (!pc) continue;
            var hp = pc.GetComponent<PlayerHealth>();
            if (hp) hp.Damage(1, bob.position);
            break;
        }
    }

    public object CaptureState() => _clock;
    public void RestoreState(object state) => _clock = (float)state;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
        var top = transform.position;
        for (int i = -1; i <= 1; i += 2)
        {
            var dir = Quaternion.Euler(0f, 0f, maxAngle * i) * Vector3.down;
            Gizmos.DrawLine(top, top + dir * length);
        }
    }
}
