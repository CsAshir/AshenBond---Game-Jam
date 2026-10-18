using UnityEngine;

/// <summary>
/// Hollow Skeleton (Area B, shadow theme). Its raised shield blocks lantern fire from the front; it only opens up
/// while its spinning sword is out (thrown like a boomerang). The sister's shadow passes through shields, so
/// the answer is: dodge the blade and shoot the open skeleton, or call the shadow at any time.
/// Sheet (6): 0-1 guard sway, 2 shield brace, 3-5 wind-up and throw.
/// </summary>
public class SkeletonAI : EnemyBrain
{
    public Sprite[] attackFrames;
    public GameObject swordPrefab;
    public float aggroRange = 12f;
    public float keepDistance = 6f;
    public float walkSpeed = 1.6f;
    public float throwInterval = 3f;
    public float throwRange = 9f;
    public float patrolMin = -999f, patrolMax = 999f;

    Sprite[] _guard, _brace, _throw;
    float _cooldown;
    float _braceTime;
    bool _throwing;
    bool _released;
    bool _dead;
    SpinningBlade _blade;

    public bool SwordOut => _blade != null;

    protected override void Start()
    {
        base.Start();
        _guard = attackFrames[..2];
        _brace = attackFrames[2..3];
        _throw = attackFrames[3..];
        _cooldown = throwInterval * 0.6f;
        Snap();
        if (Health) Health.blocks = Blocks;
    }

    void Snap()
    {
        float g = GroundAt(transform.position.x, transform.position.y);
        if (!float.IsNaN(g)) transform.position = new Vector3(transform.position.x, g, transform.position.z);
    }

    bool Blocks(DamageKind kind, Vector2 from)
    {
        if (kind != DamageKind.Fire || SwordOut || _throwing || _dead) return false;
        bool front = Mathf.Sign(from.x - transform.position.x) == Facing;
        if (front) _braceTime = 0.35f;
        return front;
    }

    void Update()
    {
        if (_dead) return;
        if (Waking) { Play(_guard, 3f); return; }
        float dx = DistToPlayerX;
        bool aggro = Player && Mathf.Abs(dx) < aggroRange && Mathf.Abs(DistToPlayerY) < 4f;
        _cooldown -= Time.deltaTime;
        _braceTime -= Time.deltaTime;

        if (_throwing)
        {
            if (!_released && FrameIndex >= 2) { _released = true; Throw(); }
            if (Finished) _throwing = false;
        }
        else if (aggro)
        {
            Face(dx);
            // Keeps its distance: steps in when far, backs off when crowded (shield always toward the boy).
            float want = Mathf.Abs(dx) - keepDistance;
            if (Mathf.Abs(want) > 0.8f && !SwordOut) Step(Mathf.Sign(dx) * Mathf.Sign(want) * walkSpeed * Time.deltaTime);
            else if (!SwordOut) Step(Crowd() * walkSpeed * Time.deltaTime);
            if (_cooldown <= 0f && !SwordOut && Mathf.Abs(dx) < throwRange)
            {
                _throwing = true;
                _released = false;
                Play(_throw, 9f, false);
            }
        }

        if (!_throwing) Play(_braceTime > 0f ? _brace : _guard, _braceTime > 0f ? 1f : 3f);
        ContactDamage();
    }

    void Step(float d)
    {
        float nx = Mathf.Clamp(transform.position.x + d, patrolMin, patrolMax);
        float g = GroundAt(nx + Mathf.Sign(d) * 0.6f, transform.position.y, 1.5f);
        if (float.IsNaN(g) || WallBetween(transform.position.x, nx + Mathf.Sign(d) * 0.6f, transform.position.y)) return;   // no ledges, no walls
        float y = GroundAt(nx, transform.position.y, 1.5f);
        transform.position = new Vector3(nx, float.IsNaN(y) ? transform.position.y : y, transform.position.z);
    }

    void Throw()
    {
        _cooldown = throwInterval;
        if (!swordPrefab) return;
        var go = Instantiate(swordPrefab, transform.position + new Vector3(Facing * 0.6f, 1.2f, 0f), Quaternion.identity);
        _blade = go.GetComponent<SpinningBlade>();
        if (_blade) _blade.Launch(this.transform, Facing);
    }

    public override void OnHurt(Vector2 from, DamageKind kind)
    {
        if (_dead) return;
        _throwing = false;
        Face(from.x - transform.position.x);
    }

    public override void OnDeath(DamageKind kind)
    {
        _dead = true;
        if (_blade) Destroy(_blade.gameObject);
        Destroy(gameObject, 1.4f);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_dead && body)
        {
            // Collapses into the ground as its shadow leaves it.
            body.transform.localPosition += Vector3.down * Time.deltaTime * 0.8f;
            var c = body.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.9f);
            body.color = c;
            if (rim) rim.enabled = false;
        }
    }
}
