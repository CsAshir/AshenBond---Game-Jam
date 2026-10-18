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
/// Area B - The Dark Forest (Ashen Bond GDD section 5). Horror mood: the boy's lantern is the main light,
/// fog, distant lightning, eyes in the dark, a swamp below the walkway (no underground), very little fire.
/// Menu: Tools > Ashen Bond > Build Area B - Dark Forest.
/// </summary>
public static partial class DarkForestBuilder
{
    const string BRoot = Root + "/AreaB";
    const string BSpriteDir = BRoot + "/Sprites";
    const string BBgDir = BRoot + "/BG";
    const string BTileDir = TileDir + "/AreaB";
    const string BScenePath = SceneDir + "/AreaB_DarkForest.unity";
    const string BTerrain = BSpriteDir + "/areaB_terrain.png";

    // (first column, last column, height). Height 0 = black swamp water.
    static readonly (int x0, int x1, int h)[] BGround =
    {
        (0, 24, 5), (25, 27, 0), (28, 42, 6), (43, 48, 0), (49, 62, 5),
        (63, 95, 7),               // one long walk (was split by a swamp): room for a fight
        (96, 97, 9),
        (98, 109, 0),              // black water too wide even for a double jump: shoot the dead tree into a bridge
        (110, 125, 8), (126, 171, 6),   // the Executioner's clearing (wide, with branches to climb), then the path out
    };

    static readonly (int x0, int x1, int top)[] BPlatforms =
    {
        (32, 35, 8),     // low log platform (GDD: hound attacks below it)
        (70, 73, 10),    // optional high branch over the long walk
        (110, 112, 11),  // high perch with an ember
        (133, 135, 9), (141, 143, 11), (149, 151, 9), (156, 158, 11),   // boss clearing: branches to escape onto
        (8, 10, 8), (18, 20, 9), (54, 56, 8), (88, 90, 10), (115, 117, 11),   // air branches
    };

    static Dictionary<Vector2Int, Tile> _bTiles;

