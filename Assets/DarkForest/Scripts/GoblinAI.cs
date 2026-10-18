using UnityEngine;

/// <summary>
/// Goblin bomber: holds a ledge and lobs fused bombs on a real arc to where the boy is heading.
/// Weak up close (no melee) - the answer is to close the distance or shoot it from below.
/// Uses the 12-frame Attack3 sheet: 0-5 idle sway, 6-11 wind-up and throw.
/// </summary>
public class GoblinAI : EnemyBrain
{
    public Sprite[] attackFrames;
    public GameObject bombPrefab;
    public float aggroRange = 13f;
    public float throwInterval = 2.6f;
    public float flightTime = 1.05f;
    public float lead = 0.45f;               // aims where the boy will be, not where he is
    public Vector2 hand = new Vector2(0.35f, 1.5f);

    Sprite[] _idle, _throw;
    float _cooldown;
    bool _throwing;
    bool _released;
    bool _dead;
    Vector3 _lastPlayer;
    Vector2 _playerVel;

    protected override void Start()
    {
        base.Start();
        _idle = attackFrames.Length >= 12 ? attackFrames[..6] : attackFrames;
        _throw = attackFrames.Length >= 12 ? attackFrames[6..] : attackFrames;
        _cooldown = throwInterval * Random.Range(0.4f, 0.8f);
        float g = GroundAt(transform.position.x, transform.position.y);
        if (!float.IsNaN(g)) transform.position = new Vector3(transform.position.x, g, transform.position.z);
        if (Player) _lastPlayer = Player.position;
    }

    void Update()
    {
        if (_dead) return;
        if (Waking) { Play(_idle, 8f); return; }
        if (Player)
        {
            _playerVel = Vector2.Lerp(_playerVel, (Player.position - _lastPlayer) / Mathf.Max(Time.deltaTime, 1e-4f), Time.deltaTime * 4f);
            _lastPlayer = Player.position;
        }
        bool aggro = Player && Mathf.Abs(DistToPlayerX) < aggroRange && Mathf.Abs(DistToPlayerY) < 9f;
        if (aggro && !_throwing) Face(DistToPlayerX);

        _cooldown -= Time.deltaTime;
        if (!_throwing && aggro && _cooldown <= 0f) { _throwing = true; _released = false; Play(_throw, 13f, false); }

        if (_throwing)
        {
            if (!_released && FrameIndex >= 3) { _released = true; Throw(); }
            if (Finished) { _throwing = false; _cooldown = throwInterval; }
        }
        else Play(_idle, 8f);

        ContactDamage();
    }

    void Throw()
    {
        if (!bombPrefab || !Player) return;
        var from = transform.position + new Vector3(hand.x * Facing, hand.y, 0f);
        var bomb = Instantiate(bombPrefab, from, Quaternion.identity);
        var rb = bomb.GetComponent<Rigidbody2D>();
        if (!rb) return;
        Vector2 target = (Vector2)Player.position + new Vector2(_playerVel.x * lead, 0.3f);
        float g = Physics2D.gravity.y * rb.gravityScale;
        float t = flightTime;
        var v = new Vector2((target.x - from.x) / t, (target.y - from.y - 0.5f * g * t * t) / t);
        rb.linearVelocity = v;
        rb.angularVelocity = -Facing * 360f;
    }

    public override void OnHurt(Vector2 from, DamageKind kind)
    {
        if (_dead) return;
        _throwing = false;
        _cooldown = Mathf.Max(_cooldown, 0.7f);    // a hit interrupts the throw
        Face(from.x - transform.position.x);
    }

    public override void OnDeath(DamageKind kind)
    {
        _dead = true;
        Destroy(gameObject, 1.4f);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_dead && body)
        {
            // Topples backwards and fades.
            body.transform.localRotation = Quaternion.RotateTowards(body.transform.localRotation, Quaternion.Euler(0f, 0f, Facing * 80f), 300f * Time.deltaTime);
            var c = body.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.9f);
            body.color = c;
            if (rim) rim.enabled = false;
        }
    }
}
