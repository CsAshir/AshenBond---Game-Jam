using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Area C - The Clock Tower Approach (GDD section 5). One long ascent, no regular enemies: the tension comes from
/// traversal. Beats, in order (intro -> develop -> twist -> resolve):
///   1. Tower foot: the first bell strike, the lantern starts to weaken.      (safe, teach the bell)
///   2. Broken stair + big jump 1 over the abyss.                              (jump, height)
///   3. Gallery with a swinging saw-pendulum.                                  (timing)
///   4. Gear lift up the shaft, clockwork hall, big jump 2.                    (moving platforms)
///   5. Gear shuttle over a 10-tile gap.                                       (moving + gap)
///   6. Bell landing checkpoint -> the collapsing bridge chase.                (twist: keep running)
///   7. Big jump 3 up, second pendulum, the last climb.                        (combine everything)
///   8. The arena door at the top: automatic checkpoint, the Ash Warden waits. (resolve)
/// Menu: Tools > Ashen Bond > Build Area C - Clock Tower.
/// </summary>
public static partial class DarkForestBuilder
{
    const string CRoot = Root + "/AreaC";
    const string CSpriteDir = CRoot + "/Sprites";
    const string CBgDir = "Assets/Area C/Area C";
    const string CScenePath = SceneDir + "/AreaC_ClockTower.unity";

    // (first column, last column, height). Height 0 = the abyss inside the tower.
    static readonly (int x0, int x1, int h)[] CGround =
    {
        (0, 17, 5),                                   // 1. tower foot
        (18, 21, 7), (22, 25, 9), (26, 29, 11),       // 2. broken stair
        (30, 33, 0),                                  //    big jump 1
        (34, 43, 11),                                 // 3. gallery (pendulum)
        (44, 49, 0),                                  // 4. gear-lift shaft
        (50, 58, 18),                                 //    clockwork hall
        (59, 62, 0),                                  //    big jump 2
        (63, 68, 18),
        (69, 78, 0),                                  // 5. gear shuttle gap
        (79, 86, 19),                                 // 6. bell landing (checkpoint)
        (87, 104, 0),                                 //    collapsing bridge over the abyss
        (105, 112, 20),
        (113, 115, 0),                                // 7. big jump 3 (up: 3 wide, +2 high)
        (116, 124, 22), (125, 128, 24),
        (129, 172, 26),                               // 8. the summit arena (wide, with gear ledges), then the door
    };

    static readonly (int x0, int x1, int top)[] CPlatforms =
    {
        (35, 37, 14),     // perch over the gallery
        (52, 54, 21),     // hall perch
        (118, 119, 25),   // perch beside pendulum 2
        (136, 138, 29), (144, 146, 31), (152, 154, 29), (159, 161, 31),   // summit gear ledges
        (3, 5, 8), (107, 108, 23),                                         // air ledges (one is an escape from the rolling gear)
    };

    const float BridgeY = 19f;
    static readonly Color Stone = new Color(0.58f, 0.58f, 0.74f);

