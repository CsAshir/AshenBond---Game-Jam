using UnityEngine;

/// <summary>Removes the object after a delay (effect roots whose animated art sits on a child).</summary>
public class SelfDestruct : MonoBehaviour
{
    public float seconds = 1f;
    void Start() => Destroy(gameObject, seconds);
}
