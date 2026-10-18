using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the playable "Dark Forest" level: purple night, fire and neon, using the
/// Legacy-Fantasy High Forest sprites plus the burning-town parallax backgrounds in DarkForest/BGs.
/// Menu: Tools > Dark Forest > Build Scene. Safe to re-run: regenerates materials, tiles,
/// the volume profile and the scene (manual edits to the scene are overwritten).
/// </summary>
public static partial class DarkForestBuilder
{
    const string Root = "Assets/DarkForest";
    const string SpriteDir = Root + "/Sprites";
    const string BgDir = Root + "/BGs";
    const string MatDir = Root + "/Materials";
    const string TileDir = Root + "/Tiles";
    const string SceneDir = Root + "/Scenes";
    const string PrefabDir = Root + "/Prefabs";
    const string ScenePath = SceneDir + "/DarkForest.unity";
    const string TerrainAtlas = SpriteDir + "/ForestTiles.png";
    const int PPU = 16;
    const int BgPPU = 33;
    const int ShadowPPU = 24;      // shadow spirit frames (~4.5 units tall)          // 724px tall backgrounds -> ~22 world units, a bit taller than the view

    static readonly string[] CenterPivot = { "white", "glow", "dot", "moon", "rune_1", "rune_2", "rune_3", "rune_4", "rune_5", "rune_6",
                                             "heal_circle", "heal_sparkle", "fire_blast", "icon_lantern", "icon_sister" };
    const int BoyPPU = 30;        // exact 2x2 screen pixels per texel under the pixel-perfect camera (was 28)
    const int AnchorPPU = 28;     // lantern hand anchors in boy_lantern_anchors.json were measured at 28        // new boy sprites: ~84 px tall -> 3 units

    // Level: columns 0..119. Camera follows the player and shows 32 x 18 units at 16:9.
    static int MapWidth = 120;
    const float CamSize = 10f;   // a little wider than the old 9 (the pixel-perfect camera had zoomed far out)
    static Vector2 PlayerSpawn = new Vector2(15.3f, 4.5f);

    // Floating islands: (first column, last column, top row).
    static readonly (int x0, int x1, int top)[] Islands =
    {
        (24, 28, 11), (31, 33, 8),     // over the first pond (chest island + stepping stone)
        (60, 63, 7), (66, 69, 10),      // burning lowland
        (88, 90, 8),                   // over the lava pit
    };

    static Dictionary<string, Sprite> _sprites;
    static Dictionary<Vector2Int, Tile> _tiles;
    static Material _lit, _unlit, _neonAdd, _neonSoft, _neonTex, _fire, _fireflies, _sky, _fog, _lava, _smoke;
    static System.Random _rng;
    static Transform _cam;

