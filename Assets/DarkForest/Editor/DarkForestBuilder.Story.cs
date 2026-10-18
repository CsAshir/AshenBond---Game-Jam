using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Story beats (GDD: Ilyan, his sister Mira, the clock frozen at 11:59, the dropped lantern) as in-engine
/// dialogue cutscenes - area openings, every fight, the bosses, memory flashes and exits - plus the pause menu
/// and the title-screen scene.
/// </summary>
public static partial class DarkForestBuilder
{
    // ------------------------------------------------------------------ lines ("Speaker|Text", empty speaker = narration)

    static readonly string[] AStart =
    {
        "|The village is burning. The great bell has stopped.",
        "Ilyan|Mira...? Mira! Where are you?",
        "|The lantern is warm in his hands. Something tall and dark rises out of his shadow.",
        "Ilyan|Stay away from me! ...Why won't it hurt me?",
    };
    static readonly string[] AAmbush =
    {
        "|Ember beasts crawl out of the ash of the square.",
        "Ilyan|There are so many... I can't go back.",
        "Mira|Light them up, little brother. I'll do the rest.",
        "Ilyan|That voice... no. It can't be.",
    };
    static readonly string[] AMemory =
    {
        "|A memory: Mira kneels, closing his small hands around the lantern.",
        "Mira|If the bell ever stops, carry the light to the clock tower. Promise me.",
        "Ilyan|...I promised.",
    };
    static readonly string[] AExit =
    {
        "Ilyan|The forest. She said the lantern always knows the way to the tower.",
        "|Behind him, the shadow lowers one long hand between the boy and the flames.",
    };

    static readonly string[] BStart =
    {
        "|The Dark Forest. The fire never reached it. Only the shadows did.",
        "Ilyan|You followed me. You always follow me.",
        "Mira|Someone has to keep you out of trouble.",
        "Ilyan|...Mira? Is it really you?",
        "|The shadow does not answer. The fireflies drift closer.",
    };
    static readonly string[] BWalk =
    {
        "|Hollow shapes rise out of the black earth.",
        "Ilyan|They look like they're made of my own shadow.",
        "Mira|Fire shows them. I finish them. Together, Ilyan.",
    };
    static readonly string[] BMemory =
    {
        "|Another memory: dry cloth in the storeroom. The lantern slipping from his fingers.",
        "|The flame spreading faster than he could scream.",
        "Ilyan|I dropped it. It was me. It was always me.",
        "Mira|I know. I came back for you anyway.",
    };
    static readonly string[] BBoss =
    {
        "|In the clearing something old is waiting. A blade made for endings.",
        "The Executioner|You dropped the lantern, boy. You remember. You always remember.",
        "Ilyan|...It was an accident.",
        "The Executioner|Tell that to the fire.",
        "Mira|Don't listen to it. Stay behind me, Ilyan. Not this time.",
    };
    static readonly string[] BExit =
    {
        "Ilyan|The tower. The clock stopped at one minute to midnight... the minute I dropped it.",
        "Mira|Then let's go and see what is waiting at the top.",
    };

    static readonly string[] CStart =
    {
        "|The Clock Tower. Every gear inside is frozen at 11:59.",
        "Ilyan|If I turn the clock back... maybe none of it ever happened.",
        "Mira|Time doesn't forgive, Ilyan. But it remembers.",
        "|The tower lends him a little of its power. Hold Q, and the last moments run backwards.",
    };
    static readonly string[] CBoss =
    {
        "|At the top of the tower, the clockwork opens one great eye.",
        "Ilyan|It's watching me. It has been watching all night.",
        "The Clockwork Eye|Every second you replay. Every second you regret.",
        "Mira|Then we will give it something new to watch.",
        "|Far below, something heavy and horned begins to climb.",
    };
    // The end of the game. Lines 2 to 5 reuse recorded voices (B-11, B-12, A-11, C-03); the narration is new.
    static readonly string[] CExit =
    {
        "|The great bell rings one last time. Far below, the fires fade to embers.",
        "Ilyan|I dropped it. It was me. It was always me.",
        "Mira|I know. I came back for you anyway.",
        "Ilyan|...I promised.",
        "Mira|Time doesn't forgive, Ilyan. But it remembers.",
        "|The clock hand moves past midnight. Mira fades into the first light of morning.",
    };

