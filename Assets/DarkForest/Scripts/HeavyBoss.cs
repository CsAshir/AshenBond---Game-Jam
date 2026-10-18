using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Configurable heavy boss (placeholder bosses until the final ones are chosen).
///   Area B - The Executioner: drifts after the boy, scythe sweep up close, summons shadow wisps, bursts shadow
///            spikes around itself; phase 2 adds shadow pillars under the boy.
///   Area C - The Horned Beast: lumbers forward, hops and slams (shockwaves run along the floor: jump them),
///            charges and stuns itself on the arena wall; phase 2 adds a time-lock ring of frozen bolts.
/// Each attack is telegraphed (glare flare + a short wind-up) so it reads before it lands.
/// </summary>
public class HeavyBoss : EnemyBrain
{
    protected override bool Reacts => false;

    public enum Move { Melee, Summon, Spikes, Slam, Charge, Pillars, TimeLock }
    public enum Intro { RiseFromShadow, DropFromAbove }

    [Header("Frames")]
    public Sprite[] idleFrames;
    public Sprite[] walkFrames;
    public Sprite[] meleeFrames;
    public Vector2Int meleeHitFrames = new Vector2Int(1, 3);
    public Sprite[] spikeFrames;
    public int spikeHitFrame = 2;
    public Sprite[] summonFrames;

    [Header("Moves")]
    public Intro intro;
    [Tooltip("Body art offset (art with empty space under the feet).")]
    public Vector3 bodyOffset;
    public Move[] phase1 = { Move.Melee };
    public Move[] phase2 = { Move.Melee };
    public float walkSpeed = 1.8f;
    public float chargeSpeed = 11f;
    public float meleeRange = 3.2f;
    public Vector2 meleeBox = new Vector2(3.4f, 3.2f);
    public float spikeRadius = 2.8f;
    public float arenaMin, arenaMax;

    [Header("Spawns")]
    public GameObject minionPrefab;
    public int minions = 3;
    public GameObject wavePrefab;
    public GameObject pillarWarnFx, pillarFx;
    public GameObject timeShotPrefab;
    public GameObject ringFx;
    public GameObject slamFx;
    public Light2D glare;
    public ParticleSystem aura;

    enum State { Intro, Walk, WindUp, Melee, Summon, Spikes, Hop, Charge, Stunned, Pillars, TimeLock, Dead }
    State _state;
    Move _pending;
    float _t;
    int _next;
    bool _done;
    bool _phase2;
    int _count;
    float _hopY;

    protected override void Start()
    {
        base.Start();
        float g = GroundAt(transform.position.x, transform.position.y + 6f, 12f);
        if (!float.IsNaN(g)) transform.position = new Vector3(transform.position.x, g, transform.position.z);
        Enter(State.Intro, 1.8f);
    }

    void Enter(State s, float t) { _state = s; _t = t; _done = false; }

