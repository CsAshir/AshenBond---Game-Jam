using UnityEngine;

/// <summary>Trigger that moves the player's respawn point here when touched.</summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController2D>();
        if (player) player.SetSpawn(transform.position);
    }
}
