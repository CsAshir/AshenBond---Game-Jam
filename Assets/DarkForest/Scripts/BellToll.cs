using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The great bell (GDD: a single bell every 12 s). Each strike: a heavy camera thump, a pale shock ring,
/// grit raining from the ceiling, the lantern gutters for a moment, and the frozen clock's light pulses.
/// </summary>
public class BellToll : MonoBehaviour
{
    public float interval = 12f;
    public float firstDelay = 3f;
    public Transform cameraTransform;
    public ParticleSystem grit;               // child of the camera: falls across the whole view
    public GameObject ringFx;
    public Light2D[] pulseLights;             // clock face / bell glow
    public LanternFade lantern;
    public float freezeTime = 1.6f;
    public GameObject clockRingFx;            // ticking ring opening around the boy
    public Transform player;

    float _next;
    float _pulse;
    float[] _base;

    void Start()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        _next = Time.time + firstDelay;
        _base = new float[pulseLights.Length];
        for (int i = 0; i < pulseLights.Length; i++) if (pulseLights[i]) _base[i] = pulseLights[i].intensity;
    }

    void Update()
    {
        if (Time.time >= _next)
        {
            _next = Time.time + interval;
            CameraFollow2D.Shake(0.28f, 0.45f);
            Sfx.Play("bell");
            if (grit) grit.Play();
            if (ringFx && cameraTransform) Instantiate(ringFx, new Vector3(cameraTransform.position.x, cameraTransform.position.y + 5f, 0f), Quaternion.identity);
            if (lantern) lantern.Gutter();
            TimeFreeze.Begin(freezeTime);
            if (clockRingFx && player) Instantiate(clockRingFx, player.position + Vector3.up * 1.4f, Quaternion.identity);
            _pulse = 1f;
        }
        _pulse = Mathf.Max(0f, _pulse - Time.deltaTime * 0.8f);
        for (int i = 0; i < pulseLights.Length; i++)
            if (pulseLights[i]) pulseLights[i].intensity = _base[i] * (1f + _pulse * 1.5f);
    }
}
