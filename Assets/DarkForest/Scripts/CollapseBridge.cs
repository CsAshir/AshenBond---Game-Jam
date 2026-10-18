using UnityEngine;

/// <summary>
/// The tower chase (GDD Area C): a long plank bridge over the abyss. Stepping on it starts the collapse -
/// planks shake and drop one after another just behind the boy, slightly slower than his run, so he has to keep
/// moving. Falling sends him to the checkpoint and the bridge rebuilds itself.
/// </summary>
public class CollapseBridge : MonoBehaviour, IRewindable
{
    public Transform[] planks;
    public float startDelay = 0.7f;
    public float interval = 0.24f;        // ~4.2 planks per second: a steady walk (4.4) escapes, running (6.6) feels safe
    public float shakeTime = 0.22f;
    public float gravity = 30f;

    Vector3[] _home;
    Quaternion[] _homeRot;
    float[] _vel;
    float[] _spin;
    bool _running;
    float _t;

    void Awake()
    {
        _home = new Vector3[planks.Length];
        _homeRot = new Quaternion[planks.Length];
        _vel = new float[planks.Length];
        _spin = new float[planks.Length];
        for (int i = 0; i < planks.Length; i++) { _home[i] = planks[i].position; _homeRot[i] = planks[i].rotation; }
        PlayerController2D.Respawned += Rebuild;
    }

    void OnDestroy() => PlayerController2D.Respawned -= Rebuild;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_running || !other.GetComponentInParent<PlayerController2D>()) return;
        _running = true;
        _t = 0f;
        CameraFollow2D.Shake(0.2f, 0.4f);
    }

    void Update()
    {
        if (!_running) return;
        _t += TimeFreeze.Delta;
        for (int i = 0; i < planks.Length; i++)
        {
            float k = _t - startDelay - i * interval;
            if (k < 0f) break;
            var p = planks[i];
            if (k < shakeTime)
            {
                p.position = _home[i] + new Vector3(Mathf.Sin(Time.time * 70f + i) * 0.05f, -k * 0.3f, 0f);
                continue;
            }
            var col = p.GetComponent<Collider2D>();
            if (col && col.enabled)
            {
                col.enabled = false;
                _spin[i] = Random.Range(-220f, 220f);
                if (i % 4 == 0) { CameraFollow2D.Shake(0.05f, 0.08f); Sfx.Play("collapse", p.position, 0.7f); }
            }
            _vel[i] += gravity * TimeFreeze.Delta;
            p.position += Vector3.down * _vel[i] * TimeFreeze.Delta;
            p.Rotate(0f, 0f, _spin[i] * TimeFreeze.Delta);
        }
    }

    class State
    {
        public bool running;
        public float t;
        public Vector3[] pos;
        public Quaternion[] rot;
        public float[] vel, spin;
        public bool[] solid;
    }

    public object CaptureState()
    {
        var s = new State { running = _running, t = _t, pos = new Vector3[planks.Length], rot = new Quaternion[planks.Length],
                            vel = (float[])_vel.Clone(), spin = (float[])_spin.Clone(), solid = new bool[planks.Length] };
        for (int i = 0; i < planks.Length; i++)
        {
            s.pos[i] = planks[i].position;
            s.rot[i] = planks[i].rotation;
            var c = planks[i].GetComponent<Collider2D>();
            s.solid[i] = c && c.enabled;
        }
        return s;
    }

    /// <summary>Rewind: fallen planks fly back up into the bridge.</summary>
    public void RestoreState(object state)
    {
        var s = (State)state;
        _running = s.running;
        _t = s.t;
        for (int i = 0; i < planks.Length; i++)
        {
            planks[i].SetPositionAndRotation(s.pos[i], s.rot[i]);
            _vel[i] = s.vel[i];
            _spin[i] = s.spin[i];
            var c = planks[i].GetComponent<Collider2D>();
            if (c) c.enabled = s.solid[i];
        }
    }

    void Rebuild()
    {
        _running = false;
        for (int i = 0; i < planks.Length; i++)
        {
            planks[i].position = _home[i];
            planks[i].rotation = _homeRot[i];
            _vel[i] = 0f;
            var col = planks[i].GetComponent<Collider2D>();
            if (col) col.enabled = true;
        }
    }
}
