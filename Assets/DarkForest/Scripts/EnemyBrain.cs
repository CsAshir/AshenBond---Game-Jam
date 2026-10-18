using UnityEngine;

/// <summary>
/// Shared enemy plumbing: finds the boy, flips to face him, plays flipbooks, snaps walkers to the ground,
/// deals contact damage and keeps an outline sprite in sync (readable silhouettes on dark backgrounds).
/// </summary>
public abstract class EnemyBrain : MonoBehaviour
{
    public SpriteRenderer body;
    public SpriteRenderer rim;              // copy of the body, slightly larger, additive: the enemy outline
    public Collider2D hurtBox;              // trigger: touching it costs the boy a heart
    public int contactDamage = 1;
    [Tooltip("Seconds after appearing (arena spawn) before it may act or hurt: the spawn effect is the warning.")]
    public float spawnGrace = 0.9f;
    [Tooltip("Art faces right by default.")]
    public bool artFacesRight = true;

    protected Transform Player;
    protected PlayerHealth PlayerHp;
    protected EnemyHealth Health;
    protected int Facing = 1;
    float _wakeAt;
    protected bool Waking => Time.time < _wakeAt;

    /// <summary>Hold still for a moment (after a rewind, so nobody strikes the instant time resumes).</summary>
    public void Grace(float seconds) => _wakeAt = Mathf.Max(_wakeAt, Time.time + seconds);

    protected virtual void OnEnable()
    {
        _wakeAt = Time.time + spawnGrace;
        if (IsWalker && !Walkers.Contains(this)) Walkers.Add(this);
    }

    Sprite[] _frames;
    float _fps;
    float _animT;
    bool _loop;

    protected virtual void Awake()
    {
        Health = GetComponent<EnemyHealth>();
    }

    protected virtual void Start()
    {
        var p = GameObject.Find("Player");
        if (p) { Player = p.transform; PlayerHp = p.GetComponent<PlayerHealth>(); }
    }

    public virtual void OnHurt(Vector2 from, DamageKind kind) { }

    public virtual void OnDeath(DamageKind kind) { }

    protected void Play(Sprite[] frames, float fps, bool loop = true)
    {
        if (frames == null || frames.Length == 0) return;
        if (_frames != frames) { _frames = frames; _animT = 0f; }
        _fps = fps;
        _loop = loop;
    }

    protected bool Finished => _frames != null && !_loop && _animT * _fps >= _frames.Length;
    protected int FrameIndex => _frames == null ? 0 : (int)(_animT * _fps);

    protected void Face(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return;
        Facing = dir > 0f ? 1 : -1;
    }

    protected float DistToPlayerX => Player ? Player.position.x - transform.position.x : 999f;
    protected float DistToPlayerY => Player ? Player.position.y - transform.position.y : 999f;

    [Tooltip("Highest ledge a walker may step up onto. Anything taller (arena walls, trunks) is a wall, not a stair.")]
    public float maxStepUp = 0.9f;

    /// <summary>
    /// Highest solid ground under x (ignores triggers, the player and this enemy). NaN when there is none, or when
    /// the first solid thing is taller than a step: walkers used to climb the arena fire wall a little each frame
    /// and float off into the sky.
    /// </summary>
    protected float GroundAt(float x, float fromY, float depth = 4f)
    {
        foreach (var hit in Physics2D.RaycastAll(new Vector2(x, fromY + 1.2f), Vector2.down, depth + 1.2f))
        {
            var c = hit.collider;
            if (c.isTrigger || c.transform.IsChildOf(transform) || c.GetComponentInParent<PlayerController2D>() || c.GetComponentInParent<EnemyBrain>()) continue;
            return hit.point.y > fromY + maxStepUp ? float.NaN : hit.point.y;
        }
        return float.NaN;
    }

    /// <summary>Solid wall anywhere between x0 and x1 at chest height (swept, so a fast lunge cannot tunnel through).</summary>
    protected bool WallBetween(float x0, float x1, float footY)
    {
        float d = x1 - x0;
        if (Mathf.Abs(d) < 1e-4f) return false;
        foreach (var hit in Physics2D.RaycastAll(new Vector2(x0, footY + maxStepUp + 0.15f), new Vector2(Mathf.Sign(d), 0f), Mathf.Abs(d)))
        {
            var c = hit.collider;
            if (c.isTrigger || c.transform.IsChildOf(transform) || c.GetComponentInParent<PlayerController2D>() || c.GetComponentInParent<EnemyBrain>()) continue;
            return true;
        }
        return false;
    }

