using UnityEngine;

/// <summary>The skeleton's shadow sword: spins out like a boomerang, hangs a moment, then comes back to its owner.</summary>
public class SpinningBlade : MonoBehaviour
{
    public Sprite[] spinFrames;
    public Sprite[] breakFrames;
    public SpriteRenderer art;
    public float distance = 7f;
    public float outTime = 0.75f;
    public float hang = 0.25f;
    public float hitRadius = 0.6f;

    Transform _owner;
    Vector3 _start;
    float _dir;
    float _t;
    bool _breaking;

    public void Launch(Transform owner, float dir)
    {
        _owner = owner;
        _dir = dir;
        _start = transform.position;
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_breaking)
        {
            int b = (int)(_t * 18f);
            if (b >= breakFrames.Length) { Destroy(gameObject); return; }
            art.sprite = breakFrames[b];
            return;
        }
        if (!_owner) { Break(); return; }
        art.sprite = spinFrames[(int)(_t * 20f) % spinFrames.Length];

        // Out (ease-out), hang, back (ease-in) toward wherever the owner is now.
        Vector3 far = _start + new Vector3(_dir * distance, 0.15f, 0f);
        if (_t < outTime) transform.position = Vector3.Lerp(_start, far, 1f - (1f - _t / outTime) * (1f - _t / outTime));
        else if (_t < outTime + hang) transform.position = far;
        else
        {
            var home = _owner.position + Vector3.up * 1.2f;
            transform.position = Vector3.MoveTowards(transform.position, home, (6f + (_t - outTime - hang) * 10f) * Time.deltaTime);
            if (Vector3.Distance(transform.position, home) < 0.3f) { Destroy(gameObject); return; }
        }

        foreach (var c in Physics2D.OverlapCircleAll(transform.position, hitRadius))
        {
            var pc = c.GetComponentInParent<PlayerController2D>();
            if (!pc) continue;
            var hp = pc.GetComponent<PlayerHealth>();
            if (hp) hp.Damage(1, transform.position);
            break;
        }
    }

    void Break()
    {
        _breaking = true;
        _t = 0f;
    }
}