    [MenuItem("Tools/Ashen Bond/Build Area B - Dark Forest")]
    public static void BuildAreaB()
    {
        var active = SceneManager.GetActiveScene();
        if (active.isDirty && active.path != BScenePath && active.path != AreaScenePath && active.path != ScenePath)
        {
            Debug.LogWarning("[AreaB] Active scene has unsaved changes. Save it first, then build again.");
            return;
        }

        _rng = new System.Random(1717);
        _ground = BGround;
        _platforms = BPlatforms;
        MapWidth = 172;
        PlayerSpawn = new Vector2(3.5f, 5.2f);

        EnsureFolders();
        if (!AssetDatabase.IsValidFolder(AreaTileDir)) AssetDatabase.CreateFolder(TileDir, "AreaA");
        if (!AssetDatabase.IsValidFolder(BTileDir)) AssetDatabase.CreateFolder(TileDir, "AreaB");
        ImportSprites();
        ImportBackgrounds();
        ImportAreaSprites();
        ImportAreaBSprites();
        BuildTiles();
        BuildMaterials();
        BuildAreaMaterials();
        BuildVolumeProfile();
        var profile = BuildAreaBVolumeProfile();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = BuildCameraAndPost(profile);
        var global = Object.FindFirstObjectByType<Light2D>();
        if (global && global.lightType == Light2D.LightType.Global)
        {
            global.color = new Color(0.42f, 0.55f, 0.6f);
            global.intensity = 0.5f;           // moonlit, not pitch black: god rays and the lantern carry the scene
            global.name = "Global Light 2D (moonless)";
        }

        BuildAreaBSky(global);
        BuildAreaBParallax();
        BuildAreaBTerrain();
        BuildAreaBColliders();
        BuildAreaBSetDressing();
        BuildAreaBHazards();
        BuildAreaBAtmosphere();
        BuildAreaBForeground();
        BuildAreaBLight();
        var player = BuildPlayer();
        BuildShadowAbility(player);
        BuildAreaBCheckpoints();
        BuildAreaBEnemies();
        BuildRewind(player);
        BuildStoryUI(BStart);
        BuildGroundDepth(new Color(0.02f, 0.03f, 0.04f));
        BuildAudio('B');
        var btree = Object.FindObjectsByType<TreeTopple>(FindObjectsSortMode.None).FirstOrDefault(t => t.trigger == TreeTopple.Trigger.Shot);
        if (btree) TutorialAt("Bridge tree", 93.5f, btree.transform, BTutTree);
        ExitLines(BExit);
        RefineSquareFire();

        var follow = cam.gameObject.AddComponent<CameraFollow2D>();
        follow.target = player.transform;
        follow.boundsMin = new Vector2(0f, 0f);
        follow.boundsMax = new Vector2(MapWidth, 40f);
        follow.offset = new Vector2(0f, 1.6f);
        var hud = new GameObject("HUD").AddComponent<AreaHUD>();
        hud.areaTitle = "Area B";
        hud.areaSubtitle = "The Dark Forest";
        hud.health = player.GetComponent<PlayerHealth>();
        hud.shadow = player.GetComponentInChildren<ShadowSummon>();
        if (_sprites.TryGetValue("icon_lantern", out var li)) hud.heartIcon = li.texture;
        if (_sprites.TryGetValue("icon_sister", out var si)) hud.sisterIcon = si.texture;

        EditorSceneManager.SaveScene(scene, BScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(sc => sc.path == BScenePath);
        int ai = scenes.FindIndex(sc => sc.path == AreaScenePath);
        scenes.Insert(ai >= 0 ? ai + 1 : 0, new EditorBuildSettingsScene(BScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        SortBuildScenes();
        AssetDatabase.SaveAssets();
        _ground = AreaGround;
        _platforms = AreaPlatforms;
        Debug.Log("[AreaB] Scene built: " + BScenePath);
    }

    // ------------------------------------------------------------------ assets

    static void ImportAreaBSprites()
    {
        foreach (var n in new[] { "AreaB-1", "AreaB-2", "AreaB-3", "AreaB-4" })
            ImportBg($"{BBgDir}/{n}.png", BgPPU);

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BSpriteDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            bool atlas = path == BTerrain;
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = atlas ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            ti.spritePixelsPerUnit = name == "lamp_post" ? 20 : PPU;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        // Slice the tileset into every non-empty 16x16 cell: b_col_row.
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BTerrain);
        var readable = new Texture2D(2, 2);
        readable.LoadImage(File.ReadAllBytes(BTerrain));
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
                    name = $"b_{c}_{r}",
                    rect = new Rect(c * 16, tex.height - (r + 1) * 16, 16, 16),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                });
            }
        Object.DestroyImmediate(readable);
        var tiImporter = (TextureImporter)AssetImporter.GetAtPath(BTerrain);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(tiImporter);
        dp.InitSpriteEditorDataProvider();
        dp.SetSpriteRects(rects.ToArray());
        dp.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        dp.Apply();
        tiImporter.SaveAndReimport();

        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { BSpriteDir }))
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>())
                _sprites[s.name] = s;

        _bTiles = new Dictionary<Vector2Int, Tile>();
        foreach (var kv in _sprites.Where(k => k.Key.StartsWith("b_")))
        {
            var parts = kv.Key.Split('_');
            var cell = new Vector2Int(int.Parse(parts[1]), int.Parse(parts[2]));
            var path = $"{BTileDir}/{kv.Key}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = kv.Value;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            _bTiles[cell] = tile;
        }
    }

    /// <summary>Cold, desaturated, heavy vignette and film grain: horror.</summary>
    static UnityEngine.Rendering.VolumeProfile BuildAreaBVolumeProfile()
    {
        var path = $"{BRoot}/AreaB_PostFX.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.7f);
        var ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(0.05f);
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-0.05f);
        color.contrast.Override(24f);
        color.saturation.Override(-22f);
        color.colorFilter.Override(new Color(0.85f, 0.95f, 1f));
        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(new Color(0.15f, 0.32f, 0.38f));     // drowned teal shadows
        split.highlights.Override(new Color(0.9f, 0.8f, 0.7f));
        split.balance.Override(-35f);
        var vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0f, 0.01f, 0.03f));
        vignette.intensity.Override(0.5f);
        vignette.smoothness.Override(0.5f);
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.35f);
        grain.response.Override(0.8f);

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static Tile BT(int c, int r) => _bTiles.TryGetValue(new Vector2Int(c, r), out var t) ? t : null;

    // ------------------------------------------------------------------ sky, parallax

    static void BuildAreaBSky(Light2D global)
    {
        Vector2 c = _cam.position;
        var sky = Group("Sky (parallax 1.0)");
        sky.transform.position = new Vector3(c.x, c.y, 120f);
        var bg = Place("white", c, sky.transform, -100, _sky);
        bg.name = "NightSky";
        bg.transform.localScale = new Vector3(48f, CamSize * 2f + 6f, 1f);
        bg.transform.localPosition = Vector3.zero;
        Parallax(sky, 1f, 1f);

        var stars = Group("Faint Stars (parallax 1.0)");
        stars.transform.position = new Vector3(c.x, c.y, 96f);
        var starMat = SaveMaterial("M_StarFieldB", Shader.Find("DarkForest/StarField"), m =>
        {
            m.SetFloat("_StarDensity", 0.006f);
            m.SetFloat("_StarGrid", 300f);
            m.SetFloat("_StarBrightness", 0.6f);
            m.SetFloat("_FadeStart", 0.6f);
        });
        var sf = Place("white", c, stars.transform, -94, starMat);
        sf.transform.localScale = new Vector3(48f, CamSize * 2f + 6f, 1f);
        sf.transform.localPosition = Vector3.zero;
        Parallax(stars, 1f, 1f);

        // Lightning: a sky-wide flash and bolts striking behind the forest.
        var flashGo = Group("Lightning Flash (parallax 1.0)");
        flashGo.transform.position = new Vector3(c.x, c.y, 90f);
        var flash = Place("white", c, flashGo.transform, -83, _neonAdd, new Color(0.55f, 0.7f, 1f, 0.35f));
        flash.transform.localScale = new Vector3(48f, CamSize * 2f + 6f, 1f);
        flash.transform.localPosition = Vector3.zero;
        Parallax(flashGo, 1f, 1f);
        var bolts = new List<GameObject>();
        if (HasSheet("vfx_lightning"))
            bolts.Add(SheetFx("FX_LightningStrike", "vfx_lightning", 18f, 4.5f, _neonAdd, new Color(0.7f, 0.85f, 1f, 1f), -82, Vector2.zero));
        if (HasSheet("vfx_dark_bolt"))
            bolts.Add(SheetFx("FX_DarkBolt", "vfx_dark_bolt", 16f, 3.2f, _neonAdd, new Color(0.75f, 0.5f, 1f, 1f), -82, Vector2.zero));
        var lf = flashGo.AddComponent<LightningFlash>();
        lf.globalLight = global;
        lf.skyFlash = flash;
        lf.boltPrefabs = bolts.ToArray();
        lf.boltArea = _cam;
    }

    static void BuildAreaBParallax()
    {
        var root = Group("Parallax Backgrounds").transform;
        Vector2 c = _cam.position;
        var layers = new (string sprite, int order, float fx, float fy, float yOffset, float z, float scale, Color tint, float boost)[]
        {
            ("AreaB-1", -95, 0.92f, 1.00f, 0.5f,  100f, 1.00f, new Color(0.85f, 0.93f, 1f), 1.0f),    // misty night sky (moonlit)
            ("AreaB-2", -85, 0.78f, 0.92f, 0.3f,   75f, 0.75f, new Color(0.68f, 0.77f, 0.86f), 1.3f), // mountains, pines, far village
            ("AreaB-3", -78, 0.58f, 0.88f, 1.1f,   50f, 0.72f, new Color(0.5f, 0.6f, 0.68f), 1.1f),   // ruined aqueduct
            ("AreaB-4", -70, 0.35f, 0.85f, -2.4f,  25f, 0.55f, new Color(0.38f, 0.43f, 0.56f), 1.3f), // swamp under the walkway
        };
        foreach (var l in layers)
        {
            if (!_sprites.TryGetValue(l.sprite, out var sprite) || sprite == null) continue;
            var mat = SaveMaterial($"M_BG_{l.sprite.Replace("-", "")}", Shader.Find("DarkForest/FireBackdrop"), m =>
            {
                m.SetFloat("_FireBoost", l.boost);
                m.SetFloat("_Blur", l.fx >= 0.9f ? 0f : Mathf.Max(0f, (l.fx - 0.3f) * 5f));   // farther = softer (depth of field on the player)
                m.SetFloat("_SmokeDistort", 0.005f);
                m.SetFloat("_FlickerAmount", 0.25f);
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

        // Thick fog between the depth planes.
        SetZ(Fog(root, 1f, 9f, new Color(0.45f, 0.55f, 0.65f), -88, 0.3f).transform, 85f);
        SetZ(Fog(root, 0f, 8f, new Color(0.4f, 0.5f, 0.6f), -80, 0.32f).transform, 60f);
        SetZ(Fog(root, -1f, 7f, new Color(0.3f, 0.38f, 0.48f), -74, 0.38f).transform, 40f);
        SetZ(Fog(root, -2f, 6f, new Color(0.22f, 0.28f, 0.36f), -60, 0.45f).transform, 15f);
    }

    // ------------------------------------------------------------------ terrain: walkable crust only

    static void BuildAreaBTerrain()
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
        var ground = Map("Ground (mossy crust)", 0);
        var roots = Map("Hanging Roots", -1);

        void Slab(int x, int h, bool openL, bool openR)
        {
            int col = openL ? 1 : openR ? 3 : 2;
            ground.SetTile(new Vector3Int(x, h - 1, 0), BT(col, 0));
            ground.SetTile(new Vector3Int(x, h - 2, 0), BT(col, 2));
            if (!openL && !openR && _rng.Next(4) == 0)
                roots.SetTile(new Vector3Int(x, h - 3, 0), BT(6 + _rng.Next(3), 3));
        }

        for (int x = -2; x < MapWidth + 2; x++)
        {
            int h = AreaH(x);
            if (h <= 0) continue;
            Slab(x, h, AreaH(x - 1) < h, AreaH(x + 1) < h);
        }
        foreach (var p in BPlatforms)
            for (int x = p.x0; x <= p.x1; x++)
                Slab(x, p.top + 1, x == p.x0, x == p.x1);
    }

    static void BuildAreaBColliders()
    {
        var go = Group("Level Colliders");
        go.isStatic = true;
        foreach (var g in BGround)
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
            wall.offset = new Vector2(wallX, 20f);
            wall.size = new Vector2(1f, 60f);
        }
        var plats = Group("Platforms (one-way)");
        foreach (var p in BPlatforms)
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

        // Black swamp water: sends you back. Dark shimmer + shadow wisps rising.
        foreach (var g in BGround.Where(g => g.h <= 0))
        {
            float cx = (g.x0 + g.x1 + 1) / 2f, w = g.x1 - g.x0 + 1;
            var pit = Group($"Swamp x{g.x0}-{g.x1}");
            var trig = pit.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.offset = new Vector2(cx, 0.5f);
            trig.size = new Vector2(w, 3f);
            pit.AddComponent<KillZone>();
            var water = Glow(new Vector2(cx, 1.6f), pit.transform, new Color(0.25f, 0.45f, 0.6f, 0.35f), w, 2, _neonSoft);
            water.transform.localScale = new Vector3(w * 1.1f / 4f, 0.35f, 1f);
            var wisps = BackgroundParticles("Shadow Wisps", pit.transform, new Vector2(cx, 1.4f), new Vector2(w, 0.3f), _smoke, 3,
                3f * w, new Vector2(2f, 3.5f), new Vector2(0.3f, 0.8f), new Vector2(0.8f, 1.6f),
                Grad((new Color(0.5f, 0.3f, 0.9f), 0f, 0f), (new Color(0.25f, 0.15f, 0.45f), 0.3f, 0.5f), (new Color(0.05f, 0.05f, 0.12f), 1f, 0f)), 80);
            wisps.transform.position = new Vector3(cx, 1.4f, 0f);
            PointLight("Swamp Glow", new Vector2(cx, 1.8f), pit.transform, new Color(0.35f, 0.55f, 0.9f), w * 0.9f, 0.5f).gameObject.AddComponent<PulseLight2D>().speed = 0.6f;
        }
    }

    // ------------------------------------------------------------------ dressing, hazards

    static void Eyes(Transform parent, float x, float y, Color color)
    {
        var go = new GameObject("Eyes in the dark");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 30f);
        var eyes = new List<SpriteRenderer>();
        foreach (var dx in new[] { -0.18f, 0.18f })
        {
            var e = Place("white", new Vector2(x + dx, y), go.transform, -50, _neonAdd, color);
            e.transform.localScale = new Vector3(0.22f, 0.1f, 1f);
            eyes.Add(e);
        }
        var be = go.AddComponent<BlinkingEyes>();
        be.eyes = eyes.ToArray();
    }

    static void BuildAreaBSetDressing()
    {
        _beamImpact = BuildBeamImpactPrefab();   // also used by the falling tree below
        var dress = Group("Forest Set Dressing").transform;
        var tint = new Color(0.05f, 0.07f, 0.08f);
        var treeWind = FoliageMat("M_FoliageTree", 1.2f, 1.45f, 0.09f, false);
        string[] trees = { "dead_a", "dead_b", "dead_c", "pine_l_a", "pine_m_b", "pine_xl_b", "pine_l_b" };
        for (float x = 1f; x < MapWidth; x += R(3.5f, 7f))
        {
            if (AreaH(Mathf.FloorToInt(x)) <= 0) continue;
            var t = Place(trees[_rng.Next(trees.Length)], new Vector2(x, ASurf(x) - 0.3f), dress, -40, treeWind, tint, R(0.7f, 1.05f), _rng.Next(2) == 0);
            SetZ(t.transform, 10f);       // trunk planted, crown bends and the leaves flutter (FoliageWind shader)
        }
        ToppleTree(dress, "dead_c", 84f, ASurf(84f), tint, 0.9f, -1f, 6f);   // cracks and falls behind you on the long walk

        // Cold, forgotten things in the fog (Cainos props, tinted to the forest's moonlight).
        var cold = new Color(0.52f, 0.6f, 0.74f);
        var relics = Group("Forest Relics", dress);
        SetZ(relics.transform, 3f);
        foreach (var (prop, x, s, flip) in new[]
        {
            ("Gravestone 01", 7f, 1.1f, false), ("Gravestone 03", 8.6f, 1f, true), ("Rock 02", 20.5f, 1.2f, false), ("Stump", 39.5f, 1.1f, false),
            ("Scarecrow 01", 57f, 1.1f, false), ("Fence B", 66f, 1.1f, true), ("Statue", 92f, 1.2f, false),
            ("Gravestone 03", 121f, 1f, false), ("Rock 02", 134f, 1.1f, true),
        })
            PlaceCainos(prop, new Vector2(x, ASurf(x)), relics.transform, -14, new Color(cold.r * 0.62f, cold.g * 0.62f, cold.b * 0.62f), s, flip, _unlit);   // unlit: dim but always readable

        // Dim lamp posts: the only warm lights besides the lantern.
        if (_sprites.ContainsKey("lamp_post"))
            foreach (var x in new[] { 11f, 53f, 88.5f, 131f })
            {
                var post = Place("lamp_post", new Vector2(x, ASurf(x) - 0.1f), dress, -10, _lit, new Color(0.85f, 0.85f, 0.9f));
                var top = post.bounds.max.y - 0.5f;
                PointLight("Lamp Light", new Vector2(x, top), post.transform, new Color(1f, 0.6f, 0.3f), 3.2f, 0.9f).gameObject.AddComponent<FlickerLight2D>().flickerAmount = 0.5f;
                LocalGlow("Lamp Glow", post.transform, new Vector2(0f, top - post.transform.position.y), new Vector2(1.6f, 1.6f), new Color(1f, 0.55f, 0.25f, 0.5f), -9, _neonSoft);
            }

        var watchers = Group("Watchers").transform;
        foreach (var (x, dy, cyan) in new[] { (22f, 3.5f, false), (40f, 5f, true), (58f, 3f, false), (68f, 4.5f, false), (86f, 6f, true), (101f, 3f, false), (118f, 5.5f, false), (137f, 4f, true) })
            Eyes(watchers, x, ASurf(x) + dy, cyan ? new Color(0.4f, 0.9f, 1f, 0.9f) : new Color(1f, 0.15f, 0.12f, 0.9f));
    }

    static void BuildAreaBHazards()
    {
        _emberBurst = HasSheet("vfx_pickup")
            ? SheetFx("FX_EmberPickup", "vfx_pickup", 16f, 1.8f, _neonAdd, new Color(1f, 0.7f, 0.35f, 1f), 18, Vector2.zero, new Color(1f, 0.6f, 0.25f), 2.5f)
            : BuildEmberBurstPrefab();
        var hz = Group("Hazards").transform;

        FireJetAt(hz, 19f, 0f);                 // GDD: a short fire jet blocks the lane
        FireJetAt(hz, 113f, 0f);
        FireJetAt(hz, 119.5f, 1.6f);

        // Trees in the playable plane: the bridge puzzle returns over deadly water, plus ahead / behind falls.
        var deadWood = new Color(0.85f, 0.85f, 0.95f);   // colder wood in the forest
        BridgeTree(hz, "dead_b", 98, 109, deadWood);
        var moonWood = Neon("M_PathTreeMoon", new Color(0.2f, 0.23f, 0.3f), 1.15f, 0.85f, false, 0f, 0f);   // moonlit bark: readable on the dark floor
        PathTree(hz, "dead_a", 57f, -1f, TreeTopple.Trigger.Behind, 4f, 5f, deadWood, moonWood);
        PathTree(hz, "pine_m_b", 121.5f, -1f, TreeTopple.Trigger.Ahead, 11f, 5.5f, deadWood, moonWood);   // before the king's clearing
        FallingBeamAt(hz, 28.5f, 37f);          // falling branch = jump cue
        // (second branch removed: x127-144 is the Hollow King's clearing)
        // Enemies are placed in BuildAreaBEnemies (after the player exists).

        foreach (var (x0, x1) in new[] { (55f, 59f), (85f, 89f) })
        {
            var strip = Group($"Brambles x{x0}-{x1}", hz);
            for (float x = x0; x < x1; x += 4f)
            {
                Place("thorns", new Vector2(x + 2f, ASurf(x0) - 0.15f), strip.transform, 6, _unlit, new Color(0.78f, 0.8f, 0.98f));
                LocalGlow("Danger Glow", strip.transform, new Vector2(x + 2f, ASurf(x0) + 0.3f), new Vector2(4.6f, 1.2f), new Color(0.75f, 0.25f, 1f, 0.5f), 5, _neonSoft);
            }
            var trig = strip.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.offset = new Vector2((x0 + x1) / 2f, ASurf(x0) + 0.35f);
            trig.size = new Vector2(x1 - x0 - 0.3f, 0.7f);
            strip.AddComponent<KillZone>();
        }

        // Drifting log over the wide swamp.
        var log = new GameObject("Drifting Log (moving)");
        log.transform.SetParent(hz, false);
        log.transform.position = new Vector3(44f, 5.55f, 0f);
        var rb = log.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = log.AddComponent<BoxCollider2D>();
        box.size = new Vector2(3f, 0.5f);
        box.offset = new Vector2(0f, 0.2f);
        var mp = log.AddComponent<MovingPlatform>();
        mp.pointA = new Vector2(44.2f, 5.55f);
        mp.pointB = new Vector2(47.8f, 5.55f);
        mp.period = 5f;
        mp.topY = 0.45f;
        var lsr = Place("beam", new Vector2(44f, 5.55f), log.transform, 8, _unlit, new Color(0.5f, 0.6f, 0.62f), 0.5f);
        lsr.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        // Embers and the Ember Bloom (heal resource).
        var embers = Group("Embers (pickups)").transform;
        foreach (var (x, y) in new[] { (14f, 7.2f), (26.5f, 8.6f), (33.5f, 10.4f), (46f, 8.4f), (71.5f, 12.2f), (93f, 9.2f), (111f, 13.6f), (140f, 8.4f) })
            Ember(embers, x, y);
        var bloom = new GameObject("Ember Bloom (+2 embers)");
        bloom.transform.SetParent(embers, false);
        bloom.transform.position = new Vector3(61f, ASurf(61f), 0f);
        var bsr = Place("flower_glow", new Vector2(61f, ASurf(61f)), bloom.transform, 9,
                        Neon("M_EmberBloom", new Color(1f, 0.55f, 0.22f), 1.6f, 0.6f, false, 2.5f, 0.2f), Color.white, 1.6f);
        bsr.transform.localPosition = Vector3.zero;
        LocalGlow("Bloom Glow", bloom.transform, new Vector2(0f, 0.9f), new Vector2(3f, 3f), new Color(1f, 0.5f, 0.2f, 0.55f), 8, _neonSoft);
        LocalLight("Bloom Light", bloom.transform, new Vector2(0f, 1f), new Color(1f, 0.55f, 0.25f), 4f, 1.6f).gameObject.AddComponent<PulseLight2D>();
        var bc = bloom.AddComponent<CircleCollider2D>();
        bc.isTrigger = true;
        bc.radius = 0.7f;
        bc.offset = new Vector2(0f, 0.8f);
        var bp = bloom.AddComponent<EmberPickup>();
        bp.amount = 2;
        bp.collectBurst = _emberBurst;
    }

    // ------------------------------------------------------------------ atmosphere, foreground

    static void BuildAreaBAtmosphere()
    {
        var fx = Group("Atmosphere").transform;

        var flies = Particles("Cold Fireflies", new Vector2(MapWidth / 2f, 7f), fx, _fireflies, 40, 6f, new Vector2(5f, 9f), new Vector2(0.05f, 0.2f),
                              new Vector2(0.08f, 0.16f), Grad((new Color(0.4f, 0.95f, 1f), 0f, 0f), (new Color(0.35f, 0.9f, 1f), 0.2f, 1f), (new Color(0.3f, 0.5f, 1f), 1f, 0f)), 0f, 0f, 0.7f, 80);
        var fs = flies.shape; fs.shapeType = ParticleSystemShapeType.Box; fs.rotation = Vector3.zero; fs.scale = new Vector3(MapWidth, 8f, 0.1f);
        var fsol = flies.sizeOverLifetime; fsol.enabled = false;

        var ash = Particles("Falling Ash", new Vector2(MapWidth / 2f, 20f), fx, _ash, 40, 18f, new Vector2(10f, 16f), Vector2.zero,
                            new Vector2(0.06f, 0.1f), Grad((new Color(0.5f, 0.55f, 0.6f), 0f, 0f), (new Color(0.45f, 0.5f, 0.55f), 0.15f, 0.7f), (new Color(0.3f, 0.32f, 0.36f), 1f, 0f)), 0f, 0f, 0.5f, 260);
        var ashShape = ash.shape; ashShape.shapeType = ParticleSystemShapeType.Box; ashShape.rotation = Vector3.zero; ashShape.scale = new Vector3(MapWidth + 20f, 1f, 0.1f);
        var av = ash.velocityOverLifetime; av.enabled = true;
        av.x = new ParticleSystem.MinMaxCurve(0.2f, 1.1f); av.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.5f); av.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var asol = ash.sizeOverLifetime; asol.enabled = false;

        Fog(fx, 1f, 4f, new Color(0.45f, 0.55f, 0.62f), 35, 0.3f).name = "Ground Fog (front)";
        Fog(fx, -2f, 4f, new Color(0.25f, 0.32f, 0.4f), 34, 0.45f).name = "Swamp Mist";
        UnderMist(fx, new Color(0.12f, 0.16f, 0.2f), 0.6f, 3.5f);
        WindStreaks(fx, new Color(0.6f, 0.72f, 0.85f, 0.16f), 9f, 12f);
        ForestSpores(fx, 7);   // green motes + a few wisps that light the trees as they blow past
    }

    static void BuildAreaBForeground()
    {
        var fg = Group("Foreground").transform;
        var near = Group("Near Foreground (parallax -0.25)", fg);
        SetZ(near.transform, -5f);
        var dark = new Color(0.02f, 0.025f, 0.04f);
        var bushWind = FoliageMat("M_FoliageBush", 0.45f, 2f, 0.08f, false);
        var trunkWind = FoliageMat("M_FoliageTrunk", 0.9f, 1.3f, 0.07f, false);
        for (float x = -6f; x < MapWidth + 30f; x += R(8f, 13f))
            Place("bush_darkest", new Vector2(x, R(-2.6f, -1.9f)), near.transform, 30, bushWind, dark, R(0.8f, 1f), _rng.Next(2) == 0);
        string[] trunks = { "pine_m_a", "dead_a", "pine_l_b", "dead_c", "pine_xl_a" };
        int n = 0;
        for (float x = 20f; x < MapWidth + 30f; x += R(18f, 26f))
        {
            var tree = Place(trunks[_rng.Next(trunks.Length)], new Vector2(x, R(-3.5f, -2.5f)), near.transform, 32, _unlit, new Color(0.015f, 0.02f, 0.03f, 0.95f), R(0.9f, 1.15f), _rng.Next(2) == 0);
            if (++n % 3 == 0)
            {
                var tt = tree.gameObject.AddComponent<TreeTopple>();
                tt.fallDirection = -1f;
                tt.triggerDistance = 5f;
                tt.fallAngle = 70f;
                tt.impactPrefab = _beamImpact;
                tt.impactOffset = new Vector3(tree.bounds.size.y * 0.6f, 1.5f, 0f);
                tt.shake = 0.2f;
            }
            else tree.sharedMaterial = trunkWind;
        }
        Parallax(near, -0.25f, 0f);
    }

    // ------------------------------------------------------------------ enemies: the shadow forest

    /// <summary>
    /// Area B shows the SHADOW element: every enemy rises from a shadow pool, trails violet smoke and dies into
    /// shadow mist. Pacing: Shade Rat on level ground with a fire-jet lane (GDD) -> Shade Rat under the low log ->
    /// Spore Shade (ranged) -> Hollow Skeleton (shield blocks fire: wait for its throw, or use the sister) ->
    /// the long-walk ambush -> mixed stretch -> boss: the Hollow King in the clearing before the tower.
    /// </summary>
    static void BuildAreaBEnemies()
    {
        BuildEnemyAssets();
        var root = Group("Enemies").transform;

        SpawnShadeRat(root, 19f, 12f, 23.5f);         // further in: the opening cutscene never frames it
        SpawnShadeRat(root, 39f, 29f, 41.8f);
        SpawnShadeGoblin(root, 33.5f, 9f);             // on the low log, lobbing bombs down at you
        SpawnMushroom(root, 52f);
        SpawnSkeleton(root, 60.5f, 49.5f, 62f);

        var w1 = new[] { SpawnSkeleton(root, 78f), SpawnShadeRat(root, 81.5f, 0f, MapWidth), SpawnShadeRat(root, 75f, 0f, MapWidth) };
        var w2 = new[] { SpawnMushroom(root, 74.5f), SpawnShadeEye(root, 80f, 12.5f), SpawnShadeRat(root, 82.5f, 0f, MapWidth), SpawnShadeGoblin(root, 83.5f) };
        var walk = ArenaAt(root, 63f, 84.5f, ASurf(72f), "Shadows in the long walk", w1, w2);
        walk.introLines = BWalk;
        SpawnShadeGoblin(root, 91f);
        SpawnShadeRat(root, 122.5f, 120.5f, 125f);
        DialogueZone("Memory 2", 96.5f, BMemory);

        SpawnMushroom(root, 116.5f);
        SpawnShadeEye(root, 114f, 13f);

        // Boss: the Hollow King rises from the clearing; the exit only opens when he falls.
        var king = SpawnExecutioner(root, 150f, 127.8f, 161.2f);
        var bossArena = ArenaAt(root, 127f, 162f, ASurf(140f), "The Executioner", new[] { king }, new EnemyHealth[0], lockBehind: true);
        bossArena.introLines = BBoss;
        var exit = GameObject.Find("Exit Trigger");
        if (exit) bossArena.enableOnClear = new[] { exit };
    }

    // ------------------------------------------------------------------ checkpoints & exit

    static void BuildAreaBCheckpoints()
    {
        var root = Group("Checkpoints").transform;
        foreach (var x in new[] { 30f, 50f, 111.5f, 124.5f })
        {
            var go = Group($"Checkpoint x{x}", root);
            go.transform.position = new Vector3(x, ASurf(x) + 0.1f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 6f);
            box.offset = new Vector2(0f, 3f);
            go.AddComponent<Checkpoint>();
        }

        float ax = 80f;
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
        ca.altarLight = LocalLight("Altar Light", altar.transform, new Vector2(0f, 2.2f), new Color(0.3f, 0.9f, 1f), 5f, 1.6f);
        ca.altarLight.gameObject.AddComponent<PulseLight2D>().pulseAmount = 0.25f;
        ca.igniteBurst = HasSheet("vfx_checkpoint")
            ? SheetFx("FX_CheckpointIgnite", "vfx_checkpoint", 14f, 2.4f, _neonTex, Color.white, 18, Vector2.zero, new Color(1f, 0.6f, 0.3f), 4f)
            : _emberBurst;

        // The forest path out, toward the clock tower.
        float gx = 165f;
        var gate = Group("Forest Path (exit)", root);
        Glow(new Vector2(gx + 2f, ASurf(gx) + 2.5f), gate.transform, new Color(0.45f, 0.6f, 1f, 0.45f), 8f, 5, _neonSoft).name = "Tower Light Beyond";
        PointLight("Path Light", new Vector2(gx + 1.5f, ASurf(gx) + 3f), gate.transform, new Color(0.5f, 0.65f, 1f), 7f, 1.2f).gameObject.AddComponent<PulseLight2D>();
        var exit = Group("Exit Trigger", gate.transform);
        exit.transform.position = new Vector3(gx + 2f, ASurf(gx), 0f);
        var et = exit.AddComponent<BoxCollider2D>();
        et.isTrigger = true;
        et.size = new Vector2(1f, 8f);
        et.offset = new Vector2(0f, 4f);
        var le = exit.AddComponent<LevelExit>();
        le.title = "Area C";
        le.subtitle = "The Clock Tower";
        le.nextScene = "AreaC_ClockTower";
    }
}
