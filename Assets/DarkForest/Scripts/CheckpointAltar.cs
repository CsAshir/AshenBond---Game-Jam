using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Stone altar checkpoint: burns spirit-cyan until the player touches it, then ignites amber
/// and becomes the respawn point (GDD: "Lantern on stone pedestal ignites blue then amber").
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CheckpointAltar : MonoBehaviour
{
    public SpriteFrameAnimator flame;
    public Sprite[] cyanFrames;
    public Sprite[] amberFrames;
    public Light2D altarLight;
    public Color cyanLight = new Color(0.3f, 0.9f, 1f);
    public Color amberLight = new Color(1f, 0.6f, 0.25f);
    public GameObject igniteBurst;
    public Vector3 respawnOffset = new Vector3(1.5f, 0.1f, 0f);

    bool _lit;

    void Start()
    {
        if (flame) flame.frames = cyanFrames;
        if (altarLight) altarLight.color = cyanLight;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController2D>();
        if (!player) return;
        player.SetSpawn(transform.position + respawnOffset);
        if (_lit) return;
        _lit = true;
        if (flame) flame.frames = amberFrames;
        if (altarLight)
        {
            altarLight.color = amberLight;
            altarLight.intensity *= 1.3f;
        }
        if (igniteBurst) Instantiate(igniteBurst, transform.position + Vector3.up * 1.5f, Quaternion.identity);
        Sfx.Play("checkpoint", transform.position);
    }
}