    void Update()
    {
        if (_state == State.Dead || TimeRewind.Rewinding) return;
        _t -= Time.deltaTime;
        if (!_phase2 && Health && Health.Hp <= Health.maxHp / 2)
        {
            _phase2 = true;
            CameraFollow2D.Shake(0.4f, 0.5f);
            if (ringFx) Instantiate(ringFx, transform.position + Vector3.up * 2f, Quaternion.identity);
        }
        float k = _phase2 ? 1.25f : 1f;
        float dx = DistToPlayerX;
        if (glare) glare.intensity = Mathf.Lerp(glare.intensity, _state == State.WindUp ? 4f : 0.7f, Time.deltaTime * 10f);

        switch (_state)
        {
            case State.Intro:
                Play(idleFrames, 6f);
                float p = Mathf.Clamp01(_t / 1.8f);
                if (body)
                    body.transform.localPosition = bodyOffset + (intro == Intro.RiseFromShadow ? new Vector3(0f, -5f * p, 0f) : new Vector3(0f, 14f * p * p, 0f));
                if (_t <= 0f)
                {
                    if (body) body.transform.localPosition = bodyOffset;
                    if (intro == Intro.DropFromAbove) Land(1f);
                    Enter(State.Walk, 1f);
                }
                return;

            case State.Walk:
                Face(dx);
                bool far = Mathf.Abs(dx) > meleeRange * 0.8f;
                Play(far && walkFrames != null && walkFrames.Length > 0 ? walkFrames : idleFrames, 10f);
                if (far) Slide(Mathf.Sign(dx) * walkSpeed * k * Time.deltaTime);
                if (_t <= 0f) Choose(Mathf.Abs(dx));
                break;

            case State.WindUp:
                // Telegraph: freeze, glare, a little shiver.
                Play(idleFrames, 2f);
                if (body) body.transform.localPosition = bodyOffset + new Vector3(Mathf.Sin(Time.time * 60f) * 0.04f, 0f, 0f);
                if (_t <= 0f) { if (body) body.transform.localPosition = bodyOffset; Begin(_pending); }
                break;

            case State.Melee:
                Play(meleeFrames, 12f * k, false);
                if (FrameIndex >= meleeHitFrames.x && FrameIndex <= meleeHitFrames.y)
                    HitBox(transform.position + new Vector3(Facing * meleeBox.x * 0.5f, meleeBox.y * 0.5f, 0f), meleeBox);
                if (Finished) Enter(State.Walk, 1.1f / k);
                break;

            case State.Spikes:
                Play(spikeFrames, 10f, false);
                if (!_done && FrameIndex >= spikeHitFrame)
                {
                    _done = true;
                    CameraFollow2D.Shake(0.25f, 0.2f);
                    if (pillarFx) Instantiate(pillarFx, transform.position + Vector3.up * 1.5f, Quaternion.identity);
                    HitCircle(transform.position + Vector3.up * 1.6f, spikeRadius);
                }
                if (Finished) Enter(State.Walk, 1.2f / k);
                break;

            case State.Summon:
                Play(summonFrames, 8f, false);
                if (!_done && FrameIndex >= 2)
                {
                    _done = true;
                    int n = minions + (_phase2 ? 1 : 0);
                    for (int i = 0; i < n && minionPrefab; i++)
                    {
                        var at = transform.position + new Vector3((i - (n - 1) * 0.5f) * 1.8f, 3.2f + (i % 2) * 1.2f, 0f);
                        Instantiate(minionPrefab, at, Quaternion.identity);
                    }
                }
                if (Finished && _t <= 0f) Enter(State.Walk, 1.6f / k);
                break;

            case State.Hop:
                // Leaps toward the boy and slams down: shockwaves both ways.
                Play(meleeFrames, meleeFrames.Length / 0.7f, false);
                float h = 1f - Mathf.Clamp01(_t / 0.7f);
                if (body) body.transform.localPosition = bodyOffset + new Vector3(0f, Mathf.Sin(h * Mathf.PI) * 3.2f, 0f);
                Slide(Facing * 5f * Time.deltaTime);
                if (_t <= 0f) { if (body) body.transform.localPosition = bodyOffset; Land(1f); if (--_count > 0 && _phase2) { Enter(State.Hop, 0.7f); Play(idleFrames, 1f); } else Enter(State.Walk, 1.3f / k); }
                break;

            case State.Charge:
                Play(walkFrames, 20f);
                float nx = transform.position.x + Facing * chargeSpeed * k * Time.deltaTime;
                if (nx <= arenaMin + 1f || nx >= arenaMax - 1f || _t <= 0f)
                {
                    transform.position = new Vector3(Mathf.Clamp(nx, arenaMin + 1f, arenaMax - 1f), transform.position.y, transform.position.z);
                    CameraFollow2D.Shake(0.45f, 0.35f);
                    if (slamFx) Instantiate(slamFx, transform.position + new Vector3(Facing * 1.5f, 1.5f, 0f), Quaternion.identity);
                    Enter(State.Stunned, 1.6f);
                    break;
                }
                transform.position = new Vector3(nx, transform.position.y, transform.position.z);
                break;

            case State.Stunned:
                Play(idleFrames, 1f);
                if (body) body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 6f) * 7f);
                if (_t <= 0f) { if (body) body.transform.localRotation = Quaternion.identity; Enter(State.Walk, 0.8f); }
                break;

            case State.Pillars:
                Play(summonFrames != null && summonFrames.Length > 0 ? summonFrames[..1] : idleFrames, 1f);
                if (_t <= 0f)
                {
                    if (_count-- <= 0) { Enter(State.Walk, 1.2f); break; }
                    StartCoroutine(Pillar(Player ? Player.position.x : transform.position.x, transform.position.y));
                    _t = 0.7f;
                }
                break;

