using UnityEngine;

/// <summary>Lobbed shadow-fire orb. Bursts into purple fire wherever it lands.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ShadowOrb : MonoBehaviour
{
    public GameObject impactPrefab;
    public ParticleSystem trail;
    public Transform core;
    public float maxLifetime = 4f;
    public float pulseSpeed = 18f;

    Rigidbody2D _rb;
    float _age;
    bool _done;
    Vector3 _coreScale;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        transform.localScale *= 1.35f;                           // a bigger, heavier shadow-fire ball
        if (core) _coreScale = core.localScale;
    }

    public void Launch(Vector2 velocity, Collider2D ignore)
    {
        if (!_rb) _rb = GetComponent<Rigidbody2D>();
        _rb.linearVelocity = velocity;
        var own = GetComponent<Collider2D>();
        if (ignore && own) Physics2D.IgnoreCollision(own, ignore);
    }

    [Tooltip("Curves toward an enemy the lantern has exposed (completes the fire -> shadow combo).")]
    public float homing = 5f;
    public float homingRange = 8f;

    void Update()
    {
        _age += Time.deltaTime;
        if (homing > 0f && _rb && !_done)
        {
            EnemyHealth best = null;
            float bestD = homingRange;
            foreach (var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            {
                if (e.IsDead || !e.IsExposed) continue;
                float d = Vector2.Distance(e.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best)
            {
                Vector2 to = ((Vector2)best.transform.position + best.fxOffset - (Vector2)transform.position).normalized;
                float speed = Mathf.Max(9f, _rb.linearVelocity.magnitude);
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, to * speed, Time.deltaTime * homing);
            }
        }
        if (core)
        {
            float p = 1f + Mathf.Sin(_age * pulseSpeed) * 0.12f;
            core.localScale = _coreScale * p;
            core.Rotate(0f, 0f, 540f * Time.deltaTime);
        }
        if (_age >= maxLifetime) Explode(transform.position);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        var point = collision.contactCount > 0 ? (Vector3)collision.GetContact(0).point : transform.position;
        Explode(point);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy && !enemy.IsDead) Explode(transform.position);
    }

    [Tooltip("Shadow strike: area damage (x3 on enemies exposed by fire).")]
    public int damage = 2;
    public float radius = 3.2f;

    void Explode(Vector3 point)
    {
        if (_done) return;
        _done = true;
        if (impactPrefab)
        {
            var fx = Instantiate(impactPrefab, point, Quaternion.identity);
            fx.transform.localScale *= radius / 1.8f;              // the burst is as big as what it hits
        }
        CameraFollow2D.Shake(0.18f, 0.18f);
        Sfx.Play("shadow_burst", point);
        var done = new System.Collections.Generic.HashSet<EnemyHealth>();
        foreach (var hit in Physics2D.OverlapCircleAll(point, radius))
        {
            var e = hit.GetComponentInParent<EnemyHealth>();
            if (e && done.Add(e)) e.Hit(damage, DamageKind.Shadow, point);
        }

        // Let the trail finish fading on its own.
        if (trail)
        {
            trail.transform.SetParent(null, true);
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(trail.gameObject, 1.5f);
        }
        Destroy(gameObject);
    }
}
