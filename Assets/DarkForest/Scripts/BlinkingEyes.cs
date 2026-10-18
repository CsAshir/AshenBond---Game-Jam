using UnityEngine;

/// <summary>A pair of glowing eyes in the dark that blink, and sometimes vanish when you get close.</summary>
public class BlinkingEyes : MonoBehaviour
{
    public SpriteRenderer[] eyes;
    public Transform player;
    public float hideDistance = 6f;

    float _nextBlink;
    float _blinkT;
    float _visible = 1f;
    float[] _alpha;

    void Start()
    {
        _alpha = new float[eyes.Length];
        for (int i = 0; i < eyes.Length; i++) _alpha[i] = eyes[i].color.a;
        _nextBlink = Time.time + Random.Range(1f, 4f);
        if (!player)
        {
            var p = GameObject.Find("Player");
            if (p) player = p.transform;
        }
    }

    void Update()
    {
        if (Time.time > _nextBlink) { _blinkT = 0.14f; _nextBlink = Time.time + Random.Range(1.5f, 5f); }
        _blinkT -= Time.deltaTime;
        bool near = player && Mathf.Abs(player.position.x - transform.position.x) < hideDistance;
        _visible = Mathf.MoveTowards(_visible, near ? 0f : 1f, Time.deltaTime * (near ? 3f : 0.4f));
        float k = (_blinkT > 0f ? 0f : 1f) * _visible;
        for (int i = 0; i < eyes.Length; i++)
        {
            var c = eyes[i].color;
            c.a = _alpha[i] * k;
            eyes[i].color = c;
        }
    }
}
