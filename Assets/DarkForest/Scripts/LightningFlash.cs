using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Horror ambience: random distant lightning — the night briefly flashes and a bolt strikes the horizon.</summary>
public class LightningFlash : MonoBehaviour
{
    public Light2D globalLight;
    public SpriteRenderer skyFlash;
    public GameObject[] boltPrefabs;
    public Transform boltArea;            // usually the camera
    public Vector2 interval = new Vector2(6f, 13f);
    public float flashBoost = 3.2f;

    float _next;
    float _base;
    float _flash;
    float _skyAlpha;

    void Start()
    {
        if (globalLight) _base = globalLight.intensity;
        if (skyFlash) _skyAlpha = skyFlash.color.a;
        _next = Time.time + Random.Range(2.5f, 5f);
    }

    void Update()
    {
        if (Time.time >= _next)
        {
            _next = Time.time + Random.Range(interval.x, interval.y);
            _flash = 1f;
            if (boltPrefabs != null && boltPrefabs.Length > 0 && boltArea)
            {
                var p = boltArea.position + new Vector3(Random.Range(-13f, 13f), Random.Range(1.5f, 4f), 0f);
                p.z = 60f;
                Instantiate(boltPrefabs[Random.Range(0, boltPrefabs.Length)], p, Quaternion.identity);
            }
        }

        // Double-flicker decay, like real lightning.
        _flash = Mathf.Max(0f, _flash - Time.deltaTime * 3.5f);
        float f = _flash * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(_flash * 40f)));
        if (globalLight) globalLight.intensity = _base * (1f + f * flashBoost);
        if (skyFlash)
        {
            var c = skyFlash.color;
            c.a = _skyAlpha * f;
            skyFlash.color = c;
        }
    }
}
