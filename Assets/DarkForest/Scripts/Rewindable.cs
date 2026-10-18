using System.Collections.Generic;
using UnityEngine;

/// <summary>A component whose private clock / state must travel back with the rewind (pendulum phase, hearts...).</summary>
public interface IRewindable
{
    object CaptureState();
    void RestoreState(object state);
}

/// <summary>
/// Records this object's recent past for <see cref="TimeRewind"/>: position, rotation, velocity, the sprite shown,
/// plus the state of every <see cref="IRewindable"/> on it. Objects that did not exist yet at the rewind target
/// (shots, blades, bombs) are removed when time runs back past their birth.
/// </summary>
[DisallowMultipleComponent]
public class Rewindable : MonoBehaviour
{
    [Tooltip("Sprite to record (defaults to this object's renderer, else a child called Body).")]
    public SpriteRenderer sprite;
    [Tooltip("Restore the transform (off for objects whose own clock positions them, e.g. pendulums).")]
    public bool trackTransform = true;
    [Tooltip("Part of the level (the boy, enemies, mechanisms): never removed by a rewind. Off only for things spawned " +
             "at runtime (shots, bombs, blades, summons) - those vanish when time runs back past their launch.")]
    public bool persistent = true;

    struct Frame
    {
        public float t;
        public Vector3 pos;
        public Quaternion rot;
        public Vector2 vel;
        public float angVel;
        public Sprite sprite;
        public bool flip;
        public object[] states;
    }

    readonly List<Frame> _frames = new();
    Rigidbody2D _rb;
    IRewindable[] _custom;
    float _born;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _custom = GetComponents<IRewindable>();
        if (!sprite) sprite = GetComponent<SpriteRenderer>();
        if (!sprite) { var b = transform.Find("Body"); if (b) sprite = b.GetComponent<SpriteRenderer>(); }
    }

    void OnEnable()
    {
        _born = TimeRewind.Now;
        TimeRewind.All.Add(this);
    }

    // Start runs after every Awake of a freshly loaded scene (TimeRewind resets its clock there), so the birth
    // stamp is never left over from the previous area. (Before this, the boy could count as "born in the future"
    // and a long rewind deleted him.)
    void Start() => _born = TimeRewind.Now;

    void OnDisable() => TimeRewind.All.Remove(this);

    public bool BornAfter(float t) => !persistent && _born > t + 0.0001f;

    public void Record(float t, int max)
    {
        var f = new Frame { t = t, pos = transform.position, rot = transform.rotation };
        if (_rb) { f.vel = _rb.linearVelocity; f.angVel = _rb.angularVelocity; }
        if (sprite) { f.sprite = sprite.sprite; f.flip = sprite.flipX; }
        if (_custom.Length > 0)
        {
            f.states = new object[_custom.Length];
            for (int i = 0; i < _custom.Length; i++) f.states[i] = _custom[i].CaptureState();
        }
        _frames.Add(f);
        if (_frames.Count > max) _frames.RemoveAt(0);
    }

    public void Clear() => _frames.Clear();

    int IndexAt(float t)
    {
        for (int i = _frames.Count - 1; i >= 0; i--)
            if (_frames[i].t <= t) return i;
        return 0;
    }

    /// <summary>Show the object as it was at time t (visual playback; physics and scripts are paused meanwhile).</summary>
    public void Show(float t)
    {
        if (_frames.Count == 0) return;
        int i = IndexAt(t);
        var a = _frames[i];
        var b = i + 1 < _frames.Count ? _frames[i + 1] : a;
        float k = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 0f;
        if (trackTransform)
        {
            transform.SetPositionAndRotation(Vector3.Lerp(a.pos, b.pos, k), Quaternion.Slerp(a.rot, b.rot, k));
            if (_rb) _rb.position = transform.position;
        }
        if (sprite && a.sprite) { sprite.sprite = a.sprite; sprite.flipX = a.flip; }
        // Custom states during playback too, so pendulums / bridges visibly run backwards.
        if (a.states != null)
            for (int s = 0; s < _custom.Length; s++) _custom[s].RestoreState(a.states[s]);
    }

    /// <summary>Become exactly what it was at time t and forget everything after it.</summary>
    public void Restore(float t)
    {
        if (_frames.Count == 0) return;
        int i = IndexAt(t);
        var f = _frames[i];
        if (trackTransform)
        {
            transform.SetPositionAndRotation(f.pos, f.rot);
            if (_rb)
            {
                _rb.position = f.pos;
                _rb.rotation = f.rot.eulerAngles.z;
                if (_rb.bodyType == RigidbodyType2D.Dynamic) { _rb.linearVelocity = f.vel; _rb.angularVelocity = f.angVel; }
            }
        }
        if (sprite && f.sprite) { sprite.sprite = f.sprite; sprite.flipX = f.flip; }
        if (f.states != null)
            for (int s = 0; s < _custom.Length; s++) _custom[s].RestoreState(f.states[s]);
        _frames.RemoveRange(i + 1, _frames.Count - i - 1);
    }
}
