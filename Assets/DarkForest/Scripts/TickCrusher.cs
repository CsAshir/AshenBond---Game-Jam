using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Clock-tower stamp: a geared iron head on a chain that slams onto the walkway on the clock's beat.
/// Readable rhythm: it rests up high, shakes and glows red (warning), slams, grinds on the floor a moment, then
/// winds back up. Stops with the bell, runs backwards with the rewind. Touching the head costs a heart.
/// </summary>
public class TickCrusher : MonoBehaviour, IRewindable
{
    public Transform head;
    public SpriteRenderer rod;              // tiled chain from the anchor down to the head
    public float drop = 5f;                 // anchor-to-floor travel of the head
    public float period = 3f;
    [Range(0f, 1f)] public float phase;
    public float warnTime = 0.45f;
    public float slamTime = 0.09f;
    public float holdTime = 0.45f;
    public float riseTime = 0.9f;
    public Vector2 hitSize = new Vector2(2f, 1.2f);
    public SpriteRenderer warnGlow;
    public Light2D warnLight;
    public ParticleSystem impactDust;
    public Transform gear;                  // turns while winding up

    float _clock;
    float _lastY;

    void Start() => _clock = phase * period;

    void Update()
    {
        _clock += TimeFreeze.Delta;
        float t = Mathf.Repeat(_clock, period);
        float wait = period - warnTime - slamTime - holdTime - riseTime;
        float y;                                    // 0 = up, 1 = on the floor
        float warn = 0f;
        if (t < wait) y = 0f;
        else if ((t -= wait) < warnTime) { y = 0f; warn = t / warnTime; }
        else if ((t -= warnTime) < slamTime) y = (t / slamTime) * (t / slamTime);
        else if ((t -= slamTime) < holdTime) y = 1f;
        else { t -= holdTime; y = 1f - Mathf.SmoothStep(0f, 1f, t / riseTime); }

        float shake = warn > 0f ? Mathf.Sin(Time.time * 80f) * 0.05f * warn : 0f;
        if (head) head.localPosition = new Vector3(shake, -0.6f - y * drop, 0f);
        if (rod) rod.size = new Vector2(rod.size.x, 0.6f + y * drop);
        if (gear) gear.Rotate(0f, 0f, (y < _lastY ? 220f : y > _lastY ? -60f : 0f) * TimeFreeze.Delta);
        if (warnGlow) { var c = warnGlow.color; c.a = Mathf.Max(warn, y >= 1f ? 0.6f : 0f) * 0.7f; warnGlow.color = c; }
        if (warnLight) warnLight.intensity = Mathf.Max(warn, y >= 1f ? 0.5f : 0f) * 2.2f;

        if (y >= 1f && _lastY < 1f && !TimeRewind.Rewinding)
        {
            if (impactDust) impactDust.Play();
            Sfx.Play("crusher", transform.position);
            float d = Camera.main ? Mathf.Abs(Camera.main.transform.position.x - transform.position.x) : 0f;
            if (d < 14f) CameraFollow2D.Shake(0.18f * (1f - d / 14f), 0.15f);
        }
        if (y < _lastY && _lastY >= 1f && !TimeRewind.Rewinding) Sfx.Play("steam", transform.position, 0.6f);   // pistons hiss as they wind up
        _lastY = y;

        if (y > 0.6f && head && !TimeRewind.Rewinding)
            foreach (var c in Physics2D.OverlapBoxAll(head.position, hitSize, 0f))
            {
                var hp = c.GetComponentInParent<PlayerHealth>();
                if (hp) { hp.Damage(1, head.position + Vector3.up); break; }
            }
    }

    public object CaptureState() => _clock;
    public void RestoreState(object state) => _clock = (float)state;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.down * (0.6f + drop), hitSize);
    }
}
