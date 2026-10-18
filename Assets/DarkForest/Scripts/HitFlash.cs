using UnityEngine;

/// <summary>Soft impact flash: pops from a small size to a bigger one while fading out (no hard sprite edges).</summary>
public class HitFlash : MonoBehaviour
{
    public float life = 0.16f;
    public Vector3 from = new Vector3(0.4f, 0.4f, 1f);
    public Vector3 to = new Vector3(1.2f, 1.2f, 1f);

    SpriteRenderer _sr;
    float _a, _t;

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        _a = _sr ? _sr.color.a : 1f;
        transform.localScale = from;
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime;           // keeps popping through the hit-stop freeze
        float k = Mathf.Clamp01(_t / life);
        transform.localScale = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));
        if (_sr) { var c = _sr.color; c.a = _a * (1f - k) * (1f - k); _sr.color = c; }
        if (k >= 1f) Destroy(gameObject);
    }
}