    static readonly string[] ATutFight =
    {
        "|An ember beast. Shoot it with the lantern (J). Hold J to charge a stronger flare.",
        "|A burning enemy glows cyan for a moment. Press P and Mira's shadow strike finishes it.",
    };
    static readonly string[] ATutTree =
    {
        "|The path is broken. The burning gap is far too wide to jump.",
        "|Shoot the cracked tree three times (J). It will fall across the gap and become your path.",
    };
    static readonly string[] BTutTree =
    {
        "|The black water is too wide, even for a double jump.",
        "|Shoot the cracked dead tree three times (J). It will fall and become your bridge.",
    };
    static readonly string[] CTutCrusher =
    {
        "|The tower's crushers slam on the beat of the clock.",
        "|Wait for the red glow to fade as it rises, then run under it. Caught? Hold Q to rewind.",
    };

    /// <summary>Line IDs in the order of Docs/Dialogue_Script.md (the recorded files are named by them).</summary>
    static (string id, string[] lines)[] VoiceTable => new[]
    {
        ("A-01", AStart), ("A-05", ATutFight), ("A-07", ATutTree), ("A-09", AMemory), ("A-12", AAmbush), ("A-16", AExit),
        ("B-01", BStart), ("B-06", BWalk), ("B-09", BMemory), ("B-13", BTutTree), ("B-15", BBoss), ("B-20", BExit),
        ("C-01", CStart), ("C-05", CTutCrusher), ("C-07", CBoss),   // CExit reuses recorded lines (matched by text)
    };

