using System.IO;
using UnityEngine;

/// <summary>
/// Trailer helper: flies the camera through the level (following the ground with a soft lead and a slow zoom),
/// hides HUD / cutscenes / the boy, and writes PNG frames at a locked frame rate. Add it in play mode.
/// </summary>
public class FlythroughRecorder : MonoBehaviour
{
    public float startX = 5f, endX = 140f;
    public float seconds = 10f;
    public int fps = 30;
    public int width = 1280, height = 720;
    public float yOffset = 2.2f;
    public Vector2 zoom = new Vector2(8.5f, 10.5f);
    public string folder;
    public string prefix = "f";

    Camera _cam;
    RenderTexture _rt;
    Texture2D _tex;
    int _frame;
    float _camY;
    public bool Done { get; private set; }

    void Start()
    {
        _cam = Camera.main;
        var follow = _cam.GetComponent<CameraFollow2D>();
        if (follow) follow.enabled = false;
        foreach (var h in FindObjectsByType<AreaHUD>(FindObjectsSortMode.None)) h.enabled = false;
        foreach (var t in FindObjectsByType<DialogueTrigger>(FindObjectsSortMode.None)) Destroy(t);
        foreach (var m in FindObjectsByType<PauseMenu>(FindObjectsSortMode.None)) m.enabled = false;
        foreach (var b in FindObjectsByType<BossBar>(FindObjectsSortMode.None)) b.enabled = false;
        var p = GameObject.Find("Player");
        if (p) p.SetActive(false);
        Time.captureFramerate = fps;
        _rt = new RenderTexture(width, height, 24);
        _tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        Directory.CreateDirectory(folder);
        _camY = GroundAt(startX) + yOffset;
    }

    float GroundAt(float x)
    {
        float best = 0f;
        foreach (var hit in Physics2D.RaycastAll(new Vector2(x, 60f), Vector2.down, 90f))
            if (!hit.collider.isTrigger) { best = hit.point.y; break; }
        return best;
    }

    void LateUpdate()
    {
        if (Done) return;
        float k = Mathf.Clamp01(_frame / (seconds * fps));
        float e = k * k * (3f - 2f * k);                                 // ease in and out
        float x = Mathf.Lerp(startX, endX, e);
        // look ahead a little so climbs and drops are anticipated, then glide (no jitter)
        float target = Mathf.Max(GroundAt(x), GroundAt(x + 4f), GroundAt(x - 3f)) + yOffset;
        _camY = Mathf.Lerp(_camY, target, 0.06f);
        _cam.transform.position = new Vector3(x, _camY, _cam.transform.position.z);
        _cam.orthographicSize = Mathf.Lerp(zoom.x, zoom.y, Mathf.Sin(k * Mathf.PI));

        var prev = _cam.targetTexture;
        _cam.targetTexture = _rt;
        _cam.Render();
        _cam.targetTexture = prev;
        var act = RenderTexture.active;
        RenderTexture.active = _rt;
        _tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _tex.Apply();
        RenderTexture.active = act;
        File.WriteAllBytes(Path.Combine(folder, $"{prefix}{_frame:0000}.png"), _tex.EncodeToPNG());
        _frame++;
        if (_frame >= seconds * fps) { Done = true; Time.captureFramerate = 0; }
    }
}