    [MenuItem("Tools/Ashen Bond/Build Area C - Clock Tower")]
    public static void BuildAreaC()
    {
        var active = SceneManager.GetActiveScene();
        if (active.isDirty && active.path != CScenePath && active.path != BScenePath && active.path != AreaScenePath && active.path != ScenePath)
        {
            Debug.LogWarning("[AreaC] Active scene has unsaved changes. Save it first, then build again.");
            return;
        }

        _rng = new System.Random(1159);   // 11:59, the frozen clock
        _ground = CGround;
        _platforms = CPlatforms;
        MapWidth = 173;
        PlayerSpawn = new Vector2(3.5f, 5.2f);

        EnsureFolders();
        if (!AssetDatabase.IsValidFolder(AreaTileDir)) AssetDatabase.CreateFolder(TileDir, "AreaA");
        ImportSprites();
        ImportBackgrounds();
        ImportAreaSprites();
        ImportAreaCSprites();
        BuildTiles();
        BuildAreaTiles();
        BuildMaterials();
        BuildAreaMaterials();
        BuildVolumeProfile();
        var profile = BuildAreaCVolumeProfile();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = BuildCameraAndPost(profile);
        var global = Object.FindFirstObjectByType<Light2D>();
        if (global && global.lightType == Light2D.LightType.Global)
        {
            global.color = new Color(0.5f, 0.46f, 0.82f);    // indigo stone, warm furnace light from below
            global.intensity = 0.56f;
            global.name = "Global Light 2D (tower)";
        }

        _emberBurst = BuildEmberBurstPrefab();
        _beamImpact = BuildBeamImpactPrefab();
        BuildAreaCSky();
        BuildAreaCParallax();
        BuildAreaCTerrain();
        BuildAreaCColliders();
        BuildAreaCDressing();
        BuildAreaCMechanisms();
        BuildAreaCObstacles();
        BuildAreaCAtmosphere(cam);
        BuildAreaCForeground();
        BuildAreaCLight();
        var player = BuildPlayer();
        BuildShadowAbility(player);
        BuildAreaCCheckpoints();
        BuildAreaCEnemies();
        BuildAreaCBell(cam, global, player);
        BuildAreaCTime(player);
        BuildAreaCRewind(player);
        BuildStoryUI(CStart);
        BuildGroundDepth(new Color(0.03f, 0.02f, 0.05f));
        BuildAudio('C');
        var crusher = Object.FindObjectsByType<TickCrusher>(FindObjectsSortMode.None).OrderBy(c => c.transform.position.x).FirstOrDefault();
        if (crusher) TutorialAt("Crusher", 8.5f, crusher.transform, CTutCrusher);
        ExitLines(CExit);
        RefineSquareFire();

        var follow = cam.gameObject.AddComponent<CameraFollow2D>();
        follow.target = player.transform;
        follow.boundsMin = new Vector2(0f, 1.8f);
        follow.boundsMax = new Vector2(MapWidth, 44f);
        follow.offset = new Vector2(0f, 1.8f);
        var hud = new GameObject("HUD").AddComponent<AreaHUD>();
        hud.areaTitle = "Area C";
        hud.areaSubtitle = "The Clock Tower";
        hud.health = player.GetComponent<PlayerHealth>();
        hud.shadow = player.GetComponentInChildren<ShadowSummon>();
        if (_sprites.TryGetValue("icon_lantern", out var li)) hud.heartIcon = li.texture;
        if (_sprites.TryGetValue("icon_sister", out var si)) hud.sisterIcon = si.texture;

        EditorSceneManager.SaveScene(scene, CScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(sc => sc.path == CScenePath);
        int bi = scenes.FindIndex(sc => sc.path == BScenePath);
        scenes.Insert(bi >= 0 ? bi + 1 : scenes.Count, new EditorBuildSettingsScene(CScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        SortBuildScenes();
        AssetDatabase.SaveAssets();
        _ground = AreaGround;
        _platforms = AreaPlatforms;
        Debug.Log("[AreaC] Scene built: " + CScenePath);
    }

    // ------------------------------------------------------------------ assets

    static void ImportAreaCSprites()
    {
        foreach (var n in new[] { "Area C-1", "Area C-2", "Area C-3" })
            ImportBg($"{CBgDir}/{n}.png", BgPPU);

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CSpriteDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = name == "areaC_door" ? 92 : name.StartsWith("chain") ? 16 : 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = name.StartsWith("chain") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.maxTextureSize = 4096;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)(name == "areaC_door" ? SpriteAlignment.BottomCenter
                                          : name.StartsWith("chain") ? SpriteAlignment.TopCenter : SpriteAlignment.Center);
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
            _sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    static Sprite[] GearFrames(string id) => Enumerable.Range(0, 4).Select(f => _sprites.TryGetValue($"gear_{id}_{f}", out var s) ? s : null).Where(s => s).ToArray();

    /// <summary>Indigo stone and furnace-orange highlights; the frozen clock's moon-white glow blooms softly.</summary>
    static UnityEngine.Rendering.VolumeProfile BuildAreaCVolumeProfile()
    {
        if (!AssetDatabase.IsValidFolder(CRoot)) AssetDatabase.CreateFolder(Root, "AreaC");
        var path = $"{CRoot}/AreaC_PostFX.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.0f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.88f, 0.8f));
        profile.Add<ChromaticAberration>(true).intensity.Override(0.05f);
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.05f);
        color.contrast.Override(22f);
        color.saturation.Override(-4f);
        color.colorFilter.Override(new Color(0.94f, 0.93f, 1f));
        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(new Color(0.3f, 0.22f, 0.62f));     // indigo stone
        split.highlights.Override(new Color(1f, 0.62f, 0.32f));    // furnace glow
        split.balance.Override(-25f);
        var vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.02f, 0.01f, 0.06f));
        vignette.intensity.Override(0.42f);
        vignette.smoothness.Override(0.5f);
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.18f);

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // ------------------------------------------------------------------ sky & parallax

    static void BuildAreaCSky()
    {
        Vector2 c = _cam.position;
        var sky = Group("Sky (parallax 1.0)");
        sky.transform.position = new Vector3(c.x, c.y, 120f);
        var bg = Place("white", c, sky.transform, -100, _sky);
        bg.name = "Tower Night";
        bg.transform.localScale = new Vector3(48f, CamSize * 2f + 8f, 1f);
        bg.transform.localPosition = Vector3.zero;
        Parallax(sky, 1f, 1f);
    }

