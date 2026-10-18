using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ember Rat (the GDD's "Ember Hound" role): charcoal rat with a burning tail.
/// Patrols its stretch; when the boy is in range it telegraphs (0.35 s crouch, sparks, eyes flare),
/// rushes ~4 tiles, then rests 0.6 s - the window to shoot it and call the sister.
/// Never walks off ledges; stays inside its patrol bounds.
/// </summary>
public class EmberRatAI : EnemyBrain
{
    public Sprite[] idleFrames, runFrames, attackFrames, hurtFrames, deathFrames;
    public float patrolMin, patrolMax;
    public float walkSpeed = 1.4f;
    public float rushSpeed = 11f;
    public float rushDistance = 4.2f;
    public float telegraphTime = 0.35f;
    public float restTime = 0.6f;
    public float aggroRange = 12f;
    [Tooltip("Inside this distance it lunges; farther away (but seen) it runs after the boy.")]
    public float lungeRange = 5f;
    public float chaseSpeed = 3.6f;
    public ParticleSystem tailFire;
    public ParticleSystem telegraphSparks;
    public Light2D emberLight;
    public SpriteRenderer eyeGlow;

    enum State { Patrol, Chase, Telegraph, Rush, Rest, Stagger, Flee, Dead }
    State _state;
    float _timer;
    float _rushLeft;
    float _lightBase;
    float _eyeBase;
    int _patrolDir = 1;
    float _turnWait;

    protected override void Start()
    {
        base.Start();
        if (emberLight) _lightBase = emberLight.intensity;
        if (eyeGlow) _eyeBase = eyeGlow.color.a;
        _patrolDir = Random.value < 0.5f ? -1 : 1;
        SnapToGround();
    }

    void SnapToGround()
    {
        float g = GroundAt(transform.position.x, transform.position.y);
        if (!float.IsNaN(g)) transform.position = new Vector3(transform.position.x, g, transform.position.z);
    }

    void Move(float dx)
    {
        if (Mathf.Abs(dx) < 1e-5f) return;
        float dir = Mathf.Sign(dx);
        var p = transform.position;
        float nx = p.x + dx;
        bool ok = nx >= patrolMin && nx <= patrolMax
                  && !float.IsNaN(GroundAt(nx + dir * 0.5f, p.y, 1.5f))      // no ledge ahead
                  && !WallBetween(p.x, nx + dir * 0.5f, p.y);                 // no wall in the way (arena fire walls, trunks)
        if (!ok) { _rushLeft = 0f; return; }
        float g = GroundAt(nx, transform.position.y, 1.5f);
        transform.position = new Vector3(nx, float.IsNaN(g) ? transform.position.y : g, transform.position.z);
    }

    void Update()
    {
        if (_state == State.Dead) return;
        if (Waking) { Play(idleFrames, 8f); return; }
        float dx = DistToPlayerX;
        bool sees = Player && Mathf.Abs(dx) < aggroRange && Mathf.Abs(DistToPlayerY) < 3f;
        _timer -= Time.deltaTime;

        switch (_state)
        {
            case State.Patrol:
                if (sees) { Face(dx); Enter(Mathf.Abs(dx) <= lungeRange ? State.Telegraph : State.Chase, telegraphTime); break; }
                Face(_patrolDir);
                if (_turnWait > 0f) { _turnWait -= Time.deltaTime; Play(idleFrames, 8f); break; }   // looks around before turning
                Play(runFrames, 7f);
                float before = transform.position.x;
                Move(_patrolDir * walkSpeed * Time.deltaTime);
                if (Mathf.Approximately(before, transform.position.x)) { _patrolDir = -_patrolDir; _turnWait = 0.6f; }
                break;

            case State.Chase:
                // Runs after the boy (ledges and its bounds still stop it); lunges once close.
                if (!sees) { Enter(State.Patrol, 0f); break; }
                Face(dx);
                float cx = transform.position.x;
                Move((Facing * chaseSpeed + Crowd() * 2.5f) * Time.deltaTime);   // a pack fans out instead of stacking
                // Blocked (wall / ledge between us): wait and growl instead of skittering on the spot.
                Play(Mathf.Approximately(cx, transform.position.x) ? idleFrames : runFrames, Mathf.Approximately(cx, transform.position.x) ? 8f : 13f);
                if (Mathf.Abs(dx) <= lungeRange) Enter(State.Telegraph, telegraphTime);
                break;

            case State.Telegraph:
                Play(attackFrames, 2f / telegraphTime, false);
                Move(-Facing * 0.6f * Time.deltaTime);            // little crouch-back before the lunge
                if (_timer <= 0f) { _rushLeft = rushDistance; Enter(State.Rush, 1f); }
                break;

            case State.Rush:
                Play(runFrames, 18f);
                float step = Mathf.Min(_rushLeft, rushSpeed * Time.deltaTime);
                _rushLeft -= step;
                Move(Facing * step);
                if (_rushLeft <= 0.01f || _timer <= 0f) Enter(State.Rest, restTime);
                break;

            case State.Rest:
                Play(idleFrames, 8f);
                Move(Crowd() * 2f * Time.deltaTime);
                if (_timer <= 0f) Enter(!sees ? State.Patrol : Mathf.Abs(dx) <= lungeRange ? State.Telegraph : State.Chase, telegraphTime);
                if (_state == State.Telegraph || _state == State.Chase) Face(dx);
                break;

            case State.Flee:
                // Shot from out of reach: it can't answer from range, so it bolts away from the boy.
                Face(-dx);
                Play(runFrames, 16f);
                Move(Facing * chaseSpeed * 1.25f * Time.deltaTime);
                if (_timer <= 0f) Enter(State.Patrol, 0f);
                break;

            case State.Stagger:
                Play(hurtFrames, 1f);
                if (_timer <= 0f) Enter(State.Rest, 0.25f);
                break;
        }

        // Feedback: tail burns harder and eyes flare while telegraphing / rushing.
        bool hot = _state == State.Telegraph || _state == State.Rush || _state == State.Chase;
        if (emberLight) emberLight.intensity = Mathf.Lerp(emberLight.intensity, _lightBase * (hot ? 2.2f : 1f), Time.deltaTime * 10f);
        if (eyeGlow) { var c = eyeGlow.color; c.a = Mathf.Lerp(c.a, hot ? 1f : _eyeBase, Time.deltaTime * 12f); eyeGlow.color = c; }
        if (tailFire) { var em = tailFire.emission; em.rateOverTimeMultiplier = hot ? 60f : 22f; }

        ContactDamage();
    }

    void Enter(State s, float time)
    {
        _state = s;
        _timer = time;
        if (s == State.Telegraph && telegraphSparks) telegraphSparks.Play();
    }

    public override void OnHurt(Vector2 from, DamageKind kind)
    {
        if (_state == State.Dead) return;
        Face(from.x - transform.position.x);                 // turn toward the attacker
        Move(-Facing * 0.5f);                               // knocked back a little
        if (Mathf.Abs(DistToPlayerX) > lungeRange + 1.5f)
        {
            Say("!!", new Color(1f, 0.35f, 0.25f), 1f, 1.3f);
            Enter(State.Flee, 1.5f);
            return;
        }
        Enter(State.Stagger, 0.22f);
    }

    public override void OnDeath(DamageKind kind)
    {
        _state = State.Dead;
        Play(deathFrames, 10f, false);
        if (tailFire) tailFire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (eyeGlow) eyeGlow.enabled = false;
        if (emberLight) emberLight.enabled = false;
        Destroy(gameObject, 1.6f);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_state == State.Dead && body)
        {
            var c = body.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.9f);
            body.color = c;
            if (rim) rim.enabled = false;
        }
        if (eyeGlow && body) eyeGlow.transform.localPosition = new Vector3(Mathf.Abs(eyeGlow.transform.localPosition.x) * Facing, eyeGlow.transform.localPosition.y, 0f);
        if (tailFire) tailFire.transform.localPosition = new Vector3(-Mathf.Abs(tailFire.transform.localPosition.x) * Facing, tailFire.transform.localPosition.y, 0f);
    }
}
