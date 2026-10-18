using UnityEngine;

/// <summary>
/// Hit-stop: the whole game freezes for a few hundredths of a second on impact (Hollow Knight / Celeste feel).
/// Longer freezes win over shorter ones; real time is used so the freeze can end.
/// </summary>
public class HitStop : MonoBehaviour
{
    static HitStop _runner;
    float _until;

    public static void Do(float seconds)
    {
        if (seconds <= 0f) return;
        if (!_runner)
        {
            var go = new GameObject("HitStop");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<HitStop>();
        }
        _runner._until = Mathf.Max(_runner._until, Time.unscaledTime + seconds);
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (Time.timeScale == 0f && Time.unscaledTime >= _until) Time.timeScale = 1f;
    }

    void OnDisable()
    {
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }
}
