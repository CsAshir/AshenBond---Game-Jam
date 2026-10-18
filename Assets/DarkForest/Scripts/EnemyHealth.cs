using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum DamageKind { Fire, Shadow, Blast }

/// <summary>
/// Enemy hit points + the GDD combat grammar:
///   1. Fire (lantern shot) damages and EXPOSES the enemy: a cyan marker for 1.2 s.
///   2. A shadow strike (the sister's orb) on an exposed enemy deals triple damage = the finisher,
///      and rewards an extra ember (refund for completing the fire -> shadow combo).
/// </summary>
public class EnemyHealth : MonoBehaviour, IRewindable
{
    public int maxHp = 3;
    public float exposeTime = 1.2f;
    public int exposedShadowMultiplier = 3;

    [Header("Feedback")]
    public SpriteRenderer body;
    public GameObject exposedMarker;        // looping cyan swirl, toggled while exposed
    public GameObject fireHitFx;
    public GameObject shadowHitFx;
    public GameObject deathFx;
    [Tooltip("Soul that flies into the boy's lantern when this enemy dies.")]
    public GameObject soulPrefab;
    public Vector2 fxOffset = new Vector2(0f, 0.7f);

    [Header("Rewards")]
    public GameObject emberDrop;
    public int embers = 1;

    [Tooltip("Optional: return true to block a hit (e.g. a raised shield).")]
    public System.Func<DamageKind, Vector2, bool> blocks;
    public GameObject blockFx;

    public bool IsDead { get; private set; }
    public bool IsExposed => _exposed > 0f;
    public int Hp => _hp;
    public event System.Action<EnemyHealth> Died;

    int _hp;
    float _exposed;
    float _flash;
    float _punch;
    Color _baseColor;
    Vector3 _baseScale;
    EnemyBrain _brain;
    Light2D _markerLight;

    void Awake()
    {
        _hp = maxHp;
        _brain = GetComponent<EnemyBrain>();
        if (body) { _baseColor = body.color; _baseScale = body.transform.localScale; }
        if (exposedMarker)
        {
            _markerLight = exposedMarker.GetComponentInChildren<Light2D>();
            exposedMarker.SetActive(false);
        }
    }

    public void Hit(int damage, DamageKind kind, Vector2 from)
    {
        if (IsDead) return;
        var at = (Vector2)transform.position + fxOffset;
        if (blocks != null && blocks(kind, from))
        {
            if (blockFx) Instantiate(blockFx, at + new Vector2(Mathf.Sign(from.x - transform.position.x) * 0.5f, 0f), Quaternion.identity);
            Sfx.Play("fire_hit", at, 0.5f, 1.5f);
            CameraFollow2D.Shake(0.05f, 0.06f);
            if (_brain) _brain.Flash(new Color(0.8f, 0.9f, 1f, 1f));
            return;
        }
        bool finisher = kind == DamageKind.Shadow && IsExposed;
        if (finisher) damage *= exposedShadowMultiplier;

        _hp -= damage;
        _flash = 1f;
        _punch = 1f;
        CameraFollow2D.Shake(finisher ? 0.22f : 0.07f, finisher ? 0.2f : 0.08f);
        Sfx.Play("fire_hit", at, 1f, kind == DamageKind.Fire ? 1f : 0.7f);
        if (kind == DamageKind.Fire)
        {
            _exposed = exposeTime;
            if (fireHitFx) Instantiate(fireHitFx, at, Quaternion.identity);
        }
        else if (shadowHitFx) Instantiate(shadowHitFx, at, Quaternion.identity);

        if (_brain) { _brain.OnHurt(from, kind); _brain.Flash(Color.white); }
        HitStop.Do(_hp <= 0 || finisher ? 0.09f : 0.045f);   // a beat of weight on every hit, longer on the kill
        if (_hp <= 0) Die(kind, finisher);
    }

    void Die(DamageKind kind, bool finisher)
    {
        IsDead = true;
        _exposed = 0f;
        if (exposedMarker) exposedMarker.SetActive(false);
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        if (deathFx) Instantiate(deathFx, (Vector2)transform.position + fxOffset, Quaternion.identity);
        Sfx.Play("fire_hit", transform.position, 1f, 0.55f);
        if (soulPrefab) Instantiate(soulPrefab, (Vector2)transform.position + fxOffset, Quaternion.identity);
        int drops = embers + (finisher ? 1 : 0);
        for (int i = 0; i < drops && emberDrop; i++)
        {
            var e = Instantiate(emberDrop, (Vector2)transform.position + fxOffset + new Vector2((i - (drops - 1) * 0.5f) * 0.9f, 0.6f), Quaternion.identity);
            e.name = "Ember (drop)";
        }
        Died?.Invoke(this);
        if (_brain) _brain.OnDeath(kind);
        else Destroy(gameObject, 0.1f);
    }

    public object CaptureState() => new Vector2(_hp, _exposed);

    /// <summary>Rewind: wounds taken in the last seconds heal again. Deaths are final (a slain enemy stays slain).</summary>
    public void RestoreState(object state)
    {
        if (IsDead) return;
        var v = (Vector2)state;
        _hp = (int)v.x;
        _exposed = v.y;
        if (_brain) _brain.Grace(0.5f);
    }

    /// <summary>Arena reset after the player respawns: back to full health.</summary>
    public void Restore()
    {
        if (IsDead) return;
        _hp = maxHp;
        _exposed = 0f;
    }

    void Update()
    {
        if (_exposed > 0f)
        {
            _exposed -= Time.deltaTime;
            if (exposedMarker && !exposedMarker.activeSelf) exposedMarker.SetActive(true);
            if (_markerLight) _markerLight.intensity = 1.6f * Mathf.Clamp01(_exposed / 0.3f);
            if (_exposed <= 0f && exposedMarker) exposedMarker.SetActive(false);
        }

        if (!body || IsDead) return;
        _flash = Mathf.Max(0f, _flash - Time.deltaTime * 6f);
        _punch = Mathf.Max(0f, _punch - Time.deltaTime * 7f);
        var c = Color.Lerp(_baseColor, new Color(1f, 0.55f, 0.35f, _baseColor.a), _flash);
        if (IsExposed) c = Color.Lerp(c, new Color(0.55f, 0.95f, 1f, _baseColor.a), 0.35f + 0.25f * Mathf.Sin(Time.time * 20f));
        body.color = c;
        // Squash on hit: wide and short for a frame, then springs back.
        body.transform.localScale = new Vector3(_baseScale.x * (1f + 0.18f * _punch), _baseScale.y * (1f - 0.14f * _punch), _baseScale.z);
    }
}
