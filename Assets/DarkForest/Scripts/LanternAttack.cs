using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lantern fire (GDD section 3). Tap J / Left Mouse: Ember Shot. Hold 0.65 s and release: Charged Flare.
/// Uses the charged-attack sprite sheet: glow -> ring forms -> white-hot ring -> release -> recovery.
/// </summary>
[RequireComponent(typeof(PlayerController2D))]
public class LanternAttack : MonoBehaviour
{
    [Tooltip("0-2 lantern glow, 3 ring forms, 4 white-hot ring, 5 release, 6 recovery")]
    public Sprite[] chargeFrames;
    public GameObject emberShotPrefab;
    public GameObject chargedFlarePrefab;
    public float chargeTime = 0.65f;
    public float shotCooldown = 0.28f;
    public float emberShotSpeed = 16f;
    public float flareSpeed = 13f;
    public Vector2 muzzle = new Vector2(1.0f, 1.05f);
    [Range(0f, 1f)] public float chargingMoveSpeed = 0.35f;

    PlayerController2D _player;
    PlayerGlow _glow;
    Collider2D _collider;
    Sprite[] _glowFrames, _ringFrames, _releaseFrames, _recoverFrames;
    float _held;
    bool _charging;
    float _cooldown;
    float _poseTimer;

    void Awake()
    {
        _player = GetComponent<PlayerController2D>();
        _glow = GetComponent<PlayerGlow>();
        _collider = GetComponent<Collider2D>();
        if (chargeFrames != null && chargeFrames.Length >= 7)
        {
            _glowFrames = new[] { chargeFrames[0], chargeFrames[1], chargeFrames[2] };
            _ringFrames = new[] { chargeFrames[3], chargeFrames[4] };
            _releaseFrames = new[] { chargeFrames[5] };
            _recoverFrames = new[] { chargeFrames[6] };
        }
    }

    bool Down()
    {
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        return (k != null && k.jKey.isPressed) || (m != null && m.leftButton.isPressed) || (g != null && g.buttonEast.isPressed);
    }

    void Update()
    {
        _cooldown -= Time.deltaTime;
        bool down = Down() && !DialogueDirector.Blocking && !PauseMenu.Paused;

        if (down && _cooldown <= 0f)
        {
            if (!_charging) { _charging = true; _held = 0f; }
            _held += Time.deltaTime;
            float k = Mathf.Clamp01(_held / chargeTime);
            if (k >= 1f && !_pinged) { _pinged = true; Sfx.Play("flare_charged", transform.position); }
            _player.moveMultiplier = Mathf.Lerp(1f, chargingMoveSpeed, k);
            _player.lockFacing = k > 0.3f;
            if (_held > 0.12f && _glowFrames != null)
                _player.SetOverride(k < 1f ? _glowFrames : _ringFrames, k < 1f ? 6f : 10f, k >= 1f);
            if (_glow) _glow.Hold(k >= 1f ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.7f, 0.4f), k * 0.5f);
        }
        else if (_charging)
        {
            bool charged = _held >= chargeTime;
            _pinged = false;
            Fire(charged);
            _charging = false;
            _cooldown = shotCooldown;
            _player.moveMultiplier = 1f;
            if (_glow)
            {
                _glow.Hold(Color.black, 0f);
                _glow.Flash(charged ? new Color(0.45f, 0.8f, 1f) : new Color(1f, 0.65f, 0.25f), charged ? 0.35f : 0.18f, charged ? 0.9f : 0.5f);
            }
            if (_releaseFrames != null) _player.SetOverride(_releaseFrames, 1f, false);
            _poseTimer = charged ? 0.32f : 0.16f;
        }

        if (!_charging && _poseTimer > 0f)
        {
            _poseTimer -= Time.deltaTime;
            if (_poseTimer <= 0.1f && _recoverFrames != null) _player.SetOverride(_recoverFrames, 1f, false);
            if (_poseTimer <= 0f)
            {
                _player.ClearOverride();
                _player.lockFacing = false;
            }
        }
    }

    bool _pinged;

    void Fire(bool charged)
    {
        var prefab = charged ? chargedFlarePrefab : emberShotPrefab;
        if (!prefab) return;
        float dir = _player.IsFacingRight ? 1f : -1f;
        var pos = transform.position + new Vector3(muzzle.x * dir, muzzle.y, 0f);
        var bolt = Instantiate(prefab, pos, Quaternion.identity).GetComponent<FireBolt>();
        if (bolt) bolt.Launch(new Vector2(dir * (charged ? flareSpeed : emberShotSpeed), 0f), _collider);
        if (charged) CameraFollow2D.Shake(0.12f, 0.12f);
        Sfx.Play(charged ? "flare_release" : "ember_shot", pos);
        if (charged) Sfx.Play("grunt_attack", pos);
    }
}
