using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Area A - The Burning Village (Ashen Bond GDD section 5). Built from the Mini Legend terrain (recoloured to
/// charred soil), props cut from the village backgrounds, and the new village parallax set.
/// Menu: Tools > Ashen Bond > Build Area A - Burning Village.
/// </summary>
public static partial class DarkForestBuilder
{
    const string AreaRoot = Root + "/AreaA";
    const string AreaSpriteDir = AreaRoot + "/Sprites";
    const string AreaBgDir = AreaRoot + "/BG";
    const string AreaTileDir = TileDir + "/AreaA";
    const string AreaScenePath = SceneDir + "/AreaA_BurningVillage.unity";
    const string AreaTerrain = AreaSpriteDir + "/areaA_terrain.png";

    // Ground: (first column, last column, height in tiles). Height 0 = fire pit.
    static readonly (int x0, int x1, int h)[] AreaGround =
    {
        // Screen 1 - escape the burning house, a beam falls ahead
        (0, 13, 5), (14, 27, 6), (28, 33, 5),
        // Screen 2 - waist-high cart, up onto the roofs, a burning gap too wide to jump: shoot the cracked tree into a bridge
        (34, 44, 5), (45, 46, 8), (47, 58, 0), (59, 62, 8), (63, 69, 5),
        // Screen 3 - fire jet, thorn cellar under platforms, spirit platform over the fire pit
        (70, 75, 5), (76, 89, 2), (90, 93, 6), (94, 101, 0),
        // Screen 4 - village square, altar checkpoint, ledge, fire jets, the collapsed gate
        (102, 133, 6), (134, 136, 8), (137, 155, 10),
    };

    // One-way floating platforms: (first column, last column, top tile row). Walk surface = top + 1.
    static readonly (int x0, int x1, int top)[] AreaPlatforms =
    {
        (78, 81, 4), (84, 87, 5),   // over the thorn strips
        (122, 125, 9),              // high ledge with an ember
        (29, 31, 9),                // optional high perch on screen 1
        (8, 10, 8), (19, 21, 10), (37, 39, 9), (64, 66, 9), (105, 107, 10), (115, 117, 10), (129, 131, 11),   // air ledges
    };

    static readonly (float x0, float x1)[] AreaThorns = { (78f, 82f), (84f, 88f) };

    static Dictionary<Vector2Int, Tile> _aTiles;
    static Material _ash, _emberSquare, _fireSquare;
    static GameObject _beamImpact, _emberBurst;

    [MenuItem("Tools/Ashen Bond/Build Area A - Burning Village")]
    public static void BuildAreaA()
    {
        var active = SceneManager.GetActiveScene();
        if (active.isDirty && active.path != AreaScenePath && active.path != ScenePath)
        {
            Debug.LogWarning("[AreaA] Active scene has unsaved changes. Save it first, then build again.");
            return;
        }

        _rng = new System.Random(2026);
        _ground = AreaGround;
        _platforms = AreaPlatforms;
        MapWidth = 156;
        PlayerSpawn = new Vector2(4f, 5.2f);

        EnsureFolders();
        if (!AssetDatabase.IsValidFolder(AreaTileDir)) AssetDatabase.CreateFolder(TileDir, "AreaA");
        ImportSprites();
        ImportBackgrounds();
        ImportAreaSprites();
        BuildTiles();
        BuildAreaTiles();
        BuildMaterials();
        BuildAreaMaterials();
        BuildVolumeProfile();
        var profile = BuildAreaVolumeProfile();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = BuildCameraAndPost(profile);
        var global = Object.FindFirstObjectByType<Light2D>();
        if (global && global.lightType == Light2D.LightType.Global)
        {
            global.color = new Color(0.5f, 0.55f, 1f);   // blue night against orange fire (GDD palette)
            global.intensity = 0.36f;
            global.name = "Global Light 2D (night)";
        }

        BuildAreaSky();
        BuildAreaParallax();
        // Area A's "lightning": houses in the far village row blow up now and then.
        var blasts = new GameObject("Distant Village Blasts").AddComponent<DistantBlasts>();
        blasts.layer = _blastLayer;
        blasts.globalLight = global;
        blasts.blastPrefab = BuildVillageBlastPrefab();
        blasts.heightRange = new Vector2(-5.5f, -3.5f);
        BuildAreaTerrain();
        BuildAreaColliders();
        BuildAreaSetDressing();
        BuildAreaProps();
        BuildAreaHazards();
        BuildAreaAtmosphere();
        BuildAreaForeground();
        var player = BuildPlayer();
        BuildShadowAbility(player);
        BuildAreaCheckpoints();
        BuildAreaEnemies();
        BuildRewind(player);
        BuildStoryUI(AStart);
        BuildGroundDepth(new Color(0.045f, 0.025f, 0.05f));
        BuildAudio('A');
        AreaTutorials();
        ExitLines(AExit);
        RefineSquareFire();

        var follow = cam.gameObject.AddComponent<CameraFollow2D>();
        follow.target = player.transform;
        follow.boundsMin = new Vector2(0f, 1.8f);   // less dead ground under the street
        follow.boundsMax = new Vector2(MapWidth, 40f);
        follow.offset = new Vector2(0f, 1.6f);
        var hud = new GameObject("HUD").AddComponent<AreaHUD>();
        hud.health = player.GetComponent<PlayerHealth>();
        hud.shadow = player.GetComponentInChildren<ShadowSummon>();
        if (_sprites.TryGetValue("icon_lantern", out var li)) hud.heartIcon = li.texture;
        if (_sprites.TryGetValue("icon_sister", out var si)) hud.sisterIcon = si.texture;

        EditorSceneManager.SaveScene(scene, AreaScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == AreaScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(AreaScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        SortBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[AreaA] Scene built: " + AreaScenePath);
    }

    // ------------------------------------------------------------------ assets

    /// <summary>Area A grading: navy shadows, ember highlights, restrained bloom (GDD: orange fire vs blue shadow).</summary>
    static UnityEngine.Rendering.VolumeProfile BuildAreaVolumeProfile()
    {
        var path = $"{AreaRoot}/AreaA_PostFX.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);      // a touch more glow on fires, embers and the lantern
        bloom.intensity.Override(1.05f);
        bloom.scatter.Override(0.72f);
        var ca = profile.Add<ChromaticAberration>(true);   // faint lens fringe at the edges: heat + cinema
        ca.intensity.Override(0.05f);   // subtle: strong fringe reads as blur on pixel art
        bloom.tint.Override(new Color(1f, 0.85f, 0.75f));

        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.1f);
        color.contrast.Override(20f);
        color.saturation.Override(8f);
        color.colorFilter.Override(new Color(0.95f, 0.96f, 1f));

        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(new Color(0.22f, 0.3f, 0.7f));    // navy shadow
        split.highlights.Override(new Color(1f, 0.6f, 0.3f));    // ember highlight
        split.balance.Override(-20f);

        var vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.02f, 0.03f, 0.1f));
        vignette.intensity.Override(0.36f);
        vignette.smoothness.Override(0.45f);

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static void ImportBg(string path, int ppu)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[AreaA] Missing " + path); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        ti.SetTextureSettings(settings);
        ti.SaveAndReimport();
        _sprites[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void ImportAreaSprites()
    {
        foreach (var n in new[] { "Area A- 2", "Area A-3", "Area A-4", "Pine Cone 5" })
            ImportBg($"{BgDir}/{n}.png", BgPPU);
        ImportBg($"{AreaBgDir}/AreaA_sky.png", BgPPU);

        var ppu = new Dictionary<string, int>
        {
            { "house_big", 60 }, { "house_left", 60 }, { "house_right", 60 }, { "house_far", 60 }, { "shed", 60 },
            { "cart", 80 }, { "gallows", 55 }, { "fence_broken", 70 },
        };
        string[] centered = { "ember_gem", "spirit_platform_0", "spirit_platform_1", "spirit_platform_2", "beam" };

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AreaSpriteDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            bool atlas = path == AreaTerrain;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = atlas ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu.TryGetValue(name, out var p) ? p : PPU;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)(centered.Contains(name) ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        // Slice the terrain sheet into every non-empty 16x16 tile, named a_col_row.
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AreaTerrain);
        var readable = new Texture2D(2, 2);
        readable.LoadImage(File.ReadAllBytes(AreaTerrain));
        var rects = new List<SpriteRect>();
        for (int c = 0; c < tex.width / 16; c++)
            for (int r = 0; r < tex.height / 16; r++)
            {
                bool any = false;
                for (int py = 0; py < 16 && !any; py++)
                    for (int px = 0; px < 16 && !any; px++)
                        any = readable.GetPixel(c * 16 + px, tex.height - (r + 1) * 16 + py).a > 0f;
                if (!any) continue;
                rects.Add(new SpriteRect
                {
                    name = $"a_{c}_{r}",
                    rect = new Rect(c * 16, tex.height - (r + 1) * 16, 16, 16),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                });
            }
        Object.DestroyImmediate(readable);

        var tiImporter = (TextureImporter)AssetImporter.GetAtPath(AreaTerrain);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(tiImporter);
        dp.InitSpriteEditorDataProvider();
        dp.SetSpriteRects(rects.ToArray());
        dp.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        dp.Apply();
        tiImporter.SaveAndReimport();

        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { AreaSpriteDir }))
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>())
                _sprites[s.name] = s;
    }

    static void BuildAreaTiles()
    {
        _aTiles = new Dictionary<Vector2Int, Tile>();
        foreach (var kv in _sprites.Where(k => k.Key.StartsWith("a_")))
        {
            var parts = kv.Key.Split('_');
            var cell = new Vector2Int(int.Parse(parts[1]), int.Parse(parts[2]));
            var path = $"{AreaTileDir}/{kv.Key}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = kv.Value;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            _aTiles[cell] = tile;
        }
    }

    static void BuildAreaMaterials()
    {
        var square = _sprites["white"].texture;   // GDD: pixel-square particles, not soft circles
        _ash = Neon("M_Ash", Color.white, 1f, 1f, false, 0f, 0f);
        _ash.mainTexture = square;
        _emberSquare = Neon("M_EmberSquare", Color.white, 2.4f, 1f, true, 4f, 0.4f, 0.3f);
        _emberSquare.mainTexture = square;
        _fireSquare = Neon("M_FireSquare", Color.white, 1.5f, 1f, true, 0f, 0f, 0.3f);
        _fireSquare.mainTexture = square;

        var stars = SaveMaterial("M_StarField", Shader.Find("DarkForest/StarField"), m =>
        {
            m.SetFloat("_StarDensity", 0.012f);
            m.SetFloat("_StarGrid", 280f);
            m.SetFloat("_StarBrightness", 1.1f);
            m.SetFloat("_FadeStart", 0.5f);
        });
    }

    // ------------------------------------------------------------------ layout helpers

    // Active layout (Area A or Area B) used by the shared helpers below.
    static (int x0, int x1, int h)[] _ground = AreaGround;
    static (int x0, int x1, int top)[] _platforms = AreaPlatforms;
    const int SlabRows = 4;   // a thicker crust; below it the ground fades into dark earth (BuildGroundDepth)

    static int AreaH(int x)
    {
        x = Mathf.Clamp(x, 0, MapWidth - 1);
        foreach (var g in _ground)
            if (x >= g.x0 && x <= g.x1) return g.h;
        return 0;
    }

    static float ASurf(float x) => AreaH(Mathf.FloorToInt(x));

    static void SetZ(Transform t, float z) => t.position = new Vector3(t.position.x, t.position.y, z);

    // ------------------------------------------------------------------ sky, moon, stars, parallax

    static void BuildAreaSky()
    {
        Vector2 c = _cam.position;

        var sky = Group("Sky (parallax 1.0)");
        sky.transform.position = new Vector3(c.x, c.y, 120f);
        var bg = Place("white", c, sky.transform, -100, _sky);
        bg.name = "NightSky";
        bg.transform.localScale = new Vector3(48f, CamSize * 2f + 6f, 1f);
        bg.transform.localPosition = Vector3.zero;
        Parallax(sky, 1f, 1f);

        // Stars and moon sit in FRONT of the opaque painted sky (layer -95) so they are always visible.
        var stars = Group("Stars (parallax 1.0)");
        stars.transform.position = new Vector3(c.x, c.y, 96f);
        var starMat = SaveMaterial("M_StarField", Shader.Find("DarkForest/StarField"), null);
        var sf = Place("white", c, stars.transform, -94, starMat);
        sf.name = "Twinkling Stars";
        sf.transform.localScale = new Vector3(48f, CamSize * 2f + 6f, 1f);
        sf.transform.localPosition = Vector3.zero;
        Parallax(stars, 1f, 1f);

        var moonRoot = Group("Moon (parallax 0.97)");
        var moonPos = c + new Vector2(7f, 6.2f);
        moonRoot.transform.position = new Vector3(moonPos.x, moonPos.y, 92f);
        LocalGlow("Moon Halo", moonRoot.transform, Vector2.zero, new Vector2(14f, 14f), new Color(0.55f, 0.6f, 1f, 0.45f), -93, _neonSoft);
        LocalGlow("Moon Glow", moonRoot.transform, Vector2.zero, new Vector2(5.5f, 5.5f), new Color(0.85f, 0.85f, 1f, 0.55f), -92, _neonSoft);
        var moon = Place("moon", moonPos, moonRoot.transform, -91, _neonTex, new Color(0.95f, 0.93f, 1f), 1.1f);
        moon.name = "Moon";
        moon.transform.localPosition = Vector3.zero;
        Parallax(moonRoot, 0.97f, 0.97f);
    }

    /// <summary>
    /// Village parallax set, far to near. Every layer gets its own Z so the Scene view shows them as
    /// separated planes (like a diorama). The orthographic camera ignores Z for size, so the parallax
    /// factor is what sells the depth in game.
    /// </summary>
    static void BuildAreaParallax()
    {
        var root = Group("Parallax Backgrounds").transform;
        Vector2 c = _cam.position;
        //            sprite          order   fx     fy     yOff  z    scale tint                                   boost
        var layers = new (string sprite, int order, float fx, float fy, float yOffset, float z, float scale, Color tint, float boost)[]
        {
            ("AreaA_sky",   -95, 0.92f, 1.00f, 5.0f, 100f, 1.00f, new Color(0.8f, 0.82f, 1f),   1.4f),  // smoky night sky, far fires
            ("Pine Cone 5", -88, 0.85f, 0.95f, 2.9f,  85f, 0.42f, new Color(0.42f, 0.42f, 0.62f, 0.55f), 0.75f), // burning forest on the hill (lost in haze)
            // Mid layers scaled up and pushed darker/cooler so the lit player separates from them (Limbo / Hollow Knight depth).
            ("Area A- 2",   -84, 0.78f, 0.92f, 3.9f,  70f, 0.60f, new Color(0.5f, 0.48f, 0.72f, 0.7f), 0.95f), // far village row (hazy, fires dimmed)
            ("Area A-3",    -78, 0.60f, 0.88f, -1.2f, 50f, 0.70f, new Color(0.5f, 0.43f, 0.68f),  1.6f), // mid village
            ("Area A-4",    -70, 0.40f, 0.80f, -2.2f, 25f, 0.50f, new Color(0.34f, 0.29f, 0.48f), 1.8f), // near debris
        };

        foreach (var l in layers)
        {
            if (!_sprites.TryGetValue(l.sprite, out var sprite) || sprite == null) continue;
            var mat = SaveMaterial($"M_BG_{l.sprite.Replace(" ", "")}", Shader.Find("DarkForest/FireBackdrop"), m =>
            {
                m.SetFloat("_FireBoost", l.boost);
                m.SetFloat("_Blur", l.fx >= 0.9f ? 0f : Mathf.Max(0f, (l.fx - 0.3f) * 12f));   // farther = softer (depth of field on the player)
                m.SetFloat("_SmokeDistort", 0.004f);
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
            LayerBase(layer.transform, sprite, l.order, l.tint);
            if (l.sprite == "Area A- 2") _blastLayer = layer.transform;

            if (l.sprite == "Area A- 2")
            {
                // Slow smoke drifting in front of the far row: the distance sinks into smoke.
                var far = BackgroundParticles("Far Smoke", layer.transform, new Vector2(0f, -1f), new Vector2(sprite.bounds.size.x * 2f, 5f), _smoke, -83,
                    3f, new Vector2(12f, 18f), new Vector2(0.1f, 0.35f), new Vector2(6f, 10f),
                    Grad((new Color(0.5f, 0.42f, 0.6f), 0f, 0f), (new Color(0.45f, 0.4f, 0.58f), 0.3f, 0.35f), (new Color(0.3f, 0.28f, 0.42f), 1f, 0f)), 160);
                var fg = far.sizeOverLifetime;
                fg.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f));
            }

            if (l.sprite == "Area A-3")
            {
                BackgroundParticles("BG Embers", layer.transform, new Vector2(0f, -4f), new Vector2(sprite.bounds.size.x * 2f, 4f), _emberSquare, -76,
                    26f, new Vector2(2.5f, 5f), new Vector2(0.6f, 1.6f), new Vector2(0.06f, 0.11f),
                    Grad((FireCore, 0f, 0f), (FireOrange, 0.15f, 1f), (FireMagenta, 1f, 0f)), 300);
                var puffs = BackgroundParticles("BG Smoke", layer.transform, new Vector2(0f, 0f), new Vector2(sprite.bounds.size.x * 2f, 3f), _smoke, -80,
                    4f, new Vector2(8f, 12f), new Vector2(0.25f, 0.6f), new Vector2(3f, 6f),
                    Grad((new Color(1f, 0.45f, 0.2f), 0f, 0f), (new Color(0.3f, 0.26f, 0.4f), 0.25f, 0.3f), (new Color(0.15f, 0.12f, 0.22f), 1f, 0f)), 220);
                var grow = puffs.sizeOverLifetime;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.4f));
            }
        }

        // Thin haze between the depth planes.
        SetZ(Fog(root, 2f, 12f, new Color(0.42f, 0.42f, 0.72f), -86, 0.5f).transform, 80f);   // thick haze over the far hill
        SetZ(Fog(root, 2f, 11f, new Color(0.45f, 0.4f, 0.65f), -81, 0.46f).transform, 60f);    // haze over the far village row
        SetZ(Fog(root, 1f, 9f, new Color(0.35f, 0.36f, 0.62f), -74, 0.22f).transform, 40f);
        SetZ(Fog(root, -1f, 7f, new Color(0.18f, 0.2f, 0.42f), -60, 0.36f).transform, 15f);
    }

    static Transform _blastLayer;

    // ------------------------------------------------------------------ terrain

    static Tile AT(int c, int r) => _aTiles.TryGetValue(new Vector2Int(c, r), out var t) ? t : null;

    static readonly Vector2Int[] FillTiles = { new(5, 6), new(5, 7), new(5, 8), new(4, 9), new(3, 9), new(2, 9), new(5, 7), new(2, 6) };

    static void BuildAreaTerrain()
    {
        var grid = Group("Grid");
        grid.AddComponent<Grid>();

        Tilemap Map(string name, int order)
        {
            var go = Group(name, grid.transform);
            var map = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sharedMaterial = _lit;
            r.sortingOrder = order;
            return map;
        }

        var ground = Map("Ground (charred soil)", 0);
        var overhang = Map("Overhang (tufts, edges)", 1);
        var platforms = Map("Platforms", 0);

        for (int x = -2; x < MapWidth + 2; x++)
        {
            int h = AreaH(x);
            if (h <= 0) continue;
            for (int y = Mathf.Max(-3, h - SlabRows); y < h; y++)
            {
                bool top = y == h - 1;
                bool openL = y >= AreaH(x - 1) || x - 1 < -2;
                bool openR = y >= AreaH(x + 1) || x + 1 > MapWidth + 1;
                Vector2Int t;
                if (top) t = openL ? new Vector2Int(1, 3) : openR ? new Vector2Int(6, 3) : new Vector2Int((x & 1) == 0 ? 3 : 4, 3);
                else if (openL) t = new Vector2Int(1, 5 + (y & 3));
                else if (openR) t = new Vector2Int(6, 5 + (y & 3));
                else t = y == h - SlabRows ? new Vector2Int(2 + (((x % 4) + 4) % 4), 9) : FillTiles[((x * 3 + y * 7) & 0x7fffffff) % FillTiles.Length];
                ground.SetTile(new Vector3Int(x, y, 0), AT(t.x, t.y));

                int fr = 4 + (((y % 5) + 5) % 5);
                if (openL && x > 0) overhang.SetTile(new Vector3Int(x - 1, y, 0), AT(0, top ? 3 : fr));
                if (openR && x < MapWidth - 1) overhang.SetTile(new Vector3Int(x + 1, y, 0), AT(7, top ? 3 : fr));
            }
            overhang.SetTile(new Vector3Int(x, h, 0), AT(8 + (((x % 3) + 3) % 3), 3));   // tufts on top
            overhang.SetTile(new Vector3Int(x, h - SlabRows - 1, 0), AT(2 + (((x % 4) + 4) % 4), 10));   // drips under the crust
        }

        foreach (var p in AreaPlatforms)
        {
            for (int x = p.x0; x <= p.x1; x++)
            {
                int col = x == p.x0 ? 11 : x == p.x1 ? 15 : 12 + ((x - p.x0) % 3);
                platforms.SetTile(new Vector3Int(x, p.top, 0), AT(col, 1));
                overhang.SetTile(new Vector3Int(x, p.top + 1, 0), AT(col, 0));
                overhang.SetTile(new Vector3Int(x, p.top - 1, 0), AT(2 + (x % 4), 10));   // drips
            }
        }
    }

    static void BuildAreaColliders()
    {
        var go = Group("Level Colliders");
        go.isStatic = true;
        const float bottom = -6f;

        foreach (var g in AreaGround)
        {
            if (g.h <= 0) continue;
            var box = go.AddComponent<BoxCollider2D>();
            float top = g.h - 0.1f;
            box.offset = new Vector2((g.x0 + g.x1 + 1) / 2f, (top + bottom) / 2f);
            box.size = new Vector2(g.x1 - g.x0 + 1, top - bottom);
        }
        foreach (var wallX in new[] { -0.5f, MapWidth + 0.5f })
        {
            var wall = go.AddComponent<BoxCollider2D>();
            wall.offset = new Vector2(wallX, 20f);
            wall.size = new Vector2(1f, 60f);
        }

        var plats = Group("Platforms (one-way)");
        foreach (var p in AreaPlatforms)
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
        eff.useSideFriction = false;

        // Fire pits: burning floor that sends you back.
        foreach (var g in AreaGround.Where(g => g.h <= 0))
        {
            var pit = Group($"Fire Pit x{g.x0}-{g.x1}");
            var trig = pit.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.offset = new Vector2((g.x0 + g.x1 + 1) / 2f, -1f);
            trig.size = new Vector2(g.x1 - g.x0 + 1, 4f);
            pit.AddComponent<KillZone>();

            float cx = (g.x0 + g.x1 + 1) / 2f;
            float w = g.x1 - g.x0 + 1;
            var flames = BackgroundParticles("Pit Flames", pit.transform, new Vector2(cx, 0.3f), new Vector2(w, 0.2f), _fireSquare, 4,
                30f * w, new Vector2(0.6f, 1.2f), new Vector2(1.5f, 3.5f), new Vector2(0.35f, 0.75f), FireGradient(), 400);
            flames.transform.position = new Vector3(cx, 0.3f, 0f);
            var embers = BackgroundParticles("Pit Embers", pit.transform, new Vector2(cx, 1f), new Vector2(w, 0.5f), _emberSquare, 5,
                5f * w, new Vector2(1.5f, 3f), new Vector2(1.5f, 4f), new Vector2(0.07f, 0.12f),
                Grad((FireCore, 0f, 1f), (FireOrange, 0.4f, 1f), (FireMagenta, 1f, 0f)), 120);
            embers.transform.position = new Vector3(cx, 1f, 0f);
            var glow = Glow(new Vector2(cx, 1.2f), pit.transform, new Color(1f, 0.45f, 0.12f, 0.75f), w * 1.2f, 3, _neonSoft);
            glow.transform.localScale = new Vector3(w * 1.2f / 4f, 0.9f, 1f);
            var l = PointLight("Pit Light", new Vector2(cx, 2f), pit.transform, new Color(1f, 0.45f, 0.15f), w * 1.1f, 2.4f);
            l.gameObject.AddComponent<FlickerLight2D>();
        }
    }

    // ------------------------------------------------------------------ set dressing, props

    static SpriteRenderer House(string sprite, float x, bool flip, float fireLight = 1f)
    {
        var parent = GameObject.Find("Village Set Dressing")?.transform ?? Group("Village Set Dressing").transform;
        var sr = Place(sprite, new Vector2(x, ASurf(x) - 0.15f), parent, -36, _unlit, new Color(0.82f, 0.78f, 0.92f), 1f, flip);
        SetZ(sr.transform, 8f);
        // Living fire + smoke where the painted (static) fire was cut out of the sprite.
        AddPropFires(sr, sprite, -36, flip, 1.6f * fireLight);
        return sr;
    }

    static readonly Color Charred = new Color(0.62f, 0.52f, 0.56f);

    static void BuildAreaSetDressing()
    {
        _emberBurst = BuildEmberBurstPrefab();
        _beamImpact = BuildBeamImpactPrefab();   // also used by the falling trees below
        House("house_big", 8f, false);
        House("house_left", 24f, false);
        House("shed", 41.5f, false, 0.7f);
        House("house_right", 66f, false);
        House("house_far", 83f, false, 0.8f);
        House("house_big", 111f, true);
        House("house_right", 129f, true);
        House("house_far", 143f, true);

        var dress = GameObject.Find("Village Set Dressing").transform;
        // Real (Cainos) fences and a well instead of flat cut-out pictures; charred, lit by the fires.
        var fenceRow = Group("Fences", dress);
        SetZ(fenceRow.transform, 4f);
        foreach (var (x, kind) in new[] { (16f, "Fence A"), (66.5f, "Fence B"), (103.5f, "Fence B"), (138f, "Fence A") })
            PlaceCainos(kind, new Vector2(x, ASurf(x)), fenceRow.transform, -20, Charred, 1.2f, _rng.Next(2) == 0);
        PlaceCainos("Well", new Vector2(121f, ASurf(121f)), fenceRow.transform, -22, Charred, 1.1f);

        // A few charred trees standing in the village, rocking in the hot wind.
        var tint = new Color(0.06f, 0.04f, 0.09f);
        foreach (var (name, x, s) in new[] { ("dead_a", 3f, 0.6f), ("dead_c", 35f, 0.8f), ("pine_m_b", 72f, 0.6f), ("dead_b", 92f, 0.6f), ("pine_s_a", 135f, 0.8f), ("dead_c", 152f, 0.8f) })
        {
            var t = Place(name, new Vector2(x, ASurf(x) - 0.2f), dress, -40, _unlit, tint, s, _rng.Next(2) == 0);
            SetZ(t.transform, 10f);
            Wind(t, 0.7f, 0.9f);
        }

        // Two burnt trees crack and come down as the boy passes (pure spectacle, behind the street).
        ToppleTree(dress, "dead_c", 65.5f, ASurf(65.5f), tint, 0.8f, -1f, 6f);
        ToppleTree(dress, "pine_m_b", 142f, ASurf(142f), tint, 0.7f, -1f, 7f);
    }

    static GameObject BuildEmberBurstPrefab()
    {
        var root = new GameObject("EmberCollectBurst");
        var flash = LocalGlow("Flash", root.transform, Vector2.zero, new Vector2(2f, 2f), new Color(1f, 0.6f, 0.25f, 0.9f), 18, _neonAdd);
        OneShot("Sparks", root.transform, Vector2.zero, _emberSquare, 19, 24, new Vector2(0.3f, 0.7f), new Vector2(2f, 5f), new Vector2(0.08f, 0.14f),
                Grad((FireCore, 0f, 1f), (FireOrange, 0.5f, 1f), (FireMagenta, 1f, 0f)), 0.2f, 360f, 0.6f, 0.2f);
        LocalLight("Flash Light", root.transform, Vector2.zero, new Color(1f, 0.6f, 0.25f), 3f, 2f);
        var vfx = root.AddComponent<VfxLifetime>();
        vfx.sustain = 0.1f; vfx.fade = 0.4f; vfx.flashes = new[] { flash }; vfx.flashDuration = 0.35f; vfx.flashGrow = 2f;
        return SavePrefab(root, "EmberCollectBurst");
    }

    static GameObject BuildBeamImpactPrefab()
    {
        var root = new GameObject("BeamImpact");
        var ring = LocalGlow("Dust Ring", root.transform, new Vector2(0f, 0.2f), new Vector2(4f, 0.6f), new Color(1f, 0.55f, 0.25f, 0.8f), 18, _neonAdd);
        OneShot("Embers", root.transform, new Vector2(0f, 0.2f), _emberSquare, 19, 40, new Vector2(0.5f, 1.1f), new Vector2(3f, 8f), new Vector2(0.08f, 0.16f),
                Grad((FireCore, 0f, 1f), (FireOrange, 0.4f, 1f), (FireMagenta, 1f, 0f)), 1.2f, 180f, 0.9f, 0.3f);
        OneShot("Ash Smoke", root.transform, new Vector2(0f, 0.4f), _smoke, 10, 14, new Vector2(0.8f, 1.6f), new Vector2(0.6f, 2f), new Vector2(1.2f, 2.4f),
                Grad((new Color(0.5f, 0.4f, 0.4f), 0f, 0f), (new Color(0.25f, 0.2f, 0.3f), 0.2f, 0.6f), (new Color(0.1f, 0.08f, 0.15f), 1f, 0f)), 1.5f, 180f, -0.1f, 0.3f);
        LocalLight("Impact Light", root.transform, new Vector2(0f, 0.8f), new Color(1f, 0.5f, 0.2f), 6f, 2.5f);
        var vfx = root.AddComponent<VfxLifetime>();
        vfx.sustain = 0.15f; vfx.fade = 0.6f; vfx.flashes = new[] { ring }; vfx.flashDuration = 0.4f; vfx.flashGrow = 2.5f;
        return SavePrefab(root, "BeamImpact");
    }

    static void Ember(Transform parent, float x, float y)
    {
        var go = new GameObject("Ember Pickup");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        var gem = Place("ember_gem", new Vector2(x, y), go.transform, 15, _neonTex, new Color(1f, 0.55f, 0.2f), 1.2f);
        gem.transform.localPosition = Vector3.zero;
        LocalGlow("Glow", go.transform, Vector2.zero, new Vector2(2.4f, 2.4f), new Color(1f, 0.5f, 0.15f, 0.6f), 14, _neonSoft);
        LocalLight("Ember Light", go.transform, Vector2.zero, new Color(1f, 0.55f, 0.2f), 2.5f, 1.2f).gameObject.AddComponent<PulseLight2D>();
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        go.AddComponent<FloatBob>().amplitude = 0.2f;
        go.AddComponent<EmberPickup>().collectBurst = _emberBurst;
    }

    static void Campfire(Transform parent, float x, float scale = 1.2f)
    {
        var sr = Place("campfire_0", new Vector2(x, ASurf(x) - 0.1f), parent, 9, _neonTex, new Color(1f, 0.9f, 0.85f), scale);
        sr.name = "Debris Fire";
        var anim = sr.gameObject.AddComponent<SpriteFrameAnimator>();
        anim.frames = Enumerable.Range(0, 4).Select(i => _sprites[$"campfire_{i}"]).ToArray();
        anim.framesPerSecond = 9f;
        var l = PointLight("Debris Fire Light", new Vector2(x, ASurf(x) + 1f), sr.transform, new Color(1f, 0.5f, 0.2f), 4.5f, 1.5f);
        l.gameObject.AddComponent<FlickerLight2D>();
        Particles("Sparks", new Vector2(x, ASurf(x) + 0.8f), sr.transform, _emberSquare, 10, 6f, new Vector2(0.8f, 1.6f), new Vector2(0.8f, 2f),
                  new Vector2(0.06f, 0.1f), Grad((FireCore, 0f, 1f), (FireOrange, 0.5f, 1f), (FireMagenta, 1f, 0f)), 0.3f, -0.05f, 0.8f, 30);
    }

    static void BuildAreaProps()
    {
        var props = Group("Props").transform;

        // Waist-high wrecked cart (GDD screen two): a real overturned barrow, burning and smoking.
        var cart = Group("Wrecked Cart (jump over)", props);
        cart.transform.position = new Vector3(39f, ASurf(39f), 0f);
        PlaceCainos("Wheelbarrow", new Vector2(39f, ASurf(39f) - 0.05f), cart.transform, 8, Charred, 1.15f, true);
        var cb = cart.AddComponent<BoxCollider2D>();
        cb.size = new Vector2(3.2f, 1.5f);
        cb.offset = new Vector2(0f, 0.75f);
        PropFire(cart.transform, new Vector2(39.6f, ASurf(39f) + 1.1f), 1.4f, 9, 1.7f, 5f);
        PlaceCainos("Barrel", new Vector2(41.2f, ASurf(41f)), cart.transform, 7, Charred, 1.1f);

        foreach (var x in new[] { 11.5f, 26f, 46.5f, 60.5f, 91.5f, 104f, 127f, 140f, 150.5f })
            Campfire(props, x);

        // Sparse village clutter (burnt). Kept light on purpose: silhouettes read better than busy streets.
        var clutter = Group("Clutter", props).transform;
        foreach (var (prop, x, s, flip) in new[]
        {
            ("Barrel", 6.5f, 1.1f, false), ("Wood Logs", 13.5f, 0.9f, false), ("Crate Large", 28.5f, 1f, false), ("Crate Small", 29.6f, 1f, false),
            ("Gunny Bag 01", 44f, 1.1f, false), ("Pot 02", 64.5f, 1.2f, false), ("Barricade", 69.3f, 1.1f, true),
            ("Wood Log 01", 90.6f, 0.9f, false), ("Hay Pile", 133f, 1f, false), ("Crate Large", 144.5f, 1f, false),
        })
            PlaceCainos(prop, new Vector2(x, ASurf(x)), clutter, 5, Charred, s, flip);

        var chest = Place("chest_0", new Vector2(118.5f, ASurf(118.5f) - 0.05f), props, 8, _lit, null, 1.3f);
        chest.name = "Chest";

        var embers = Group("Embers (pickups)").transform;
        Ember(embers, 22f, 8.2f);
        Ember(embers, 54f, 11.4f);        // hop for it on the tree bridge
        Ember(embers, 65f, 6.6f);
        Ember(embers, 80f, 6.8f);
        Ember(embers, 86f, 7.8f);
        Ember(embers, 97.8f, 8.2f);       // ride the spirit platform for it
        Ember(embers, 118.5f, 8.4f);
        Ember(embers, 123.5f, 11.8f);     // high ledge
        Ember(embers, 30.5f, 11.6f);      // optional perch
        Ember(embers, 139f, 11.8f);
    }

    // ------------------------------------------------------------------ hazards

    static void FireJetAt(Transform parent, float x, float offset)
    {
        float y = ASurf(x);
        var go = new GameObject($"Fire Jet x{x}");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        var jet = go.AddComponent<FireJet>();
        jet.timeOffset = offset;

        var vent = Place("rock_s", new Vector2(x, y - 0.25f), go.transform, 9, _lit, new Color(0.5f, 0.45f, 0.5f), 0.9f);
        vent.name = "Vent";
        jet.ventGlow = LocalGlow("Vent Glow", go.transform, new Vector2(0f, 0.1f), new Vector2(2f, 0.6f), new Color(1f, 0.45f, 0.1f, 0.9f), 10, _neonAdd);

        var warn = Particles("Warning Sputter", new Vector2(x, y + 0.2f), go.transform, _emberSquare, 12, 30f, new Vector2(0.3f, 0.6f), new Vector2(1f, 3f),
                             new Vector2(0.08f, 0.14f), Grad((FireCore, 0f, 1f), (FireOrange, 1f, 0f)), 0.2f, 0.3f, 0.2f, 60);
        var wm = warn.main; wm.prewarm = false; wm.playOnAwake = false;
        var flames = Particles("Flame Column", new Vector2(x, y + 0.1f), go.transform, _fireSquare, 13, 110f, new Vector2(0.45f, 0.65f), new Vector2(6.5f, 9f),
                               new Vector2(0.35f, 0.7f), FireGradient(), 0.25f, 0f, 0.3f, 220);
        var fm = flames.main; fm.prewarm = false; fm.playOnAwake = false;
        var fs = flames.shape; fs.angle = 4f;
        jet.warning = warn;
        jet.flames = flames;

        var trig = go.AddComponent<BoxCollider2D>();
        trig.isTrigger = true;
        trig.size = new Vector2(1.1f, 4.2f);
        trig.offset = new Vector2(0f, 2.1f);
        jet.killZone = trig;

        jet.jetLight = PointLight("Jet Light", new Vector2(x, y + 2f), go.transform, new Color(1f, 0.5f, 0.15f), 6f, 2.6f);
    }

    static void FallingBeamAt(Transform parent, float triggerX, float landX)
    {
        float y = ASurf(landX);
        var root = new GameObject($"Falling Beam x{landX}");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(triggerX, y, 0f);
        var trig = root.AddComponent<BoxCollider2D>();
        trig.isTrigger = true;
        trig.size = new Vector2(1f, 8f);
        trig.offset = new Vector2(0f, 4f);
        var fb = root.AddComponent<FallingBeam>();

        var beam = new GameObject("Beam");
        beam.transform.SetParent(root.transform, false);
        beam.transform.position = new Vector3(landX, y + 22f, 0f);   // well above the camera top: unseen until it drops
        var sr = Place("beam", Vector2.zero, beam.transform, 11, _lit, null, 0.55f);
        sr.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        sr.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
        var solid = beam.AddComponent<BoxCollider2D>();
        solid.size = new Vector2(3.6f, 0.75f);
        solid.offset = new Vector2(0f, 0.35f);

        // Burning the whole time, plus heavier fire once it lies on the street.
        BackgroundParticles("Beam Fire", beam.transform, Vector2.zero, new Vector2(3.2f, 0.2f), _fireSquare, 12, 40f, new Vector2(0.4f, 0.8f),
                            new Vector2(1f, 2.2f), new Vector2(0.25f, 0.5f), FireGradient(), 110).transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var lying = BackgroundParticles("Lying Fire", beam.transform, Vector2.zero, new Vector2(3.4f, 0.2f), _fireSquare, 12, 45f, new Vector2(0.6f, 1.1f),
                            new Vector2(1.2f, 2.8f), new Vector2(0.3f, 0.6f), FireGradient(), 140);
        lying.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        PointLight("Beam Light", Vector2.zero, beam.transform, new Color(1f, 0.5f, 0.2f), 5f, 1.8f).transform.localPosition = new Vector3(0f, 1f, 0f);

        fb.beam = beam.transform;
        fb.solidCollider = solid;
        fb.groundY = y;
        fb.gravity = 60f;
        fb.impactPrefab = _beamImpact;
        fb.burnWhileLying = new[] { lying };
    }

    static void BuildAreaHazards()
    {
        var hz = Group("Hazards").transform;

        FallingBeamAt(hz, 13f, 21.5f);
        FallingBeamAt(hz, 104.5f, 113f);

        FireJetAt(hz, 73f, 0f);
        FireJetAt(hz, 106f, 0.9f);

        // Trees in the playable plane (intro -> develop -> twist):
        var charredWood = new Color(1f, 0.9f, 0.85f);
        BridgeTree(hz, "dead_c", 47, 58, charredWood);                                           // shoot 3x -> bridge over the fire gap
        PathTree(hz, "pine_m_b", 37f, -1f, TreeTopple.Trigger.Behind, 4f, 6f, charredWood);       // crashes down behind you
        PathTree(hz, "dead_a", 69f, -1f, TreeTopple.Trigger.Ahead, 12f, 5.5f, charredWood);       // falls into your path before you arrive: hop it
        // x112-133 is kept flat and clear: the enemy arena (see ArenaMarker).
        // x112-133: the village-square ambush (see BuildAreaEnemies).

        // Thorn strips in the cellar.
        foreach (var (x0, x1) in AreaThorns)
        {
            var strip = Group($"Thorns x{x0}-{x1}", hz);
            for (float x = x0; x < x1; x += 4f)
                Place("thorns", new Vector2(x + 2f, ASurf(x0) - 0.15f), strip.transform, 6, _lit, new Color(1f, 0.8f, 0.8f));
            var trig = strip.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.offset = new Vector2((x0 + x1) / 2f, ASurf(x0) + 0.35f);
            trig.size = new Vector2(x1 - x0 - 0.3f, 0.7f);
            strip.AddComponent<KillZone>();
        }

        // Spirit platform over the wide fire pit (cyan = the sister's power).
        var plat = new GameObject("Spirit Platform (moving)");
        plat.transform.SetParent(hz, false);
        plat.transform.position = new Vector3(95.8f, 5.6f, 0f);
        var rb = plat.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = plat.AddComponent<BoxCollider2D>();
        box.size = new Vector2(2.9f, 0.5f);
        box.offset = new Vector2(0f, 0.15f);
        var mp = plat.AddComponent<MovingPlatform>();
        mp.pointA = new Vector2(95.8f, 5.6f);
        mp.pointB = new Vector2(100.2f, 5.6f);
        mp.period = 4.5f;
        mp.topY = 0.4f;
        var psr = Place("spirit_platform_0", new Vector2(95.8f, 5.6f), plat.transform, 10, _neonTex, Color.white, 1.5f);
        psr.transform.localPosition = Vector3.zero;
        var panim = psr.gameObject.AddComponent<SpriteFrameAnimator>();
        panim.frames = Enumerable.Range(0, 3).Select(i => _sprites[$"spirit_platform_{i}"]).ToArray();
        panim.framesPerSecond = 6f;
        LocalGlow("Spirit Glow", plat.transform, new Vector2(0f, -0.2f), new Vector2(4f, 1.2f), new Color(0.3f, 0.9f, 1f, 0.5f), 9, _neonSoft);
        LocalLight("Spirit Light", plat.transform, Vector2.zero, new Color(0.3f, 0.85f, 1f), 4f, 1.4f).gameObject.AddComponent<PulseLight2D>();
    }

    // ------------------------------------------------------------------ atmosphere

    static void BuildAreaAtmosphere()
    {
        var fx = Group("Atmosphere").transform;

        // Falling ash over the whole village (GDD: screen one teaches running under falling ash).
        var ash = Particles("Falling Ash", new Vector2(MapWidth / 2f, 22f), fx, _ash, 40, 45f, new Vector2(10f, 16f), new Vector2(0f, 0f),
                            new Vector2(0.07f, 0.13f), Grad((new Color(0.75f, 0.72f, 0.8f), 0f, 0f), (new Color(0.7f, 0.68f, 0.75f), 0.1f, 0.85f),
                            (new Color(0.5f, 0.45f, 0.55f), 0.9f, 0.6f), (new Color(0.4f, 0.35f, 0.45f), 1f, 0f)), 0f, 0f, 0.6f, 500);
        var ashShape = ash.shape;
        ashShape.shapeType = ParticleSystemShapeType.Box;
        ashShape.rotation = Vector3.zero;
        ashShape.scale = new Vector3(MapWidth + 20f, 1f, 0.1f);
        var av = ash.velocityOverLifetime;
        av.enabled = true;
        av.x = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);   // blown with the wind (same way the smoke leans)
        av.y = new ParticleSystem.MinMaxCurve(-1.6f, -0.7f);
        av.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var asol = ash.sizeOverLifetime;
        asol.enabled = false;
        var arot = ash.rotationOverLifetime;
        arot.enabled = true;
        arot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        // Embers drifting up from the streets.
        var up = Particles("Rising Embers", new Vector2(MapWidth / 2f, 4f), fx, _emberSquare, 40, 30f, new Vector2(4f, 7f), new Vector2(0f, 0f),
                           new Vector2(0.07f, 0.12f), Grad((FireCore, 0f, 0f), (FireOrange, 0.15f, 1f), (FireMagenta, 0.8f, 0.8f), (Violet, 1f, 0f)), 0f, 0f, 0.8f, 260);
        var us = up.shape;
        us.shapeType = ParticleSystemShapeType.Box;
        us.rotation = Vector3.zero;
        us.scale = new Vector3(MapWidth, 4f, 0.1f);
        var uv = up.velocityOverLifetime;
        uv.enabled = true;
        uv.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.6f);
        uv.y = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        uv.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        Fog(fx, -2f, 5f, new Color(0.2f, 0.25f, 0.5f), 35, 0.2f);
        WindStreaks(fx, new Color(0.85f, 0.7f, 0.65f, 0.22f), 9f, 12f);

        // Smoky mist hiding the void under the walkway (behind the street, in front of the backgrounds).
        UnderMist(fx, new Color(0.1f, 0.08f, 0.15f), 0.6f, 3.5f);

        // Fires burning below the street: warm under-glow on the ground fill (behind props and player).
        var under = Fog(fx, -1.5f, 5.5f, new Color(1f, 0.38f, 0.12f), 2, 0.3f);
        under.name = "Under-street Fire Glow";
        var ue = Particles("Under-street Embers", new Vector2(MapWidth / 2f, 0f), fx, _emberSquare, 3, 22f, new Vector2(2f, 4f), new Vector2(0f, 0f),
                           new Vector2(0.06f, 0.11f), Grad((FireCore, 0f, 0f), (FireOrange, 0.2f, 1f), (FireMagenta, 1f, 0f)), 0f, 0f, 0.6f, 160);
        var ues = ue.shape;
        ues.shapeType = ParticleSystemShapeType.Box;
        ues.rotation = Vector3.zero;
        ues.scale = new Vector3(MapWidth, 2f, 0.1f);
        var uev = ue.velocityOverLifetime;
        uev.enabled = true;
        uev.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.4f);
        uev.y = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        uev.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    // ------------------------------------------------------------------ foreground

    static void BuildAreaForeground()
    {
        var fg = Group("Foreground").transform;
        var near = Group("Near Foreground (parallax -0.25)", fg);
        SetZ(near.transform, -5f);
        var dark = new Color(0.035f, 0.015f, 0.06f);
        for (float x = -6f; x < MapWidth + 30f; x += R(9f, 14f))
            Wind(Place("bush_darkest", new Vector2(x, R(-2.4f, -1.8f)), near.transform, 30, _unlit, dark, R(0.8f, 0.95f), _rng.Next(2) == 0), 2f, 1.6f);

        // Burning pines right in front of the lens. Every other one cracks and crashes down as you pass.
        string[] trunks = { "pine_m_a", "pine_m_b", "dead_a", "pine_l_b", "dead_c" };
        int n = 0;
        for (float x = 30f; x < MapWidth + 30f; x += R(26f, 36f))
        {
            var tree = Place(trunks[_rng.Next(trunks.Length)], new Vector2(x, R(-3.5f, -2.5f)), near.transform, 32, _unlit,
                             new Color(0.03f, 0.015f, 0.04f, 0.94f), R(0.85f, 1.05f), _rng.Next(2) == 0);
            if (n++ % 2 == 1)
            {
                var tt = tree.gameObject.AddComponent<TreeTopple>();
                tt.fallDirection = n % 4 == 0 ? 1f : -1f;
                tt.triggerDistance = 5f;
                tt.fallAngle = 70f;
                tt.impactPrefab = _beamImpact;
                tt.impactOffset = new Vector3(tree.bounds.size.y * 0.6f, 1.5f, 0f);
                tt.shake = 0.25f;
            }
            else Wind(tree, 0.5f, 0.8f);
            var b = tree.bounds;
            foreach (var k in new[] { 0.55f, 0.8f })
            {
                var p = new Vector2(b.center.x + R(-0.6f, 0.6f), b.min.y + b.size.y * k);
                var f = Particles("Tree Fire", p, tree.transform, _fireSquare, 33, 14f, new Vector2(0.45f, 0.9f), new Vector2(1f, 2.2f),
                                  new Vector2(0.3f, 0.6f), FireGradient(), 0.6f, -0.1f, 0.5f, 60);
                f.transform.position = new Vector3(p.x, p.y, -5f);
                var g = Glow(p + Vector2.up * 0.6f, tree.transform, new Color(1f, 0.45f, 0.15f, 0.25f), 3.5f, 31, _neonSoft);
                SetZ(g.transform, -5f);
            }
            var embers = Particles("Tree Embers", new Vector2(b.center.x, b.min.y + b.size.y * 0.7f), tree.transform, _emberSquare, 34, 10f,
                                   new Vector2(1.5f, 3f), new Vector2(1f, 2.5f), new Vector2(0.1f, 0.18f),
                                   Grad((FireCore, 0f, 1f), (FireOrange, 0.4f, 1f), (FireMagenta, 1f, 0f)), 1.2f, -0.05f, 1f, 50);
            SetZ(embers.transform, -5f);
        }
        Parallax(near, -0.25f, 0f);
    }

    /// <summary>Pixel fire reads best as many small squares: shrink every square-flame system and add density.</summary>
    static void RefineSquareFire()
    {
        foreach (var r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
        {
            if (r.sharedMaterial != _fireSquare) continue;
            var ps = r.GetComponent<ParticleSystem>();
            var main = ps.main;
            var size = main.startSize;
            main.startSize = new ParticleSystem.MinMaxCurve(size.constantMin * 0.5f, size.constantMax * 0.5f);
            main.maxParticles = Mathf.RoundToInt(main.maxParticles * 1.6f);
            var em = ps.emission;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(em.rateOverTime.constant * 1.5f);
        }
    }

    // ------------------------------------------------------------------ enemies

    /// <summary>
    /// Area A roster, paced intro -> develop -> twist:
    ///   Flying Eye over the cart street (learn to aim at the air),
    ///   Goblin on the far roof bombing you while you cross the tree bridge (pressure on a known puzzle),
    ///   one Ember Rat on the open street after the fallen tree (learn rush / rest: shoot, then call the sister),
    ///   the village-square ambush: fire walls, two rats, then a rat + eye + goblin on the high ledge.
    /// </summary>
    static void AreaTutorials()
    {
        var rat = GameObject.Find("Ember Rat x21");
        if (rat) TutorialAt("First fight", 11f, rat.transform, ATutFight);
        var tree = Object.FindObjectsByType<TreeTopple>(FindObjectsSortMode.None).FirstOrDefault(t => t.trigger == TreeTopple.Trigger.Shot);
        if (tree) TutorialAt("Bridge tree", 41f, tree.transform, ATutTree);
    }

    static void BuildAreaEnemies()
    {
        BuildEnemyAssets();
        var root = Group("Enemies").transform;

        // (Goblins moved to the forest.) The village is ember beasts and fire-eyes.
        SpawnRat(root, 21f, 15f, 27f);                 // GDD: the first Ember Hound, on open ground
        SpawnEye(root, 42f, 10f);
        SpawnRat(root, 66f, 63.5f, 69f);
        SpawnRat(root, 72f, 69.6f, 75.6f);
        SpawnEye(root, 97.5f, 11f);                     // over the fire pit
        SpawnRat(root, 106f, 102.5f, 110.5f);

        // Spawns sit deep in the square, away from the entrance at x~114: you see them rise before they come.
        var w1 = new[] { SpawnRat(root, 125.5f, 113.5f, 132.5f), SpawnRat(root, 131f, 113.5f, 132.5f, 1.1f), SpawnEye(root, 128f, 12f) };
        var w2 = new[] { SpawnRat(root, 129.5f, 113.5f, 132.5f, 1.1f), SpawnRat(root, 117f, 113.5f, 132.5f), SpawnEye(root, 127f, 11.5f), SpawnEye(root, 121f, 12.5f) };
        var arena = ArenaAt(root, 112f, 133.5f, ASurf(120f), "Ambush in the square", w1, w2);
        arena.introLines = AAmbush;
        DialogueZone("Memory 1", 103.5f, AMemory);
    }

    // ------------------------------------------------------------------ checkpoints & exit

    static void BuildAreaCheckpoints()
    {
        var root = Group("Checkpoints").transform;
        foreach (var x in new[] { 36f, 63f, 90.5f, 135.5f })
        {
            var go = Group($"Checkpoint x{x}", root);
            go.transform.position = new Vector3(x, ASurf(x) + 0.1f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 6f);
            box.offset = new Vector2(0f, 3f);
            go.AddComponent<Checkpoint>();
        }

        // Altar checkpoint: ignites blue, then amber (GDD section 3).
        float ax = 109.5f;   // just before the ambush: a failed fight restarts at the square's edge
        var altar = new GameObject("Checkpoint Altar");
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

        // The collapsed gate out of the village.
        float gx = 149f;
        var gate = Group("Collapsed Gate (exit)", root);
        // Two broken brick pillars (stacked wall pieces), a torn banner, torches and the burning lintel beam.
        float gy = ASurf(gx);
        for (int side = -1; side <= 1; side += 2)
        {
            float px = gx + side * 2.8f;
            for (int k = 0; k < 4; k++)   // crumbling: each course a little narrower and shifted outward
                PlaceCainos("Brick Wall 01", new Vector2(px + side * k * 0.12f, gy + k * 0.64f), gate.transform, 5, Charred, 0.5f - k * 0.05f, side > 0);
            PlaceCainos("Torch", new Vector2(px - side * 1.3f, gy + 1.8f), gate.transform, 7, Color.white, 1.4f);
            PointLight("Torch Light", new Vector2(px - side * 1.3f, gy + 2.4f), gate.transform, new Color(1f, 0.55f, 0.25f), 4f, 1.2f)
                .gameObject.AddComponent<FlickerLight2D>();
        }
        PlaceCainos("Banner", new Vector2(gx - 2.8f, gy + 0.2f), gate.transform, 8, new Color(0.7f, 0.5f, 0.55f), 0.9f);
        var lintel = Place("beam", new Vector2(gx, gy + 3.1f), gate.transform, 7, _lit, null, 0.8f);
        lintel.transform.rotation = Quaternion.Euler(0f, 0f, -9f);
        PropFire(gate.transform, new Vector2(gx - 0.5f, gy + 3.5f), 2.6f, 8, 0f, 0f);
        Glow(new Vector2(gx + 3f, ASurf(gx) + 2.5f), gate.transform, new Color(0.3f, 0.55f, 1f, 0.5f), 8f, 5, _neonSoft).name = "Forest Beyond (blue)";
        PointLight("Gate Light", new Vector2(gx, ASurf(gx) + 3f), gate.transform, new Color(1f, 0.5f, 0.2f), 7f, 2f).gameObject.AddComponent<FlickerLight2D>();
        var exit = Group("Exit Trigger", gate.transform);
        exit.transform.position = new Vector3(gx + 2f, ASurf(gx), 0f);
        var et = exit.AddComponent<BoxCollider2D>();
        et.isTrigger = true;
        et.size = new Vector2(1f, 8f);
        et.offset = new Vector2(0f, 4f);
        exit.AddComponent<LevelExit>().nextScene = "AreaB_DarkForest";
    }
}
