using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Shared environment helpers: Cainos props, living fire on props, wind, blasts, mist, arena markers.</summary>
public static partial class DarkForestBuilder
{
    const string CainosPrefabs = "Assets/Cainos/Pixel Art Platformer - Village Props/Prefab";
    static readonly Dictionary<string, Material> _cainosFx = new Dictionary<string, Material>();

    /// <summary>
    /// Places a Cainos village prop. Sprites get our lit material (fires and the lantern light them) and a tint
    /// for the area's mood; its particle flames get our glow shader with Cainos's own flame textures.
    /// </summary>
    static GameObject PlaceCainos(string prop, Vector2 pos, Transform parent, int order, Color tint, float scale = 1f, bool flip = false, Material mat = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CainosPrefabs}/PF Village Props - {prop}.prefab");
        if (!prefab) { Debug.LogWarning("[Props] Missing Cainos prop: " + prop); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = prop;
        // Set dressing only: the pack's physics (rolling barrels, wheel joints) would fight our level colliders.
        foreach (var j in go.GetComponentsInChildren<Joint2D>(true)) Object.DestroyImmediate(j);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(rb);
        foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(c);
        go.transform.position = new Vector3(pos.x, pos.y, parent ? parent.position.z : 0f);
        go.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1f);
        // Pack pivots differ (centre / bottom / offset): sit every prop on the ground line.
        var rs = go.GetComponentsInChildren<SpriteRenderer>(true);
        if (rs.Length > 0)
        {
            var bounds = rs[0].bounds;
            foreach (var r in rs) bounds.Encapsulate(r.bounds);
            go.transform.position += new Vector3(0f, pos.y - bounds.min.y, 0f);
        }
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sharedMaterial = mat ? mat : _lit;
            sr.color = sr.color * tint;
            sr.sortingOrder += order;
        }
        foreach (var pr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            var src = pr.sharedMaterial;
            if (src)
            {
                if (!_cainosFx.TryGetValue(src.name, out var m) || !m)
                {
                    m = Neon("M_Cainos_" + src.name.Replace(" ", ""), Color.white, 1.6f, 0f, true, 0f, 0f, 0.15f);
                    m.mainTexture = src.mainTexture;
                    _cainosFx[src.name] = m;
                }
                pr.sharedMaterial = m;
            }
            pr.sortingOrder += order + 1;
        }
        return go;
    }

    // ------------------------------------------------------------------ living fire on the village props

    static Dictionary<string, List<Vector3>> _propFires;

    static List<Vector3> PropFireAnchors(string sprite)
    {
        if (_propFires == null)
        {
            _propFires = new Dictionary<string, List<Vector3>>();
            var path = Path.Combine(Application.dataPath, "DarkForest/AreaA/Sprites/prop_fire_anchors.json");
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                foreach (Match m in Regex.Matches(text, @"""(\w+)"":\s*\[((?:\s*\[[^\]]*\]\s*,?)*)\s*\]"))
                {
                    var list = new List<Vector3>();
                    foreach (Match p in Regex.Matches(m.Groups[2].Value, @"\[\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)\s*\]"))
                        list.Add(new Vector3(F(p.Groups[1].Value), F(p.Groups[2].Value), F(p.Groups[3].Value)));
                    _propFires[m.Groups[1].Value] = list;
                }
            }
        }
        return _propFires.TryGetValue(sprite, out var l) ? l : new List<Vector3>();
    }

    static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Animated pixel fire + rising smoke + warm light where the painted fire used to be.</summary>
    static void AddPropFires(SpriteRenderer sr, string sprite, int order, bool flip, float light)
    {
        var b = sr.bounds;
        int i = 0;
        foreach (var a in PropFireAnchors(sprite))
        {
            float u = flip ? 1f - a.x : a.x;
            var p = new Vector2(b.min.x + u * b.size.x, b.min.y + a.y * b.size.y);
            PropFire(sr.transform, p, Mathf.Clamp(a.z * b.size.x * 0.7f, 0.5f, 3f), order, i++ == 0 ? light : 0f, b.size.x * 0.9f);
        }
    }

    /// <summary>One living fire: pixel flames, a smoke plume leaning with the wind, optionally a flickering light.</summary>
    static void PropFire(Transform parent, Vector2 p, float w, int order, float light, float lightRadius)
    {
        float z = parent.position.z;
        var flames = BackgroundParticles("Fire", parent, Vector2.zero, new Vector2(w, 0.25f), _fireSquare, order + 1,
            32f * w + 16f, new Vector2(0.6f, 1.2f), new Vector2(1.2f, 2.8f), new Vector2(0.4f, 0.8f), FireGradient(), 160);
        flames.transform.position = new Vector3(p.x, p.y, z);
        var smoke = BackgroundParticles("Smoke", parent, Vector2.zero, new Vector2(w * 0.8f, 0.3f), _smoke, order - 1,
            3f + w, new Vector2(3f, 5f), new Vector2(0.6f, 1.4f), new Vector2(1.2f, 2.4f),
            Grad((new Color(0.85f, 0.42f, 0.2f), 0f, 0f), (new Color(0.26f, 0.22f, 0.3f), 0.25f, 0.6f), (new Color(0.12f, 0.12f, 0.16f), 1f, 0f)), 60);
        smoke.transform.position = new Vector3(p.x, p.y + 0.8f, z);
        var grow = smoke.sizeOverLifetime;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
        var vel = smoke.velocityOverLifetime;   // smoke leans with the wind
        vel.x = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        if (light > 0f)
            PointLight("Fire Light", p + Vector2.up * 0.6f, parent, new Color(1f, 0.5f, 0.2f), lightRadius, light)
                .gameObject.AddComponent<FlickerLight2D>();
    }

    // ------------------------------------------------------------------ wind, blasts, mist, arenas

    static void Wind(Component c, float amount, float speed = 1.3f)
    {
        if (!c) return;
        var w = c.gameObject.AddComponent<WindSway>();
        w.amount = amount;
        w.speed = speed;
    }

    /// <summary>Faint stretched streaks blowing across the level now and then.</summary>
    static void WindStreaks(Transform parent, Color color, float y, float height)
    {
        var streaks = Particles("Wind Streaks", new Vector2(MapWidth / 2f, y), parent, PixelSquareMaterial(), 36, 3f, new Vector2(1.2f, 2.2f),
                                Vector2.zero, new Vector2(0.025f, 0.04f), Grad((color, 0f, 0f), (color, 0.3f, color.a), (color, 1f, 0f)), 0f, 0f, 0.2f, 40);
        var sh = streaks.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.rotation = Vector3.zero;
        sh.scale = new Vector3(MapWidth, height, 0.1f);
        var v = streaks.velocityOverLifetime;
        v.enabled = true;
        v.x = new ParticleSystem.MinMaxCurve(9f, 15f);
        v.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var sol = streaks.sizeOverLifetime;
        sol.enabled = false;
        var r = streaks.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.12f;
        r.lengthScale = 2f;
    }

    /// <summary>
    /// Green spores in the wind: a field of tiny glowing motes plus a few larger wisps that carry real light, so
    /// the forest floor and trunks get lit as they drift past (sparse on purpose).
    /// </summary>
    static void ForestSpores(Transform parent, int wisps)
    {
        var green = new Color(0.55f, 1f, 0.45f);
        var motes = Particles("Green Spores", new Vector2(MapWidth / 2f, 8f), parent, PixelSquareMaterial(), 38, 9f, new Vector2(5f, 9f), Vector2.zero,
                              new Vector2(0.07f, 0.13f), Grad((green, 0f, 0f), (green, 0.2f, 0.9f), (new Color(0.4f, 0.9f, 0.6f), 0.8f, 0.7f), (green, 1f, 0f)), 0f, 0f, 0.6f, 160);
        var ms = motes.shape;
        ms.shapeType = ParticleSystemShapeType.Box;
        ms.rotation = Vector3.zero;
        ms.scale = new Vector3(MapWidth + 20f, 9f, 0.1f);
        var mv = motes.velocityOverLifetime;
        mv.enabled = true;
        mv.x = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);   // with the wind, same way as the ash
        mv.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
        mv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var msol = motes.sizeOverLifetime;
        msol.enabled = false;
        motes.GetComponent<ParticleSystemRenderer>().sharedMaterial = _fireflies ? _fireflies : PixelSquareMaterial();

        var root = Group("Wind Wisps (green light)", parent).transform;
        for (int i = 0; i < wisps; i++)
        {
            var w = new GameObject($"Wisp {i}");
            w.transform.SetParent(root, false);
            var halo = Place("glow", Vector2.zero, w.transform, 37, _neonSoft, new Color(0.4f, 1f, 0.45f, 0.22f), 0.75f);   // soft green pool of light (reads on unlit art)
            halo.transform.localPosition = Vector3.zero;
            var sr = Place("glow", Vector2.zero, w.transform, 39, _neonSoft, new Color(0.5f, 1f, 0.45f, 0.7f), 0.22f);
            sr.transform.localPosition = Vector3.zero;
            var core = Place("white", Vector2.zero, w.transform, 40, PixelSquareMaterial(), new Color(0.8f, 1f, 0.7f), 0.09f);
            core.transform.localPosition = Vector3.zero;
            var l = LocalLight("Wisp Light", w.transform, Vector2.zero, new Color(0.45f, 1f, 0.5f), R(4f, 5.5f), R(1.1f, 1.5f));
            var wisp = w.AddComponent<WindWisp>();
            wisp.wispLight = l;
            wisp.glow = sr;
            wisp.speed = R(0.6f, 1.1f);
        }
    }

    /// <summary>Soft themed mist that hides the void under the walkway (behind the terrain, in front of the backgrounds).</summary>
    static SpriteRenderer UnderMist(Transform parent, Color color, float alpha, float top)
    {
        var fog = Fog(parent, -4f, top + 4f, color, -6, alpha);
        fog.name = "Under-walkway Mist";
        SetZ(fog.transform, 2f);
        return fog;
    }

    static GameObject BuildVillageBlastPrefab()
    {
        var frames = Sheet("vfx_fire_bomb").Skip(7).ToArray();   // second half of the sheet = the fireball
        var root = new GameObject("FX_VillageBlast");
        var sr = root.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = _neonTex;
        sr.color = new Color(1f, 0.85f, 0.75f);
        sr.sortingOrder = -80;
        sr.sprite = frames.Length > 0 ? frames[0] : _sprites["glow"];
        root.transform.localScale = Vector3.one * 1.6f;
        var one = root.AddComponent<SpriteOneShot>();
        one.frames = frames;
        one.fps = 11f;
        one.linger = 4f;
        var flash = LocalGlow("Flash", root.transform, Vector2.zero, new Vector2(3.5f, 3.5f), new Color(1f, 0.55f, 0.2f, 0.8f), -81, _neonSoft);
        OneShot("Debris", root.transform, Vector2.zero, _emberSquare, -79, 40, new Vector2(0.8f, 1.6f), new Vector2(2f, 5f), new Vector2(0.05f, 0.1f),
                Grad((FireCore, 0f, 1f), (FireOrange, 0.4f, 1f), (FireMagenta, 1f, 0f)), 0.3f, 180f, 0.7f, 0.3f);
        var col = OneShot("Smoke Column", root.transform, new Vector2(0f, 0.4f), _smoke, -82, 12, new Vector2(2.5f, 4f), new Vector2(0.4f, 1.2f), new Vector2(0.6f, 1.2f),
                Grad((new Color(0.9f, 0.45f, 0.2f), 0f, 0f), (new Color(0.25f, 0.2f, 0.28f), 0.2f, 0.55f), (new Color(0.12f, 0.1f, 0.16f), 1f, 0f)), 0.3f, 90f, -0.05f, 0.3f);
        var grow = col.sizeOverLifetime;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2f));
        var vfx = root.AddComponent<VfxLifetime>();
        vfx.sustain = 0.4f;
        vfx.fade = 2f;
        vfx.flashes = new[] { flash };
        vfx.flashDuration = 0.6f;
        vfx.flashGrow = 2.5f;
        return SavePrefab(root, "FX_VillageBlast");
    }

    static void Arena(Transform parent, float x0, float x1, float groundY, string enemy)
    {
        var go = new GameObject($"Enemy Arena: {enemy} (x{x0:0}-{x1:0})");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) / 2f, groundY, 0f);
        var m = go.AddComponent<ArenaMarker>();
        m.enemy = enemy;
        m.size = new Vector2(x1 - x0, 6f);
    }

    /// <summary>
    /// A tree in the playable plane that falls across the walkway. Ahead = lands in front of the boy before he
    /// gets there (an obstacle to hop), Behind = comes down once he has passed (no way back).
    /// </summary>
    static TreeTopple PathTree(Transform parent, string sprite, float x, float dir, TreeTopple.Trigger trigger, float distance, float length, Color tint, Material wood = null)
    {
        wood ??= Neon("M_PathTree", new Color(0.16f, 0.05f, 0.035f), 1.1f, 0.85f, false, 0f, 0f);   // silhouette art -> readable ember-dark wood
        var t = GameplayTree(parent, sprite, x, ASurf(x) + 0.15f, dir, length, 0.9f, ASurf(x + dir * length), tint, wood);
        t.trigger = trigger;
        t.triggerDistance = distance;
        t.hurtWhileFalling = trigger == TreeTopple.Trigger.Ahead;
        t.name = $"Falling Tree ({trigger}) x{x}";
        return t;
    }

    /// <summary>
    /// A cracked, smouldering tree on the lip of a gap too wide to jump. Two or three lantern shots (a charged
    /// flare counts double) bring it down across the gap, and its trunk becomes the bridge.
    /// </summary>
    static TreeTopple BridgeTree(Transform parent, string sprite, int gapX0, int gapX1, Color tint)
    {
        float x = gapX0 - 1.2f;
        float length = (gapX1 + 1 - x) + 2.2f;
        var cracked = Neon("M_BridgeTree", new Color(0.3f, 0.07f, 0.035f), 1.3f, 0.85f, false, 2.2f, 0.35f);   // slow ember pulse = "shoot me"
        var t = GameplayTree(parent, sprite, x, ASurf(x) - 0.25f, 1f, length, 0.6f, ASurf(gapX1 + 2), tint, cracked);
        t.trigger = TreeTopple.Trigger.Shot;
        t.hitsToBreak = 3;
        t.fallAngle = 90f;   // a bridge lies dead flat: no slope to slide on, the run cycle plays normally
        t.name = $"Bridge Tree (shoot it) x{gapX0}-{gapX1}";

        // Read it at a glance: glowing cracks at the base, a small fire and embers (Hollow Knight-style "this breaks").
        t.weakSpotGlow = LocalGlow("Weak Spot", t.transform, new Vector2(0f, 1.1f), new Vector2(2.2f, 2.4f), new Color(1f, 0.45f, 0.15f, 0.5f), 4, _neonSoft);
        PropFire(t.transform, (Vector2)t.transform.position + new Vector2(0.15f, 0.6f), 0.6f, 3, 0.9f, 3f);
        t.chipsPrefab = _woodChips ? _woodChips : (_woodChips = BuildWoodChipsPrefab());
        return t;
    }

    static GameObject _woodChips;

    static TreeTopple GameplayTree(Transform parent, string sprite, float x, float baseY, float dir, float length, float thickness, float farY, Color tint, Material mat)
    {
        // Unscaled root (rotates, holds the collider and effects) + scaled sprite child.
        var root = new GameObject("Tree");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(x, baseY, 0f);
        var sr = Place(sprite, new Vector2(x, baseY), root.transform, -3, mat, tint, 1f, dir < 0f);
        float sy = length / sr.sprite.bounds.size.y;
        sr.transform.localScale = new Vector3(sy * 0.75f, sy, 1f);   // slimmer than uniform so a long trunk doesn't become a huge canopy
        var box = root.AddComponent<BoxCollider2D>();
        box.size = new Vector2(thickness, length * 0.92f);
        box.offset = new Vector2(0f, length * 0.46f);
        var tt = root.AddComponent<TreeTopple>();
        tt.trunk = box;
        tt.trunkLength = length;
        tt.fallDirection = dir;
        // Rest on whatever ground is under the tip (past 90 degrees when the far side is lower).
        tt.fallAngle = 90f + Mathf.Atan2(baseY - farY, length) * Mathf.Rad2Deg;
        tt.creakTime = 0.5f;
        tt.impactPrefab = _beamImpact;
        tt.impactOffset = new Vector3(length * 0.75f, 0.3f, 0f);
        tt.shake = 0.3f;
        return tt;
    }

    static GameObject BuildWoodChipsPrefab()
    {
        var root = new GameObject("FX_WoodChips");
        OneShot("Chips", root.transform, Vector2.zero, PixelSquareMaterial(), 16, 14, new Vector2(0.4f, 0.8f), new Vector2(2f, 5f), new Vector2(0.08f, 0.14f),
                Grad((new Color(0.55f, 0.38f, 0.25f), 0f, 1f), (new Color(0.3f, 0.2f, 0.15f), 1f, 0f)), 0.2f, 140f, 1.2f, 0.2f);
        OneShot("Embers", root.transform, Vector2.zero, _emberSquare, 17, 10, new Vector2(0.3f, 0.7f), new Vector2(1.5f, 3.5f), new Vector2(0.06f, 0.1f),
                Grad((FireCore, 0f, 1f), (FireOrange, 0.5f, 1f), (FireMagenta, 1f, 0f)), 0.2f, 180f, 0.4f, 0.3f);
        var vfx = root.AddComponent<VfxLifetime>();
        vfx.sustain = 0.1f;
        vfx.fade = 0.6f;
        return SavePrefab(root, "FX_WoodChips");
    }

    /// <summary>A midground tree that cracks and falls when the player comes near (sparks + dust where it lands).</summary>
    static void ToppleTree(Transform parent, string sprite, float x, float groundY, Color tint, float scale, float dir, float triggerDistance)
    {
        var sr = Place(sprite, new Vector2(x, groundY - 0.2f), parent, -38, _unlit, tint, scale, dir < 0f);
        sr.name = "Falling Tree";
        SetZ(sr.transform, 8f);
        var tt = sr.gameObject.AddComponent<TreeTopple>();
        tt.fallDirection = dir;
        tt.triggerDistance = triggerDistance;
        tt.impactPrefab = _beamImpact;
        tt.impactOffset = new Vector3(sr.bounds.size.y * 0.75f, 0f, 0f);
    }
}
