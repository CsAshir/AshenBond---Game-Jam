using UnityEngine;

/// <summary>A ground shockwave from the beast's slam: runs along the floor, the boy must jump it.</summary>
public class ShockWave : MonoBehaviour
{
    public float direction = 1f;
    public float speed = 7f;
    public float minX = -999f, maxX = 999f;
    public float life = 2.2f;
    public Vector2 hitSize = new Vector2(1f, 1.1f);
    public SpriteRenderer art;
    public Sprite[] frames;
    public float fps = 18f;

    float _t;

    void Update()
    {
        if (TimeRewind.Rewinding) return;
        _t += TimeFreeze.Delta;
        transform.position += Vector3.right * direction * speed * TimeFreeze.Delta;
        if (art)
        {
            art.flipX = direction < 0f;
            if (frames != null && frames.Length > 0) art.sprite = frames[Mathf.Min(frames.Length - 1, (int)(_t * fps)) % frames.Length];
            var c = art.color;
            c.a = Mathf.Clamp01((life - _t) / 0.3f);
            art.color = c;
        }
        float x = transform.position.x;
        if (_t > life || x < minX || x > maxX) { Destroy(gameObject); return; }
        foreach (var c in Physics2D.OverlapBoxAll((Vector2)transform.position + Vector2.up * hitSize.y * 0.5f, hitSize, 0f))
        {
            var hp = c.GetComponentInParent<PlayerHealth>();
            if (hp) { hp.Damage(1, transform.position - Vector3.right * direction); break; }
        }
    }
}
