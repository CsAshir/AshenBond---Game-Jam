using UnityEngine;

/// <summary>
/// A burnt tree that cracks and falls: creak wobble -> accelerating fall around its base -> sparks + shake -> lies there.
/// Trigger modes:
///   Near    - when the player comes within triggerDistance (spectacle).
///   Ahead   - triggerDistance before the player reaches it, so it lands across the path (obstacle to jump).
///   Behind  - once the player has walked triggerDistance past it (cuts off the way back).
///   Shot    - only when hit by lantern fire hitsToBreak times (2-3): falls across a gap and becomes a bridge.
/// </summary>
public class TreeTopple : MonoBehaviour, IRewindable
{
    public enum Trigger { Near, Ahead, Behind, Shot }

    public Trigger trigger = Trigger.Near;
    public Transform player;
    public float triggerDistance = 10f;
    public float fallDirection = 1f;        // +1 falls right, -1 left
    public float fallAngle = 82f;
    public float creakTime = 0.6f;
    public GameObject impactPrefab;
    public Vector3 impactOffset = new Vector3(4f, 0f, 0f);
    public float shake = 0.15f;

    [Header("Gameplay (playable plane only)")]
    [Tooltip("Trigger while standing (catches lantern shots), becomes solid when the tree lies down (obstacle / bridge).")]
    public Collider2D trunk;
    public bool hurtWhileFalling;
    public float trunkLength = 6f;

    [Header("Shot trees")]
    public int hitsToBreak = 3;
    public SpriteRenderer weakSpotGlow;
    public GameObject chipsPrefab;

    int _state;        // 0 waiting, 1 creak, 2 falling, 3 down
    float _t;
    float _angle;
    float _vel;
    int _hits;
    float _hitWobble;
    SpriteRenderer _sr;
    Color _baseColor;

    public bool IsDown => _state == 3;

    void Start()
    {
        if (!player)
        {
            var p = GameObject.Find("Player");
            if (p) player = p.transform;
        }
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr) _baseColor = _sr.color;
        if (trunk) trunk.isTrigger = true;
    }

    /// <summary>Lantern fire hit (FireBolt calls this). Charged flares count double.</summary>
    public void Hit(int damage, Vector3 at)
    {
        if (trigger != Trigger.Shot || _state != 0) return;
        _hits += Mathf.Max(1, damage);
        Sfx.Play("wood_crack", at, 0.8f);
        _hitWobble = 1f;
        if (chipsPrefab) Instantiate(chipsPrefab, at, Quaternion.identity);
        CameraFollow2D.Shake(0.05f, 0.1f);
        if (_hits >= hitsToBreak) StartFall();
    }

    void StartFall()
    {
        Sfx.Play("wood_crack", transform.position);
        _state = 1;
        _t = 0f;
        if (weakSpotGlow) weakSpotGlow.enabled = false;
    }

    void Update()
    {
        switch (_state)
        {
            case 0:
                if (trigger == Trigger.Shot)
                {
                    // Wobble on every hit; the cracked base glows brighter the closer it is to breaking.
                    _hitWobble = Mathf.Max(0f, _hitWobble - Time.deltaTime * 3f);
                    transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 45f) * 2.5f * _hitWobble);
                    if (_sr) _sr.color = Color.Lerp(_baseColor, new Color(1f, 0.6f, 0.35f, _baseColor.a), _hitWobble * 0.7f);
                    if (weakSpotGlow)
                    {
                        var c = weakSpotGlow.color;
                        c.a = (0.35f + 0.25f * Mathf.Sin(Time.time * 3f)) * (1f + (float)_hits / hitsToBreak);
                        weakSpotGlow.color = c;
                    }
                    break;
                }
                if (!player) break;
                float dx = player.position.x - transform.position.x;
                bool go = trigger switch
                {
                    Trigger.Ahead => dx > -triggerDistance,
                    Trigger.Behind => dx > triggerDistance,
                    _ => Mathf.Abs(dx) < triggerDistance,
                };
                if (go) StartFall();
                break;
            case 1:
                _t += Time.deltaTime;
                if (_sr) _sr.color = _baseColor;
                transform.localRotation = Quaternion.Euler(0f, 0f, -fallDirection * Mathf.Sin(_t * 40f) * 1.5f * (_t / creakTime));
                if (_t >= creakTime) { _state = 2; _angle = 3f; }
                break;
            case 2:
                _vel += (60f + _angle * 4f) * Time.deltaTime;     // accelerates as it leans further
                _angle = Mathf.Min(fallAngle, _angle + _vel * Time.deltaTime);
                transform.localRotation = Quaternion.Euler(0f, 0f, -fallDirection * _angle);
                if (hurtWhileFalling && _angle > 25f) CrushCheck();
                if (_angle >= fallAngle) Land();
                break;
        }
    }

    class State { public int state, hits; public float t, angle, vel; public Quaternion rot; public bool hurt; }

    public object CaptureState() =>
        new State { state = _state, hits = _hits, t = _t, angle = _angle, vel = _vel, rot = transform.localRotation, hurt = hurtWhileFalling };

    /// <summary>Rewind: a fallen tree swings back up (and stops being solid ground).</summary>
    public void RestoreState(object state)
    {
        var s = (State)state;
        _state = s.state; _hits = s.hits; _t = s.t; _angle = s.angle; _vel = s.vel; hurtWhileFalling = s.hurt;
        transform.localRotation = s.rot;
        if (trunk) trunk.isTrigger = _state != 3;
        if (weakSpotGlow) weakSpotGlow.enabled = _state == 0;
    }

    void CrushCheck()
    {
        Vector2 a = transform.position;
        Vector2 b = a + (Vector2)(transform.up * trunkLength);
        foreach (var hit in Physics2D.LinecastAll(a, b))
        {
            var pc = hit.collider.GetComponentInParent<PlayerController2D>();
            if (pc) { pc.Hurt(); hurtWhileFalling = false; break; }
        }
    }

    void Land()
    {
        _state = 3;
        if (trunk) trunk.isTrigger = false;
        Sfx.Play("tree_fall", transform.position);
        if (impactPrefab)
            Instantiate(impactPrefab, transform.position + new Vector3(impactOffset.x * fallDirection, impactOffset.y, 0f), Quaternion.identity);
        CameraFollow2D.Shake(shake, 0.25f);
    }
}
