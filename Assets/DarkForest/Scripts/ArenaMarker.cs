using UnityEngine;

/// <summary>
/// Marks a long, flat stretch reserved for an enemy encounter (Ember Hound etc.).
/// Purely a level-design guide for now: draws a red box + label in the Scene view.
/// </summary>
public class ArenaMarker : MonoBehaviour
{
    public string enemy = "Ember Hound";
    public Vector2 size = new Vector2(16f, 6f);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawCube(transform.position + new Vector3(0f, size.y / 2f, 0f), new Vector3(size.x, size.y, 0.1f));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(transform.position + new Vector3(0f, size.y / 2f, 0f), new Vector3(size.x, size.y, 0.1f));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + new Vector3(-size.x / 2f, size.y + 0.6f, 0f), "ENEMY ARENA: " + enemy);
#endif
    }
}
