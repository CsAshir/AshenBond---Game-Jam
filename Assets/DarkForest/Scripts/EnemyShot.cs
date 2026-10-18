using UnityEngine;

/// <summary>Enemy projectile (Flying Eye spit): flies straight, hurts the boy on touch, bursts on walls / ground.</summary>
public class EnemyShot : MonoBehaviour
{
    public Sprite[] flyFrames;
    public Sprite[] burstFrames;
    public float fps = 12f;
    public float lifetime = 3.5f;
    public int damage = 1;
    [Tooltip("0 = straight line; spores arc and fall.")]
    public float gravity;
    [Tooltip("Time-lock: hang frozen in the air this long, then fire at the boy.")]
    public float hold;
    public float releaseSpeed = 9f;
    public SpriteRenderer holdGlow;
    public SpriteRenderer art;
    [Tooltip("Lantern fire can shoot it down.")]
    public bool shootable = true;

    Vector2 _vel;
    float _t;
    bool _burst;

    public void Launch(Vector2 velocity)
    {
        _vel = velocity;
        if (art) art.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_burst)
        {
            int i = (int)(_t * 18f);
            if (i >= burstFrames.Length) { Destroy(gameObject); return; }
            art.sprite = burstFrames[i];
            return;
        }
        _vel.y -= gravity * Time.deltaTime;
        if (hold > 0f)
        {
            // Frozen in time: drift to a stop, shiver, then lock onto the boy and release.
            _vel = Vector2.Lerp(_vel, Vector2.zero, Time.deltaTime * 8f);
            transform.position += (Vector3)_vel * Time.deltaTime + new Vector3(Mathf.Sin(Time.time * 50f) * 0.004f, 0f, 0f);
            if (holdGlow) holdGlow.enabled = true;
            if (_t >= hold)
            {
                hold = 0f;
                if (holdGlow) holdGlow.enabled = false;
                var p = GameObject.Find("Player");
                var to = p ? (Vector2)(p.transform.position + Vector3.up * 1.3f - transform.position) : Vector2.down;
                Launch(to.normalized * releaseSpeed);
            }
            if (art && flyFrames.Length > 0) art.sprite = flyFrames[0];
            return;
        }
        transform.position += (Vector3)_vel * Time.deltaTime;
        if (art && flyFrames.Length > 0) art.sprite = flyFrames[(int)(_t * fps) % flyFrames.Length];
        if (_t > lifetime + hold) Burst();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_burst) return;
        var player = other.GetComponentInParent<PlayerController2D>();
        if (player)
        {
            var hp = player.GetComponent<PlayerHealth>();
            if (hp) hp.Damage(damage, transform.position - (Vector3)_vel.normalized);
            Burst();
            return;
        }
        if (shootable && other.GetComponentInParent<FireBolt>()) { Burst(); return; }
        if (!other.isTrigger && !other.GetComponentInParent<EnemyHealth>()) Burst();   // walls, ground, bridges
    }

    void Burst()
    {
        _burst = true;
        _t = 0f;
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        if (burstFrames == null || burstFrames.Length == 0) Destroy(gameObject);
    }
}
