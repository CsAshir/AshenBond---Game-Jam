using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Flying Eye: hovers over the street, keeps ~5 tiles from the boy and a few tiles above him,
/// curls up (0.45 s telegraph, red glow) and spits a slow bolt at where he is. Teaches aiming at the air:
/// the lantern shot travels straight, so line up or wait for it to swoop lower.
/// </summary>
public class FlyingEyeAI : EnemyBrain
{
    protected override bool IsWalker => false;

    public Sprite[] flyFrames, curlFrames;
    public GameObject shotPrefab;
    public float aggroRange = 11f;
    public float keepDistance = 5f;
    public float hoverHeight = 3f;
    public float moveSpeed = 2.6f;
    public float shotInterval = 2.6f;
    public float telegraphTime = 0.45f;
    public float shotSpeed = 6.5f;
    public Vector2 leash = new Vector2(8f, 4f);   // how far it may wander from its home
    public SpriteRenderer chargeGlow;
    public Light2D eyeLight;

    Vector3 _home;
    float _shotTimer;
    float _tele = -1f;
    float _phase;
    Vector2 _vel;
    bool _dead;
    float _deadVy;
    float _spin;

    protected override void Start()
    {
        base.Start();
        _home = transform.position;
        _phase = Random.value * 6f;
        _shotTimer = shotInterval * Random.Range(0.6f, 1f);
        if (chargeGlow) chargeGlow.enabled = false;
    }

    void Update()
    {
        if (_dead) { Fall(); return; }
        if (Waking) { Play(flyFrames, 10f); return; }

        float t = Time.time + _phase;
        Vector3 target = _home;
        bool aggro = Player && Mathf.Abs(DistToPlayerX) < aggroRange && Mathf.Abs(DistToPlayerY) < 8f;
        if (aggro)
        {
            float side = DistToPlayerX > 0f ? -1f : 1f;        // stay on the side it's already on
            target = new Vector3(Player.position.x + side * keepDistance, Player.position.y + hoverHeight, _home.z);
            target.x = Mathf.Clamp(target.x, _home.x - leash.x, _home.x + leash.x);
            target.y = Mathf.Clamp(target.y, _home.y - leash.y, _home.y + leash.y);
            Face(DistToPlayerX);
        }
        target.y += Mathf.Sin(t * 2.2f) * 0.35f;

        if (_tele < 0f)
        {
            Vector2 to = target - transform.position;
            _vel = Vector2.Lerp(_vel, Vector2.ClampMagnitude(to * 1.6f, moveSpeed), Time.deltaTime * 3f);
            transform.position += (Vector3)_vel * Time.deltaTime;
            Play(flyFrames, 10f);
            if (!aggro) Face(_vel.x);

            _shotTimer -= Time.deltaTime;
            if (aggro && _shotTimer <= 0f) { _tele = telegraphTime; if (chargeGlow) chargeGlow.enabled = true; }
        }
        else
        {
            // Telegraph: curl up, glow builds, tiny shiver, then spit.
            _tele -= Time.deltaTime;
            Play(curlFrames, curlFrames.Length / telegraphTime, false);
            transform.position += new Vector3(Mathf.Sin(Time.time * 60f) * 0.01f, 0f, 0f);
            float k = 1f - Mathf.Clamp01(_tele / telegraphTime);
            if (chargeGlow) { chargeGlow.color = new Color(1f, 0.25f, 0.2f, k * 0.9f); chargeGlow.transform.localScale = Vector3.one * (0.2f + 0.25f * k); }
            if (eyeLight) eyeLight.intensity = 0.8f + 2f * k;
            if (_tele <= 0f) Shoot();
        }

        ContactDamage();
    }

    void Shoot()
    {
        _tele = -1f;
        _shotTimer = shotInterval;
        if (chargeGlow) chargeGlow.enabled = false;
        if (eyeLight) eyeLight.intensity = 0.8f;
        if (!shotPrefab || !Player) return;
        var from = transform.position + new Vector3(Facing * 0.4f, -0.1f, 0f);
        var aim = (Player.position + Vector3.up * 1.2f - from).normalized;
        var shot = Instantiate(shotPrefab, from, Quaternion.identity);
        var es = shot.GetComponent<EnemyShot>();
        if (es) es.Launch(aim * shotSpeed);
        _vel -= (Vector2)aim * 2f;                              // recoil
    }

    public override void OnHurt(Vector2 from, DamageKind kind)
    {
        if (_dead) return;
        _vel += new Vector2(Mathf.Sign(transform.position.x - from.x) * 4f, 1.5f);
        _tele = -1f;
        if (chargeGlow) chargeGlow.enabled = false;
        _shotTimer = Mathf.Max(_shotTimer, 0.8f);
    }

    public override void OnDeath(DamageKind kind)
    {
        _dead = true;
        _deadVy = 3f;
        _spin = Random.value < 0.5f ? -540f : 540f;
        if (chargeGlow) chargeGlow.enabled = false;
        Play(curlFrames, 8f, false);
        Destroy(gameObject, 2.2f);
    }

    void Fall()
    {
        _deadVy -= 22f * Time.deltaTime;
        transform.position += new Vector3(_vel.x * 0.5f, _deadVy, 0f) * Time.deltaTime;
        if (body) body.transform.Rotate(0f, 0f, _spin * Time.deltaTime);
        if (eyeLight) eyeLight.intensity = Mathf.Max(0f, eyeLight.intensity - Time.deltaTime * 2f);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_dead && body)
        {
            var c = body.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.7f);
            body.color = c;
            if (rim) rim.enabled = false;
        }
    }
}
