using UnityEngine;

/// <summary>Plays a dialogue once: at scene start (after a short beat) or when the boy walks into the trigger.</summary>
public class DialogueTrigger : MonoBehaviour
{
    [TextArea] public string[] lines;
    public bool playOnStart;
    [Tooltip("What the camera swings to (tutorials: the tree to shoot). Empty = Ilyan.")]
    public Transform focus;
    public float startDelay = 0.8f;
    bool _done;

    float _grounded, _waited;
    PlayerController2D _pc;

    void Start()
    {
        if (!playOnStart) return;
        _pc = FindFirstObjectByType<PlayerController2D>();
        foreach (var e in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) e.Grace(4f);   // nobody moves before the opening
    }

    void Update()
    {
        if (!playOnStart || _done) return;
        // Wait until Ilyan has landed and is standing (no frozen mid-air pose under the subtitles).
        _waited += Time.deltaTime;
        _grounded = _pc && _pc.IsGrounded && Mathf.Abs(_pc.Velocity.y) < 0.1f ? _grounded + Time.deltaTime : 0f;
        if ((_grounded > 0.35f && _waited > startDelay) || _waited > 3f) Fire();
    }

    void Fire()
    {
        if (_done) return;
        _done = true;
        DialogueDirector.Play(lines, null, focus);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!playOnStart && other.GetComponentInParent<PlayerController2D>()) Fire();
    }
}
