using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fused bomb: sparks while it flies and bounces, blinks faster before it goes off, then explodes
/// (flash + blast frames + shake). The blast hurts the boy AND any enemy caught in it.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GoblinBomb : MonoBehaviour
{
    public Sprite[] frames;                  // 0-2 fuse, 3-8 ball, 9-11 flash, 12-18 blast
    public SpriteRenderer art;
    public float fuse = 1.5f;
    public float radius = 1.6f;
    public Light2D fuseLight;
    public Light2D blastLight;
    public GameObject blastFx;
    public ParticleSystem sparks;

    float _t;
    bool _exploded;
    float _blastT;
    bool _landed;

    void Update()
    {
        _t += Time.deltaTime;
        if (_exploded)
        {
            _blastT += Time.deltaTime;
            int i = 9 + (int)(_blastT * 16f);
            if (i >= frames.Length) { Destroy(gameObject); return; }
            art.sprite = frames[i];
            if (blastLight) blastLight.intensity = Mathf.Max(0f, 4f * (1f - _blastT * 1.8f));
            return;
        }

        // Fuse: blink between the lit and dark ball, faster as it runs out.
        float left = fuse - _t;
        float rate = Mathf.Lerp(16f, 4f, Mathf.Clamp01(left / fuse));
        bool lit = Mathf.Repeat(_t * rate, 1f) < 0.5f;
        art.sprite = frames[lit ? (int)(_t * 12f) % 3 : 3 + (int)(_t * 8f) % 6];
        if (fuseLight) fuseLight.intensity = lit ? 1.4f : 0.4f;
        if (left <= 0f) Explode();
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (c.collider.GetComponentInParent<PlayerController2D>()) { Explode(); return; }
        if (!_landed) { _landed = true; _t = Mathf.Max(_t, fuse - 0.45f); }   // first bounce: short fuse
    }

    void Explode()
    {
        if (_exploded) return;
        _exploded = true;
        _blastT = 0f;
        var rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        art.transform.rotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;
        art.transform.localScale *= 1.6f;
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        if (sparks) sparks.Stop();
        if (fuseLight) fuseLight.enabled = false;
        if (blastFx) Instantiate(blastFx, transform.position, Quaternion.identity);
        CameraFollow2D.Shake(0.3f, 0.22f);

        var done = new System.Collections.Generic.HashSet<Object>();
        foreach (var hit in Physics2D.OverlapCircleAll(transform.position, radius))
        {
            var pc = hit.GetComponentInParent<PlayerController2D>();
            if (pc) { var hp = pc.GetComponent<PlayerHealth>(); if (hp) hp.Damage(1, transform.position); continue; }
            var eh = hit.GetComponentInParent<EnemyHealth>();
            if (eh && done.Add(eh) && !eh.GetComponent<GoblinAI>()) eh.Hit(2, DamageKind.Blast, transform.position);
        }
    }
}
