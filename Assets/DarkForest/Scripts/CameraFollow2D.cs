using UnityEngine;

/// <summary>Smooth side-scroller camera with look-ahead, clamped to the level bounds.</summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = new Vector2(0f, 2.2f);
    public float smoothTimeX = 0.18f;
    public float smoothTimeY = 0.3f;
    public float lookAhead = 2.5f;
    [Tooltip("Pixels per unit to snap to (0 = off).")]
    public float pixelSnap = 30f;
    public float lookAheadSmoothing = 3f;

    [Header("Level Bounds (world units)")]
    public bool clampToBounds = true;
    public Vector2 boundsMin = new Vector2(0f, 0f);
    public Vector2 boundsMax = new Vector2(120f, 40f);

    Camera _cam;
    Rigidbody2D _targetBody;
    float _lookAheadX;
    Vector2 _velocity;

    static float _shakeAmount;
    static float _shakeTime;

    /// <summary>Kick a short screen shake (falling beams, fire jets...).</summary>
    /// <summary>Player setting (0 = off).</summary>
    public static float ShakeScale = 1f;

    public static void Shake(float amount, float duration)
    {
        amount *= ShakeScale;
        if (amount <= 0f) return;
        _shakeAmount = Mathf.Max(_shakeAmount, amount);
        _shakeTime = Mathf.Max(_shakeTime, duration);
    }

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (target) _targetBody = target.GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (target) transform.position = Clamp(Desired(), 0f);
    }

    void LateUpdate()
    {
        if (!target) return;

        float vx = _targetBody ? _targetBody.linearVelocity.x : 0f;
        float aim = Mathf.Abs(vx) > 0.5f ? Mathf.Sign(vx) * lookAhead : _lookAheadX;
        _lookAheadX = Mathf.Lerp(_lookAheadX, aim, Time.deltaTime * lookAheadSmoothing);

        Vector2 desired = Desired();
        Vector3 pos = transform.position;
        float dt = TimeRewind.Rewinding ? Time.unscaledDeltaTime : Time.deltaTime;   // keeps following the boy back in time
        pos.x = Mathf.SmoothDamp(pos.x, desired.x, ref _velocity.x, smoothTimeX, Mathf.Infinity, dt);
        pos.y = Mathf.SmoothDamp(pos.y, desired.y, ref _velocity.y, smoothTimeY, Mathf.Infinity, dt);
        var clamped = Clamp(pos, pos.z);

        if (_shakeTime > 0f)
        {
            _shakeTime -= Time.deltaTime;
            clamped += (Vector3)(Random.insideUnitCircle * _shakeAmount);
            if (_shakeTime <= 0f) _shakeAmount = 0f;
        }
        // Snap to the art's pixel grid (30 px per unit) so sprites never sit between pixels and look soft.
        if (pixelSnap > 0f)
            clamped = new Vector3(Mathf.Round(clamped.x * pixelSnap) / pixelSnap, Mathf.Round(clamped.y * pixelSnap) / pixelSnap, clamped.z);
        transform.position = clamped;
    }

    Vector2 Desired() => (Vector2)target.position + offset + Vector2.right * _lookAheadX;

    Vector3 Clamp(Vector2 p, float z)
    {
        if (z == 0f) z = transform.position.z;
        if (!clampToBounds) return new Vector3(p.x, p.y, z);
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;
        float x = boundsMax.x - boundsMin.x > halfW * 2f ? Mathf.Clamp(p.x, boundsMin.x + halfW, boundsMax.x - halfW) : (boundsMin.x + boundsMax.x) * 0.5f;
        float y = Mathf.Clamp(p.y, boundsMin.y + halfH, Mathf.Max(boundsMin.y + halfH, boundsMax.y - halfH));
        return new Vector3(x, y, z);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 1f);
        var c = (boundsMin + boundsMax) * 0.5f;
        Gizmos.DrawWireCube(c, boundsMax - boundsMin);
    }
}