    static void BuildAreaCParallax()
    {
        var root = Group("Parallax Backgrounds").transform;
        Vector2 c = _cam.position;
        //  sprite      order  fx     fy     yOff   z     scale tint                                 boost
        var layers = new (string sprite, int order, float fx, float fy, float yOffset, float z, float scale, Color tint, float boost)[]
        {
            ("Area C-1", -95, 0.9f,  0.93f, 4.5f,  100f, 0.85f, new Color(0.78f, 0.74f, 0.95f), 1.4f),   // the frozen clock, 11:59
            ("Area C-2", -84, 0.7f,  0.82f, 1.5f,   60f, 0.75f, new Color(0.56f, 0.5f, 0.74f),  1.5f),   // bells, gears, gothic arches
            ("Area C-3", -72, 0.45f, 0.62f, -3.2f,  25f, 0.8f,  new Color(0.4f, 0.36f, 0.55f),  1.6f),   // near ruins: sink below as you climb
        };
        foreach (var l in layers)
        {
            if (!_sprites.TryGetValue(l.sprite, out var sprite) || sprite == null) continue;
            var mat = SaveMaterial($"M_BG_{l.sprite.Replace(" ", "")}", Shader.Find("DarkForest/FireBackdrop"), m =>
            {
                m.SetFloat("_FireBoost", l.boost);
                m.SetFloat("_Blur", l.fx >= 0.9f ? 0.8f : Mathf.Max(0f, (l.fx - 0.3f) * 5f));
                m.SetFloat("_SmokeDistort", 0.003f);
            });
            var layer = Group($"{l.sprite} (parallax {l.fx:0.00}, z {l.z:0})", root);
            layer.transform.position = new Vector3(c.x, c.y + l.yOffset, l.z);
            layer.transform.localScale = new Vector3(l.scale, l.scale, 1f);
            float width = sprite.bounds.size.x * l.scale;
            for (int k = -2; k <= 2; k++)
            {
                var go = new GameObject($"{l.sprite} [{k}]");
                go.transform.SetParent(layer.transform, false);
                go.transform.localPosition = new Vector3(k * sprite.bounds.size.x, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = mat;
                sr.sortingOrder = l.order;
                sr.color = l.tint;
                sr.flipX = (k & 1) != 0;
            }
            Parallax(layer, l.fx, l.fy, width * 2f);
            if (l.fx < 0.9f) LayerBase(layer.transform, sprite, l.order, l.tint);
        }
        SetZ(Fog(root, 0f, 30f, new Color(0.3f, 0.26f, 0.55f), -88, 0.16f).transform, 80f);
        SetZ(Fog(root, 0f, 30f, new Color(0.22f, 0.18f, 0.42f), -76, 0.2f).transform, 40f);
        SetZ(Fog(root, -2f, 34f, new Color(0.12f, 0.1f, 0.24f), -62, 0.26f).transform, 15f);
    }

    // ------------------------------------------------------------------ terrain: scorched tower stone

    static void BuildAreaCTerrain()
    {
        var grid = Group("Grid");
        grid.AddComponent<Grid>();
        Tilemap Map(string name, int order, Color tint, Material mat = null)
        {
            var go = Group(name, grid.transform);
            var map = go.AddComponent<Tilemap>();
            map.color = tint;
            var r = go.AddComponent<TilemapRenderer>();
            r.sharedMaterial = mat ? mat : _lit;
            r.sortingOrder = order;
            return map;
        }
        var ground = Map("Ground (tower stone)", 0, Stone);
        var masonry = Map("Masonry (pillars, in shadow)", -2, new Color(0.2f, 0.18f, 0.3f), _unlit);   // unlit: dim indigo stone, never a black hole
        var overhang = Map("Overhang (edges, drips)", 1, Stone);
        var platforms = Map("Platforms", 0, Stone);

        for (int x = -2; x < MapWidth + 2; x++)
        {
            int h = AreaH(x);
            if (h <= 0) continue;
            for (int y = h - SlabRows; y < h; y++)
            {
                bool top = y == h - 1;
                bool openL = y >= AreaH(x - 1);
                bool openR = y >= AreaH(x + 1);
                Vector2Int t;
                if (top) t = openL ? new Vector2Int(1, 3) : openR ? new Vector2Int(6, 3) : new Vector2Int((x & 1) == 0 ? 3 : 4, 3);
                else if (openL) t = new Vector2Int(1, 5 + (y & 3));
                else if (openR) t = new Vector2Int(6, 5 + (y & 3));
                else t = new Vector2Int(2 + (((x % 4) + 4) % 4), 9);
                ground.SetTile(new Vector3Int(x, y, 0), AT(t.x, t.y));
                if (openL && x > 0) overhang.SetTile(new Vector3Int(x - 1, y, 0), AT(0, top ? 3 : 4 + (((y % 5) + 5) % 5)));
                if (openR && x < MapWidth - 1) overhang.SetTile(new Vector3Int(x + 1, y, 0), AT(7, top ? 3 : 4 + (((y % 5) + 5) % 5)));
            }
            // The tower's pillars: masonry in deep shadow below the walking crust (reads as built stone, not floating slabs).
            for (int y = -3; y < h - SlabRows; y++)
                masonry.SetTile(new Vector3Int(x, y, 0), AT(FillTiles[((x * 3 + y * 7) & 0x7fffffff) % FillTiles.Length].x, FillTiles[((x * 3 + y * 7) & 0x7fffffff) % FillTiles.Length].y));
        }

        foreach (var p in CPlatforms)
            for (int x = p.x0; x <= p.x1; x++)
            {
                int col = x == p.x0 ? 11 : x == p.x1 ? 15 : 12 + ((x - p.x0) % 3);
                platforms.SetTile(new Vector3Int(x, p.top, 0), AT(col, 1));
                overhang.SetTile(new Vector3Int(x, p.top + 1, 0), AT(col, 0));
            }
    }

    static void BuildAreaCColliders()
    {
        var go = Group("Level Colliders");
        go.isStatic = true;
        foreach (var g in CGround)
        {
            if (g.h <= 0) continue;
            var box = go.AddComponent<BoxCollider2D>();
            float top = g.h - 0.1f;
            box.offset = new Vector2((g.x0 + g.x1 + 1) / 2f, (top - 6f) / 2f);
            box.size = new Vector2(g.x1 - g.x0 + 1, top + 6f);
        }
        foreach (var wallX in new[] { -0.5f, MapWidth + 0.5f })
        {
            var wall = go.AddComponent<BoxCollider2D>();
            wall.offset = new Vector2(wallX, 25f);
            wall.size = new Vector2(1f, 70f);
        }
        var plats = Group("Platforms (one-way)");
        foreach (var p in CPlatforms)
        {
            var box = plats.AddComponent<BoxCollider2D>();
            float top = p.top + 1 - 0.1f;
            box.offset = new Vector2((p.x0 + p.x1 + 1) / 2f, top - 0.25f);
            box.size = new Vector2(p.x1 - p.x0 + 1 - 0.1f, 0.5f);
            box.usedByEffector = true;
        }
        var eff = plats.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true;
        eff.surfaceArc = 170f;

        // The abyss: falling in sends you back. The kill line sits a few tiles under the lower lip, so a missed
        // jump reads as a fall (not an instant reset), and the furnace glows far below.
        for (int i = 0; i < CGround.Length; i++)
        {
            var g = CGround[i];
            if (g.h > 0) continue;
            float lip = Mathf.Min(i > 0 ? CGround[i - 1].h : 5, i < CGround.Length - 1 ? CGround[i + 1].h : 5);
            float cx = (g.x0 + g.x1 + 1) / 2f, w = g.x1 - g.x0 + 1;
            var pit = Group($"Abyss x{g.x0}-{g.x1}");
            var trig = pit.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            float killTop = lip - 4f;
            trig.offset = new Vector2(cx, (killTop - 8f) / 2f);
            trig.size = new Vector2(w, killTop + 8f);
            pit.AddComponent<KillZone>();
            var glow = Glow(new Vector2(cx, -1f), pit.transform, new Color(1f, 0.42f, 0.15f, 0.45f), w * 1.3f, -20, _neonSoft);
            glow.transform.localScale = new Vector3(w * 1.4f / 4f, 1.4f, 1f);
            var rise = BackgroundParticles("Furnace Embers", pit.transform, new Vector2(cx, 0f), new Vector2(w, 0.5f), _emberSquare, -19,
                3f * w, new Vector2(3f, 6f), new Vector2(1.5f, 3.5f), new Vector2(0.05f, 0.1f),
                Grad((FireCore, 0f, 0f), (FireOrange, 0.15f, 1f), (FireMagenta, 1f, 0f)), 120);
            rise.transform.position = new Vector3(cx, 0f, 0f);
            PointLight("Furnace Light", new Vector2(cx, 1f), pit.transform, new Color(1f, 0.45f, 0.15f), w * 1.2f, 1.2f).gameObject.AddComponent<FlickerLight2D>();
        }
    }

    // ------------------------------------------------------------------ dressing

    /// <summary>Animated cog; <paramref name="diameter"/> is in world units.</summary>
    static SpriteRenderer Gear(Transform parent, string id, Vector2 pos, float diameter, int order, Material mat, Color tint, float fps, bool reverse = false)
    {
        var frames = GearFrames(id);
        if (frames.Length == 0) return null;
        if (reverse) frames = frames.Reverse().ToArray();
        var sr = new GameObject($"Gear {id}").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        sr.transform.position = new Vector3(pos.x, pos.y, parent ? parent.position.z : 0f);
        float scale = diameter / frames[0].bounds.size.x;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.sprite = frames[0];
        sr.sharedMaterial = mat;
        sr.color = tint;
        sr.sortingOrder = order;
        var anim = sr.gameObject.AddComponent<SpriteFrameAnimator>();
        anim.frames = frames;
        anim.framesPerSecond = fps;
        anim.obeysTimeFreeze = true;      // the clockwork stops when the bell stops time
        return sr;
    }

    static SpriteRenderer Chain(Transform parent, Vector2 top, float length, int order, Color tint, int kind = 1, float width = 1f)
    {
        if (!_sprites.TryGetValue($"chain_{kind}", out var sp)) return null;
        var sr = new GameObject("Chain").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        sr.transform.position = new Vector3(top.x, top.y, parent ? parent.position.z : 0f);
        sr.sprite = sp;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(sp.bounds.size.x * width, length);
        sr.sharedMaterial = _unlit;
        sr.color = tint;
        sr.sortingOrder = order;
        return sr;
    }

    static void BuildAreaCDressing()
    {
        var dress = Group("Tower Set Dressing").transform;
        SetZ(dress, 8f);
        var bronze = new Color(0.42f, 0.32f, 0.3f);
        var iron = new Color(0.32f, 0.32f, 0.42f);

        // Great clockwork turning behind the climb (dark, slow; the bigger the slower).
        foreach (var (id, x, y, s, fps, rev) in new[]
        {
            ("b9", 12f, 13f, 7f, 3f, false), ("s11", 22f, 19f, 5f, 4f, true), ("b10", 46.5f, 25f, 9f, 2.5f, false),
            ("s8", 57f, 27f, 4.5f, 5f, true), ("b11", 74f, 26f, 8f, 2.5f, true), ("s9", 96f, 28f, 10f, 2f, false),
            ("b8", 118f, 32f, 5.5f, 4f, false), ("s10", 139f, 37f, 11f, 2f, true),
        })
        {
            Gear(dress, id, new Vector2(x, y), s, -46, _unlit, rev ? iron : bronze, fps, rev);
            // Neon rim: the clockwork outlined in faint cyan light so it reads against the dark.
            var rim = Gear(dress, id, new Vector2(x, y), s * 1.02f, -47, _neonAdd, new Color(0.35f, 0.85f, 1f, 0.15f), fps, rev);
            if (rim) rim.name = "Gear Neon Rim";
        }

        // Chains hanging out of the dark above.
        for (float x = 3f; x < MapWidth; x += R(4f, 9f))
        {
            float top = AreaH(Mathf.FloorToInt(x)) + R(14f, 20f);
            Chain(dress, new Vector2(x, top), R(5f, 11f), -42, new Color(0.3f, 0.28f, 0.36f), _rng.Next(3), 1f);
        }

        // (Light through the windows: god rays in BuildAreaCLight.)

        // Fire bowls and torches: the only warm light up here besides the lantern.
        var lights = Group("Braziers & Torches", dress).transform;
        SetZ(lights, 2f);
        foreach (var x in new[] { 8f, 16f, 40f, 56f, 82f, 108f, 122f, 133f, 148f })
        {
            var bowl = PlaceCainos("Fire Bowl", new Vector2(x, ASurf(x)), lights, 4, new Color(0.75f, 0.7f, 0.8f), 1.3f);
            PointLight("Brazier Light", new Vector2(x, ASurf(x) + 1.6f), lights, new Color(1f, 0.55f, 0.22f), 4.5f, 1.4f).gameObject.AddComponent<FlickerLight2D>();
        }
        foreach (var (prop, x, s, flip) in new[]
        {
            ("Statue", 3f, 1.4f, false), ("Banner", 14f, 1.2f, false), ("Barrel", 20.5f, 1f, false), ("Crate Small", 41.5f, 1f, false),
            ("Banner", 64f, 1.2f, true), ("Wood Logs", 84f, 0.9f, false), ("Statue", 110.5f, 1.3f, true), ("Banner", 127f, 1.2f, false),
        })
            PlaceCainos(prop, new Vector2(x, ASurf(x)), dress, -12, new Color(0.62f, 0.6f, 0.75f), s, flip);
    }

    // ------------------------------------------------------------------ mechanisms

    static GameObject GearPlatform(Transform parent, string name, Vector2 a, Vector2 b, float period, float width, bool hangingChains)
    {
        var plat = new GameObject(name);
        plat.transform.SetParent(parent, false);
        plat.transform.position = new Vector3(a.x, a.y, 0f);
        var rb = plat.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = plat.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, 0.5f);
        box.offset = new Vector2(0f, 0.15f);
        var mp = plat.AddComponent<MovingPlatform>();
        mp.pointA = a;
        mp.pointB = b;
        mp.period = period;
        mp.topY = 0.4f;
        int n = Mathf.RoundToInt(width);
        for (int i = 0; i < n; i++)
        {
            int col = i == 0 ? 11 : i == n - 1 ? 15 : 12 + (i % 3);
            var t = Place($"a_{col}_1", Vector2.zero, plat.transform, 6, _lit, Stone);
            t.transform.localPosition = new Vector3(i - (n - 1) / 2f, -0.1f, 0f);
            var lip = Place($"a_{col}_0", Vector2.zero, plat.transform, 7, _lit, Stone);
            lip.transform.localPosition = new Vector3(i - (n - 1) / 2f, 0.9f, 0f);
        }
        var g = Gear(plat.transform, "b2", Vector2.zero, 1.8f, 5, _unlit, new Color(0.75f, 0.55f, 0.45f), 10f);
        if (g) g.transform.localPosition = new Vector3(0f, -0.8f, 0f);
        LocalLight("Gear Lamp", plat.transform, new Vector2(0f, 0.8f), new Color(1f, 0.65f, 0.35f), 3f, 0.7f);
        if (hangingChains)
            foreach (var dx in new[] { -width / 2f + 0.3f, width / 2f - 0.3f })
            {
                var c = Chain(plat.transform, Vector2.zero, 14f, 5, new Color(0.55f, 0.52f, 0.62f), 1, 1f);
                if (c) c.transform.localPosition = new Vector3(dx, 14.5f, 0f);
            }
        return plat;
    }

