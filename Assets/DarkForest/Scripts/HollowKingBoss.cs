using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Area B boss - The Hollow King: a giant shadow skeleton that rises out of a shadow pool.
///   Twin Blades  - throws two spinning blades (low + high): jump the low one, stay under the high one.
///   Shield Charge- braces (red glare, 0.7 s), then charges across the arena; hitting the wall stuns it 1.4 s
///                  with the shield down: the window for lantern fire.
///   Shadow Rise  - (phase 2) three shadow pillars erupt under the boy, one after another (ground glows first).
/// The shield blocks fire from the front except while stunned / throwing; the sister's shadow always passes.
/// </summary>
public class HollowKingBoss : EnemyBrain
{
    protected override bool Reacts => false;

    public Sprite[] attackFrames;            // skeleton sheet: 0-1 guard, 2 brace, 3-5 throw
    public GameObject bladePrefab;
    public GameObject pillarWarnFx;          // ground glow
    public GameObject pillarFx;              // shadow eruption
    public float arenaMin, arenaMax;
    public float walkSpeed = 1.8f;
    public float chargeSpeed = 12f;
    public Light2D glare;
    public ParticleSystem aura;

    enum State { Rise, Walk, Throw, Brace, Charge, Stunned, Pillars, Dead }
    State _state;
    float _t;
    int _next;
    bool _released;
    int _pillars;
    bool _phase2;
    float _pillarX;
    Sprite[] _guard, _brace, _throw;

    protected override void Start()
    {
        base.Start();
        _guard = attackFrames[..2];
        _brace = attackFrames[2..3];
        _throw = attackFrames[3..];
        if (Health) Health.blocks = Blocks;
        float g = GroundAt(transform.position.x, transform.position.y);
        if (!float.IsNaN(g)) transform.position = new Vector3(transform.position.x, g, transform.position.z);
        Enter(State.Rise, 1.6f);
    }

    bool Blocks(DamageKind kind, Vector2 from)
    {
        if (kind != DamageKind.Fire || _state == State.Stunned || _state == State.Throw || _state == State.Rise) return false;
        return Mathf.Sign(from.x - transform.position.x) == Facing;
    }

    void Enter(State s, float t) { _state = s; _t = t; _released = false; }

    void Update()
    {
        if (_state == State.Dead) return;
        _t -= Time.deltaTime;
        if (!_phase2 && Health && Health.Hp <= Health.maxHp / 2)
        {
            _phase2 = true;
            CameraFollow2D.Shake(0.4f, 0.5f);
            if (pillarFx) Instantiate(pillarFx, transform.position + Vector3.up * 2f, Quaternion.identity);
        }
        float speedK = _phase2 ? 1.3f : 1f;
        float dx = DistToPlayerX;
        if (glare) glare.intensity = Mathf.Lerp(glare.intensity, _state == State.Brace ? 3f : 0.6f, Time.deltaTime * 8f);

        switch (_state)
        {
            case State.Rise:
                // Emerges from the shadow pool: grows up out of the ground.
                Play(_guard, 2f);
                if (body) body.transform.localPosition = new Vector3(0f, -4.5f * Mathf.Clamp01(_t / 1.6f), 0f);
                if (_t <= 0f) { if (body) body.transform.localPosition = Vector3.zero; Enter(State.Walk, 1.2f); }
                return;

            case State.Walk:
                Face(dx);
                Play(_guard, 4f);
                if (Mathf.Abs(dx) > 4f) Slide(Mathf.Sign(dx) * walkSpeed * speedK * Time.deltaTime);
                if (_t <= 0f) NextAttack();
                break;

            case State.Throw:
                Play(_throw, 8f * speedK, false);
                if (!_released && FrameIndex >= 2)
                {
                    _released = true;
                    Blade(0f);
                    Blade(2.2f);
                }
                if (Finished && _t <= 0f) Enter(State.Walk, 1.4f / speedK);
                break;

            case State.Brace:
                Play(_brace, 1f);
                transform.position += new Vector3(Mathf.Sin(Time.time * 70f) * 0.015f, 0f, 0f);
                if (_t <= 0f) Enter(State.Charge, 3f);
                break;

            case State.Charge:
                Play(_brace, 1f);
                float nx = transform.position.x + Facing * chargeSpeed * speedK * Time.deltaTime;
                if (nx <= arenaMin || nx >= arenaMax || _t <= 0f)
                {
                    transform.position = new Vector3(Mathf.Clamp(nx, arenaMin, arenaMax), transform.position.y, transform.position.z);
                    CameraFollow2D.Shake(0.45f, 0.35f);
                    Enter(State.Stunned, 1.4f);
                    break;
                }
                transform.position = new Vector3(nx, transform.position.y, transform.position.z);
                break;

            case State.Stunned:
                // Shield down, staggered: shoot it now.
                Play(_throw[..1], 1f);
                if (body) body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 6f) * 6f);
                if (_t <= 0f) { if (body) body.transform.localRotation = Quaternion.identity; Enter(State.Walk, 0.8f); }
                break;

