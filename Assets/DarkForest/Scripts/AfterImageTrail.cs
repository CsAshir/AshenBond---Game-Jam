using UnityEngine;

/// <summary>Leaves fading cyan copies of a sprite behind it (the boy moving through stopped time).</summary>
public class AfterImageTrail : MonoBehaviour
{
    public SpriteRenderer source;
    public Material material;
    public Color color = new Color(0.45f, 0.9f, 1f, 0.6f);
    public float interval = 0.07f;
    public float life = 0.45f;

    float _next;

    void Update()
    {
        // Unscaled: it also runs while the rewind holds the game at timeScale 0.
        if (!source || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + interval;
        var go = new GameObject("After Image");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.sharedMaterial = material ? material : source.sharedMaterial;
        sr.color = TimeRewind.Rewinding ? new Color(1f, 0.78f, 0.4f, 0.65f) : color;   // gold ghosts running backwards
        sr.sortingOrder = source.sortingOrder - 2;
        go.AddComponent<FadeOut>().life = life;
    }
}

/// <summary>Fades a sprite to nothing, then removes it.</summary>
public class FadeOut : MonoBehaviour
{
    public float life = 0.5f;
    SpriteRenderer _sr;
    float _t;
    float _a;

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        _a = _sr ? _sr.color.a : 1f;
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime;
        if (_sr) { var c = _sr.color; c.a = _a * (1f - _t / life); _sr.color = c; }
        if (_t >= life) Destroy(gameObject);
    }
}
