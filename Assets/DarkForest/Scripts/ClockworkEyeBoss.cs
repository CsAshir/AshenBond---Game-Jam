using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Area C boss - The Clockwork Eye: the tower's warden of time, a giant eye with a brass gear halo.
///   Fan        - curls up, spits a fan of five bolts.
///   Time Lock  - stops time around itself: a ring of bolts hangs frozen (cyan), the clock ring ticks, then every
///                bolt fires at the boy at once. Move after the freeze ends, not before.
///   Dive       - glare, then a swooping dive across the floor; it lands dazed for 1.5 s - the time to strike.
/// Phase 2 (half health): faster, bigger rings, the halo gear spins fast.
/// </summary>
public class ClockworkEyeBoss : EnemyBrain
{
    protected override bool Reacts => false;

    protected override bool IsWalker => false;

    public Sprite[] flyFrames, curlFrames;
    public GameObject shotPrefab;
    public GameObject timeLockShotPrefab;
    public GameObject clockRingFx;
    public Transform halo;                     // brass gear behind the eye
    public Light2D eyeLight;
    public float arenaMin, arenaMax, floorY, hoverY;

    enum State { Intro, Hover, Fan, TimeLock, DiveAim, Dive, Dazed, Dead }
    State _state;
    float _t;
    int _next;
    bool _phase2;
    bool _fired;
    Vector3 _diveFrom, _diveTo;
    float _diveT;
    float _deadVy;

    protected override void Start()
    {
        base.Start();
        Enter(State.Intro, 2f);
    }

    void Enter(State s, float t) { _state = s; _t = t; _fired = false; }