            case State.Pillars:
                Play(_throw[..1], 1f);
                if (_t <= 0f)
                {
                    if (_pillars-- <= 0) { Enter(State.Walk, 1.2f); break; }
                    _pillarX = Player ? Player.position.x : transform.position.x;
                    StartCoroutine(Pillar(_pillarX, transform.position.y));
                    _t = 0.75f;
                }
                break;
        }
        ContactDamage();
    }

    void NextAttack()
    {
        Face(DistToPlayerX);
        int pick = _next++ % (_phase2 ? 3 : 2);
        if (pick == 0) Enter(State.Throw, 0.8f);
        else if (pick == 1) Enter(State.Brace, _phase2 ? 0.55f : 0.7f);
        else { _pillars = 3; Enter(State.Pillars, 0.2f); }
    }

    void Slide(float d)
    {
        float nx = Mathf.Clamp(transform.position.x + d, arenaMin + 1f, arenaMax - 1f);
        transform.position = new Vector3(nx, transform.position.y, transform.position.z);
    }

    void Blade(float height)
    {
        if (!bladePrefab) return;
        var go = Instantiate(bladePrefab, transform.position + new Vector3(Facing * 1.4f, 1.6f + height, 0f), Quaternion.identity);
        go.transform.localScale *= 1.5f;
        var b = go.GetComponent<SpinningBlade>();
        if (b) { b.distance = 10f; b.hitRadius = 0.8f; b.Launch(transform, Facing); }
    }

    System.Collections.IEnumerator Pillar(float x, float y)
    {
        if (pillarWarnFx) Instantiate(pillarWarnFx, new Vector3(x, y + 0.1f, 0f), Quaternion.identity);
        yield return new WaitForSeconds(0.6f);
        if (pillarFx) Instantiate(pillarFx, new Vector3(x, y + 1.5f, 0f), Quaternion.identity);
        CameraFollow2D.Shake(0.2f, 0.15f);
        foreach (var c in Physics2D.OverlapBoxAll(new Vector2(x, y + 2f), new Vector2(1.6f, 4f), 0f))
        {
            var pc = c.GetComponentInParent<PlayerController2D>();
            if (pc) { var hp = pc.GetComponent<PlayerHealth>(); if (hp) hp.Damage(1, new Vector2(x, y)); break; }
        }
    }

    public override void OnHurt(Vector2 from, DamageKind kind) { }

    public override void OnDeath(DamageKind kind)
    {
        _state = State.Dead;
        StopAllCoroutines();
        CameraFollow2D.Shake(0.6f, 0.8f);
        HitStop.Do(0.25f);
        if (aura) aura.Stop();
        if (pillarFx) for (int i = -1; i <= 1; i++) Instantiate(pillarFx, transform.position + new Vector3(i * 1.5f, 1.5f, 0f), Quaternion.identity);
        Destroy(gameObject, 2.5f);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_state == State.Dead && body)
        {
            body.transform.localPosition += Vector3.down * Time.deltaTime * 1.2f;
            var c = body.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.6f);
            body.color = c;
            if (rim) rim.enabled = false;
        }
    }
}
