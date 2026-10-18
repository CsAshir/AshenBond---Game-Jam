using UnityEngine;

/// <summary>
/// Moves a layer with a fraction of the camera's motion (orthographic parallax).
///   1.0        sky / moon, glued to the camera (infinitely far)
///   0.7 - 0.9  far background
///   0.4 - 0.6  mid background
///   0          play area, fixed in the world
///   -0.1 - -0.3 foreground, slides the opposite way so it feels very close
/// When repeatWidth > 0 the layer re-centres itself so its tiled children never run out.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    public Transform cameraTransform;
    [Range(-0.5f, 1f)] public float factorX = 0.5f;
    [Range(-0.5f, 1f)] public float factorY = 0.8f;
    [Tooltip("World width after which the layer pattern repeats (0 = no wrapping).")]
    public float repeatWidth;

    Vector3 _startPos;
    Vector3 _camStart;
    float _wrapOffset;

    void Start()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        _startPos = transform.position;
        _camStart = cameraTransform ? cameraTransform.position : Vector3.zero;
    }

    void LateUpdate()
    {
        if (!cameraTransform) return;

        Vector3 delta = cameraTransform.position - _camStart;
        float x = _startPos.x + _wrapOffset + delta.x * factorX;

        if (repeatWidth > 0f)
        {
            float rel = cameraTransform.position.x - x;
            if (rel > repeatWidth * 0.5f) _wrapOffset += repeatWidth;
            else if (rel < -repeatWidth * 0.5f) _wrapOffset -= repeatWidth;
            x = _startPos.x + _wrapOffset + delta.x * factorX;
        }

        float y = _startPos.y + delta.y * factorY;
        transform.position = new Vector3(x, y, transform.position.z);
    }
}
