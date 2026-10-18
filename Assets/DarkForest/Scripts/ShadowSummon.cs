using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The player's shadow spirit. Idles as a flat shadow on the ground under the player's feet.
/// Press P (gamepad: West button) and she rises out of the shadow, hurls a neon shadow-fire orb,
/// lingers for a moment, then sinks back into the ground.
/// Press E (gamepad: North button) to heal: she rises, channels a cyan magic circle under the boy
/// and restores one heart for a few embers.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShadowSummon : MonoBehaviour
{
    [Header("Links")]
    public PlayerController2D player;
    public SpriteRenderer playerRenderer;

    [Header("Animation")]
    [Tooltip("0 = flat ground shadow ... last = casting pose. The art faces left.")]
    public Sprite[] frames;
    public float emergeFps = 14f;
    public float retreatFps = 18f;
    public float holdTime = 1.5f;
    public float cooldown = 0.35f;
    [Tooltip("How far behind the player she stands once fully risen.")]
    public float backOffset = 0.45f;
    [Range(0f, 1f)] public float idleShadowAlpha = 0.85f;

    [Header("Attack")]
    public GameObject orbPrefab;
    public Vector2 handOffset = new Vector2(0.35f, 3.0f);
    public Vector2 orbVelocity = new Vector2(11f, 4.5f);

    [Header("Heal (E)")]
    [Tooltip("Float, float, point, charge, cast-circle. The art faces left like the rise frames.")]
    public Sprite[] healFrames;
    public float healFps = 7f;
    public float healHold = 1.1f;
    public int healCost = 2;
    public GameObject healCirclePrefab;
    public PlayerHealth health;
    public PlayerGlow glow;

    /// <summary>HUD reads this to explain why a heal did not start.</summary>
    public static string HealHint;
    public static float HealHintUntil;

    [Header("Effects")]
    public GameObject summonBurstPrefab;
    public SpriteRenderer aura;
    [Range(0f, 1f)] public float auraAlpha = 0.35f;
    public Light2D auraLight;
    public float auraLightIntensity = 1.6f;
    public ParticleSystem wisps;

    enum State { Idle, Emerging, Holding, Healing, Retreating }

    State _state;
    float _t;
    int _facing = 1;
    float _cooldownTimer;
    SpriteRenderer _sr;
    Collider2D _playerCollider;
    float _auraAlpha;
    float _auraLightBase;
    int _frame;
    bool _healMode;
    bool _healed;
    readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
    ContactFilter2D _groundFilter;

    public bool IsBusy => _state != State.Idle;

    [Header("Attack recharge")]
    [Tooltip("Seconds for the shadow strike (P) to fill up again. It can only be used when full.")]
    public float attackRecharge = 7f;
    float _charge = 1f;
    /// <summary>0..1 fill of the shadow strike (the HUD's sister icon fills with it).</summary>
    public float AttackCharge01 => _charge;
    public bool AttackReady => _charge >= 1f;
    /// <summary>Souls of fallen enemies feed her (fraction of a full charge).</summary>
    public void AddCharge(float amount) => _charge = Mathf.Min(1f, _charge + amount);

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (player) _playerCollider = player.GetComponent<Collider2D>();
        _auraAlpha = auraAlpha;
        _auraLightBase = auraLightIntensity;
        _groundFilter = new ContactFilter2D { useTriggers = false };
        SetFrame(0);
    }

    void Update()
    {
        if (!player || frames == null || frames.Length < 2) return;
        float dt = Time.deltaTime;
        _cooldownTimer -= dt;
        if (DialogueDirector.Active || PauseMenu.Paused) return;
        if (_state == State.Idle || _healMode) _charge = Mathf.Min(1f, _charge + dt / attackRecharge);

        if (_state == State.Idle)
        {
            _facing = playerRenderer && playerRenderer.flipX ? -1 : 1;
            bool p = Pressed();
            if (p && !AttackReady) Hint("Mira is still gathering her strength");
            if (_cooldownTimer <= 0f && p && AttackReady) { _charge = 0f; Begin(false); }
            else if (_cooldownTimer <= 0f && HealPressed() && CanHeal()) Begin(true);
        }

        switch (_state)
        {
            case State.Emerging:
                _t += dt * emergeFps;
                SetFrame(Mathf.Min((int)_t, frames.Length - 1));
                if (_t >= frames.Length)
                {
                    _t = 0f;
                    if (_healMode && healFrames != null && healFrames.Length > 0) _state = State.Healing;
                    else
                    {
                        Cast();
                        _state = State.Holding;
                    }
                }
                break;

            case State.Healing:
                _t += dt;
                int hf = Mathf.Min((int)(_t * healFps), healFrames.Length - 1);
                _sr.sprite = healFrames[hf];
                if (hf == healFrames.Length - 1 && !_healed) DoHeal();
                if (_t >= healFrames.Length / healFps + healHold)
                {
                    Burst();
                    _state = State.Retreating;
                    _t = 0f;
                    player.moveMultiplier = 1f;
                    player.lockFacing = false;
                }
                break;

            case State.Holding:
                _t += dt;
                if (_t >= holdTime)
                {
                    Burst();
                    _state = State.Retreating;
                    _t = 0f;
                }
                break;

            case State.Retreating:
                _t += dt * retreatFps;
                int f = frames.Length - 1 - (int)_t;
                if (f <= 0)
                {
                    SetFrame(0);
                    _state = State.Idle;
                    _cooldownTimer = cooldown;
                }
                else SetFrame(f);
                break;
        }

        FollowPlayer();
        UpdateEffects();
        if (glow) glow.SetSister(_state != State.Idle);
    }

    bool Pressed()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && k.pKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame);
    }

    bool HealPressed()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && k.eKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame);
    }

    bool CanHeal()
    {
        if (health && health.IsFull) { Hint("Hearts are full"); return false; }
        if (EmberPickup.Collected < healCost) { Hint($"Need {healCost} embers to heal"); return false; }
        return true;
    }

    static void Hint(string text)
    {
        HealHint = text;
        HealHintUntil = Time.time + 1.6f;
    }

    void Begin(bool heal)
    {
        _healMode = heal;
        _healed = false;
        _state = State.Emerging;
        _t = 0f;
        Burst();
        if (heal)
        {
            player.moveMultiplier = 0f;     // the boy stays still while she channels
            player.lockFacing = true;
        }
    }

    void DoHeal()
    {
        _healed = true;
        Sfx.Play("heal", transform.position);
        EmberPickup.Collected = Mathf.Max(0, EmberPickup.Collected - healCost);
        if (health) health.Heal(1);
        if (glow) glow.Flash(new Color(0.35f, 0.95f, 1f), 1.4f, 0.9f);
        if (healCirclePrefab) Instantiate(healCirclePrefab, player.transform.position, Quaternion.identity);
    }

    void Cast()
    {
        if (!orbPrefab) return;
        Sfx.Play("orb_throw", transform.position);
        Vector3 hand = transform.position + new Vector3(handOffset.x * _facing, handOffset.y, 0f);
        var orb = Instantiate(orbPrefab, hand, Quaternion.identity);
        var shadowOrb = orb.GetComponent<ShadowOrb>();
        if (!shadowOrb) return;
        // Aim at the middle of the enemies around the boy so the burst catches the whole group.
        Vector2 sum = Vector2.zero;
        int n = 0;
        foreach (var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (e.IsDead) continue;
            Vector2 d = (Vector2)e.transform.position - (Vector2)player.transform.position;
            if (Mathf.Abs(d.x) > 13f || Mathf.Abs(d.y) > 7f) continue;
            sum += (Vector2)e.transform.position + e.fxOffset * 0.5f;
            n++;
        }
        if (n == 0) { shadowOrb.Launch(new Vector2(orbVelocity.x * _facing, orbVelocity.y), _playerCollider); return; }
        Vector2 target = sum / n;
        Vector2 delta = target - (Vector2)hand;
        var rb = orb.GetComponent<Rigidbody2D>();
        float g = Physics2D.gravity.magnitude * (rb ? rb.gravityScale : 1f);
        float t = Mathf.Clamp(Mathf.Abs(delta.x) / 11f, 0.45f, 1.1f);
        shadowOrb.homing = 0f;                                   // it is already aimed at the group
        shadowOrb.Launch(new Vector2(delta.x / t, delta.y / t + 0.5f * g * t), _playerCollider);
    }

    void Burst()
    {
        if (summonBurstPrefab) Instantiate(summonBurstPrefab, transform.position, Quaternion.identity);
    }

    void SetFrame(int index)
    {
        _frame = Mathf.Clamp(index, 0, frames.Length - 1);
        _sr.sprite = frames[_frame];
    }

    float Emerged => frames.Length > 1 ? _frame / (float)(frames.Length - 1) : 0f;

    /// <summary>Stick to the ground under the player (the shadow stays on the floor while jumping).</summary>
    void FollowPlayer()
    {
        Vector2 feet = player.transform.position;
        float groundY = feet.y;
        float height = 0f;
        int count = Physics2D.Raycast(feet + Vector2.up * 0.1f, Vector2.down, _groundFilter, _hits, 15f);
        for (int i = 0; i < count; i++)
        {
            var c = _hits[i].collider;
            if (c == _playerCollider || c.isTrigger) continue;
            groundY = _hits[i].point.y;
            height = feet.y - groundY;
            break;
        }

        float x = feet.x - _facing * backOffset * Emerged;
        transform.position = new Vector3(x, groundY, 0f);
        _sr.flipX = _facing > 0;   // the art faces left

        // Idle shadow fades and shrinks as the player jumps away from the ground.
        float a = _state == State.Idle ? idleShadowAlpha * Mathf.Clamp01(1f - height / 5f) : 1f;
        float s = _state == State.Idle ? Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(height / 5f)) : 1f;
        var col = _sr.color;
        col.a = a;
        _sr.color = col;
        transform.localScale = new Vector3(s, 1f, 1f);
    }

    void UpdateEffects()
    {
        float e = _state == State.Idle ? 0f : Emerged;
        if (aura)
        {
            var c = aura.color;
            c.a = _auraAlpha * e;
            aura.color = c;
            aura.transform.localPosition = new Vector3(-_facing * 0.55f, 2.1f, 0f);
        }
        if (auraLight)
        {
            auraLight.intensity = _auraLightBase * e;
            auraLight.transform.localPosition = new Vector3(-_facing * 0.4f, 2.4f, 0f);
        }
        if (wisps)
        {
            var em = wisps.emission;
            em.enabled = e > 0.5f;
            wisps.transform.localPosition = new Vector3(-_facing * 0.5f, 1.6f, 0f);
        }
    }
}
