using UnityEngine;

/// <summary>
/// The title screen's living scene: the camera drifts slowly along the burning village (the parallax layers move
/// at their own speeds), Ilyan stands at the right holding the lantern, and Mira's shadow breathes and sways
/// behind him, her glow pulsing with the flame.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    public Transform cameraTransform;
    public float drift = 0.7f;
    public float loopAfter = 60f;
    public Transform mira;
    public SpriteRenderer miraRenderer;
    public float miraBob = 0.18f;

    Vector3 _camStart, _miraBase;
    float _t;
    float _miraAlpha;

    void Start()
    {
        if (cameraTransform) _camStart = cameraTransform.position;
        if (mira) _miraBase = mira.localPosition;
        if (miraRenderer) _miraAlpha = miraRenderer.color.a;
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime;
        if (cameraTransform)
        {
            float x = Mathf.Repeat(_t * drift, loopAfter);
            cameraTransform.position = _camStart + new Vector3(x, Mathf.Sin(_t * 0.2f) * 0.3f, 0f);
        }
        if (mira) mira.localPosition = _miraBase + new Vector3(Mathf.Sin(_t * 0.6f) * 0.08f, Mathf.Sin(_t * 1.1f) * miraBob, 0f);
        if (miraRenderer)
        {
            var c = miraRenderer.color;
            c.a = _miraAlpha * (0.8f + 0.2f * Mathf.Sin(_t * 1.7f));
            miraRenderer.color = c;
        }
    }
}