    [MenuItem("Tools/Dark Forest/Build Scene")]
    public static void Build()
    {
        if (SceneManager.GetActiveScene().isDirty && SceneManager.GetActiveScene().path != ScenePath)
        {
            Debug.LogWarning("[DarkForest] Active scene has unsaved changes. Save it first, then run Build Scene again.");
            return;
        }

        _rng = new System.Random(1337);
        MapWidth = 120;
        PlayerSpawn = new Vector2(15.3f, 4.5f);
        EnsureFolders();
        ImportSprites();
        ImportBackgrounds();
        BuildTiles();
        BuildMaterials();
        var profile = BuildVolumeProfile();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = BuildCameraAndPost(profile);
        BuildSky();
        BuildParallaxBackgrounds();
        var heights = BuildTerrain();
        BuildColliders(heights);
        BuildMidground(heights);
        BuildProps(heights);
        BuildFireAndNeon(heights);
        BuildForeground(heights);
        var player = BuildPlayer();
        BuildShadowAbility(player);
        BuildCheckpoints(heights);

        var follow = cam.gameObject.AddComponent<CameraFollow2D>();
        follow.target = player.transform;
        follow.boundsMin = new Vector2(0f, 0f);
        follow.boundsMax = new Vector2(MapWidth, 40f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[DarkForest] Scene built: " + ScenePath);
    }

    // ------------------------------------------------------------------ assets

    static void EnsureFolders()
    {
        foreach (var dir in new[] { MatDir, TileDir, SceneDir, PrefabDir })
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(Root, Path.GetFileName(dir));
    }

    static void ImportSprites()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            var name = Path.GetFileNameWithoutExtension(path);
            if (name == "Shadow") continue;   // source sheet; the sliced shadow_N frames are used instead
            bool atlas = path == TerrainAtlas;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = atlas ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            ti.spritePixelsPerUnit = name.StartsWith("shadow") ? ShadowPPU
                                   : name.StartsWith("boy_") || name == "fire_blast" ? BoyPPU
                                   : name.StartsWith("heal_") || name.StartsWith("icon_") ? 32
                                   : name.StartsWith("lantern_") ? 48
                                   : PPU;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)(name.StartsWith("lantern_") ? SpriteAlignment.TopCenter
                                       : CenterPivot.Contains(name) ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        SliceTerrainAtlas();

        _sprites = new Dictionary<string, Sprite>();
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteDir }))
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>())
                _sprites[s.name] = s;
        ImportVfxSheets();
    }

    static void ImportBackgrounds()
    {
        for (int i = 1; i <= 4; i++)
        {
            var path = $"{BgDir}/Fire-{i}.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { Debug.LogWarning("[DarkForest] Missing background " + path); continue; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = BgPPU;
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
            _sprites[$"Fire-{i}"] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    static IEnumerable<Vector2Int> TerrainCells()
    {
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                yield return new Vector2Int(x, y);       // grass block
        for (int x = 6; x < 10; x++)
            for (int y = 18; y < 21; y++)
                yield return new Vector2Int(x, y);       // water (also used for lava)
    }

    static void SliceTerrainAtlas()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(TerrainAtlas);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainAtlas);
        int h = tex.height;
        var rects = TerrainCells().Select(c => new SpriteRect
        {
            name = $"t_{c.x}_{c.y}",
            rect = new Rect(c.x * 16, h - (c.y + 1) * 16, 16, 16),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            spriteID = GUID.Generate()
        }).ToArray();

        dp.SetSpriteRects(rects);
        var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        dp.Apply();
        ti.SaveAndReimport();
    }

    static void BuildTiles()
    {
        _tiles = new Dictionary<Vector2Int, Tile>();
        foreach (var c in TerrainCells())
        {
            var path = $"{TileDir}/t_{c.x}_{c.y}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = _sprites[$"t_{c.x}_{c.y}"];
            tile.colliderType = Tile.ColliderType.None;   // physics comes from BuildColliders (matches the grass line)
            EditorUtility.SetDirty(tile);
            _tiles[c] = tile;
        }
    }

    static Material SaveMaterial(string name, Shader shader, System.Action<Material> setup)
    {
        var path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        setup?.Invoke(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material Neon(string name, Color emission, float intensity, float flatten, bool additive,
                         float pulseSpeed = 2f, float pulseAmount = 0.25f, float flicker = 0f)
    {
        return SaveMaterial(name, Shader.Find("DarkForest/NeonSprite"), m =>
        {
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Flatten", flatten);
            m.SetFloat("_PulseSpeed", pulseSpeed);
            m.SetFloat("_PulseAmount", pulseAmount);
            m.SetFloat("_FlickerAmount", flicker);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        });
    }

    static void BuildMaterials()
    {
        const string urp = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/";
        _lit = AssetDatabase.LoadAssetAtPath<Material>(urp + "Sprite-Lit-Default.mat");
        _unlit = AssetDatabase.LoadAssetAtPath<Material>(urp + "Sprite-Unlit-Default.mat");

        _neonAdd = Neon("M_NeonAdditive", Color.white, 2.2f, 1f, true, 2.2f, 0.3f);
        _neonSoft = Neon("M_NeonGlowSoft", Color.white, 1.1f, 1f, true, 1.3f, 0.15f);
        _neonTex = Neon("M_NeonTextured", Color.white, 1.35f, 0f, false, 1.5f, 0.12f);
        _fire = Neon("M_Fire", Color.white, 1.5f, 1f, true, 0f, 0f, 0.25f);
        _fireflies = Neon("M_Fireflies", Color.white, 4f, 1f, true, 3.5f, 0.6f);
        _smoke = Neon("M_BgSmoke", Color.white, 1f, 1f, false, 0f, 0f);
        _lava = Neon("M_Lava", new Color(1f, 0.28f, 0.04f), 1.25f, 0.15f, false, 1.1f, 0.15f, 0.08f);

        // Particles have no sprite of their own, so give them soft round textures.
        var glowTex = _sprites["glow"].texture;
        var dotTex = _sprites["dot"].texture;
        _fire.mainTexture = glowTex;
        _neonAdd.mainTexture = dotTex;
        _neonSoft.mainTexture = glowTex;
        _fireflies.mainTexture = dotTex;
        _smoke.mainTexture = glowTex;

        _sky = SaveMaterial("M_NightSky", Shader.Find("DarkForest/NightSky"), null);
        _fog = SaveMaterial("M_MistFog", Shader.Find("DarkForest/MistFog"), m =>
        {
            m.SetColor("_Color", new Color(0.55f, 0.22f, 0.85f));
            m.SetFloat("_Density", 0.55f);
        });
    }

    static VolumeProfile BuildVolumeProfile()
    {
        var path = $"{Root}/DarkForest_PostFX.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(1.2f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.78f, 1f));
        bloom.highQualityFiltering.Override(true);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.15f);
        color.contrast.Override(18f);
        color.saturation.Override(14f);
        color.colorFilter.Override(new Color(0.96f, 0.9f, 1f));

        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(new Color(0.42f, 0.2f, 0.75f));   // purple shadows
        split.highlights.Override(new Color(1f, 0.58f, 0.32f));  // fire highlights
        split.balance.Override(-25f);

        var vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.06f, 0f, 0.1f));
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);

        foreach (var c in profile.components)
            AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // ------------------------------------------------------------------ scene helpers

    static GameObject Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        return go;
    }

    static SpriteRenderer Place(string sprite, Vector2 pos, Transform parent, int order, Material mat,
                                 Color? color = null, float scale = 1f, bool flip = false)
    {
        var go = new GameObject(sprite);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, parent ? parent.position.z : 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sprites[sprite];
        sr.sharedMaterial = mat;
        sr.sortingOrder = order;
        sr.color = color ?? Color.white;
        sr.flipX = flip;
        return sr;
    }

    static SpriteRenderer Glow(Vector2 pos, Transform parent, Color color, float size, int order, Material mat = null)
    {
        var sr = Place("glow", pos, parent, order, mat ? mat : _neonSoft, color, size / 4f);
        sr.name = "Glow";
        return sr;
    }

    static Light2D PointLight(string name, Vector2 pos, Transform parent, Color color, float radius, float intensity, float falloff = 0.55f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, parent ? parent.position.z : 0f);
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = radius * 0.08f;
        l.falloffIntensity = falloff;
        return l;
    }

    static ParallaxLayer Parallax(GameObject go, float fx, float fy, float repeatWidth = 0f)
    {
        var p = go.AddComponent<ParallaxLayer>();
        p.cameraTransform = _cam;
        p.factorX = fx;
        p.factorY = fy;
        p.repeatWidth = repeatWidth;
        return p;
    }

    static float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    static bool InPit(float x) => (x > 21f && x < 30.5f) || (x > 86f && x < 92f);

    // ------------------------------------------------------------------ camera, sky, backgrounds

    static Camera BuildCameraAndPost(VolumeProfile profile)
    {
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        float halfW = CamSize * 16f / 9f;
        camGo.transform.position = new Vector3(Mathf.Max(PlayerSpawn.x, halfW), Mathf.Max(PlayerSpawn.y + 2.2f, CamSize), -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CamSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.01f, 0.06f);
        cam.allowHDR = true;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        camGo.AddComponent<AudioListener>();
        _cam = camGo.transform;

        var volGo = new GameObject("PostFX Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        var globalGo = new GameObject("Global Light 2D (moonlight)");
        var global = globalGo.AddComponent<Light2D>();
        global.lightType = Light2D.LightType.Global;
        global.color = new Color(0.58f, 0.46f, 0.95f);
        global.intensity = 0.32f;
        return cam;
    }

    static void BuildSky()
    {
        Vector2 c = _cam.position;

        // Sky is "infinitely far": it moves exactly with the camera.
        var sky = Group("Sky (parallax 1.0)");
        sky.transform.position = c;
        var bg = Place("white", c, sky.transform, -100, _sky);
        bg.name = "NightSky";
        bg.transform.localScale = new Vector3(48f, CamSize * 2f + 6f, 1f);
        Parallax(sky, 1f, 1f);

        var moonRoot = Group("Moon (parallax 0.97)");
        var moonPos = c + new Vector2(7f, 5f);
        moonRoot.transform.position = moonPos;
        Glow(moonPos, moonRoot.transform, new Color(0.75f, 0.45f, 1f, 0.55f), 16f, -89);
        Glow(moonPos, moonRoot.transform, new Color(0.95f, 0.75f, 1f, 0.6f), 6f, -88);
        Place("moon", moonPos, moonRoot.transform, -87, _neonTex, new Color(1f, 0.92f, 1f), 1.2f).name = "Moon";
        Parallax(moonRoot, 0.97f, 0.97f);
    }

    /// <summary>
    /// The four burning-town layers from DarkForest/BGs. Each layer is tiled with mirrored copies
    /// (normal, flipped, normal...) so the joins are always seamless, and wraps around the camera forever.
    /// </summary>
    static void BuildParallaxBackgrounds()
    {
        var root = Group("Parallax Backgrounds").transform;
        Vector2 c = _cam.position;
        // tint = purple night colour for the non-fire pixels; boost = how hot the painted fire glows.
        var layers = new (string sprite, int order, float fx, float fy, float yOffset, Color tint, float boost, float smoke)[]
        {
            ("Fire-1", -90, 0.90f, 1.00f, 1.5f, new Color(0.62f, 0.55f, 0.8f), 1.5f, 0.004f),   // smoky sky
            ("Fire-2", -85, 0.75f, 0.90f, 2.5f, new Color(0.58f, 0.5f, 0.76f), 1.7f, 0.004f),  // far burning town
            ("Fire-3", -78, 0.55f, 0.85f, 2.0f, new Color(0.47f, 0.4f, 0.64f), 1.6f, 0.006f),  // ruins
            ("Fire-4", -70, 0.40f, 0.75f, 0.5f, new Color(0.34f, 0.27f, 0.48f), 2.0f, 0.003f), // near debris
        };


        foreach (var l in layers)
        {
            if (!_sprites.TryGetValue(l.sprite, out var sprite) || sprite == null) continue;
            var mat = SaveMaterial($"M_BG_{l.sprite}", Shader.Find("DarkForest/FireBackdrop"), m =>
            {
                m.SetFloat("_FireBoost", l.boost);
                m.SetFloat("_SmokeDistort", l.smoke);
            });

            var layer = Group($"{l.sprite} (parallax {l.fx:0.00})", root);
            layer.transform.position = new Vector3(c.x, c.y + l.yOffset, 0f);
            float width = sprite.bounds.size.x;
            for (int k = -2; k <= 2; k++)
            {
                var go = new GameObject($"{l.sprite} [{k}]");
                go.transform.SetParent(layer.transform, false);
                go.transform.localPosition = new Vector3(k * width, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = mat;
                sr.sortingOrder = l.order;
                sr.color = l.tint;
                sr.flipX = (k & 1) != 0;
            }
            Parallax(layer, l.fx, l.fy, width * 2f);

            // Living fire in the distance: embers and smoke that scroll with their layer.
            if (l.sprite == "Fire-3")
            {
                var smokePuffs = BackgroundParticles("BG Smoke Puffs", layer.transform, new Vector2(0f, -2f), new Vector2(width * 2f, 3f), _smoke, -80,
                    5f, new Vector2(8f, 12f), new Vector2(0.25f, 0.6f), new Vector2(3f, 6f),
                    Grad((new Color(1f, 0.45f, 0.2f), 0f, 0f), (new Color(0.35f, 0.22f, 0.4f), 0.25f, 0.28f), (new Color(0.18f, 0.12f, 0.24f), 1f, 0f)), 250);
                var grow = smokePuffs.sizeOverLifetime;   // smoke expands as it rises
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.4f));
                BackgroundParticles("BG Embers", layer.transform, new Vector2(0f, -6f), new Vector2(width * 2f, 5f), _neonAdd, -76,
                    28f, new Vector2(2.5f, 5f), new Vector2(0.6f, 1.6f), new Vector2(0.06f, 0.13f),
                    Grad((FireCore, 0f, 0f), (FireOrange, 0.15f, 1f), (FireMagenta, 1f, 0f)), 300);
            }
            else if (l.sprite == "Fire-2")
            {
                BackgroundParticles("BG Far Embers", layer.transform, new Vector2(0f, -3f), new Vector2(width * 2f, 4f), _neonAdd, -84,
                    16f, new Vector2(2f, 4f), new Vector2(0.4f, 1f), new Vector2(0.04f, 0.08f),
                    Grad((FireCore, 0f, 0f), (FireOrange, 0.2f, 0.8f), (FireMagenta, 1f, 0f)), 160);
            }
        }

        // Purple mist between the layers (fixed in the world), kept thin so the fires read.
        Fog(root, 1f, 10f, new Color(0.7f, 0.28f, 0.9f), -82, 0.22f);
        Fog(root, 0f, 9f, new Color(0.6f, 0.2f, 0.8f), -74, 0.16f);
        Fog(root, -1f, 8f, new Color(0.5f, 0.15f, 0.65f), -60, 0.35f);
    }

    /// <summary>Rising particles in a wide box, simulated in the layer's local space so they parallax with it.</summary>
    static ParticleSystem BackgroundParticles(string name, Transform layer, Vector2 localPos, Vector2 boxSize, Material mat, int order,
                                    float rate, Vector2 life, Vector2 speed, Vector2 size, Gradient colors, int max)
    {
        var ps = Particles(name, (Vector2)layer.position + localPos, layer, mat, order, rate, life, speed, size, colors, 0f, -0.02f, 0.5f, max);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(boxSize.x, boxSize.y, 0.1f);
        shape.rotation = Vector3.zero;
        var vel = ps.velocityOverLifetime;   // box emits along +Z, so push the particles upward
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
        vel.y = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startSpeed = 0f;
        return ps;
    }

    static SpriteRenderer Fog(Transform parent, float y, float height, Color color, int order, float alpha)
    {
        var sr = Place("white", new Vector2(MapWidth / 2f, y + height / 2f), parent, order, _fog, new Color(color.r, color.g, color.b, alpha));
        sr.name = "Mist";
        sr.transform.localScale = new Vector3(MapWidth + 40, height, 1);
        return sr;
    }

    // ------------------------------------------------------------------ terrain

    static int[] Heights()
    {
        var h = new int[MapWidth + 4]; // index = x + 2, covers x = -2 .. MapWidth+1
        for (int x = -2; x < MapWidth + 2; x++)
        {
            int v;
            if (x <= 9) v = 8;          // rune shrine
            else if (x <= 11) v = 7;
            else if (x <= 13) v = 6;
            else if (x <= 21) v = 5;    // campfire clearing
            else if (x <= 29) v = 2;    // pond bed
            else if (x <= 35) v = 5;
            else if (x <= 37) v = 7;
            else if (x <= 51) v = 10;   // mine cliff
            else if (x <= 54) v = 8;
            else if (x <= 57) v = 6;
            else if (x <= 71) v = 4;    // burning lowland
            else if (x <= 74) v = 6;
            else if (x <= 86) v = 8;    // dead wood
            else if (x <= 91) v = 2;    // lava pit
            else if (x <= 104) v = 7;   // second shrine
            else v = 6;                 // portal grove
            h[x + 2] = v;
        }
        return h;
    }

    static int H(int[] h, int x) => h[Mathf.Clamp(x, -2, MapWidth + 1) + 2];
    static float Surface(int[] h, float x) => H(h, Mathf.FloorToInt(x)) - 0.55f;
    const float WalkOffset = 0.6f;   // collider top sits this far below the tile top (inside the grass)

    static Tile GroundTile(int col, int row, int x, int y)
    {
        if (col == 1) col = 1 + ((x * 7 + y * 3) & 0x7fffffff) % 3;
        if (row >= 2 && row <= 3) row = 2 + (((x * 5 + y) & 1));
        return _tiles[new Vector2Int(col, row)];
    }

    static int[] BuildTerrain()
    {
        var h = Heights();
        var grid = Group("Grid");
        grid.AddComponent<Grid>();

        var groundGo = Group("Ground", grid.transform);
        var ground = groundGo.AddComponent<Tilemap>();
        var gr = groundGo.AddComponent<TilemapRenderer>();
        gr.sharedMaterial = _lit;
        gr.sortingOrder = 0;

        for (int x = -2; x < MapWidth + 2; x++)
        {
            int top = H(h, x);
            for (int y = -3; y < top; y++)
            {
                int d = top - 1 - y;
                int row = d == 0 ? 0 : d == 1 ? 1 : 2;
                bool openL = y >= H(h, x - 1);
                bool openR = y >= H(h, x + 1);
                int col = openL ? 0 : openR ? 4 : 1;
                ground.SetTile(new Vector3Int(x, y, 0), GroundTile(col, row, x, y));
            }
        }

        foreach (var isl in Islands)
        {
            for (int x = isl.x0; x <= isl.x1; x++)
            {
                int col = x == isl.x0 ? 0 : x == isl.x1 ? 4 : 1;
                ground.SetTile(new Vector3Int(x, isl.top, 0), GroundTile(col, 0, x, isl.top));
                ground.SetTile(new Vector3Int(x, isl.top - 1, 0), GroundTile(col, 1, x, isl.top - 1));
                ground.SetTile(new Vector3Int(x, isl.top - 2, 0), GroundTile(col, 4, x, isl.top - 2));
            }
        }

        // Pond water (lit so the neon glow reflects in it).
        var waterGo = Group("Water", grid.transform);
        var water = waterGo.AddComponent<Tilemap>();
        water.color = new Color(0.42f, 0.24f, 0.75f, 0.78f);
        var wr = waterGo.AddComponent<TilemapRenderer>();
        wr.sharedMaterial = _lit;
        wr.sortingOrder = 3;
        PaintLiquid(water, 21, 30);

        // Lava pit: same tiles, emissive neon material so it glows and blooms.
        var lavaGo = Group("Lava", grid.transform);
        var lava = lavaGo.AddComponent<Tilemap>();
        var lr = lavaGo.AddComponent<TilemapRenderer>();
        lr.sharedMaterial = _lava;
        lr.sortingOrder = 3;
        PaintLiquid(lava, 87, 91);
        return h;
    }

    static void PaintLiquid(Tilemap map, int x0, int x1)
    {
        for (int x = x0; x <= x1; x++)
        {
            int c = 6 + ((x - x0) % 4);
            map.SetTile(new Vector3Int(x, 4, 0), _tiles[new Vector2Int(c, 18)]);
            map.SetTile(new Vector3Int(x, 3, 0), _tiles[new Vector2Int(c, 19)]);
            map.SetTile(new Vector3Int(x, 2, 0), _tiles[new Vector2Int(c, 20)]);
        }
    }

    /// <summary>One box per run of equal ground height, plus islands and level walls.</summary>
    static void BuildColliders(int[] h)
    {
        var go = Group("Level Colliders");
        go.isStatic = true;
        const float bottom = -6f;

        int start = 0;
        for (int x = 1; x <= MapWidth; x++)
        {
            if (x < MapWidth && H(h, x) == H(h, start)) continue;
            float top = H(h, start) - WalkOffset;
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = new Vector2((start + x) / 2f, (top + bottom) / 2f);
            box.size = new Vector2(x - start, top - bottom);
            start = x;
        }

        // Floating islands are one-way platforms: jump up through them, land on top.
        var platforms = Group("Island Platforms (one-way)");
        platforms.isStatic = true;
        foreach (var isl in Islands)
        {
            var box = platforms.AddComponent<BoxCollider2D>();
            float top = isl.top + 1 - WalkOffset;
            box.offset = new Vector2((isl.x0 + isl.x1 + 1) / 2f, top - 0.25f);
            box.size = new Vector2(isl.x1 - isl.x0 + 1 - 0.2f, 0.5f);
            box.usedByEffector = true;
        }
        var effector = platforms.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f;
        effector.useSideFriction = false;

        foreach (var wallX in new[] { -0.5f, MapWidth + 0.5f })
        {
            var wall = go.AddComponent<BoxCollider2D>();
            wall.offset = new Vector2(wallX, 20f);
            wall.size = new Vector2(1f, 60f);
        }

        // Lava kills, pond is safe.
        var lavaZone = Group("Lava Kill Zone");
        var trig = lavaZone.AddComponent<BoxCollider2D>();
        trig.isTrigger = true;
        trig.offset = new Vector2(89.5f, 3.2f);
        trig.size = new Vector2(5f, 2.4f);
        lavaZone.AddComponent<KillZone>();
    }

    // ------------------------------------------------------------------ midground / props

    static void BuildMidground(int[] h)
    {
        var mid = Group("Midground Trees").transform;
        var tint = new Color(0.05f, 0.02f, 0.09f);
        var tint2 = new Color(0.09f, 0.04f, 0.16f);
        string[] front = { "pine_l_a", "pine_l_b", "pine_m_a", "pine_m_b", "pine_xl_a", "pine_m_b", "dead_c", "pine_l_a" };
        string[] back = { "pine_s_a", "pine_s_b", "pine_xs_a", "pine_xs_b", "dead_b" };

        for (float x = 0.5f; x < MapWidth; x += R(4f, 8f))
        {
            if (InPit(x)) continue;
            var name = front[_rng.Next(front.Length)];
            Place(name, new Vector2(x, Surface(h, x) - 0.2f), mid, -40, _unlit, tint, R(0.65f, 0.9f), _rng.Next(2) == 0);
        }
        for (float x = 2f; x < MapWidth; x += R(3f, 6f))
        {
            if (InPit(x)) continue;
            var name = back[_rng.Next(back.Length)];
            Place(name, new Vector2(x, Surface(h, x) + 0.2f), mid, -45, _unlit, tint2, R(0.8f, 1.05f), _rng.Next(2) == 0);
        }
    }

    static void BuildProps(int[] h)
    {
        var props = Group("Props").transform;

        // --- Zone 1: rune shrine
        var shrine = Group("Rune Shrine", props).transform;
        Place("stone_tall", new Vector2(2.2f, Surface(h, 2) + 0.15f), shrine, 6, _lit);
        Place("stone_c", new Vector2(5.1f, Surface(h, 5) + 0.15f), shrine, 6, _lit);
        Place("stone_b", new Vector2(8.0f, Surface(h, 8) + 0.15f), shrine, 6, _lit);
        Place("rock_moss_m", new Vector2(3.7f, Surface(h, 3) + 0.1f), shrine, 7, _lit);
        Place("rock_s", new Vector2(6.6f, Surface(h, 6) + 0.1f), shrine, 7, _lit);
        Place("flower_glow", new Vector2(4.4f, Surface(h, 4) + 0.2f), shrine, 8, _neonTex, new Color(0.9f, 0.6f, 1f));
        Place("flower_glow2", new Vector2(9.1f, Surface(h, 9) + 0.2f), shrine, 8, _neonTex, new Color(0.9f, 0.6f, 1f));
        Place("lavender", new Vector2(1.0f, Surface(h, 1) + 0.2f), shrine, 8, _lit);

        // --- Zone 1: campfire clearing (player spawn)
        var camp = Group("Campfire Clearing", props).transform;
        float gy = Surface(h, 17);
        Place("logs", new Vector2(17.6f, gy + 0.05f), camp, 9, _lit, null, 0.9f);
        Place("rock_xs", new Vector2(16.3f, gy + 0.1f), camp, 10, _lit);
        Place("rock_xs", new Vector2(18.9f, gy + 0.1f), camp, 10, _lit, null, 1f, true);
        Place("rock_moss_flat", new Vector2(14.7f, gy + 0.1f), camp, 7, _lit, null, 0.7f);
        Place("mush_big", new Vector2(20.4f, Surface(h, 20) + 0.15f), camp, 8, _lit);
        Place("mush_s3", new Vector2(21.1f, Surface(h, 21) + 0.15f), camp, 8, _lit);
        Place("mush_s1", new Vector2(13.1f, Surface(h, 13) + 0.15f), camp, 8, _lit);
        Place("fence", new Vector2(11.4f, Surface(h, 11) + 0.15f), camp, 7, _lit);

        // --- Zone 1: pond + islands
        var pond = Group("Pond", props).transform;
        Place("cattail", new Vector2(21.6f, 4.35f), pond, 4, _lit);
        Place("cattail", new Vector2(29.8f, 4.35f), pond, 4, _lit, null, 1f, true);
        Place("lily", new Vector2(24.2f, 4.15f), pond, 5, _neonTex, new Color(0.95f, 0.7f, 1f), 0.9f);
        Place("lily", new Vector2(27.6f, 4.15f), pond, 5, _neonTex, new Color(0.95f, 0.7f, 1f), 0.75f, true);
        Place("chest", new Vector2(26.6f, 11.45f), pond, 8, _lit);
        Place("lavender", new Vector2(24.8f, 11.5f), pond, 8, _lit);
        Place("mush_tall", new Vector2(28.1f, 11.5f), pond, 8, _lit);
        Place("mush_s2", new Vector2(32.4f, 8.45f), pond, 8, _lit);

        // --- Zone 1: mine cliff
        var mine = Group("Mine Cliff", props).transform;
        Place("rock_moss_big", new Vector2(34.6f, Surface(h, 34) + 0.1f), mine, 6, _lit, null, 0.8f);
        Place("mush_big2", new Vector2(30.9f, Surface(h, 30) + 0.15f), mine, 8, _lit);
        Place("stone_d", new Vector2(36.6f, Surface(h, 36) + 0.1f), mine, 6, _lit, null, 0.8f);
        Place("mine", new Vector2(42.6f, Surface(h, 42) + 0.05f), mine, 5, _lit, null, 0.9f);
        Place("post", new Vector2(38.8f, Surface(h, 38) + 0.1f), mine, 9, _lit);
        Place("post", new Vector2(46.6f, Surface(h, 46) + 0.1f), mine, 9, _lit);
        Place("logs", new Vector2(49.5f, Surface(h, 49) + 0.05f), mine, 9, _lit);

        // --- Zone 2: burning lowland
        var low = Group("Burning Lowland", props).transform;
        Place("post", new Vector2(58.6f, Surface(h, 58) + 0.1f), low, 9, _lit);
        Place("post", new Vector2(70.8f, Surface(h, 70) + 0.1f), low, 9, _lit);
        Place("rock_big", new Vector2(60.6f, Surface(h, 60) + 0.1f), low, 6, _lit, null, 0.75f);
        Place("logs", new Vector2(64.6f, Surface(h, 64) + 0.05f), low, 9, _lit, null, 1.1f);
        Place("fence", new Vector2(67.4f, Surface(h, 67) + 0.15f), low, 7, _lit, null, 1f, true);
        Place("rock_moss_flat", new Vector2(69.2f, Surface(h, 69) + 0.1f), low, 7, _lit, null, 0.8f);
        Place("lavender", new Vector2(61.3f, 7.5f), low, 8, _lit);
        Place("chest", new Vector2(67.6f, 10.45f), low, 8, _lit);
        Place("mush_big", new Vector2(73.2f, Surface(h, 73) + 0.15f), low, 8, _lit, null, 1f, true);

        // --- Zone 3: dead wood before the lava
        var dead = Group("Dead Wood", props).transform;
        Place("stone_d", new Vector2(77.8f, Surface(h, 77) + 0.1f), dead, 6, _lit, null, 0.8f);
        Place("rock_moss_big", new Vector2(80.8f, Surface(h, 80) + 0.1f), dead, 6, _lit, null, 0.75f);
        Place("flower_glow", new Vector2(76.4f, Surface(h, 76) + 0.2f), dead, 8, _neonTex, new Color(0.9f, 0.6f, 1f));
        Place("mush_big2", new Vector2(83.0f, Surface(h, 83) + 0.15f), dead, 8, _lit);
        Place("mush_tall", new Vector2(84.2f, Surface(h, 84) + 0.15f), dead, 8, _lit);
        Place("post", new Vector2(85.8f, Surface(h, 85) + 0.1f), dead, 9, _lit);
        Place("post", new Vector2(92.4f, Surface(h, 92) + 0.1f), dead, 9, _lit);

        // --- Zone 4: second shrine and the portal grove
        var shrine2 = Group("Ember Shrine", props).transform;
        Place("stone_tall", new Vector2(95.2f, Surface(h, 95) + 0.15f), shrine2, 6, _lit);
        Place("rock_moss_m", new Vector2(96.9f, Surface(h, 96) + 0.1f), shrine2, 7, _lit);
        Place("stone_b", new Vector2(98.6f, Surface(h, 98) + 0.15f), shrine2, 6, _lit, null, 1f, true);
        Place("stone_c", new Vector2(102.0f, Surface(h, 102) + 0.15f), shrine2, 6, _lit);
        Place("flower_glow2", new Vector2(100.2f, Surface(h, 100) + 0.2f), shrine2, 8, _neonTex, new Color(0.9f, 0.6f, 1f));
        Place("lavender", new Vector2(103.6f, Surface(h, 103) + 0.2f), shrine2, 8, _lit);

        var grove = Group("Portal Grove", props).transform;
        Place("stone_b", new Vector2(110.6f, Surface(h, 110) + 0.15f), grove, 6, _lit, null, 1.15f);
        Place("stone_b", new Vector2(115.6f, Surface(h, 115) + 0.15f), grove, 6, _lit, null, 1.15f, true);
        Place("rock_moss_s2", new Vector2(108.4f, Surface(h, 108) + 0.1f), grove, 7, _lit);
        Place("mush_big", new Vector2(117.6f, Surface(h, 117) + 0.15f), grove, 8, _lit);
        Place("lavender", new Vector2(106.7f, Surface(h, 106) + 0.2f), grove, 8, _lit);
    }

    // ------------------------------------------------------------------ fire, neon, particles

    static ParticleSystem Particles(string name, Vector2 pos, Transform parent, Material mat, int order,
                                    float rate, Vector2 life, Vector2 speed, Vector2 size, Gradient colors,
                                    float radius, float gravity, float noise, int max = 200)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, parent ? parent.position.z : 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = Color.white;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = radius;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = colors;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

        if (noise > 0f)
        {
            var n = ps.noise;
            n.enabled = true;
            n.strength = noise;
            n.frequency = 0.8f;
            n.scrollSpeed = 0.4f;
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = order;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static Gradient Grad(params (Color c, float t, float a)[] keys)
    {
        var g = new Gradient();
        g.SetKeys(keys.Select(k => new GradientColorKey(k.c, k.t)).ToArray(),
                  keys.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray());
        return g;
    }

    static readonly Color FireCore = new Color(1f, 0.92f, 0.55f);
    static readonly Color FireOrange = new Color(1f, 0.42f, 0.1f);
    static readonly Color FireMagenta = new Color(0.85f, 0.12f, 0.6f);
    static readonly Color Violet = new Color(0.62f, 0.2f, 1f);

    static Gradient FireGradient() => Grad((FireCore, 0f, 0f), (FireCore, 0.08f, 1f), (FireOrange, 0.35f, 0.9f), (FireMagenta, 0.75f, 0.5f), (Violet, 1f, 0f));

    static void Fire(string name, Vector2 pos, Transform parent, float scale, float lightRadius, float lightIntensity)
    {
        var root = Group(name, parent).transform;
        root.position = pos;
        Particles("Flames", pos, root, _fire, 14, 45f * scale, new Vector2(0.5f, 1.0f), new Vector2(1.0f, 2.2f) * scale,
                  new Vector2(0.7f, 1.4f) * scale, FireGradient(), 0.3f * scale, -0.1f, 0.35f);
        Particles("Embers", pos + Vector2.up * 0.3f, root, _neonAdd, 15, 9f * scale, new Vector2(1.2f, 2.6f), new Vector2(1f, 2.5f) * scale,
                  new Vector2(0.06f, 0.14f), Grad((FireCore, 0f, 1f), (FireOrange, 0.4f, 1f), (FireMagenta, 1f, 0f)),
                  0.4f * scale, -0.05f, 1.2f, 80);
        Glow(pos + Vector2.up * 0.5f * scale, root, new Color(1f, 0.45f, 0.15f, 0.4f), 5f * scale, 13, _neonSoft);
        var l = PointLight("Fire Light", pos + Vector2.up * 0.6f, root, new Color(1f, 0.5f, 0.2f), lightRadius, lightIntensity);
        l.gameObject.AddComponent<FlickerLight2D>();
        var rim = PointLight("Fire Rim (magenta)", pos + Vector2.up * 1.2f, root, FireMagenta, lightRadius * 1.6f, lightIntensity * 0.35f, 0.8f);
        rim.gameObject.AddComponent<FlickerLight2D>().flickerAmount = 0.2f;
    }

    static void Torch(string name, float x, int[] h, Transform parent) =>
        Fire(name, new Vector2(x, Surface(h, x) + 2.1f), parent, 0.45f, 4.5f, 1.6f);

    static void NeonRune(string sprite, Vector2 pos, Transform parent, Color color)
    {
        var sr = Place(sprite, pos, parent, 20, _neonAdd, color, 1.4f);
        sr.name = "Neon " + sprite;
        sr.gameObject.AddComponent<FloatBob>().amplitude = 0.18f;
        Glow(pos, sr.transform, new Color(color.r, color.g, color.b, 0.45f), 3.2f, 19).transform.localScale = Vector3.one * (3.2f / 4f / 1.4f);
        var l = PointLight("Rune Light", pos, parent, color, 4.5f, 1.6f);
        l.gameObject.AddComponent<PulseLight2D>();
    }

    static void Portal(string name, Vector2 pos, Transform parent, float size)
    {
        var root = Group(name, parent).transform;
        Glow(pos, root, new Color(0.7f, 0.2f, 1f, 0.5f), 6f * size, 6, _neonAdd).name = "Portal Glow";
        Glow(pos, root, new Color(1f, 0.4f, 0.9f, 0.35f), 2.5f * size, 7, _neonAdd).name = "Portal Core";
        PointLight("Portal Light", pos, root, Violet, 7f * size, 2.4f).gameObject.AddComponent<PulseLight2D>().speed = 2.2f;
        Particles("Portal Sparks", pos + Vector2.down * 1.5f * size, root, _neonAdd, 16, 10f * size, new Vector2(1.5f, 3f), new Vector2(0.3f, 0.9f),
                  new Vector2(0.06f, 0.14f), Grad((new Color(1f, 0.6f, 1f), 0f, 0f), (Violet, 0.2f, 1f), (new Color(0.4f, 0.1f, 1f), 1f, 0f)),
                  1f * size, -0.02f, 0.6f, 60);
    }

    static void BuildFireAndNeon(int[] h)
    {
        var fx = Group("Fire & Neon").transform;
        var runeA = new Color(0.75f, 0.25f, 1f);
        var runeB = new Color(1f, 0.3f, 0.8f);
        var runeFire = new Color(1f, 0.55f, 0.25f);

        // Zone 1
        Fire("Campfire", new Vector2(17.6f, Surface(h, 17) + 0.25f), fx, 1f, 9f, 2.3f);
        Torch("Torch Mine L", 38.8f, h, fx);
        Torch("Torch Mine R", 46.6f, h, fx);
        Portal("Mine Portal", new Vector2(43.2f, Surface(h, 43) + 2.4f), fx, 1f);
        NeonRune("rune_1", new Vector2(2.2f, Surface(h, 2) + 7.0f), fx, runeA);
        NeonRune("rune_3", new Vector2(5.1f, Surface(h, 5) + 5.2f), fx, runeB);
        NeonRune("rune_4", new Vector2(8.0f, Surface(h, 8) + 6.2f), fx, runeA);
        NeonRune("rune_6", new Vector2(26.6f, 14.6f), fx, runeFire);
        Particles("Shrine Motes", new Vector2(5f, Surface(h, 5) + 0.3f), fx, _neonAdd, 18, 6f, new Vector2(3f, 5f), new Vector2(0.2f, 0.6f),
                  new Vector2(0.05f, 0.12f), Grad((runeA, 0f, 0f), (runeA, 0.2f, 1f), (runeB, 1f, 0f)), 3.5f, -0.02f, 0.5f, 60);
        PointLight("Mushroom Glow L", new Vector2(20.6f, Surface(h, 20) + 1f), fx, new Color(1f, 0.25f, 0.65f), 3.5f, 1.4f).gameObject.AddComponent<PulseLight2D>();
        PointLight("Mushroom Glow R", new Vector2(30.9f, Surface(h, 30) + 1f), fx, new Color(1f, 0.25f, 0.65f), 3.5f, 1.4f).gameObject.AddComponent<PulseLight2D>();
        PointLight("Chest Glow", new Vector2(26.6f, 12.4f), fx, new Color(1f, 0.7f, 0.3f), 3.5f, 1.5f).gameObject.AddComponent<PulseLight2D>();
        PointLight("Pond Glow", new Vector2(25.8f, 2.8f), fx, new Color(0.7f, 0.3f, 1f), 6f, 1.1f).gameObject.AddComponent<PulseLight2D>().speed = 0.8f;
        PointLight("Flower Glow", new Vector2(4.4f, Surface(h, 4) + 0.8f), fx, new Color(0.7f, 0.35f, 1f), 2.5f, 1.2f);
        var refl = Glow(new Vector2(25.5f, 4.25f), fx, new Color(0.85f, 0.6f, 1f, 0.55f), 4f, 6, _neonAdd);
        refl.name = "Moon Reflection";
        refl.transform.localScale = new Vector3(1.6f, 0.12f, 1f);

        // Zone 2: burning lowland
        Torch("Brazier L", 58.6f, h, fx);
        Torch("Brazier R", 70.8f, h, fx);
        Fire("Bonfire", new Vector2(64.6f, Surface(h, 64) + 0.3f), fx, 1.3f, 11f, 2.6f);
        NeonRune("rune_2", new Vector2(67.6f, 13.6f), fx, runeB);
        PointLight("Chest Glow 2", new Vector2(67.6f, 11.4f), fx, new Color(1f, 0.7f, 0.3f), 3.5f, 1.5f).gameObject.AddComponent<PulseLight2D>();

        // Zone 3: lava pit
        Torch("Torch Lava L", 85.8f, h, fx);
        Torch("Torch Lava R", 92.4f, h, fx);
        var lavaCenter = new Vector2(89.5f, 4.2f);
        var lavaLight = PointLight("Lava Light", lavaCenter, fx, new Color(1f, 0.35f, 0.1f), 9f, 2.2f);
        lavaLight.gameObject.AddComponent<FlickerLight2D>().flickerAmount = 0.2f;
        var lavaGlow = Glow(lavaCenter + Vector2.up * 0.4f, fx, new Color(1f, 0.35f, 0.1f, 0.6f), 6f, 4, _neonSoft);
        lavaGlow.transform.localScale = new Vector3(2.2f, 0.6f, 1f);
        var lavaEmbers = Particles("Lava Embers", lavaCenter, fx, _neonAdd, 15, 14f, new Vector2(1.5f, 3f), new Vector2(0.6f, 1.8f),
                  new Vector2(0.06f, 0.16f), Grad((FireCore, 0f, 1f), (FireOrange, 0.4f, 1f), (FireMagenta, 1f, 0f)), 0f, -0.04f, 0.8f, 120);
        var ls = lavaEmbers.shape;
        ls.shapeType = ParticleSystemShapeType.Box;
        ls.rotation = Vector3.zero;
        ls.scale = new Vector3(5f, 0.2f, 0.1f);

        // Zone 4: ember shrine and the final portal
        NeonRune("rune_5", new Vector2(95.2f, Surface(h, 95) + 7.0f), fx, runeFire);
        NeonRune("rune_1", new Vector2(98.6f, Surface(h, 98) + 6.2f), fx, runeB);
        NeonRune("rune_3", new Vector2(102.0f, Surface(h, 102) + 5.2f), fx, runeA);
        Particles("Ember Motes", new Vector2(98.6f, Surface(h, 98) + 0.3f), fx, _neonAdd, 18, 6f, new Vector2(3f, 5f), new Vector2(0.2f, 0.6f),
                  new Vector2(0.05f, 0.12f), Grad((runeFire, 0f, 0f), (runeFire, 0.2f, 1f), (runeB, 1f, 0f)), 3.5f, -0.02f, 0.5f, 60);
        Portal("Exit Portal", new Vector2(113.1f, Surface(h, 113) + 3f), fx, 1.4f);

        // Fireflies across the whole level.
        var ff = Particles("Fireflies", new Vector2(MapWidth / 2f, 9f), fx, _fireflies, 40, 18f, new Vector2(6f, 10f), new Vector2(0.05f, 0.25f),
                           new Vector2(0.1f, 0.22f), Grad((new Color(1f, 0.55f, 1f), 0f, 0f), (new Color(0.8f, 0.4f, 1f), 0.2f, 1f),
                           (new Color(1f, 0.6f, 0.35f), 0.7f, 1f), (Violet, 1f, 0f)), 0f, 0f, 0.7f, 220);
        var shape = ff.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(MapWidth, 12f, 0.1f);
        var sol = ff.sizeOverLifetime;
        sol.enabled = false;
    }

    static void BuildForeground(int[] h)
    {
        var fg = Group("Foreground").transform;
        var dark = new Color(0.035f, 0.015f, 0.06f);

        // Foreground silhouettes slide against the camera (parallax -0.25) so they feel right in front of the lens.
        // They drift up to ~25 units over the level, so they are scattered wider than the map.
        var near = Group("Near Foreground (parallax -0.25)", fg);
        for (float x = -6f; x < MapWidth + 30f; x += R(9f, 14f))
            Place("bush_darkest", new Vector2(x, R(-2.4f, -1.8f)), near.transform, 30, _unlit, dark, R(0.8f, 0.95f), _rng.Next(2) == 0);
        string[] trunks = { "dead_a", "dead_b", "dead_c", "pine_m_a" };
        for (float x = 10f; x < MapWidth + 30f; x += R(18f, 28f))
            Place(trunks[_rng.Next(trunks.Length)], new Vector2(x, R(-3.5f, -2.5f)), near.transform, 32, _unlit,
                  new Color(0.02f, 0.01f, 0.035f), R(1.25f, 1.5f), _rng.Next(2) == 0);
        Parallax(near, -0.25f, 0f);

        // Lit bushes along the ground line.
        for (float x = 6f; x < MapWidth; x += R(5f, 9f))
        {
            if (InPit(x) || InPit(x + 1f) || InPit(x - 1f)) continue;
            Place(_rng.Next(2) == 0 ? "bush_dark" : "bush_darkest", new Vector2(x, Surface(h, x) - 0.35f), fg, 11, _lit, null, R(0.3f, 0.42f), _rng.Next(2) == 0);
        }

        Fog(fg, -1.5f, 4f, new Color(0.6f, 0.2f, 0.8f), 35, 0.22f);
    }

    // ------------------------------------------------------------------ player & checkpoints

    static GameObject BuildPlayer()
    {
        var pmPath = $"{MatDir}/PM_NoFriction.physicsMaterial2D";
        var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(pmPath);
        if (pm == null)
        {
            pm = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(pm, pmPath);
        }

        var go = new GameObject("Player");
        go.transform.position = PlayerSpawn;
        var sr = go.AddComponent<SpriteRenderer>();
        bool boy = _sprites.ContainsKey("boy_idle_0");
        string prefix = boy ? "boy" : "hero";
        sr.sprite = _sprites[$"{prefix}_idle_0"];
        // Unlit so the boy always reads against the dark village; the rim adds the neon outline.
        sr.sharedMaterial = Neon("M_PlayerSprite", Color.white, 1.3f, 0f, false, 0f, 0f);
        sr.sortingOrder = 12;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3.5f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sharedMaterial = pm;

        var col = go.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.8f, 2.6f);
        col.offset = new Vector2(0f, 1.32f);
        col.sharedMaterial = pm;

        var pc = go.AddComponent<PlayerController2D>();
        pc.idleFrames = Frames("idle", prefix);
        pc.runFrames = Frames("run", prefix);
        pc.riseFrames = Frames("rise", prefix);
        pc.fallFrames = Frames("fall", prefix);
        pc.landFrames = Frames("land", prefix);
        // PixelLab frames: a careful walk is the default pace, Shift runs; the 16-frame jump is split into
        // launch/rise, apex + cape-up fall, and the landing recovery.
        var walk = Frames("walk", prefix);
        var jump = Frames("jump", prefix);
        if (walk.Length > 0) pc.walkFrames = walk;
        if (jump.Length >= 16)
        {
            pc.riseFrames = jump[5..9];
            pc.fallFrames = jump[9..13];
            pc.landFrames = jump[13..15];
        }
        pc.moveSpeed = walk.Length > 0 ? 4.4f : 5.8f;
        pc.runSpeed = 6.6f;
        pc.acceleration = 40f;
        pc.deceleration = 55f;
        pc.idleFps = 7f;
        pc.walkFps = 10f;
        pc.runFps = 15f;
        pc.airFps = 12f;

        go.AddComponent<PlayerHealth>();

        // Double jump ring, dash streak + trail, violet dash ghosts, and a floor under the level so he can never fall forever.
        pc.airJumpFx = SheetFx("FX_AirJump", "fx_ring", 40f, 1.1f, _neonAdd, new Color(0.75f, 0.55f, 1f, 0.9f), 9, Vector2.zero, new Color(0.6f, 0.4f, 1f), 2.5f);
        if (HasSheet("fx_dash")) pc.dashFx = SheetFx("FX_Dash", "fx_dash", 18f, 0.7f, _neonAdd, new Color(0.85f, 0.7f, 1f, 0.95f), 13, Vector2.zero);
        if (HasSheet("fx_dashtrail")) pc.dashTrailFx = SheetFx("FX_DashTrail", "fx_dashtrail", 18f, 0.7f, _neonAdd, new Color(1f, 0.72f, 0.45f, 0.85f), 12, Vector2.zero);
        pc.ghostMaterial = Neon("M_AfterImage", Color.white, 1.6f, 1f, true, 0f, 0f);
        pc.fallLimitY = _ground.Where(g => g.h > 0).Min(g => g.h) - 7f;

        // Neon glow overlay (heal = cyan, charge = orange) + the lantern's warm light (GDD: #FF9A3C).
        var glowMat = Neon("M_PlayerGlow", Color.white, 2.2f, 1f, true, 0f, 0f);
        var overlay = new GameObject("Glow Overlay").AddComponent<SpriteRenderer>();
        overlay.transform.SetParent(go.transform, false);
        overlay.sharedMaterial = glowMat;
        overlay.sortingOrder = 13;
        overlay.color = new Color(1f, 1f, 1f, 0f);
        BuildHandLantern(go, sr, rb);
        var rim = new GameObject("Neon Rim").AddComponent<SpriteRenderer>();
        rim.transform.SetParent(go.transform, false);
        rim.sharedMaterial = glowMat;
        rim.sortingOrder = 11;              // behind the boy -> only the outline shows
        rim.color = new Color(1f, 0.6f, 0.25f, 0f);
        var pg = go.AddComponent<PlayerGlow>();
        pg.source = sr;
        pg.overlay = overlay;
        pg.rim = rim;
        pg.rimColor = new Color(0.4f, 0.95f, 0.55f);       // soft green outline (reads on fire and on moonlight)
        pg.sisterRimColor = new Color(0.35f, 0.85f, 1f);
        pg.rimAlpha = 0.55f;                              // stronger outline: the boy must pop off the backgrounds
        pg.rimScale = 1.09f;

        if (HasSheet("vfx_jump_dust"))
        {
            pc.jumpDust = SheetFx("FX_JumpDust", "vfx_jump_dust", 16f, 1.3f, _unlit, new Color(0.75f, 0.7f, 0.8f, 0.9f), 9, new Vector2(0f, 0.35f));
            pc.landDust = SheetFx("FX_LandDust", "vfx_land_dust", 16f, 1.6f, _unlit, new Color(0.75f, 0.7f, 0.8f, 0.9f), 9, new Vector2(0f, 0.35f));
            var hp = go.GetComponent<PlayerHealth>();
            hp.hurtFlash = SheetFx("FX_HurtFlash", "vfx_hurt_flash", 18f, 2.4f, _neonAdd, new Color(1f, 0.45f, 0.4f, 1f), 16, Vector2.zero, new Color(1f, 0.3f, 0.25f), 3f);
            hp.respawnOut = SheetFx("FX_RespawnOut", "vfx_respawn_out", 14f, 2.6f, _neonTex, Color.white, 16, Vector2.zero);
            hp.respawnIn = SheetFx("FX_RespawnIn", "vfx_respawn_in", 14f, 2.6f, _neonTex, Color.white, 16, Vector2.zero, new Color(1f, 0.6f, 0.3f), 3f);
        }

        if (boy)
        {
            var atk = go.AddComponent<LanternAttack>();
            atk.chargeFrames = Frames("charge", prefix);
            var hit = BuildFireHitPrefab();
            atk.emberShotPrefab = BuildFireBoltPrefab("EmberShot", false, hit);
            atk.chargedFlarePrefab = BuildFireBoltPrefab("ChargedFlare", true, hit);
        }
        return go;
    }

    // ------------------------------------------------------------------ shadow spirit ability (P)

    static readonly Color ShadowCore = new Color(0.85f, 0.85f, 1f);
    static readonly Color ShadowBlue = new Color(0.3f, 0.5f, 1f);
    static readonly Color ShadowViolet = new Color(0.62f, 0.22f, 1f);
    static readonly Color ShadowMagenta = new Color(0.95f, 0.22f, 0.85f);
    static readonly Color ShadowDeep = new Color(0.25f, 0.1f, 0.75f);

    static Gradient ShadowFireGradient() => Grad((ShadowCore, 0f, 0f), (ShadowBlue, 0.12f, 1f), (ShadowViolet, 0.45f, 0.9f),
                                                 (ShadowMagenta, 0.8f, 0.45f), (ShadowDeep, 1f, 0f));

    static GameObject SavePrefab(GameObject go, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>A one-shot particle burst from a circle (arc 180 = upward fan, 360 = ring).</summary>
    static ParticleSystem OneShot(string name, Transform parent, Vector2 localPos, Material mat, int order, int count,
                                  Vector2 life, Vector2 speed, Vector2 size, Gradient colors, float radius, float arc,
                                  float gravity, float noise)
    {
        var ps = Particles(name, Vector2.zero, parent, mat, order, 0f, life, speed, size, colors, radius, gravity, noise, count * 2);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.localPosition = localPos;
        var main = ps.main;
        main.loop = false;
        main.prewarm = false;
        main.duration = 0.5f;
        main.playOnAwake = true;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.arc = arc;
        shape.rotation = Vector3.zero;
        return ps;
    }

    static SpriteRenderer LocalGlow(string name, Transform parent, Vector2 localPos, Vector2 size, Color color, int order, Material mat)
    {
        var sr = Place("glow", Vector2.zero, parent, order, mat, color);
        sr.name = name;
        sr.transform.localPosition = localPos;
        sr.transform.localScale = new Vector3(size.x / 4f, size.y / 4f, 1f);
        return sr;
    }

    static Light2D LocalLight(string name, Transform parent, Vector2 localPos, Color color, float radius, float intensity)
    {
        var l = PointLight(name, Vector2.zero, parent, color, radius, intensity);
        l.transform.localPosition = localPos;
        return l;
    }

    static GameObject BuildSummonBurstPrefab()
    {
        var root = new GameObject("ShadowSummonBurst");
        var t = root.transform;
        var ring = LocalGlow("Flash Ring", t, new Vector2(0f, 0.1f), new Vector2(3f, 0.6f), new Color(ShadowViolet.r, ShadowViolet.g, ShadowViolet.b, 1f), 16, _neonAdd);
        var pillar = LocalGlow("Flash Pillar", t, new Vector2(0f, 1.6f), new Vector2(1f, 4.5f), new Color(ShadowBlue.r, ShadowBlue.g, ShadowBlue.b, 0.55f), 16, _neonAdd);
        OneShot("Sparks", t, Vector2.zero, _neonAdd, 17, 45, new Vector2(0.4f, 0.9f), new Vector2(3f, 7f), new Vector2(0.08f, 0.18f),
                Grad((ShadowCore, 0f, 1f), (ShadowBlue, 0.3f, 1f), (ShadowViolet, 1f, 0f)), 0.4f, 180f, 0.5f, 0.4f);
        OneShot("Shadow Smoke", t, new Vector2(0f, 0.3f), _smoke, 10, 16, new Vector2(0.8f, 1.4f), new Vector2(0.5f, 1.6f), new Vector2(1f, 2.2f),
                Grad((ShadowViolet, 0f, 0f), (new Color(0.22f, 0.12f, 0.45f), 0.2f, 0.6f), (new Color(0.1f, 0.05f, 0.2f), 1f, 0f)), 0.6f, 180f, -0.1f, 0.3f);
        LocalLight("Flash Light", t, new Vector2(0f, 1f), ShadowViolet, 6f, 2.2f);
        var vfx = root.AddComponent<VfxLifetime>();
        vfx.sustain = 0.15f;
        vfx.fade = 0.5f;
        vfx.flashes = new[] { ring, pillar };
        vfx.flashDuration = 0.45f;
        vfx.flashGrow = 2.5f;
        return SavePrefab(root, "ShadowSummonBurst");
    }

    static Material ShadowFireMaterial()
    {
        var m = Neon("M_ShadowFire", Color.white, 2.6f, 1f, true, 0f, 0f, 0.3f);
        m.mainTexture = _sprites["glow"].texture;
        return m;
    }

    static GameObject BuildImpactPrefab()
    {
        var root = new GameObject("ShadowFireImpact");
        var hot = ShadowFireMaterial();
        var t = root.transform;
        var shock = LocalGlow("Shockwave", t, new Vector2(0f, 0.15f), new Vector2(3.5f, 0.7f), new Color(ShadowViolet.r, ShadowViolet.g, ShadowViolet.b, 1f), 18, _neonAdd);
        var flash = LocalGlow("Flash", t, new Vector2(0f, 0.6f), new Vector2(3f, 3f), new Color(0.75f, 0.8f, 1f, 0.9f), 18, _neonAdd);
        LocalGlow("Scorch Glow", t, new Vector2(0f, 0.1f), new Vector2(4f, 0.8f), new Color(ShadowViolet.r, ShadowViolet.g, ShadowViolet.b, 0.8f), 4, _neonSoft);

        // Purple-blue flames burning along the ground, with embers.
        var flames = BackgroundParticles("Shadow Flames", t, new Vector2(0f, 0.1f), new Vector2(2.8f, 0.1f), hot, 15,
            110f, new Vector2(0.5f, 1.1f), new Vector2(2f, 4.5f), new Vector2(0.9f, 1.8f), ShadowFireGradient(), 220);
        flames.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var embers = BackgroundParticles("Shadow Embers", t, new Vector2(0f, 0.2f), new Vector2(2.4f, 0.2f), _neonAdd, 16,
            30f, new Vector2(1f, 2.2f), new Vector2(2f, 4.5f), new Vector2(0.07f, 0.14f),
            Grad((ShadowCore, 0f, 1f), (ShadowBlue, 0.3f, 1f), (ShadowMagenta, 1f, 0f)), 60);
        embers.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        OneShot("Impact Sparks", t, new Vector2(0f, 0.1f), _neonAdd, 17, 50, new Vector2(0.4f, 0.9f), new Vector2(4f, 9f), new Vector2(0.08f, 0.16f),
                Grad((ShadowCore, 0f, 1f), (ShadowBlue, 0.35f, 1f), (ShadowViolet, 1f, 0f)), 0.3f, 180f, 0.8f, 0.3f);

        LocalLight("Fire Light", t, new Vector2(0f, 0.9f), new Color(0.65f, 0.3f, 1f), 7f, 3f);
        LocalLight("Blue Light", t, new Vector2(0f, 0.4f), ShadowBlue, 4f, 1.5f);

        var vfx = root.AddComponent<VfxLifetime>();
        vfx.sustain = 1.6f;
        vfx.fade = 0.9f;
        vfx.flashes = new[] { shock, flash };
        vfx.flashDuration = 0.35f;
        vfx.flashGrow = 3.5f;
        return SavePrefab(root, "ShadowFireImpact");
    }

    static GameObject BuildOrbPrefab(GameObject impact)
    {
        var root = new GameObject("ShadowOrb");
        var t = root.transform;
        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0.9f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        root.AddComponent<CircleCollider2D>().radius = 0.28f;

        LocalGlow("Outer Glow", t, Vector2.zero, new Vector2(3.5f, 3.5f), new Color(ShadowViolet.r, ShadowViolet.g, ShadowViolet.b, 0.4f), 20, _neonSoft);
        LocalGlow("Blue Ring", t, Vector2.zero, new Vector2(1.7f, 1.7f), new Color(ShadowBlue.r, ShadowBlue.g, ShadowBlue.b, 0.75f), 21, _neonAdd);
        var core = new GameObject("Core").transform;
        core.SetParent(t, false);
        LocalGlow("Core Glow", core, Vector2.zero, new Vector2(0.95f, 0.95f), new Color(0.9f, 0.85f, 1f, 1f), 22, _neonAdd);
        Place("rune_6", Vector2.zero, core, 23, _neonAdd, new Color(0.7f, 0.5f, 1f, 0.9f), 0.7f).transform.localPosition = Vector3.zero;

        var trail = Particles("Blue Fire Trail", Vector2.zero, t, ShadowFireMaterial(), 19, 15f, new Vector2(0.3f, 0.55f), new Vector2(0f, 0.3f),
                              new Vector2(0.5f, 0.9f), ShadowFireGradient(), 0.15f, 0f, 0.6f, 150);
        var em = trail.emission;
        em.rateOverDistance = 30f;
        var sparks = Particles("Sparks", Vector2.zero, t, _neonAdd, 22, 25f, new Vector2(0.3f, 0.6f), new Vector2(0.5f, 1.5f),
                               new Vector2(0.05f, 0.1f), Grad((ShadowCore, 0f, 1f), (ShadowBlue, 0.4f, 1f), (ShadowMagenta, 1f, 0f)), 0.2f, 0f, 0.5f, 60);
        var ss = sparks.shape;
        ss.shapeType = ParticleSystemShapeType.Circle;
        ss.rotation = Vector3.zero;
        foreach (var ps in new[] { trail, sparks })
        {
            var m = ps.main;
            m.prewarm = false;
        }
        LocalLight("Orb Light", t, Vector2.zero, new Color(0.5f, 0.4f, 1f), 4.5f, 2.2f);

        var orb = root.AddComponent<ShadowOrb>();
        orb.impactPrefab = impact;
        orb.trail = trail;
        orb.core = core;
        return SavePrefab(root, "ShadowOrb");
    }

    static void BuildShadowAbility(GameObject player)
    {
        var burst = BuildSummonBurstPrefab();
        var impact = BuildImpactPrefab();
        var orb = BuildOrbPrefab(impact);

        var shadow = new GameObject("Shadow Spirit (press P)");
        shadow.transform.SetParent(player.transform, false);
        var sr = shadow.AddComponent<SpriteRenderer>();
        sr.sprite = _sprites["shadow_0"];
        sr.sharedMaterial = _neonTex;          // unlit: stays inky, the blue eyes bloom
        sr.sortingOrder = 11;                  // just behind the hero

        var aura = LocalGlow("Aura", shadow.transform, new Vector2(0f, 2.1f), new Vector2(5f, 6f), new Color(ShadowViolet.r, ShadowViolet.g, ShadowViolet.b, 0.35f), 10, _neonSoft);
        var light = LocalLight("Aura Light", shadow.transform, new Vector2(0f, 2.4f), ShadowViolet, 5f, 1.6f);
        var wisps = Particles("Shadow Wisps", Vector2.zero, shadow.transform, _smoke, 10, 10f, new Vector2(1f, 1.8f), new Vector2(0.3f, 0.8f),
                              new Vector2(0.4f, 0.9f), Grad((ShadowViolet, 0f, 0f), (new Color(0.25f, 0.15f, 0.5f), 0.3f, 0.5f), (new Color(0.1f, 0.05f, 0.2f), 1f, 0f)),
                              0.5f, -0.05f, 0.4f, 60);
        wisps.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var we = wisps.emission;
        we.enabled = false;

        var summon = shadow.AddComponent<ShadowSummon>();
        summon.player = player.GetComponent<PlayerController2D>();
        summon.playerRenderer = player.GetComponent<SpriteRenderer>();
        summon.frames = Enumerable.Range(0, 7).Select(i => _sprites[$"shadow_{i}"]).ToArray();
        summon.orbPrefab = orb;
        summon.summonBurstPrefab = burst;
        summon.aura = aura;
        summon.auraAlpha = aura.color.a;
        summon.auraLight = light;
        summon.auraLightIntensity = light.intensity;
        aura.color = new Color(aura.color.r, aura.color.g, aura.color.b, 0f);   // hidden until summoned
        light.intensity = 0f;
        summon.wisps = wisps;
        summon.healFrames = Enumerable.Range(0, 5).Where(i => _sprites.ContainsKey($"shadowheal_{i}")).Select(i => _sprites[$"shadowheal_{i}"]).ToArray();
        summon.healCirclePrefab = BuildHealCirclePrefab();
        summon.health = player.GetComponent<PlayerHealth>();
        summon.glow = player.GetComponent<PlayerGlow>();
    }

    static readonly Color HealCyan = new Color(0.35f, 0.95f, 1f);

    static Material PixelSquareMaterial()
    {
        var sq = Neon("M_PixelSquare", Color.white, 2.4f, 1f, true, 4f, 0.4f, 0.2f);
        sq.mainTexture = _sprites["white"].texture;
        return sq;
    }

    static GameObject BuildHealCirclePrefab()
    {
        var root = new GameObject("HealCircle");
        var t = root.transform;
        var sq = PixelSquareMaterial();

        // Ground circle: flattened parent + spinning child = a magic circle lying on the floor.
        var ground = new GameObject("Ground Circle").transform;
        ground.SetParent(t, false);
        ground.localPosition = new Vector3(0f, 0.08f, 0f);
        ground.localScale = new Vector3(1f, 0.36f, 1f);
        var g = Place("heal_circle", Vector2.zero, ground, 11, _neonAdd, new Color(HealCyan.r, HealCyan.g, HealCyan.b, 0.95f), 1.25f);
        g.transform.localPosition = Vector3.zero;
        g.gameObject.AddComponent<SpinPulse>().degreesPerSecond = 120f;

        // Upright circle behind the boy.
        var up = Place("heal_circle", Vector2.zero, t, 10, _neonAdd, new Color(HealCyan.r, HealCyan.g, HealCyan.b, 0.45f), 1.6f);
        up.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        var sp = up.gameObject.AddComponent<SpinPulse>();
        sp.degreesPerSecond = -45f;
        sp.growTime = 0.4f;

        var sparkle = Place("heal_sparkle", Vector2.zero, t, 14, _neonAdd, new Color(0.7f, 1f, 1f, 0.9f), 1.1f);
        sparkle.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var motes = Particles("Rising Motes", Vector2.zero, t, sq, 15, 40f, new Vector2(0.8f, 1.5f), new Vector2(1.2f, 3f),
                              new Vector2(0.07f, 0.14f), Grad((Color.white, 0f, 0f), (HealCyan, 0.2f, 1f), (new Color(0.3f, 0.5f, 1f), 1f, 0f)), 0.9f, -0.05f, 0.5f, 120);
        motes.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var m = motes.main;
        m.prewarm = false;
        LocalLight("Heal Light", t, new Vector2(0f, 1.2f), HealCyan, 5f, 2.2f);

        var vfx = root.AddComponent<VfxLifetime>();
        vfx.sustain = 1.1f;
        vfx.fade = 0.6f;
        vfx.flashes = new[] { sparkle };
        vfx.flashDuration = 0.6f;
        vfx.flashGrow = 1.4f;
        vfx.lightFlicker = 0.1f;
        return SavePrefab(root, "HealCircle");
    }

    // ------------------------------------------------------------------ VFX sheets (effects folder)

    const string VfxDir = Root + "/VFX";
    const int VfxPPU = 24;
    static readonly Dictionary<string, int> VfxFrameWidth = new Dictionary<string, int> { { "vfx_lightning", 64 } };

    /// <summary>Slices every strip in DarkForest/VFX into frames named sheet_0, sheet_1, ...</summary>
    static void ImportVfxSheets()
    {
        if (!AssetDatabase.IsValidFolder(VfxDir)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VfxDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = VfxPPU;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 8192;      // long strips (clock ring, freeze, nebula) were shrunk to 2048 and sliced into half-frames
            ti.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int fh = tex.height;
            int fw = VfxFrameWidth.TryGetValue(name, out var w) ? w : fh;
            bool ground = name == "vfx_lightning" || name == "vfx_dark_bolt" || name.StartsWith("en_rat") || name.StartsWith("en_gob") || name.StartsWith("en_mush") || name.StartsWith("en_skel") || name.StartsWith("en_exec") || name.StartsWith("en_beast");
            var rects = new List<SpriteRect>();
            for (int i = 0; i * fw + fw <= tex.width; i++)
                rects.Add(new SpriteRect
                {
                    name = $"{name}_{i}",
                    rect = new Rect(i * fw, 0, fw, fh),
                    alignment = ground ? SpriteAlignment.BottomCenter : SpriteAlignment.Center,
                    pivot = ground ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                });
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            dp.SetSpriteRects(rects.ToArray());
            dp.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            dp.Apply();
            ti.SaveAndReimport();
            foreach (var sp in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                _sprites[sp.name] = sp;
        }
    }

    static bool HasSheet(string sheet) => _sprites != null && _sprites.ContainsKey(sheet + "_0");

    static Sprite[] Sheet(string sheet)
    {
        var list = new List<Sprite>();
        for (int i = 0; _sprites.TryGetValue($"{sheet}_{i}", out var s); i++) list.Add(s);
        return list.ToArray();
    }

    /// <summary>One-shot effect prefab from a sheet: sprite on the root, optional light.</summary>
    static GameObject SheetFx(string prefabName, string sheet, float fps, float scale, Material mat, Color color, int order,
                              Vector2 offset, Color? light = null, float lightRadius = 0f, bool loop = false)
    {
        var root = new GameObject(prefabName);
        var sr = root.AddComponent<SpriteRenderer>();
        var frames = Sheet(sheet);
        sr.sprite = frames.Length > 0 ? frames[0] : null;
        sr.sharedMaterial = mat;
        sr.color = color;
        sr.sortingOrder = order;
        root.transform.localScale = new Vector3(scale, scale, 1f);
        var one = root.AddComponent<SpriteOneShot>();
        one.frames = frames;
        one.fps = fps;
        one.loop = loop;
        if (offset != Vector2.zero)
        {
            // Offset the art (not the root) so callers can spawn at the feet.
            var child = new GameObject("Art");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = offset / scale;
            var csr = child.AddComponent<SpriteRenderer>();
            csr.sprite = sr.sprite; csr.sharedMaterial = mat; csr.color = color; csr.sortingOrder = order;
            Object.DestroyImmediate(one);
            Object.DestroyImmediate(sr);
            var cone = child.AddComponent<SpriteOneShot>();
            cone.frames = frames; cone.fps = fps; cone.loop = loop;
            root.AddComponent<SelfDestruct>().seconds = frames.Length / fps + 0.5f;
        }
        if (light.HasValue)
        {
            var l = LocalLight("Light", root.transform, Vector2.zero, light.Value, lightRadius, 1.8f);
            l.transform.localPosition = Vector3.zero;
        }
        return SavePrefab(root, prefabName);
    }

    static void BuildHandLantern(GameObject player, SpriteRenderer boy, Rigidbody2D rb)
    {
        if (!_sprites.ContainsKey("lantern_12")) return;
        var lantern = new GameObject("Lantern (hand)");
        lantern.transform.SetParent(player.transform, false);
        var lsr = lantern.AddComponent<SpriteRenderer>();
        lsr.sprite = _sprites["lantern_12"];
        lsr.sharedMaterial = Neon("M_Lantern", Color.white, 1.25f, 0f, false, 0f, 0f);
        lsr.sortingOrder = 13;
        var anim = lantern.AddComponent<SpriteOneShot>();
        anim.frames = Enumerable.Range(12, 26).Select(i => _sprites[$"lantern_{i:00}"]).ToArray();   // lit, flickering loop
        anim.fps = 12f;
        anim.loop = true;

        // Halo + warm light at the glass (GDD: inner 0.25, outer 2.2, #FF9A3C).
        var halo = LocalGlow("Lantern Halo", lantern.transform, new Vector2(0f, -0.42f), new Vector2(2.4f, 2.4f), new Color(1f, 0.6f, 0.24f, 0.5f), 12, _neonSoft);
        var light = LocalLight("Lantern Light", lantern.transform, new Vector2(0f, -0.42f), new Color(1f, 0.6f, 0.24f), 6.5f, 2.3f);
        light.pointLightInnerRadius = 0.9f;
        light.gameObject.AddComponent<FlickerLight2D>().flickerAmount = 0.15f;
        // Wide, soft spill so the lantern pushes back the dark around the boy (the GDD's "circle of safety").
        var spill = LocalLight("Lantern Spill", lantern.transform, new Vector2(0f, -0.42f), new Color(1f, 0.66f, 0.36f), 10f, 0.45f);
        spill.falloffIntensity = 0.8f;

        // Hand positions recorded per boy frame by the sprite processing.
        var keys = new List<Sprite>();
        var anchors = new List<Vector3>();
        var json = Path.Combine(Application.dataPath, "DarkForest/Sprites/boy_lantern_anchors.json");
        if (File.Exists(json))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(json), @"""(boy_[a-z]+_\d+)"":\s*\[\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(true|false)\s*\]"))
            {
                if (!_sprites.TryGetValue(m.Groups[1].Value, out var sp)) continue;
                keys.Add(sp);
                const float k = AnchorPPU / (float)BoyPPU;   // anchors were recorded at 28 px per unit
                anchors.Add(new Vector3(k * float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                                        k * float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                                        m.Groups[4].Value == "true" ? 1f : 0f));
            }
        }
        var follow = lantern.AddComponent<LanternFollower>();
        follow.boy = boy;
        follow.body = rb;
        follow.frameKeys = keys.ToArray();
        follow.anchors = anchors.ToArray();
    }

    static GameObject BuildFireHitPrefab() =>
        HasSheet("vfx_hit_spark")
            ? SheetFx("FireHit", "vfx_hit_spark", 18f, 1.5f, _neonTex, Color.white, 18, Vector2.zero, new Color(1f, 0.55f, 0.2f), 3f)
            : null;

    static GameObject BuildBlueHitPrefab()
    {
        var root = new GameObject("BlueFlareHit");
        var sr = root.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = _neonAdd;
        sr.color = new Color(0.55f, 0.85f, 1f, 1f);
        sr.sortingOrder = 18;
        root.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
        var one = root.AddComponent<SpriteOneShot>();
        one.frames = Sheet("vfx_blue_spark");
        one.fps = 16f;
        one.linger = 0.6f;
        OneShot("Blue Sparks", root.transform, Vector2.zero, PixelSquareMaterial(), 19, 22, new Vector2(0.3f, 0.7f), new Vector2(2.5f, 6f),
                new Vector2(0.05f, 0.09f), Grad((Color.white, 0f, 1f), (new Color(0.4f, 0.8f, 1f), 0.4f, 1f), (new Color(0.4f, 0.3f, 1f), 1f, 0f)), 0.1f, 360f, 0.5f, 0.3f);
        LocalLight("Light", root.transform, Vector2.zero, new Color(0.4f, 0.75f, 1f), 2.2f, 2.2f).transform.localPosition = Vector3.zero;
        return SavePrefab(root, "BlueFlareHit");
    }

    /// <summary>Ember Shot (orange) or Charged Flare (lighter, blue neon fire) built from the effects pack.</summary>
    static GameObject BuildFireBoltPrefab(string name, bool charged, GameObject hit)
    {
        var root = new GameObject(name);
        var t = root.transform;
        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        root.AddComponent<CircleCollider2D>().radius = charged ? 0.32f : 0.2f;

        var art = new GameObject("Art");
        art.transform.SetParent(t, false);
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 16;
        var anim = art.AddComponent<SpriteOneShot>();
        anim.loop = true;
        anim.fps = 14f;
        if (charged)
        {
            sr.sharedMaterial = Neon("M_BlueNeonFire", new Color(0.55f, 0.85f, 1f), 1.7f, 0.55f, false, 9f, 0.12f);
            anim.frames = Sheet("vfx_charged_projectile");
            art.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            if (anim.frames.Length == 0) anim.frames = new[] { _sprites["glow"] };
        }
        else
        {
            sr.sharedMaterial = _neonTex;
            anim.frames = Sheet("vfx_ember_shot");
            art.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            if (anim.frames.Length == 0) anim.frames = new[] { _sprites["glow"] };
        }
        sr.sprite = anim.frames[0];

        Color c0 = charged ? new Color(0.85f, 0.95f, 1f) : FireCore;
        Color c1 = charged ? new Color(0.35f, 0.7f, 1f) : FireOrange;
        Color c2 = charged ? new Color(0.45f, 0.3f, 1f) : FireMagenta;
        var trail = Particles("Trail", Vector2.zero, t, PixelSquareMaterial(), 14, 18f, new Vector2(0.2f, 0.4f), new Vector2(0f, 0.4f),
                              charged ? new Vector2(0.09f, 0.16f) : new Vector2(0.07f, 0.12f),
                              Grad((c0, 0f, 0f), (c0, 0.1f, 1f), (c1, 0.5f, 0.9f), (c2, 1f, 0f)), charged ? 0.25f : 0.1f, 0f, 0.5f, 160);
        var em = trail.emission;
        em.rateOverDistance = charged ? 34f : 22f;
        var tm = trail.main;
        tm.prewarm = false;
        var glow = LocalGlow("Glow", t, Vector2.zero, charged ? new Vector2(2.4f, 2.4f) : new Vector2(1.6f, 1.6f),
                             charged ? new Color(0.4f, 0.75f, 1f, 0.45f) : new Color(1f, 0.5f, 0.15f, 0.5f), 15, _neonSoft);
        LocalLight("Bolt Light", t, Vector2.zero, charged ? new Color(0.4f, 0.75f, 1f) : new Color(1f, 0.55f, 0.2f), charged ? 4f : 3f, charged ? 2f : 1.5f);

        var bolt = root.AddComponent<FireBolt>();
        bolt.hitPrefab = charged ? BuildBlueHitPrefab() : hit;
        bolt.trail = trail;
        bolt.maxLifetime = charged ? 1.3f : 0.9f;
        bolt.damage = charged ? 2 : 1;
        return SavePrefab(root, name);
    }

    static Sprite[] Frames(string state, string prefix = "hero")
    {
        var list = new List<Sprite>();
        for (int i = 0; _sprites.TryGetValue($"{prefix}_{state}_{i}", out var s); i++)
            list.Add(s);
        return list.ToArray();
    }

    static void BuildCheckpoints(int[] h)
    {
        var root = Group("Checkpoints").transform;
        foreach (var x in new[] { 44f, 64f, 80f, 96f })
        {
            var go = Group($"Checkpoint x{x}", root);
            go.transform.position = new Vector3(x, H(h, Mathf.FloorToInt(x)) - WalkOffset + 0.1f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 6f);
            box.offset = new Vector2(0f, 3f);
            go.AddComponent<Checkpoint>();
        }
    }

    static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ------------------------------------------------------------------ preview capture

    /// <summary>Renders Main Camera to a 1920x1080 PNG (works in edit and play mode).</summary>
    [MenuItem("Tools/Dark Forest/Capture Preview (1080p)")]
    public static void CapturePreview() => Capture("preview_1080p");

    public static void Capture(string fileName)
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[DarkForest] No Main Camera in the open scene."); return; }

        if (!Application.isPlaying)
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                ps.Simulate(4f, true, true);

        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();

        var ldr = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(rt, ldr);
        var active = RenderTexture.active;
        RenderTexture.active = ldr;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        cam.targetTexture = prev;

        var dir = Root + "/Screenshots";
        Directory.CreateDirectory(dir);
        var file = $"{dir}/{fileName}.png";
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.ReleaseTemporary(ldr);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        if (!Application.isPlaying) AssetDatabase.ImportAsset(file);
        Debug.Log("[DarkForest] Preview saved: " + file);
    }
}
