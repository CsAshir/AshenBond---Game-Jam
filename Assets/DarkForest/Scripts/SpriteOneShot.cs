using UnityEngine;

/// <summary>Plays a sprite-sheet effect once (or loops) and then removes the object.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOneShot : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 16f;
    public bool loop;
    [Tooltip("Extra seconds to keep the object after the last frame (lets lights / particles fade).")]
    public float linger = 0.4f;

    SpriteRenderer _sr;
    float _t;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (frames != null && frames.Length > 0) _sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        _t += Time.deltaTime;
        int i = (int)(_t * fps);
        if (loop) { _sr.sprite = frames[i % frames.Length]; return; }
        if (i < frames.Length) _sr.sprite = frames[i];
        else
        {
            _sr.enabled = false;
            if (_t > frames.Length / fps + linger) Destroy(gameObject);   // effect prefabs keep the sheet on their root
        }
    }
}
