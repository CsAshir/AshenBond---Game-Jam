using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hangs the animated lantern from the boy's hand. Every boy frame has a recorded hand position
/// (from the sprite processing), so the lantern follows the hand through idle, run and charge.
/// It swings with movement like a real lantern on a ring.
/// </summary>
public class LanternFollower : MonoBehaviour
{
    public SpriteRenderer boy;
    public Rigidbody2D body;
    public Sprite[] frameKeys;
    [Tooltip("x, y = hand position in units from the boy's feet; z = 1 visible in front, 0 = behind the body")]
    public Vector3[] anchors;
    public int frontOrder = 13;
    public int backOrder = 11;
    public float swing = 3.2f;
    public float maxSwing = 28f;

    readonly Dictionary<Sprite, Vector3> _map = new Dictionary<Sprite, Vector3>();
    SpriteRenderer[] _renderers;
    float _angle;
    float _angVel;
    Vector3 _last = new Vector3(0.45f, 1.2f, 1f);

    void Awake()
    {
        for (int i = 0; i < frameKeys.Length && i < anchors.Length; i++)
            if (frameKeys[i]) _map[frameKeys[i]] = anchors[i];
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void LateUpdate()
    {
        if (!boy) return;
        if (boy.sprite && _map.TryGetValue(boy.sprite, out var a)) _last = a;
        float dir = boy.flipX ? -1f : 1f;
        transform.localPosition = new Vector3(_last.x * dir, _last.y, 0f);

        // Pendulum: the lantern lags behind horizontal motion and settles back.
        float vx = body ? body.linearVelocity.x : 0f;
        float vy = body ? body.linearVelocity.y : 0f;
        float target = Mathf.Clamp(vx * swing + vy * 0.4f * dir, -maxSwing, maxSwing) + Mathf.Sin(Time.time * 2.4f) * 2f;
        _angVel += (target - _angle) * 60f * Time.deltaTime;
        _angVel *= Mathf.Exp(-6f * Time.deltaTime);
        _angle += _angVel * Time.deltaTime;
        transform.localRotation = Quaternion.Euler(0f, 0f, _angle);

        int order = _last.z > 0.5f ? frontOrder : backOrder;
        foreach (var r in _renderers)
            if (r) r.sortingOrder = order + (r.gameObject == gameObject ? 0 : -1);
    }
}
