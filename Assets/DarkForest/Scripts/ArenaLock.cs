using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hollow Knight-style ambush: walking into the arena raises two walls of fire behind and ahead,
/// wave one attacks, wave two bursts in when it is cleared, and the walls collapse with an ember
/// fountain when everything is dead. A fight only ever starts once: if the boy falls back to a checkpoint the
/// gates reopen, but the survivors stay where they are (no respawn, no second boss intro) and the walls close
/// again when he walks back in.
/// </summary>
public class ArenaLock : MonoBehaviour
{
    public Collider2D[] walls;
    public ParticleSystem[] wallFire;
    public Light2D[] wallLights;
    public EnemyHealth[] wave1;
    public EnemyHealth[] wave2;
    public GameObject spawnFx;
    public GameObject clearFx;
    public string title = "Ambush";
    [Tooltip("Turned on when the arena is cleared (e.g. the door after a boss).")]
    public GameObject[] enableOnClear = new GameObject[0];
    [Tooltip("Cutscene lines played (game paused, camera on Ilyan) before the fight starts. \"Speaker|Text\".")]
    public string[] introLines = new string[0];

    enum State { Waiting, Wave1, Wave2, Cleared }
    State _state;
    bool _reopened;     // fight started, gates opened after a respawn
    float _bannerT = -1f;
    string _banner;

    void Awake()
    {
        SetWalls(false);
        foreach (var e in wave2) if (e) e.gameObject.SetActive(false);
        foreach (var e in wave1) if (e) e.gameObject.SetActive(false);
        foreach (var g in enableOnClear) if (g) g.SetActive(false);
        PlayerController2D.Respawned += OnPlayerRespawned;
    }

    void OnDestroy() => PlayerController2D.Respawned -= OnPlayerRespawned;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.GetComponentInParent<PlayerController2D>()) return;
        if (_reopened && _state != State.Cleared)
        {
            _reopened = false;
            SetWalls(true);           // the same fight resumes: nobody respawns
            return;
        }
        if (_state != State.Waiting || _starting) return;
        if (introLines != null && introLines.Length > 0)
        {
            _starting = true;
            DialogueDirector.Play(introLines, StartFight);
            return;
        }
        StartFight();
    }

    bool _starting;

    void StartFight()
    {
        _starting = false;
        _state = State.Wave1;
        SetWalls(true);
        Spawn(wave1);
        CameraFollow2D.Shake(0.35f, 0.3f);
        Banner(title);
    }

    void Update()
    {
        if (_bannerT >= 0f) _bannerT += Time.deltaTime;
        if (_state == State.Wave1 && AllDead(wave1))
        {
            if (wave2.Length > 0) { _state = State.Wave2; Spawn(wave2); Banner("More are coming"); }
            else Clear();
        }
        else if (_state == State.Wave2 && AllDead(wave2)) Clear();
    }

    void Spawn(EnemyHealth[] wave)
    {
        foreach (var e in wave)
        {
            if (!e || e.IsDead) continue;
            e.gameObject.SetActive(true);
            if (spawnFx) Instantiate(spawnFx, e.transform.position + Vector3.up * 0.8f, Quaternion.identity);
        }
    }

    static bool AllDead(EnemyHealth[] wave)
    {
        foreach (var e in wave) if (e && !e.IsDead) return false;
        return true;
    }

    void Clear()
    {
        _state = State.Cleared;
        SetWalls(false);
        foreach (var g in enableOnClear) if (g) g.SetActive(true);
        if (clearFx) Instantiate(clearFx, transform.position + Vector3.up * 2f, Quaternion.identity);
        CameraFollow2D.Shake(0.2f, 0.25f);
        Banner("The way is open");
    }

    void SetWalls(bool on)
    {
        foreach (var w in walls) if (w) w.enabled = on;
        foreach (var f in wallFire)
        {
            if (!f) continue;
            if (on) f.Play(); else f.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        foreach (var l in wallLights) if (l) l.enabled = on;
    }

    void OnPlayerRespawned()
    {
        if (_state == State.Waiting || _state == State.Cleared) return;
        // Reopen so he can walk back in; the fight keeps its progress.
        SetWalls(false);
        _reopened = true;
    }

    void Banner(string text) { _banner = text; _bannerT = 0f; }

    void OnGUI()
    {
        MenuUI.ApplyFont();
        if (_bannerT < 0f || _bannerT > 2.4f) return;
        float a = Mathf.Clamp01(_bannerT / 0.3f) * Mathf.Clamp01((2.4f - _bannerT) / 0.6f);
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.045f), fontStyle = FontStyle.Bold };
        GUI.color = new Color(1f, 0.6f, 0.3f, a);
        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, Screen.height * 0.1f), _banner, style);
    }
}
