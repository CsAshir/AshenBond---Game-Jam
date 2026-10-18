using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Side-scroller controller: run, variable-height jump, coyote time, jump buffer and sprite flipbook.
/// Keyboard: A/D or arrows to walk, hold Shift to run, Space/W/Up to jump (again in the air = double jump),
/// K / right mouse to dash (ground dodge: untouchable while it lasts), R to respawn.
/// Gamepad: left stick / d-pad, hold a shoulder button to run, South to jump, right trigger to dash.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Walking speed (default pace).")]
    public float moveSpeed = 7.5f;
    [Tooltip("Speed while holding Run (Shift / shoulder).")]
    public float runSpeed = 6.6f;
    public float acceleration = 70f;
    public float deceleration = 80f;
    [Range(0f, 1f)] public float airControl = 0.75f;

    [Header("Jump")]
    public float jumpVelocity = 18.5f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;
    public float fallGravityMultiplier = 1.6f;
    public float lowJumpGravityMultiplier = 2.2f;
    public float maxFallSpeed = 25f;
    [Tooltip("Extra jumps in the air. Tuned so the double jump reaches ~9 units: the tree gaps (10) still need the tree.")]
    public int airJumps = 1;
    public float airJumpVelocity = 10f;
    [Tooltip("Horizontal speed cap after a double jump: it is a lift to fix a landing, not a long jump (tree gaps stay tree gaps).")]
    public float doubleJumpDrift = 2.5f;
    public GameObject airJumpFx;

    [Header("Dash (ground dodge)")]
    public float dashSpeed = 17f;
    public float dashTime = 0.2f;
    public float dashCooldown = 0.55f;
    [Tooltip("Air dash: shorter, once per jump, and it uses up the double jump (and vice versa) so tree gaps still need the tree.")]
    public float airDashSpeed = 15f;
    public float airDashTime = 0.16f;
    public GameObject dashFx;
    public GameObject dashTrailFx;
    public Material ghostMaterial;

    [Header("Safety")]
    [Tooltip("Below this height the boy has fallen out of the level: he loses a heart and goes back to the checkpoint.")]
    public float fallLimitY = -40f;

    [Header("Ground Check")]
    public Vector2 groundCheckSize = new Vector2(0.6f, 0.12f);

    [Header("Animation")]
    public Sprite[] idleFrames;
    public Sprite[] walkFrames;
    public Sprite[] runFrames;
    public Sprite[] riseFrames;
    public Sprite[] fallFrames;
    public Sprite[] landFrames;

    [Header("Effects")]
    public GameObject jumpDust;
    public GameObject landDust;
    public float idleFps = 6f;
    public float runFps = 14f;
    public float walkFps = 10f;
    public float landFps = 14f;
    public float landTime = 0.14f;
    public float airFps = 8f;

    public bool IsGrounded { get; private set; }
    public bool IsDashing => _dashT > 0f;
    /// <summary>0..1 dash readiness (HUD).</summary>
    public float DashReady01 => 1f - Mathf.Clamp01(_dashCd / dashCooldown);
    public Vector2 Velocity => _rb.linearVelocity;
    public bool IsFacingRight => !_sr.flipX;

    /// <summary>Scales run speed (e.g. while charging the lantern).</summary>
    [HideInInspector] public float moveMultiplier = 1f;
    /// <summary>Keeps the current facing (e.g. while casting).</summary>
    [HideInInspector] public bool lockFacing;

    Sprite[] _override;
    float _overrideFps;
    bool _overrideLoop;
    float _overrideTime;
    bool _wasGrounded = true;
    float _landTimer;
    float _airTime;
    float _jumpTime = -1f;
    float _stun;
    int _airJumpsLeft;
    bool _driftCap;
    float _dashT, _dashCd, _ghostT;
    int _dashDir = 1;
    bool _dashPressed;

    /// <summary>Play an action animation (attack, heal...) on top of the movement animations.</summary>
    public void SetOverride(Sprite[] frames, float fps, bool loop)
    {
        if (_override == frames) return;
        _override = frames;
        _overrideFps = fps;
        _overrideLoop = loop;
        _overrideTime = 0f;
    }

    public void ClearOverride() => _override = null;

    /// <summary>Hazard hit: lose a heart (if PlayerHealth exists) and go back to the checkpoint.</summary>
    public void Hurt()
    {
        var health = GetComponent<PlayerHealth>();
        if (health) health.Hurt();
        else Respawn();
    }

    Rigidbody2D _rb;
    SpriteRenderer _sr;
    Collider2D _collider;
    readonly Collider2D[] _hits = new Collider2D[8];
    ContactFilter2D _groundFilter;

    Vector3 _spawn;
    float _baseGravity;
    float _move;
    bool _jumpHeld;
    bool _runHeld;
    float _coyoteTimer;
    float _jumpBufferTimer;
    Sprite[] _currentFrames;
    float _animTime;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _baseGravity = _rb.gravityScale;
        _spawn = transform.position;
        _groundFilter = new ContactFilter2D { useTriggers = false };
    }

    void Update()
    {
        ReadInput();

        _coyoteTimer = IsGrounded ? coyoteTime : _coyoteTimer - Time.deltaTime;
        _jumpBufferTimer -= Time.deltaTime;

        if (IsGrounded) { _airJumpsLeft = airJumps; _driftCap = false; }
        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpVelocity);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            IsGrounded = false;
            _jumpTime = Time.time;
            Spawn(jumpDust);
            Sfx.Play("jump", transform.position);
            if (Random.value < 0.35f) Sfx.Play("grunt_jump", transform.position);
        }
        else if (_jumpBufferTimer > 0f && !IsGrounded && _airJumpsLeft > 0 && _stun <= 0f && Time.time - _jumpTime > 0.12f)
        {
            // Double jump: a smaller second kick, a ring of light under the feet.
            _airJumpsLeft--;
            _driftCap = true;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, airJumpVelocity);
            _jumpBufferTimer = 0f;
            _jumpTime = Time.time;
            if (airJumpFx) Instantiate(airJumpFx, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            Sfx.Play("jump", transform.position, 0.8f, 1.25f);
            Sfx.Play("grunt_jump", transform.position, 0.8f);
        }

        _dashCd -= Time.deltaTime;
        if (_dashPressed && _dashCd <= 0f && _stun <= 0f && _dashT <= 0f && (IsGrounded || _airJumpsLeft > 0)) StartDash();
        if (_dashT > 0f)
        {
            _ghostT -= Time.deltaTime;
            if (_ghostT <= 0f) { _ghostT = 0.035f; Ghost(); }
        }

        if (transform.position.y < fallLimitY)
        {
            // Fell out of the level (a pit without a kill zone, or a rewind that ended mid-fall): never lost forever.
            var hp = GetComponent<PlayerHealth>();
            if (hp) hp.Hurt(true); else Respawn();
        }

        Animate();
    }

    void FixedUpdate()
    {
        IsGrounded = CheckGround();

        if (_stun > 0f)
        {
            _stun -= Time.fixedDeltaTime;
            _rb.gravityScale = _baseGravity;
            return;
        }

        if (_dashT > 0f)
        {
            _dashT -= Time.fixedDeltaTime;
            _rb.gravityScale = _baseGravity;
            _rb.linearVelocity = new Vector2(_dashDir * (_airDash ? airDashSpeed : dashSpeed), _airDash ? 0f : Mathf.Min(_rb.linearVelocity.y, 0f));
            if (_dashT <= 0f) _rb.linearVelocity = new Vector2(_dashDir * runSpeed, _rb.linearVelocity.y);
            return;
        }

        float target = _move * (_runHeld ? runSpeed : moveSpeed) * moveMultiplier;
        if (_driftCap && !IsGrounded)
        {
            target = Mathf.Clamp(target, -doubleJumpDrift, doubleJumpDrift);
            _rb.linearVelocity = new Vector2(Mathf.Clamp(_rb.linearVelocity.x, -doubleJumpDrift, doubleJumpDrift), _rb.linearVelocity.y);
        }
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
        if (!IsGrounded) rate *= airControl;
        float vx = Mathf.MoveTowards(_rb.linearVelocity.x, target, rate * Time.fixedDeltaTime);

        // Snappier jump arc: heavier when falling or when the jump button is released early.
        float vy = _rb.linearVelocity.y;
        float gravity = _baseGravity;
        if (vy < 0f) gravity *= fallGravityMultiplier;
        else if (vy > 0f && !_jumpHeld) gravity *= lowJumpGravityMultiplier;
        _rb.gravityScale = gravity;

        _rb.linearVelocity = new Vector2(vx, Mathf.Max(vy, -maxFallSpeed));
    }

    void ReadInput()
    {
        if (DialogueDirector.Active || PauseMenu.Paused) { _move = 0f; _jumpHeld = false; _dashPressed = false; return; }
        float x = 0f;
        bool pressed = false, held = false, run = false, dash = false;

        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
            pressed |= k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame;
            held |= k.spaceKey.isPressed || k.wKey.isPressed || k.upArrowKey.isPressed;
            if (k.rKey.wasPressedThisFrame) Respawn();
            run |= k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
            dash |= k.kKey.isPressed;
        }
        var mouse = Mouse.current;
        if (mouse != null) dash |= mouse.rightButton.isPressed;

        var g = Gamepad.current;
        if (g != null)
        {
            float gx = g.leftStick.x.ReadValue();
            if (Mathf.Abs(gx) > 0.2f) x = gx;
            if (g.dpad.left.isPressed) x = -1f;
            if (g.dpad.right.isPressed) x = 1f;
            pressed |= g.buttonSouth.wasPressedThisFrame;
            held |= g.buttonSouth.isPressed;
            run |= g.leftShoulder.isPressed || g.rightShoulder.isPressed || g.leftStickButton.isPressed;
            dash |= g.rightTrigger.isPressed;
        }
        _dashPressed = dash && !_dashHeld;     // own edge detection (never misses a frame)
        _dashHeld = dash;

        _move = Mathf.Clamp(x, -1f, 1f);
        _jumpHeld = held;
        _runHeld = run;
        if (pressed) _jumpBufferTimer = jumpBufferTime;
    }

    bool CheckGround()
    {
        var center = (Vector2)transform.position + Vector2.down * 0.02f;
        int count = Physics2D.OverlapBox(center, groundCheckSize, 0f, _groundFilter, _hits);
        for (int i = 0; i < count; i++)
            // Rising only counts as airborne right after a jump: walking up a slope (a fallen tree) is still grounded.
            if (_hits[i] != _collider) return _rb.linearVelocity.y <= 0.05f || Time.time - _jumpTime > 0.15f;
        return false;
    }

    void Animate()
    {
        if (!lockFacing)
        {
            if (_move > 0.05f) _sr.flipX = false;
            else if (_move < -0.05f) _sr.flipX = true;
        }

        if (IsGrounded && !_wasGrounded)
        {
            _landTimer = landTime;
            if (_airTime > 0.25f) { Spawn(landDust); Sfx.Play("land", transform.position, Mathf.Clamp01(_airTime)); }
        }
        _airTime = IsGrounded ? 0f : _airTime + Time.deltaTime;
        _wasGrounded = IsGrounded;
        _landTimer -= Time.deltaTime;

        if (_override != null && _override.Length > 0)
        {
            _overrideTime += Time.deltaTime;
            int oi = (int)(_overrideTime * _overrideFps);
            _sr.sprite = _override[_overrideLoop ? oi % _override.Length : Mathf.Min(oi, _override.Length - 1)];
            return;
        }

        Sprite[] frames;
        float fps;
        if (_landTimer > 0f && landFrames != null && landFrames.Length > 0 && Mathf.Abs(_move) < 0.05f)
        {
            frames = landFrames;
            fps = landFps;
        }
        else if (!IsGrounded)
        {
            frames = _rb.linearVelocity.y > 0.5f ? riseFrames : fallFrames;
            fps = airFps;
        }
        else if (Mathf.Abs(_rb.linearVelocity.x) > 0.4f)
        {
            // Walk by default, run when going faster than a walk; cadence follows the actual speed.
            float speed = Mathf.Abs(_rb.linearVelocity.x);
            bool running = walkFrames == null || walkFrames.Length == 0 || speed > moveSpeed + 0.5f;
            frames = running ? runFrames : walkFrames;
            fps = running ? runFps * Mathf.Clamp(speed / runSpeed, 0.6f, 1.2f) : walkFps * Mathf.Clamp(speed / moveSpeed, 0.5f, 1.2f);
        }
        else
        {
            frames = idleFrames;
            fps = idleFps;
        }

        bool stepping = IsGrounded && (frames == walkFrames || frames == runFrames) && !IsDashing;
        bool runningNow = frames == runFrames;
        Sfx.Loop(this, "step_walk", stepping && !runningNow, null, 0.8f);
        Sfx.Loop(_stepKey, "step_run", stepping && runningNow, null, 0.8f);
        if (frames == null || frames.Length == 0) return;
        if (frames != _currentFrames)
        {
            _currentFrames = frames;
            _animTime = 0f;
        }
        _animTime += Time.deltaTime;
        int index = frames == idleFrames || frames == runFrames || frames == walkFrames
            ? (int)(_animTime * fps) % frames.Length
            : Mathf.Min((int)(_animTime * fps), frames.Length - 1);
        _sr.sprite = frames[index];
    }

    bool _dashHeld, _airDash;
    readonly object _stepKey = new();

    void StartDash()
    {
        _dashDir = _sr.flipX ? -1 : 1;
        if (Mathf.Abs(_move) > 0.1f) _dashDir = _move > 0f ? 1 : -1;
        _airDash = !IsGrounded;
        if (_airDash) { _airJumpsLeft = 0; _driftCap = false; }
        _dashT = _airDash ? airDashTime : dashTime;
        _dashCd = dashCooldown + _dashT;
        _ghostT = 0f;
        CameraFollow2D.Shake(0.05f, 0.08f);
        Sfx.Play("dash", transform.position);
        foreach (var fx in new[] { dashFx, dashTrailFx })
        {
            if (!fx) continue;
            var go = Instantiate(fx, transform.position + new Vector3(-_dashDir * 0.6f, 1.2f, 0f), Quaternion.identity);
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>()) r.flipX = _dashDir < 0;
        }
    }

    /// <summary>A fading violet copy of the boy left along the dash.</summary>
    void Ghost()
    {
        var go = new GameObject("Dash Ghost");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sr.sprite;
        sr.flipX = _sr.flipX;
        sr.sharedMaterial = ghostMaterial ? ghostMaterial : _sr.sharedMaterial;
        sr.color = new Color(0.75f, 0.45f, 1f, 0.55f);
        sr.sortingOrder = _sr.sortingOrder - 1;
        go.AddComponent<FadeOut>().life = 0.25f;
    }

    void Spawn(GameObject fx)
    {
        if (!fx) return;
        var go = Instantiate(fx, transform.position, Quaternion.identity);
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr) sr.flipX = _sr.flipX;
    }

    public void SetSpawn(Vector3 position) => _spawn = position;

    /// <summary>Raised whenever the boy is sent back to a checkpoint (arenas reopen their gates).</summary>
    public static event System.Action Respawned;

    public void Respawn()
    {
        transform.position = _spawn;
        _rb.position = _spawn;
        _rb.linearVelocity = Vector2.zero;
        _stun = 0f;
        _dashT = 0f;
        Respawned?.Invoke();
    }

    /// <summary>Enemy hit: thrown back, controls locked for a moment.</summary>
    public void Knockback(Vector2 velocity, float stun = 0.28f)
    {
        _rb.linearVelocity = velocity;
        _stun = stun;
        IsGrounded = false;
        _jumpTime = Time.time;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position + Vector3.down * 0.02f, groundCheckSize);
    }
}