    void Update()
    {
        if (_state == State.Dead) { Fall(); return; }
        _t -= Time.deltaTime;
        if (!_phase2 && Health && Health.Hp <= Health.maxHp / 2)
        {
            _phase2 = true;
            CameraFollow2D.Shake(0.4f, 0.5f);
            if (clockRingFx) Instantiate(clockRingFx, transform.position, Quaternion.identity).transform.localScale *= 2f;
        }
        float k = _phase2 ? 1.35f : 1f;
        if (halo) halo.Rotate(0f, 0f, (_phase2 ? -160f : -45f) * Time.deltaTime);
        if (Player) Face(DistToPlayerX);
        float bob = Mathf.Sin(Time.time * 2f) * 0.4f;

        switch (_state)
        {
            case State.Intro:
                Play(flyFrames, 10f);
                transform.position = Vector3.Lerp(transform.position, new Vector3((arenaMin + arenaMax) / 2f, hoverY, 0f), Time.deltaTime * 1.5f);
                if (_t <= 0f) Enter(State.Hover, 1.2f);
                break;

            case State.Hover:
                Play(flyFrames, 10f * k);
                float tx = Player ? Mathf.Clamp(Player.position.x + (DistToPlayerX > 0 ? -5f : 5f), arenaMin + 1f, arenaMax - 1f) : transform.position.x;
                transform.position = Vector3.Lerp(transform.position, new Vector3(tx, hoverY + bob, 0f), Time.deltaTime * 1.6f);
                if (_t <= 0f) NextAttack();
                break;

            case State.Fan:
                Play(curlFrames, curlFrames.Length / 0.45f, false);
                if (eyeLight) eyeLight.intensity = 1f + 3f * Mathf.Clamp01(1f - _t / 0.45f);
                if (!_fired && _t <= 0f)
                {
                    _fired = true;
                    int n = _phase2 ? 7 : 5;
                    var aim = Player ? (Player.position + Vector3.up * 1.2f - transform.position).normalized : Vector3.down;
                    float baseA = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                    for (int i = 0; i < n; i++)
                    {
                        float a = (baseA + (i - (n - 1) / 2f) * 14f) * Mathf.Deg2Rad;
                        Shot(shotPrefab, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7f * k, 0f);
                    }
                    CameraFollow2D.Shake(0.15f, 0.15f);
                    Enter(State.Hover, 1.6f / k);
                }
                break;

            case State.TimeLock:
                Play(curlFrames, 6f, false);
                if (!_fired)
                {
                    _fired = true;
                    if (clockRingFx) Instantiate(clockRingFx, transform.position, Quaternion.identity);
                    int n = _phase2 ? 12 : 8;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * Mathf.PI * 2f / n;
                        Shot(timeLockShotPrefab, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, 1.3f + i * 0.06f);
                    }
                }
                if (_t <= 0f) Enter(State.Hover, 1.8f / k);
                break;

            case State.DiveAim:
                Play(curlFrames, 8f, false);
                if (eyeLight) eyeLight.intensity = 1f + 3f * Mathf.Clamp01(1f - _t / 0.7f);
                transform.position += new Vector3(Mathf.Sin(Time.time * 60f) * 0.02f, 0f, 0f);
                if (_t <= 0f)
                {
                    _diveFrom = transform.position;
                    float px = Player ? Player.position.x : transform.position.x;
                    float side = Mathf.Sign(px - transform.position.x);
                    _diveTo = new Vector3(Mathf.Clamp(px + side * 4f, arenaMin + 1f, arenaMax - 1f), floorY + 1.2f, 0f);
                    _diveT = 0f;
                    Enter(State.Dive, 2f);
                }
                break;

            case State.Dive:
                Play(flyFrames, 18f);
                _diveT += Time.deltaTime * 1.4f * k;
                // Swoop: drops on a curve, skims the floor, ends low.
                var p = Vector3.Lerp(_diveFrom, _diveTo, _diveT);
                p.y = Mathf.Lerp(_diveFrom.y, _diveTo.y, Mathf.Sin(Mathf.Clamp01(_diveT) * Mathf.PI * 0.5f));
                transform.position = p;
                if (_diveT >= 1f) { CameraFollow2D.Shake(0.35f, 0.3f); Enter(State.Dazed, 1.5f); }
                break;

            case State.Dazed:
                Play(curlFrames[..1], 1f);
                if (body) body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5f) * 10f);
                if (_t <= 0f) { if (body) body.transform.localRotation = Quaternion.identity; Enter(State.Hover, 0.6f); }
                break;
        }
        if (eyeLight && _state != State.Fan && _state != State.DiveAim) eyeLight.intensity = Mathf.Lerp(eyeLight.intensity, 1f, Time.deltaTime * 4f);
        ContactDamage();
    }

    void NextAttack()
    {
        int pick = _next++ % 3;
        if (pick == 0) Enter(State.Fan, 0.45f);
        else if (pick == 1) Enter(State.TimeLock, 2.4f);
        else Enter(State.DiveAim, 0.7f);
    }

    void Shot(GameObject prefab, Vector2 v, float hold)
    {
        if (!prefab) return;
        var s = Instantiate(prefab, transform.position, Quaternion.identity);
        var es = s.GetComponent<EnemyShot>();
        if (!es) return;
        es.hold = hold;
        es.Launch(v);
    }

    public override void OnHurt(Vector2 from, DamageKind kind) { }

    public override void OnDeath(DamageKind kind)
    {
        _state = State.Dead;
        _deadVy = 2f;
        CameraFollow2D.Shake(0.6f, 0.8f);
        HitStop.Do(0.25f);
        if (clockRingFx) Instantiate(clockRingFx, transform.position, Quaternion.identity).transform.localScale *= 3f;
        Destroy(gameObject, 3f);
    }

    void Fall()
    {
        _deadVy -= 14f * Time.deltaTime;
        if (transform.position.y > floorY + 0.8f) transform.position += new Vector3(0f, _deadVy * Time.deltaTime, 0f);
        if (body) body.transform.Rotate(0f, 0f, 200f * Time.deltaTime);
        if (halo) halo.Rotate(0f, 0f, 400f * Time.deltaTime);
        if (body) { var c = body.color; c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.4f); body.color = c; }
        if (rim) rim.enabled = false;
    }
}
