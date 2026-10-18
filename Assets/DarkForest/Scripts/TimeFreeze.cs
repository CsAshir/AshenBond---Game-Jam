using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Area C's time theme: each bell strike stops time for a moment. Mechanisms that respect it (pendulums, gears,
/// lifts, the collapsing bridge, drifting dust) freeze; the picture turns cold and desaturated; only the boy keeps
/// moving, leaving after-images. A frozen pendulum is a free pass: the bell is also a gameplay beat.
/// </summary>
public class TimeFreeze : MonoBehaviour
{
    public static bool Active => Time.time < _until;
    static float _until;

    public Volume volume;                    // cold, desaturated grade blended in while frozen
    public ParticleSystem[] pausable;
    public AfterImageTrail trail;

    bool _was;

    public static void Begin(float seconds) => _until = Mathf.Max(_until, Time.time + seconds);

    void OnDisable() => _until = 0f;

    void Update()
    {
        bool on = Active;
        if (volume) volume.weight = Mathf.MoveTowards(volume.weight, on ? 1f : 0f, Time.deltaTime * (on ? 6f : 2.5f));
        if (on == _was) return;
        _was = on;
        foreach (var ps in pausable)
        {
            if (!ps) continue;
            if (on) ps.Pause(true); else ps.Play(true);
        }
        if (trail) trail.enabled = on;
    }

    /// <summary>Delta time for things that obey frozen time.</summary>
    public static float Delta => Active ? 0f : Time.deltaTime;
    public static float FixedDelta => Active ? 0f : Time.fixedDeltaTime;
}
