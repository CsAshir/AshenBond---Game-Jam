using UnityEngine;

/// <summary>
/// Spore Shade (Area B, shadow theme): a rooted mushroom grown out of a shadow pool. When the boy comes close it
/// clamps its cap shut, then bursts a fan of shadow spores that arc and fall. Sheet (11): 0-2 idle, 3-6 cap shut,
/// 7-10 burst.
/// </summary>
public class MushroomAI : EnemyBrain
{
    public Sprite[] attackFrames;
    public GameObject sporePrefab;
    public float triggerRange = 6.5f;
    public float cooldown = 2.8f;
    public int spores = 3;
    public float sporeSpeed = 6f;

    Sprite[] _idle, _shut, _burst;
    float _cd;
    int _phase;          // 0 idle, 1 shutting, 2 bursting
    bool _fired;
    bool _dead;

    protected override void Start()
    {
        base.Start();
        _idle = attackFrames[..3];
        _shut = attackFrames[3..7];
        _burst = attackFrames[7..];
        _cd = cooldown * 0.5f;
        float g = GroundAt(transform.position.x, transform.position.y);
        if (!float.IsNaN(g)) transform.position = new Vector3(transform.position.x, g, transform.position.z);
    }

    void Update()
    {
        if (_dead) return;
        if (Waking) { Play(_idle, 6f); return; }
        float dx = DistToPlayerX;
        Face(dx);
        _cd -= Time.deltaTime;
        switch (_phase)
        {
            case 0:
                Play(_idle, 6f);
                if (_cd <= 0f && Player && Mathf.Abs(dx) < triggerRange && Mathf.Abs(DistToPlayerY) < 4f) { _phase = 1; Play(_shut, 9f, false); }
                break;
            case 1:
                if (Finished) { _phase = 2; _fired = false; Play(_burst, 12f, false); }
                break;
            case 2:
                if (!_fired) { _fired = true; Burst(); }
                if (Finished) { _phase = 0; _cd = cooldown; }
                break;
        }
        ContactDamage();
    }

    void Burst()
    {
        if (!sporePrefab) return;
        CameraFollow2D.Shake(0.05f, 0.1f);
        for (int i = 0; i < spores; i++)
        {
            float a = Mathf.Lerp(55f, 110f, spores == 1 ? 0.5f : i / (float)(spores - 1));   // fan, leaning toward the boy
            var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * Facing, Mathf.Sin(a * Mathf.Deg2Rad));
            var s = Instantiate(sporePrefab, transform.position + Vector3.up * 0.9f, Quaternion.identity);
            var shot = s.GetComponent<EnemyShot>();
            if (shot) shot.Launch(dir * sporeSpeed * Random.Range(0.85f, 1.1f));
        }
    }

    public override void OnDeath(DamageKind kind)
    {
        _dead = true;
        Destroy(gameObject, 1.2f);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_dead && body)
        {
            body.transform.localScale = Vector3.Lerp(body.transform.localScale, new Vector3(body.transform.localScale.x * 1.3f, 0.01f, 1f), Time.deltaTime * 4f);
            if (rim) rim.enabled = false;
        }
    }
}