    static void PendulumAt(Transform parent, float x, float groundY, float length, float period, float phase)
    {
        var pivot = new GameObject($"Saw Pendulum x{x}");
        pivot.transform.SetParent(parent, false);
        pivot.transform.position = new Vector3(x, groundY + length + 1.3f, 0f);
        var p = pivot.AddComponent<Pendulum>();
        p.length = length;
        p.period = period;
        p.phase = phase;
        p.maxAngle = 52f;
        p.rod = Chain(pivot.transform, Vector2.zero, length, 7, new Color(0.62f, 0.6f, 0.7f), 1, 1.4f);
        if (p.rod) p.rod.transform.localPosition = Vector3.zero;
        var bobRoot = new GameObject("Saw Gear");
        bobRoot.transform.SetParent(pivot.transform, false);
        var saw = Place("gear_s8_0", Vector2.zero, bobRoot.transform, 9, _unlit, new Color(0.85f, 0.85f, 0.95f), 2.1f / _sprites["gear_s8_0"].bounds.size.x);
        saw.transform.localPosition = Vector3.zero;
        LocalGlow("Danger Glow", bobRoot.transform, Vector2.zero, new Vector2(3.4f, 3.4f), new Color(1f, 0.35f, 0.2f, 0.35f), 8, _neonSoft);
        LocalLight("Saw Light", bobRoot.transform, Vector2.zero, new Color(1f, 0.5f, 0.3f), 3.2f, 0.9f);
        p.bob = bobRoot.transform;
        p.hitRadius = 1.05f;
        var sparks = OneShot("Sparks", bobRoot.transform, Vector2.zero, _emberSquare, 10, 10, new Vector2(0.25f, 0.5f), new Vector2(2f, 4f),
                             new Vector2(0.05f, 0.09f), Grad((FireCore, 0f, 1f), (FireOrange, 1f, 0f)), 0.6f, 360f, 0.8f, 0.2f);
        var sm = sparks.main; sm.playOnAwake = false; sm.simulationSpace = ParticleSystemSimulationSpace.World;
        p.sparks = sparks;
        Gear(pivot.transform, "b1", Vector2.zero, 1.2f, 8, _unlit, new Color(0.7f, 0.55f, 0.45f), 6f).transform.localPosition = Vector3.zero;
    }

