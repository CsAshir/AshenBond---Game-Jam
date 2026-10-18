using UnityEngine;

/// <summary>
/// The Executioner's summon: a small shadow puppet that drifts after the boy with a wobble. One fire shot or a
/// touch of the sister's orb pops it; it fades on its own after a while.
/// </summary>
public class ShadowWisp : EnemyBrain
{
    protected override bool Reacts => false;

    public Sprite[] appearFrames, idleFrames, deathFrames;
    public float speed = 2.4f;
    public float life = 9f;

    protected override bool IsWalker => false;
    float _age;
    bool _dead;
    float _phase;

    protected override void Start()
    {
        base.Start();
        _phase = Random.value * 10f;
        Play(appearFrames, 10f, false);
    }

    void Update()
    {
        if (_dead || TimeRewind.Rewinding) return;
        _age += Time.deltaTime;
        if (Finished || _age > 0.6f) Play(idleFrames, 8f);
        if (_age > life) { Health?.Hit(99, DamageKind.Shadow, transform.position); return; }
        if (Waking || !Player) return;
        Vector2 to = (Vector2)(Player.position + Vector3.up * 1.4f - transform.position);
        Face(to.x);
        var v = to.normalized * speed + new Vector2(0f, Mathf.Sin(Time.time * 3f + _phase) * 1.2f);
        transform.position += (Vector3)(v * Time.deltaTime);
        ContactDamage();
    }

    public override void OnDeath(DamageKind kind)
    {
        _dead = true;
        Play(deathFrames, 12f, false);
        Destroy(gameObject, 0.6f);
    }
}
