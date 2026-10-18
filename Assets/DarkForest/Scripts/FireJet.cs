using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ground vent that cycles: idle -> warning sputter -> roaring flame column (deadly) -> idle.
/// </summary>
public class FireJet : MonoBehaviour
{
    public float idleTime = 1.6f;
    public float warnTime = 0.6f;
    public float activeTime = 1.1f;
    [Tooltip("Shift this jet's cycle so neighbouring jets alternate.")]
    public float timeOffset;

    public ParticleSystem warning;
    public ParticleSystem flames;
    public Collider2D killZone;
    public Light2D jetLight;
    public SpriteRenderer ventGlow;

    float _lightBase;
    float _glowAlpha;
    int _state = -1;

    void Awake()
    {
        if (jetLight) _lightBase = jetLight.intensity;
        if (ventGlow) _glowAlpha = ventGlow.color.a;
        SetState(0);
    }

    void Update()
    {
        float cycle = idleTime + warnTime + activeTime;
        float t = Mathf.Repeat(Time.time + timeOffset, cycle);
        int state = t < idleTime ? 0 : t < idleTime + warnTime ? 1 : 2;
        if (state != _state) SetState(state);

        float target = state == 2 ? 1f : state == 1 ? 0.35f : 0.08f;
        if (jetLight) jetLight.intensity = Mathf.Lerp(jetLight.intensity, _lightBase * target, Time.deltaTime * 12f);
        if (ventGlow)
        {
            var c = ventGlow.color;
            c.a = Mathf.Lerp(c.a, _glowAlpha * (state == 0 ? 0.3f : 1f), Time.deltaTime * 10f);
            ventGlow.color = c;
        }
    }

    void SetState(int state)
    {
        _state = state;
        if (warning)
        {
            if (state == 1) warning.Play(); else warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (flames)
        {
            if (state == 2) flames.Play(); else flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (killZone) killZone.enabled = state == 2;
        if (state == 2) Sfx.Play("fire_jet", transform.position);
    }

    void OnTriggerEnter2D(Collider2D other) => Hit(other);
    void OnTriggerStay2D(Collider2D other) => Hit(other);

    void Hit(Collider2D other)
    {
        if (_state != 2) return;
        var player = other.GetComponentInParent<PlayerController2D>();
        if (player)
        {
            CameraFollow2D.Shake(0.25f, 0.2f);
            player.Hurt();
        }
    }
}