    static void BuildAreaCMechanisms()
    {
        var mech = Group("Mechanisms").transform;

        // 3. Gallery pendulum, 7. second pendulum (offset beat, so a wait-then-run rhythm).
        PendulumAt(mech, 40.5f, 11f, 6f, 2.6f, 0f);   // leaves a safe landing (34-35.5) after jump 1
        PendulumAt(mech, 121.5f, 22f, 6f, 2.2f, 0.35f);

        // 4. Gear lift up the shaft (slow: the climb is the reward for patience), 5. horizontal shuttle.
        GearPlatform(mech, "Gear Lift (vertical)", new Vector2(46.5f, 10.6f), new Vector2(46.5f, 17.6f), 6f, 3f, true);
        GearPlatform(mech, "Gear Shuttle (horizontal)", new Vector2(70.6f, 17.6f), new Vector2(77.4f, 17.6f), 4.4f, 3f, true);

        // 6. The collapsing bridge: planks over the abyss that fall just behind you once you step on.
        var bridge = new GameObject("Collapsing Bridge (chase)");
        bridge.transform.SetParent(mech, false);
        bridge.transform.position = new Vector3(87f, BridgeY, 0f);
        var trig = bridge.AddComponent<BoxCollider2D>();
        trig.isTrigger = true;
        trig.size = new Vector2(1.2f, 4f);
        trig.offset = new Vector2(0.9f, 2f);
        var planks = new List<Transform>();
        for (int x = 87; x <= 104; x++)
        {
            var plank = new GameObject($"Plank {x}");
            plank.transform.SetParent(bridge.transform, false);
            plank.transform.position = new Vector3(x + 0.5f, BridgeY - 0.5f, 0f);
            var col = plank.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.02f, 0.5f);
            col.offset = new Vector2(0f, 0.15f);
            int c = x == 87 ? 11 : x == 104 ? 15 : 12 + (x % 3);
            var t = Place($"a_{c}_1", Vector2.zero, plank.transform, 6, _lit, Stone);
            t.transform.localPosition = Vector3.zero;
            var lip = Place($"a_{c}_0", Vector2.zero, plank.transform, 7, _lit, Stone);
            lip.transform.localPosition = new Vector3(0f, 1f, 0f);
            planks.Add(plank.transform);
        }
        var cb = bridge.AddComponent<CollapseBridge>();
        cb.planks = planks.ToArray();
        // Rope posts + two sagging chains so it reads as a hung bridge.
        foreach (var px in new[] { 87f, 104.9f })
        {
            var post = Chain(bridge.transform, new Vector2(px, BridgeY + 2.4f), 2.6f, 8, new Color(0.6f, 0.55f, 0.6f), 1, 1.6f);
            LocalLight("Bridge Lamp", bridge.transform, new Vector2(px - 87f, 2.6f), new Color(1f, 0.55f, 0.25f), 4f, 1.1f).gameObject.AddComponent<FlickerLight2D>();
        }
        for (float cx = 88f; cx < 104f; cx += 3f)
            Chain(bridge.transform, new Vector2(cx, BridgeY + 13f), 12.6f, 5, new Color(0.45f, 0.42f, 0.5f), 1, 1f);

