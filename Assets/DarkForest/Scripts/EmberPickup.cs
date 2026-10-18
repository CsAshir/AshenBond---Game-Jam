using UnityEngine;

/// <summary>Floating ember the player collects by touching it. Adds to the ember count shown by AreaHUD.</summary>
[RequireComponent(typeof(Collider2D))]
public class EmberPickup : MonoBehaviour
{
    public static int Collected;
    public GameObject collectBurst;
    public int amount = 1;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.GetComponentInParent<PlayerController2D>()) return;
        Collected += amount;
        Sfx.Play("ember", transform.position);
        if (collectBurst) Instantiate(collectBurst, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCount() => Collected = 0;
}
