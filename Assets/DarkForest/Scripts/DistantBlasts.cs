using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Area A's answer to Area B's lightning: every few seconds something in the burning village behind you
/// explodes — a flash on the horizon, a fireball, embers and a smoke column, plus a faint camera rumble.
/// Blasts are spawned as children of a parallax layer so they move with the background.
/// </summary>
public class DistantBlasts : MonoBehaviour
{
    public Transform layer;              // parallax layer the blasts belong to
    public Transform cameraTransform;
    public GameObject blastPrefab;
    public Light2D globalLight;
    public Vector2 interval = new Vector2(5f, 11f);
    public Vector2 heightRange = new Vector2(-1.5f, 1.5f);   // relative to the layer's village line
    public float flashBoost = 0.9f;
    public float rumble = 0.06f;

    float _next;
    float _base;
    float _flash;

    void Start()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        if (globalLight) _base = globalLight.intensity;
        _next = Time.time + Random.Range(2f, 4f);
    }

    void Update()
    {
        if (Time.time >= _next && blastPrefab && layer && cameraTransform)
        {
            _next = Time.time + Random.Range(interval.x, interval.y);
            var p = new Vector3(cameraTransform.position.x + Random.Range(-14f, 14f),
                                layer.position.y + Random.Range(heightRange.x, heightRange.y),
                                layer.position.z - 1f);
            var b = Instantiate(blastPrefab, p, Quaternion.identity, layer);
            b.transform.localScale = blastPrefab.transform.localScale * Random.Range(0.8f, 1.25f) / Mathf.Max(0.01f, layer.lossyScale.x);
            _flash = 1f;
            CameraFollow2D.Shake(rumble, 0.35f);
        }
        _flash = Mathf.Max(0f, _flash - Time.deltaTime * 2.2f);
        if (globalLight) globalLight.intensity = _base * (1f + _flash * flashBoost);
    }
}