            case State.TimeLock:
                Play(idleFrames, 3f);
                if (!_done)
                {
                    _done = true;
                    if (ringFx) Instantiate(ringFx, transform.position + Vector3.up * 2.5f, Quaternion.identity);
                    int n = 10;
                    for (int i = 0; i < n && timeShotPrefab; i++)
                    {
                        float a = i * Mathf.PI * 2f / n;
                        var s = Instantiate(timeShotPrefab, transform.position + new Vector3(Mathf.Cos(a) * 3.5f, 2.5f + Mathf.Sin(a) * 3.5f, 0f), Quaternion.identity);
                        var es = s.GetComponent<EnemyShot>();
                        if (es) { es.hold = 1.1f + i * 0.06f; es.Launch(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.6f); }
                    }
                }
                if (_t <= 0f) Enter(State.Walk, 1f);
                break;
        }
        ContactDamage();
    }

    void Choose(float dist)
    {
        var moves = _phase2 ? phase2 : phase1;
        Move m = moves[_next++ % moves.Length];
        if (m == Move.Melee && dist > meleeRange * 1.6f) m = moves[_next++ % moves.Length];   // too far to swing: next move
        _pending = m;
        Enter(State.WindUp, m == Move.Melee ? 0.35f : 0.6f);
    }

    void Begin(Move m)
    {
        Face(DistToPlayerX);
        switch (m)
        {
            case Move.Melee: Enter(State.Melee, 0f); break;
            case Move.Summon: Enter(State.Summon, 0.8f); break;
            case Move.Spikes: Enter(State.Spikes, 0f); break;
            case Move.Slam: _count = 2; Enter(State.Hop, 0.7f); break;
            case Move.Charge: Enter(State.Charge, 2.5f); break;
            case Move.Pillars: _count = 3; Enter(State.Pillars, 0.2f); break;
            case Move.TimeLock: Enter(State.TimeLock, 2.6f); break;
        }
    }

    void Land(float strength)
    {
        CameraFollow2D.Shake(0.4f * strength, 0.3f);
        if (slamFx) Instantiate(slamFx, transform.position + Vector3.up * 0.4f, Quaternion.identity);
        HitCircle(transform.position + Vector3.up, 1.8f);
        if (!wavePrefab) return;
        foreach (var dir in new[] { -1f, 1f })
        {
            var w = Instantiate(wavePrefab, transform.position + new Vector3(dir * 1.2f, 0f, 0f), Quaternion.identity);
            var sw = w.GetComponent<ShockWave>();
            if (sw) { sw.direction = dir; sw.minX = arenaMin; sw.maxX = arenaMax; if (_phase2) sw.speed *= 1.25f; }
        }
    }

    void Slide(float d)
    {
        float nx = Mathf.Clamp(transform.position.x + d, arenaMin + 1f, arenaMax - 1f);
        transform.position = new Vector3(nx, transform.position.y, transform.position.z);
    }

    void HitBox(Vector3 center, Vector2 size)
    {
        foreach (var c in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            var hp = c.GetComponentInParent<PlayerHealth>();
            if (hp) { hp.Damage(1, transform.position); break; }
        }
    }

    void HitCircle(Vector3 center, float r)
    {
        foreach (var c in Physics2D.OverlapCircleAll(center, r))
        {
            var hp = c.GetComponentInParent<PlayerHealth>();
            if (hp) { hp.Damage(1, transform.position); break; }
        }
    }

    IEnumerator Pillar(float x, float y)
    {
        if (pillarWarnFx) Instantiate(pillarWarnFx, new Vector3(x, y + 0.1f, 0f), Quaternion.identity);
        yield return new WaitForSeconds(0.6f);
        if (pillarFx) Instantiate(pillarFx, new Vector3(x, y + 1.5f, 0f), Quaternion.identity);
        CameraFollow2D.Shake(0.2f, 0.15f);
        HitBox(new Vector3(x, y + 2f, 0f), new Vector2(1.6f, 4f));
    }

    public override void OnHurt(Vector2 from, DamageKind kind) { }

    public override void OnDeath(DamageKind kind)
    {
        _state = State.Dead;
        StopAllCoroutines();
        CameraFollow2D.Shake(0.6f, 0.8f);
        HitStop.Do(0.25f);
        if (aura) aura.Stop();
        if (deathFrames != null && deathFrames.Length > 0) Play(deathFrames, 10f, false);
        if (pillarFx) for (int i = -1; i <= 1; i++) Instantiate(pillarFx, transform.position + new Vector3(i * 1.5f, 1.5f, 0f), Quaternion.identity);
        Destroy(gameObject, 3f);
    }

    public Sprite[] deathFrames;

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_state == State.Dead && body && Finished)
        {
            var c = body.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.8f);
            body.color = c;
            if (rim) rim.enabled = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + new Vector3(Facing * meleeBox.x * 0.5f, meleeBox.y * 0.5f, 0f), meleeBox);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.6f, spikeRadius);
    }
}
