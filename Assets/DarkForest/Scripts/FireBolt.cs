using UnityEngine;

/// <summary>Straight lantern-fire projectile (Ember Shot / Charged Flare). Bursts on whatever it hits.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FireBolt : MonoBehaviour
{
    public GameObject hitPrefab;
    public ParticleSystem trail;
    public float maxLifetime = 1.6f;
    [Tooltip("Hits dealt to shootable things (charged flare = 2).")]
    public int damage = 1;

    Rigidbody2D _rb;
    float _age;
    bool _done;

    public void Launch(Vector2 velocity, Collider2D ignore)
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.linearVelocity = velocity;
        var own = GetComponent<Collider2D>();
        if (ignore && own) Physics2D.IgnoreCollision(own, ignore);
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr && velocity.x < 0f) sr.flipX = true;
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age >= maxLifetime) Burst(transform.position);
    }

    void OnCollisionEnter2D(Collision2D c) =>
        Burst(c.contactCount > 0 ? (Vector3)c.GetContact(0).point : transform.position);

    // Shootable things (cracked trees) use trigger colliders so they never block the player.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (_done) return;
        var enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy)
        {
            if (enemy.IsDead) return;
            enemy.Hit(damage, DamageKind.Fire, transform.position - (Vector3)(_rb ? _rb.linearVelocity.normalized : Vector2.zero));
            Burst(transform.position);
            return;
        }
        var tree = other.GetComponentInParent<TreeTopple>();
        if (tree == null || tree.trigger != TreeTopple.Trigger.Shot || tree.IsDown) return;
        tree.Hit(damage, transform.position);
        Burst(transform.position);
    }

    void Burst(Vector3 at)
    {
        if (_done) return;
        _done = true;
        if (hitPrefab) Instantiate(hitPrefab, at, Quaternion.identity);
        if (trail)
        {
            trail.transform.SetParent(null, true);
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(trail.gameObject, 1.2f);
        }
        Destroy(gameObject);
    }
}
