using UnityEngine;

/// <summary>Minimal looping flipbook for a SpriteRenderer.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 8f;
    [Tooltip("Stops while the bell freezes time (Area C clockwork).")]
    public bool obeysTimeFreeze;

    SpriteRenderer _renderer;
    float _clock;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        if (TimeRewind.Rewinding) _clock -= Time.unscaledDeltaTime * TimeRewind.PlaybackSpeed;   // clockwork spins backwards
        else _clock += obeysTimeFreeze ? TimeFreeze.Delta : Time.deltaTime;
        int index = ((int)Mathf.Floor(_clock * framesPerSecond) % frames.Length + frames.Length) % frames.Length;
        _renderer.sprite = frames[index];
    }
}
