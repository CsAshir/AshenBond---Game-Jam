using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A fallen enemy's soul: pops up out of the body, hangs for a breath, then curves into the boy's lantern on a
/// long glowing tail. On arrival the lantern flares and Mira's shadow strike charges a little.
/// </summary>
public class SoulOrb : MonoBehaviour
{
    public float rise = 2.2f;
    public float hang = 0.35f;
    public float speed = 13f;
    public float turn = 7f;
    [Range(0f, 1f)] public float sisterCharge = 0.15f;
    public Color color = new Color(0.55f, 0.95f, 1f);
    public GameObject arriveFx;
    public Light2D glow;

    Transform _target;
    Vector3 _vel;
    float _t;

    void Start()
    {
        var p = GameObject.Find("Player");
        if (p)
        {
            var lantern = p.transform.Find("Lantern (hand)");
            _target = lantern ? lantern : p.transform;
        }
        _vel = new Vector3(Random.Range(-2.5f, 2.5f), rise * 2.2f, 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;
        if (glow) glow.intensity = 1.4f + 0.5f * Mathf.Sin(Time.time * 14f);
        if (_t < hang)
        {
            _vel = Vector3.Lerp(_vel, Vector3.zero, dt * 5f);          // floats up and slows: a beat to notice it
        }
        else if (_target)
        {
            var to = _target.position + Vector3.down * 0.3f - transform.position;
            float k = Mathf.Clamp01((_t - hang) * 1.6f);
            _vel = Vector3.Lerp(_vel, to.normalized * speed * (0.6f + k), dt * turn * (0.5f + k));
            if (to.magnitude < 0.45f) { Arrive(); return; }
        }
        transform.position += _vel * dt;
        if (_t > 6f) Destroy(gameObject);
    }

    void Arrive()
    {
        if (arriveFx) Instantiate(arriveFx, _target.position, Quaternion.identity);
        Sfx.Play("soul", _target.position);
        var pg = _target.GetComponentInParent<PlayerGlow>();
        if (pg) pg.Flash(color, 0.35f, 1.1f);
        var sister = _target.root.GetComponentInChildren<ShadowSummon>();
        if (sister) sister.AddCharge(sisterCharge);
        var trail = GetComponentInChildren<TrailRenderer>();
        if (trail) { trail.transform.SetParent(null); trail.emitting = false; Destroy(trail.gameObject, trail.time); }
        Destroy(gameObject);
    }
}