    static readonly System.Collections.Generic.List<EnemyBrain> Walkers = new();
    protected virtual bool IsWalker => true;
    protected virtual void OnDisable() => Walkers.Remove(this);

    /// <summary>Sideways push away from other walkers standing on top of this one, so a pack spreads instead of stacking.</summary>
    protected float Crowd(float radius = 1.1f)
    {
        float push = 0f;
        foreach (var o in Walkers)
        {
            if (o == this || !o || o.Health && o.Health.IsDead) continue;
            float d = transform.position.x - o.transform.position.x;
            if (Mathf.Abs(d) >= radius || Mathf.Abs(transform.position.y - o.transform.position.y) > 1.5f) continue;
            push += (d == 0f ? (GetInstanceID() > o.GetInstanceID() ? 1f : -1f) : Mathf.Sign(d)) * (1f - Mathf.Abs(d) / radius);
        }
        return Mathf.Clamp(push, -1f, 1f);
    }

    protected void ContactDamage()
    {
        if (Waking || TimeRewind.Rewinding || !hurtBox || !hurtBox.enabled || contactDamage <= 0 || !PlayerHp || Health && Health.IsDead) return;
        var b = hurtBox.bounds;
        foreach (var c in Physics2D.OverlapBoxAll(b.center, b.size, 0f))
            if (c.GetComponentInParent<PlayerController2D>()) { PlayerHp.Damage(contactDamage, transform.position); break; }
    }

    Color _rimBase;
    bool _rimInit;
    float _flash;
    Color _flashColor;

    /// <summary>Whole silhouette flashes (the additive outline turns solid and bright for a moment).</summary>
    public void Flash(Color color)
    {
        _flash = 1f;
        _flashColor = color;
    }

    // ------------------------------------------------------------------ reactions: "!" when it spots the boy, taunts

    static readonly string[] Taunts =
    {
        "Run, little ember...",
        "Your light won't save you.",
        "The fire took everything. You're next.",
        "I smell guilt on you, boy.",
        "That lantern is all you have left.",
        "The clock will never move again.",
        "She can't protect you forever.",
        "Burn with the village!",
        "Ashes. All of it, ashes.",
        "Turn back, Ilyan.",
    };
    static float _nextTaunt;
    bool _spotted;

    /// <summary>Show a line above this enemy (alerts, taunts).</summary>
    public void Say(string text, Color color, float life = 1.4f, float size = 1f)
    {
        float top = hurtBox ? hurtBox.bounds.max.y - transform.position.y : 1.5f;
        FloatingText.Show(transform, new Vector3(0f, top + 0.35f, 0f), text, color, life, size);
    }

    /// <summary>Bosses and summons have their own staging: no "!" / taunts.</summary>
    protected virtual bool Reacts => true;

    void Spot()
    {
        if (!Reacts || !Player || Health && Health.IsDead || Waking) return;
        float dx = Mathf.Abs(DistToPlayerX), dy = Mathf.Abs(DistToPlayerY);
        if (!_spotted && dx < 11f && dy < 4f)
        {
            _spotted = true;
            if (Time.time > _nextTaunt && Random.value < 0.45f)
            {
                _nextTaunt = Time.time + 7f;
                Say(Taunts[Random.Range(0, Taunts.Length)], new Color(1f, 0.88f, 0.8f), 2.8f, 1f);
            }
            else Say("!", new Color(1f, 0.85f, 0.3f), 0.9f, 1.2f);
        }
        else if (_spotted && dx > 18f) _spotted = false;
    }

    protected virtual void LateUpdate()
    {
        Spot();
        if (rim)
        {
            if (!_rimInit) { _rimBase = rim.color; _rimInit = true; }
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 9f);
            rim.color = Color.Lerp(_rimBase, _flashColor, _flash);
            rim.transform.localScale = Vector3.one * (1.09f + 0.12f * _flash);
        }
        if (_frames != null && body)
        {
            _animT += Time.deltaTime;
            int i = (int)(_animT * _fps);
            body.sprite = _frames[_loop ? i % _frames.Length : Mathf.Min(i, _frames.Length - 1)];
        }
        if (body) body.flipX = artFacesRight ? Facing < 0 : Facing > 0;
        if (rim && body)
        {
            rim.sprite = body.sprite;
            rim.flipX = body.flipX;
            rim.enabled = body.enabled;
        }
    }
}
