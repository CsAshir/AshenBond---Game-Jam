using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Time power (all areas): three rewind charges. HOLD Q (gamepad: left trigger) and the world runs backwards
/// for as long as it is held, up to 3 seconds per charge: the boy, his hearts, enemies and their shots, pendulums,
/// lifts, crushers, rolling gears, falling trees and the collapsing bridge. Release to resume from that moment. Gears spin in reverse while it plays (sepia, lens
/// warp, after-images). Deaths are final: a slain enemy stays slain (keeps arenas fair), and history is wiped
/// on respawn, so you cannot rewind out of a pit.
/// </summary>
[DefaultExecutionOrder(1000)]   // after every script has posed its sprites, so playback wins
public class TimeRewind : MonoBehaviour
{
    public static readonly List<Rewindable> All = new();
    public static bool Rewinding { get; private set; }
    /// <summary>Rewindable timeline clock: game time that jumps back on a rewind.</summary>
    public static float Now { get; private set; }
    /// <summary>How fast the past plays back (timeline seconds per real second); gears use it to spin in reverse.</summary>
    public static float PlaybackSpeed { get; private set; } = 1f;

    public int maxCharges = 3;
    public float window = 3f;
    [Tooltip("Timeline seconds rewound per real second while Q is held.")]
    public float rewindSpeed = 1.6f;
    [Tooltip("Only used by TryRewind() (scripted / test): plays the full window back over this time.")]
    public float playbackTime = 0.9f;
    public float recordRate = 30f;
    public Volume volume;
    public AfterImageTrail trail;
    public GameObject startFx;               // clock ring opening around the boy as time resumes
    public Transform player;

    public int Charges { get; private set; }

    float _nextRecord;
    float _from, _to, _p;
    float _flash;
    bool _auto;          // scripted rewind (no key held): play the whole window back

    void Awake()
    {
        Charges = maxCharges;
        Now = 0f;
        All.RemoveAll(r => !r);
        Rewinding = false;
        PlayerController2D.Respawned += Forget;
    }

    void OnDestroy()
    {
        PlayerController2D.Respawned -= Forget;
        if (Rewinding) Time.timeScale = 1f;
        Rewinding = false;
    }

    void Forget()
    {
        foreach (var r in All) r.Clear();
    }

    static bool Held()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && k.qKey.isPressed) || (g != null && g.leftTrigger.isPressed);
    }

    bool _wasHeld;

    void Update()
    {
        bool held = Held() && !DialogueDirector.Active && !PauseMenu.Paused;
        bool pressed = held && !_wasHeld;          // own edge detection: never misses a frame
        _wasHeld = held;
        if (Rewinding) return;
        Now += Time.deltaTime;
        if (pressed) Begin(false);
        if (Rewinding || Time.timeScale <= 0f || Now < _nextRecord) return;
        _nextRecord = Now + 1f / recordRate;
        int max = Mathf.CeilToInt(window * recordRate) + 4;
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i]) All[i].Record(Now, max);
    }

    /// <summary>Rewind the whole window without a key held (tests / scripted moments).</summary>
    public bool TryRewind() => Begin(true);

    bool Begin(bool auto)
    {
        if (Rewinding || Charges <= 0 || TimeFreeze.Active || Now < 0.5f) { _flash = 0.6f; return false; }
        Charges--;
        Rewinding = true;
        _auto = auto;
        _from = Now;
        _to = Mathf.Max(0f, Now - window);
        _p = 0f;
        PlaybackSpeed = auto ? (_from - _to) / playbackTime : rewindSpeed;
        if (trail) trail.enabled = true;
        CameraFollow2D.Shake(0.15f, 0.25f);
        Sfx.Play("rewind");
        return true;
    }

    void LateUpdate()
    {
        if (volume) volume.weight = Mathf.MoveTowards(volume.weight, Rewinding ? 1f : 0f, Time.unscaledDeltaTime * (Rewinding ? 8f : 3f));
        _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime);
        if (!Rewinding) return;

        Time.timeScale = 0f;                      // everything holds still; we pose the past ourselves
        if (_auto)
        {
            _p = Mathf.Min(1f, _p + Time.unscaledDeltaTime / playbackTime);
            Now = Mathf.Lerp(_from, _to, 1f - (1f - _p) * (1f - _p));
        }
        else
        {
            // Runs back while held; stops at the 3 s limit or the moment Q is released.
            Now = Mathf.Max(_to, Now - Time.unscaledDeltaTime * rewindSpeed);
            _p = Mathf.InverseLerp(_from, _to, Now);
        }
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i]) All[i].Show(Now);
        if (_auto ? _p >= 1f : (!Held() || Now <= _to)) Finish();
    }

    void Finish()
    {
        float t = Now;
        // Things born after the target never happened.
        var gone = new List<Rewindable>();
        foreach (var r in All) if (r && r.BornAfter(t)) gone.Add(r);
        foreach (var r in gone) Destroy(r.gameObject);
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] && !gone.Contains(All[i])) All[i].Restore(t);
        _nextRecord = Now;
        Rewinding = false;
        Time.timeScale = 1f;
        if (trail) trail.enabled = TimeFreeze.Active;
        if (startFx && player) Instantiate(startFx, player.position + Vector3.up * 1.4f, Quaternion.identity);
    }

    /// <summary>0..1 left of the current charge (drains while Q is held), 1 when idle.</summary>
    public float CurrentCharge01 => Rewinding ? 1f - _p : 1f;

    void OnGUI()
    {
        MenuUI.ApplyFont();
        if (!Rewinding) return;
        float s = Screen.height / 1080f;
        var big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(44 * s), fontStyle = FontStyle.Bold };
        GUI.color = new Color(1f, 0.85f, 0.5f, 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 18f));
        GUI.Label(new Rect(0, Screen.height * 0.16f, Screen.width, 60 * s), "<<  REWIND", big);
        float w = 360 * s, bx = (Screen.width - w) / 2f, by = Screen.height * 0.16f + 64 * s;
        GUI.color = new Color(0.2f, 0.15f, 0.1f, 0.6f);
        GUI.DrawTexture(new Rect(bx, by, w, 8 * s), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.82f, 0.45f, 0.95f);
        GUI.DrawTexture(new Rect(bx, by, w * (1f - _p), 8 * s), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