        // Embers on the route (rest points for the dimming lantern).
        var embers = Group("Embers (pickups)").transform;
        foreach (var (x, y) in new[] { (24f, 12.4f), (36f, 17.3f), (53f, 24.3f), (74f, 21.4f), (95f, 21.6f), (110f, 22.6f), (118.5f, 28.2f), (136f, 28.6f) })
            Ember(embers, x, y);
    }

    // ------------------------------------------------------------------ atmosphere, foreground

    static void BuildAreaCAtmosphere(Camera cam)
    {
        var fx = Group("Atmosphere").transform;

        // Dust drifting through the shafts of moonlight.
        var dust = Particles("Tower Dust", new Vector2(MapWidth / 2f, 18f), fx, PixelSquareMaterial(), 38, 14f, new Vector2(7f, 12f), Vector2.zero,
                             new Vector2(0.04f, 0.08f), Grad((new Color(0.85f, 0.8f, 1f), 0f, 0f), (new Color(0.8f, 0.78f, 1f), 0.25f, 0.55f), (new Color(0.6f, 0.58f, 0.8f), 1f, 0f)), 0f, 0f, 0.5f, 300);
        var ds = dust.shape; ds.shapeType = ParticleSystemShapeType.Box; ds.rotation = Vector3.zero; ds.scale = new Vector3(MapWidth + 10f, 36f, 0.1f);
        var dv = dust.velocityOverLifetime; dv.enabled = true;
        dv.x = new ParticleSystem.MinMaxCurve(0.1f, 0.5f); dv.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.05f); dv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var dsol = dust.sizeOverLifetime; dsol.enabled = false;

        // Embers drifting up from the furnace far below.
        var up = Particles("Rising Embers", new Vector2(MapWidth / 2f, 4f), fx, _emberSquare, 40, 14f, new Vector2(5f, 9f), Vector2.zero,
                           new Vector2(0.06f, 0.1f), Grad((FireCore, 0f, 0f), (FireOrange, 0.15f, 1f), (FireMagenta, 0.8f, 0.7f), (Violet, 1f, 0f)), 0f, 0f, 0.8f, 200);
        var us = up.shape; us.shapeType = ParticleSystemShapeType.Box; us.rotation = Vector3.zero; us.scale = new Vector3(MapWidth, 6f, 0.1f);
        var uv = up.velocityOverLifetime; uv.enabled = true;
        uv.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.4f); uv.y = new ParticleSystem.MinMaxCurve(1.2f, 3f); uv.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        WindStreaks(fx, new Color(0.75f, 0.7f, 1f, 0.12f), 20f, 30f);
        UnderMist(fx, new Color(0.09f, 0.06f, 0.16f), 0.55f, 4f);
        Fog(fx, -4f, 9f, new Color(0.45f, 0.2f, 0.12f), 34, 0.12f).name = "Furnace Haze (front)";
    }

    static void BuildAreaCForeground()
    {
        var fg = Group("Foreground").transform;
        var near = Group("Near Foreground (parallax -0.25)", fg);
        SetZ(near.transform, -5f);
        var black = new Color(0.03f, 0.02f, 0.05f, 0.96f);
        // Huge dark cogs and chains passing in front of the lens: depth without clutter.
        for (float x = 14f; x < MapWidth + 30f; x += R(22f, 32f))
        {
            // Sunk below the walkway: only the top arc rises into the bottom of the frame (never over the path).
            float d = R(9f, 13f);
            Gear(near.transform, _rng.Next(2) == 0 ? "b11" : "s10", new Vector2(x, AreaH(Mathf.FloorToInt(x)) - d * 0.5f - 2.5f), d, 32, _unlit, black, 1.5f, _rng.Next(2) == 0);
            Chain(near.transform, new Vector2(x + R(5f, 9f), 40f), R(18f, 30f), 31, black, _rng.Next(3), 1.8f);
        }
        Parallax(near, -0.25f, -0.1f);
    }

    // ------------------------------------------------------------------ bell, lantern, checkpoints, door

    static void BuildAreaCBell(Camera cam, Light2D global, GameObject player)
    {
        var bell = new GameObject("Great Bell (every 12 s)").AddComponent<BellToll>();
        bell.cameraTransform = cam.transform;
        // Grit shaken loose from the ceiling: emitted above the view, falls through it.
        var grit = Particles("Bell Grit", Vector2.zero, cam.transform, PixelSquareMaterial(), 39, 0f, new Vector2(1.6f, 2.4f), Vector2.zero,
                             new Vector2(0.05f, 0.1f), Grad((new Color(0.7f, 0.65f, 0.75f), 0f, 1f), (new Color(0.5f, 0.45f, 0.55f), 1f, 0f)), 0f, 1.2f, 0.4f, 300);
        grit.transform.localPosition = new Vector3(0f, CamSize + 1f, 10f);
        var gm = grit.main; gm.loop = false; gm.playOnAwake = false; gm.prewarm = false; gm.simulationSpace = ParticleSystemSimulationSpace.World;
        var ge = grit.emission; ge.SetBursts(new[] { new ParticleSystem.Burst(0f, 160), new ParticleSystem.Burst(0.3f, 80) });
        var gs = grit.shape; gs.shapeType = ParticleSystemShapeType.Box; gs.rotation = Vector3.zero; gs.scale = new Vector3(CamSize * 4f, 0.5f, 0.1f);
        var gsol = grit.sizeOverLifetime; gsol.enabled = false;
        bell.grit = grit;
        bell.ringFx = SheetFx("FX_BellRing", "fx_ring", 26f, 7f, _neonAdd, new Color(0.85f, 0.8f, 1f, 0.5f), -30, Vector2.zero);
        var clockGlow = PointLight("Clock Glow", new Vector2(MapWidth / 2f, 30f), bell.transform, new Color(0.7f, 0.65f, 1f), 60f, 0.25f, 0.2f);
        bell.pulseLights = new[] { clockGlow };

        // The lantern weakens with the climb (GDD flame drain) and gutters at every strike.
        var fade = player.AddComponent<LanternFade>();
        fade.player = player.transform;
        fade.startX = 6f;
        fade.endX = 156f;
        fade.minStrength = 0.5f;
        fade.lights = player.GetComponentsInChildren<Light2D>(true).Where(l => l.name.StartsWith("Lantern")).ToArray();
        var halo = player.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s => s.name == "Lantern Halo");
        fade.halo = halo;
        bell.lantern = fade;
        bell.clockRingFx = _fxClockRing;
        bell.player = player.transform;
    }

    // ------------------------------------------------------------------ time: the tower's theme

    /// <summary>
    /// Time made visible: the bell stops time (TimeFreeze: mechanisms, dust and embers halt, the picture turns cold,
    /// the boy leaves after-images), the frozen clock's second hand keeps trying to pass 11:59, golden time-sand
    /// drifts through the shafts and time sparks flicker around the clockwork.
    /// </summary>
    static void BuildAreaCTime(GameObject player)
    {
        var tf = new GameObject("Time Freeze (bell)").AddComponent<TimeFreeze>();
        var vol = tf.gameObject.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;
        vol.priority = 5f;
        vol.weight = 0f;
        var path = $"{CRoot}/AreaC_TimeStop.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(-75f);
        ca.contrast.Override(15f);
        ca.colorFilter.Override(new Color(0.75f, 0.95f, 1.15f));
        profile.Add<ChromaticAberration>(true).intensity.Override(0.45f);
        var vg = profile.Add<Vignette>(true);
        vg.color.Override(new Color(0.05f, 0.25f, 0.35f));
        vg.intensity.Override(0.45f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        vol.sharedProfile = profile;
        tf.volume = vol;
        tf.pausable = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
            .Where(ps => ps.name is "Tower Dust" or "Rising Embers" or "Furnace Embers" or "Wind Streaks" or "Time Sand").ToArray();

        var trail = player.AddComponent<AfterImageTrail>();
        trail.source = player.GetComponent<SpriteRenderer>();
        trail.material = Neon("M_AfterImage", Color.white, 1.6f, 1f, true, 0f, 0f);
        trail.enabled = false;
        tf.trail = trail;

        // The frozen clock (far layer): a pale second hand stuck at 11:59, ticking and dragged back.
        var clockLayer = GameObject.Find("Parallax Backgrounds")?.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Area C-1"));
        if (clockLayer && _sprites.TryGetValue("Area C-1", out var face))
        {
            float w = face.bounds.size.x;
            for (int k = -2; k <= 2; k++)
            {
                var pivot = new GameObject("Second Hand (stuck at 11:59)");
                pivot.transform.SetParent(clockLayer, false);
                pivot.transform.localPosition = new Vector3(k * w, 1.9f, -0.1f);
                var hand = Place("white", Vector2.zero, pivot.transform, -93, _neonTex, new Color(0.85f, 0.8f, 1f, 0.75f), 1f);
                hand.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                hand.transform.localScale = new Vector3(0.09f, 3.2f, 1f);
                pivot.AddComponent<ClockHand>();
            }
        }

        // Golden time-sand drifting down through the light shafts.
        var fx = GameObject.Find("Atmosphere").transform;
        var sand = Particles("Time Sand", new Vector2(MapWidth / 2f, 26f), fx, PixelSquareMaterial(), 37, 10f, new Vector2(6f, 10f), Vector2.zero,
                             new Vector2(0.04f, 0.07f), Grad((new Color(1f, 0.85f, 0.45f), 0f, 0f), (new Color(1f, 0.8f, 0.4f), 0.2f, 0.8f), (new Color(0.9f, 0.6f, 0.3f), 1f, 0f)), 0f, 0f, 0.15f, 220);
        var ss = sand.shape; ss.shapeType = ParticleSystemShapeType.Box; ss.rotation = Vector3.zero; ss.scale = new Vector3(MapWidth, 30f, 0.1f);
        var sv = sand.velocityOverLifetime; sv.enabled = true;
        sv.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); sv.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.3f); sv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var ssol = sand.sizeOverLifetime; ssol.enabled = false;
        tf.pausable = tf.pausable.Append(sand).ToArray();

        // Time sparks flickering on the great gears.
        var sparks = Sheet("fx_timespark");
        if (sparks.Length > 0)
            foreach (var (x, y) in new[] { (12f, 13f), (46.5f, 25f), (74f, 26f), (96f, 28f), (139f, 37f) })
            {
                var sp = Place("white", new Vector2(x, y), fx, -44, _neonAdd, new Color(0.6f, 0.95f, 1f, 0.8f), 2.2f);
                sp.name = "Time Spark";
                sp.sprite = sparks[0];
                var a = sp.gameObject.AddComponent<SpriteFrameAnimator>();
                a.frames = sparks;
                a.framesPerSecond = 18f;
                a.obeysTimeFreeze = true;
            }
    }

    // ------------------------------------------------------------------ boss: the Clockwork Eye

    /// <summary>The top floor before the door: the Clockwork Eye. Both sides seal; the door opens when it falls.</summary>
    static void BuildAreaCEnemies()
    {
        BuildEnemyAssets();
        var root = Group("Enemies").transform;
        // Rust-red spore caps on the high ledges of the climb.
        SpawnRedMushroom(root, 24f);
        SpawnRedMushroom(root, 36f, 15f);
        SpawnRedMushroom(root, 53f, 22f);
        SpawnRedMushroom(root, 117.5f);

        // The summit: the Clockwork Eye first, then the Horned Beast climbs up.
        float floor = ASurf(145f);
        var clockEye = SpawnClockworkEye(root, 150f, floor, floor + 5.5f, 130.5f, 162.5f);
        var beast = SpawnBeast(root, 148f, 129.5f, 163.5f);
        var arena = ArenaAt(root, 129f, 164.2f, floor, "The Clockwork Eye", new[] { clockEye }, new[] { beast }, lockBehind: true);
        arena.introLines = CBoss;
        var exit = GameObject.Find("Exit Trigger");
        if (exit) arena.enableOnClear = new[] { exit };
    }

    static void BuildAreaCCheckpoints()
    {
        var root = Group("Checkpoints").transform;
        foreach (var x in new[] { 35f, 51f, 106f, 126.5f })
        {
            var go = Group($"Checkpoint x{x}", root);
            go.transform.position = new Vector3(x, ASurf(x) + 0.1f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 6f);
            box.offset = new Vector2(0f, 3f);
            go.AddComponent<Checkpoint>();
        }

        // Bell landing altar: the safe breath before the bridge chase.
        float ax = 81f;
        var altar = new GameObject("Checkpoint Altar (bell landing)");
        altar.transform.SetParent(root, false);
        altar.transform.position = new Vector3(ax, ASurf(ax) - 0.1f, 0f);
        var asr = Place("altar_cyan_0", new Vector2(ax, ASurf(ax) - 0.1f), altar.transform, 7, _neonTex, Color.white, 0.6f);
        asr.transform.localPosition = Vector3.zero;
        var anim = asr.gameObject.AddComponent<SpriteFrameAnimator>();
        anim.framesPerSecond = 7f;
        var trig = altar.AddComponent<BoxCollider2D>();
        trig.isTrigger = true;
        trig.size = new Vector2(2f, 4f);
        trig.offset = new Vector2(0f, 2f);
        var ca = altar.AddComponent<CheckpointAltar>();
        ca.flame = anim;
        ca.cyanFrames = Enumerable.Range(0, 4).Select(i => _sprites[$"altar_cyan_{i}"]).ToArray();
        ca.amberFrames = Enumerable.Range(0, 4).Select(i => _sprites[$"altar_amber_{i}"]).ToArray();
        anim.frames = ca.cyanFrames;
        ca.altarLight = LocalLight("Altar Light", altar.transform, new Vector2(0f, 2.2f), new Color(0.3f, 0.9f, 1f), 5f, 1.8f);
        ca.altarLight.gameObject.AddComponent<PulseLight2D>().pulseAmount = 0.2f;
        ca.igniteBurst = _emberBurst;

        // 8. The arena door: the ornate gothic frame, cold light beyond, the boss waiting.
        float gx = 166f, gy = ASurf(gx);
        var door = Group("Arena Door (to the Ash Warden)", root);
        if (_sprites.TryGetValue("areaC_door", out var ds) && ds)
        {
            var frame = Place("areaC_door", new Vector2(gx, gy - 1.2f), door.transform, -8, _lit, new Color(0.8f, 0.76f, 0.95f));
            frame.name = "Gothic Arch Frame";
        }
        var beyond = Glow(new Vector2(gx, gy + 4f), door.transform, new Color(1f, 0.5f, 0.2f, 0.35f), 12f, -9, _neonSoft);
        beyond.name = "Furnace Light Beyond";
        beyond.transform.localScale = new Vector3(3.2f, 2f, 1f);
        PointLight("Door Light", new Vector2(gx, gy + 3f), door.transform, new Color(1f, 0.5f, 0.2f), 9f, 1.6f).gameObject.AddComponent<FlickerLight2D>();
        var exit = Group("Exit Trigger", door.transform);
        exit.transform.position = new Vector3(gx + 4f, gy, 0f);
        var et = exit.AddComponent<BoxCollider2D>();
        et.isTrigger = true;
        et.size = new Vector2(1f, 8f);
        et.offset = new Vector2(0f, 4f);
        var le = exit.AddComponent<LevelExit>();
        le.title = "Dawn";
        le.subtitle = "The clock moves again";
        le.nextScene = "Cinematic_Ending";
    }
}