    /// <summary>Every recorded line (Assets/ALL Dialogues/Ashen_Bond_Voice, named "..._A-02_ilyan.mp3") matched to its subtitle.</summary>
    static void BuildVoiceBank()
    {
        var files = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/ALL Dialogues" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var texts = new System.Collections.Generic.List<string>();
        var clips = new System.Collections.Generic.List<AudioClip>();
        foreach (var (first, lines) in VoiceTable)
        {
            char area = first[0];
            int n = int.Parse(first.Substring(2));
            for (int i = 0; i < lines.Length; i++)
            {
                string id = $"{area}-{n + i:00}";
                var path = files.FirstOrDefault(f => System.IO.Path.GetFileName(f).Contains("_" + id + "_"));
                if (path == null) continue;
                int bar = lines[i].IndexOf('|');
                texts.Add(bar < 0 ? lines[i] : lines[i][(bar + 1)..]);
                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
        }
        var bank = new GameObject("Voice Bank").AddComponent<VoiceBank>();
        bank.texts = texts.ToArray();
        bank.clips = clips.ToArray();
    }

    /// <summary>Pixel UI kit (Resources/UI): crisp point filtering, no compression.</summary>
    static void ImportUiKit()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Resources/UI" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed && !ti.mipmapEnabled) continue;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
        }
    }

    /// <summary>Build order: title, opening film, the three areas, closing film (old prototype scenes disabled).</summary>
    static void SortBuildScenes()
    {
        string[] order = { MenuScenePath, $"{SceneDir}/Cinematic_Intro.unity", AreaScenePath, BScenePath, CScenePath, $"{SceneDir}/Cinematic_Ending.unity" };
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var p in order)
            if (System.IO.File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
        foreach (var sc in EditorBuildSettings.scenes)
            if (!order.Contains(sc.path)) list.Add(new EditorBuildSettingsScene(sc.path, false));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ------------------------------------------------------------------ cutscenes (video) and the ending

    [MenuItem("Tools/Ashen Bond/Build Cutscenes")]
    public static void BuildCinematics()
    {
        foreach (var (scene, video, next, title, sub, credits) in new[]
        {
            ("Cinematic_Intro", "Assets/Cutscenes/final first cutscene-upscaled-4x.mp4", "AreaA_BurningVillage", "Area A", "The Burning Village", false),
            ("Cinematic_Ending", "Assets/Cutscenes/Scene 02.mp4", "MainMenu", "Ashen Bond", "", true),
        })
        {
            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.gameObject.AddComponent<AudioListener>();
            var cp = new GameObject("Cinematic").AddComponent<CinematicPlayer>();
            cp.clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(video);
            cp.nextScene = next;
            cp.nextTitle = title;
            cp.nextSubtitle = sub;
            cp.rollCredits = credits;
            if (scene == "Cinematic_Intro")
            {
                // Ilyan's own breaths and cries laid on the film's beats: he wakes in the smoke and coughs, the roof
                // caves in, he sees Mira on the floor, the spirit flows into the lantern, she rises, they run.
                var cues = new (float t, int sfx)[]
                {
                    (6.4f, 11), (9.0f, 3), (10.8f, 6), (11.9f, 4), (13.6f, 7), (16.6f, 14), (24.2f, 11), (33.2f, 8), (38.1f, 2),
                };
                _ = cues;                                   // (overlay removed: the film plays with its own sound only)
                cp.cueTimes = new float[0];
                cp.cueClips = new AudioClip[0];
                cp.cueVolume = 0.9f;
            }
            if (!cp.clip) Debug.LogWarning("[Cutscene] Video not found: " + video);
            var path = $"{SceneDir}/{scene}.unity";
            EditorSceneManager.SaveScene(sc, path);
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => s.path == path);
            int mi = list.FindIndex(s => s.path == MenuScenePath);
            list.Insert(scene == "Cinematic_Intro" ? mi + 1 : list.Count, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
        SortBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[Cutscene] Scenes built.");
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Pause menu + the opening dialogue of an area.</summary>
    static void BuildStoryUI(string[] opening)
    {
        ImportUiKit();
        BuildVoiceBank();
        new GameObject("Pause Menu (Esc)").AddComponent<PauseMenu>();
        DialogueZone("Opening", PlayerSpawn.x + 5f, opening);       // a few steps in: he is already walking when it plays
    }

    /// <summary>A dialogue beat that plays once when the boy walks past x.</summary>
    static void DialogueZone(string name, float x, string[] lines)
    {
        var go = new GameObject($"Dialogue: {name} x{x}");
        go.transform.position = new Vector3(x, ASurf(x), 0f);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1f, 14f);
        box.offset = new Vector2(0f, 5f);
        go.AddComponent<DialogueTrigger>().lines = lines;
    }

    static void ExitLines(string[] lines)
    {
        var exit = GameObject.Find("Exit Trigger");
        if (exit && exit.TryGetComponent<LevelExit>(out var le)) le.outroLines = lines;
    }

    // ------------------------------------------------------------------ title screen

    const string MenuScenePath = "Assets/DarkForest/Scenes/MainMenu.unity";

    [MenuItem("Tools/Ashen Bond/Build Main Menu")]
    public static void BuildMainMenu()
    {
        var active = SceneManager.GetActiveScene();
        if (active.isDirty && active.path != MenuScenePath && active.path != AreaScenePath && active.path != BScenePath && active.path != CScenePath)
        {
            Debug.LogWarning("[Menu] Active scene has unsaved changes. Save it first, then build again.");
            return;
        }
        _rng = new System.Random(7);
        _ground = AreaGround;
        _platforms = AreaPlatforms;
        MapWidth = 156;
        PlayerSpawn = new Vector2(3.5f, 5.2f);
        EnsureFolders();
        if (!AssetDatabase.IsValidFolder(AreaTileDir)) AssetDatabase.CreateFolder(TileDir, "AreaA");
        _ground = CGround;
        _platforms = CPlatforms;
        MapWidth = 173;
        ImportSprites();
        ImportBackgrounds();
        ImportAreaSprites();
        ImportAreaBSprites();
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
        if (global && global.lightType == Light2D.LightType.Global) { global.intensity = 0.62f; global.color = new Color(0.55f, 0.55f, 0.85f); }
        BuildAreaCSky();
        BuildAreaCParallax();                     // the frozen clock tower drifting behind the menu
        // A fringe of moonlit forest in front of the tower: swaying pines, god rays, green spores.
        var fringe = Group("Forest Fringe (parallax -0.1)");
        var treeWind = FoliageMat("M_FoliageTree", 1.2f, 1.45f, 0.09f, false);
        string[] pines = { "pine_l_a", "pine_xl_b", "dead_a", "pine_m_b", "pine_l_b" };
        for (float x = cam.transform.position.x - 30f; x < cam.transform.position.x + 90f; x += R(4f, 8f))
        {
            var t = Place(pines[_rng.Next(pines.Length)], new Vector2(x, cam.transform.position.y - CamSize - 1f), fringe.transform, -30, treeWind,
                          new Color(0.05f, 0.08f, 0.09f), R(0.9f, 1.3f), _rng.Next(2) == 0);
            t.name = "Pine";
        }
        Parallax(fringe, -0.1f, 0f);
        foreach (var dx in new[] { -6f, 8f, 22f, 40f })
            GodRay(fringe.transform, new Vector2(cam.transform.position.x + dx, cam.transform.position.y - CamSize), 28f, R(3f, 5f), R(14f, 20f),
                   new Color(0.72f, 0.93f, 0.84f), 0.16f, -29, 0f, true, false);
        var fx = Group("Atmosphere").transform;
        ForestSpores(fx, 4);

        // Ilyan and Mira on the right third of the screen, riding with the camera.
        var stage = new GameObject("Menu Stage").transform;
        stage.SetParent(cam.transform, false);
        stage.localPosition = new Vector3(CamSize * 16f / 9f * 0.42f, -CamSize * 0.62f, 10f);
        var boy = Place("boy_idle_0", Vector2.zero, stage, 12, Neon("M_PlayerSprite", Color.white, 1.3f, 0f, false, 0f, 0f), Color.white, 1.6f, true);
        boy.transform.localPosition = Vector3.zero;
        var idle = Enumerable.Range(0, 64).Select(i => _sprites.TryGetValue($"boy_idle_{i}", out var sp) ? sp : null).Where(sp => sp).ToArray();
        var ba = boy.gameObject.AddComponent<SpriteFrameAnimator>();
        ba.frames = idle;
        ba.framesPerSecond = 7f;
        LocalLight("Lantern Glow", stage, new Vector2(-1.4f, 2f), new Color(1f, 0.6f, 0.25f), 9f, 2.4f).gameObject.AddComponent<FlickerLight2D>();
        LocalGlow("Lantern Halo", stage, new Vector2(-1.4f, 2f), new Vector2(5f, 5f), new Color(1f, 0.55f, 0.2f, 0.4f), 13, _neonSoft);
        var mira = Place("shadow_3", Vector2.zero, stage, 10, _neonTex, new Color(0.35f, 0.4f, 0.75f, 0.85f), 1.5f, false);
        mira.transform.localPosition = new Vector3(2.2f, 0.2f, 0f);
        LocalGlow("Mira Aura", mira.transform, new Vector2(0f, 2.5f), new Vector2(5f, 7f), new Color(0.45f, 0.4f, 1f, 0.3f), 9, _neonSoft);
        var embers = Particles("Menu Embers", Vector2.zero, stage, _emberSquare, 30, 14f, new Vector2(3f, 6f), new Vector2(0.5f, 1.6f),
                               new Vector2(0.06f, 0.12f), Grad((FireCore, 0f, 0f), (FireOrange, 0.2f, 1f), (FireMagenta, 1f, 0f)), 6f, -0.05f, 0.6f, 160);
        embers.transform.localPosition = new Vector3(-6f, -1f, 0f);
        var menuObj = new GameObject("Main Menu");
        var md = menuObj.AddComponent<MenuBackdrop>();
        md.cameraTransform = cam.transform;
        md.mira = mira.transform;
        md.miraRenderer = mira;
        menuObj.AddComponent<MainMenu>();
        ImportUiKit();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(sc => sc.path == MenuScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        SortBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[Menu] Scene built: " + MenuScenePath);
    }
}
