using UnityEngine;

/// <summary>Three hearts (GDD). Hazards remove one and send you to the checkpoint; empty hearts refill on respawn.</summary>
[RequireComponent(typeof(PlayerController2D))]
public class PlayerHealth : MonoBehaviour, IRewindable
{
    public int maxHearts = 3;
    public float invulnerableTime = 1f;
    public GameObject hurtFlash;
    public GameObject respawnOut;
    public GameObject respawnIn;
    public int Hearts { get; private set; }
    public bool IsFull => Hearts >= maxHearts;

    PlayerController2D _player;
    PlayerGlow _glow;
    float _invulnerable;

    void Awake()
    {
        _player = GetComponent<PlayerController2D>();
        _glow = GetComponent<PlayerGlow>();
        Hearts = maxHearts;
        GameRun.EnsureStarted();
    }

    void Update()
    {
        _invulnerable -= Time.deltaTime;
        Sfx.Loop(this, "breathing", Hearts == 1 && !PauseMenu.Paused, null, 0.6f);   // last heart: he is out of breath
    }

    public void Hurt() => Hurt(false);

    /// <param name="force">Pits / water ignore the post-hit blink so you can never get stuck down there.</param>
    public void Hurt(bool force)
    {
        if (TimeRewind.Rewinding) return;                    // nothing can hurt him while the past plays back
        if (_invulnerable > 0f && !force) return;
        if (!force && _player && _player.IsDashing) return;   // dashing through danger
        _invulnerable = invulnerableTime;
        Hearts--;
        CameraFollow2D.Shake(0.3f, 0.2f);
        Sfx.Play("gasp", transform.position);
        if (_glow) _glow.Flash(new Color(1f, 0.25f, 0.2f), 0.5f, 1.2f);
        if (Hearts <= 0)
        {
            // Out of health: a chance is spent. None left = game over.
            if (!GameRun.LoseChance()) { Hearts = 0; GameOverPanel.Show(); return; }
            Hearts = maxHearts;
        }
        var center = transform.position + Vector3.up * 1.4f;
        if (hurtFlash) Instantiate(hurtFlash, center, Quaternion.identity);
        if (respawnOut) Instantiate(respawnOut, center, Quaternion.identity);
        _player.Respawn();
        if (respawnIn) Instantiate(respawnIn, transform.position + Vector3.up * 1.4f, Quaternion.identity);
    }

    public void Heal(int amount = 1) => Hearts = Mathf.Min(maxHearts, Hearts + amount);

    public bool IsInvulnerable => _invulnerable > 0f;

    /// <summary>
    /// Enemy damage (GDD: normal contact = one heart). Unlike hazards it does not reset you to the checkpoint:
    /// you are knocked back and blink for a moment. Only losing the last heart sends you back.
    /// </summary>
    public void Damage(int amount, Vector2 from)
    {
        if (_invulnerable > 0f || TimeRewind.Rewinding || (_player && _player.IsDashing)) return;   // the dash dodges everything
        Hearts -= amount;
        Sfx.Play("hurt", transform.position);
        if (Hearts <= 0) { Hearts = 1; _invulnerable = 0f; Hurt(true); return; }   // the last hit: Hurt takes the final point
        _invulnerable = invulnerableTime * 1.2f;
        CameraFollow2D.Shake(0.25f, 0.18f);
        if (_glow) _glow.Flash(new Color(1f, 0.25f, 0.2f), 0.45f, 1.2f);
        var center = transform.position + Vector3.up * 1.4f;
        if (hurtFlash) Instantiate(hurtFlash, center, Quaternion.identity);
        float dir = Mathf.Sign(transform.position.x - from.x);
        if (dir == 0f) dir = 1f;
        _player.Knockback(new Vector2(dir * 7.5f, 8f));
        _blink = _invulnerable;
    }

    float _blink;
    SpriteRenderer _sr;

    public object CaptureState() => new Vector2(Hearts, _invulnerable);

    public void RestoreState(object state)
    {
        var v = (Vector2)state;
        Hearts = (int)v.x;
        _invulnerable = Mathf.Max(v.y, 0.4f);   // a short grace after landing back in time
        _blink = 0f;
        _sr ??= GetComponent<SpriteRenderer>();
        _sr.enabled = true;
    }

    void LateUpdate()
    {
        if (_blink <= 0f) return;
        _blink -= Time.deltaTime;
        _sr ??= GetComponent<SpriteRenderer>();
        _sr.enabled = _blink <= 0f || Mathf.Repeat(_blink, 0.12f) > 0.05f;   // i-frame blink
    }
}
