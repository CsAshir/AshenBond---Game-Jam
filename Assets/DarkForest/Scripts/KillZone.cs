using UnityEngine;

/// <summary>Trigger that sends the player back to the spawn point (lava, pits).</summary>
[RequireComponent(typeof(Collider2D))]
public class KillZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other) => Kill(other);

    // Stay too: a pit must catch the boy even if he fell in while still blinking from an enemy hit.
    void OnTriggerStay2D(Collider2D other) => Kill(other);

    static void Kill(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController2D>();
        if (!player) return;
        var hp = player.GetComponent<PlayerHealth>();
        if (hp) hp.Hurt(true);
        else player.Hurt();
    }
}
