using UnityEngine;

/// <summary>
/// The frozen clock's second hand: it tries to tick past 11:59 and is pulled back every time (time is stuck).
/// Rotates around its own pivot (the clock centre).
/// </summary>
public class ClockHand : MonoBehaviour
{
    public float baseAngle = 6f;      // resting on "59"
    public float tick = 6f;
    public float interval = 1f;

    float _t;

    void Update()
    {
        _t += TimeFreeze.Delta;
        float phase = Mathf.Repeat(_t, interval) / interval;
        // Snap forward one tick, shudder, then drag back to 59.
        float a = phase < 0.12f ? Mathf.Lerp(0f, tick, phase / 0.12f)
                : phase < 0.5f ? tick + Mathf.Sin(phase * 120f) * 0.6f
                : Mathf.Lerp(tick, 0f, (phase - 0.5f) / 0.5f);
        transform.localRotation = Quaternion.Euler(0f, 0f, -(baseAngle + a) + 360f);
    }
}
