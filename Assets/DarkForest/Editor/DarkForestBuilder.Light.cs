using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Light & wind pass shared by Area B and C: god-rays falling through gaps (with a real 2D light so the ground
/// and the boy brighten inside the beam), a swaying canopy overhead, falling leaves, trees that bend at the top.
/// Area C additions: tick crushers, a rolling saw-gear, sand that bounces, and the 3-charge rewind.
/// </summary>
public static partial class DarkForestBuilder
{
    static Material FoliageMat(string name, float sway, float speed, float flutter, bool hang) =>
        SaveMaterial(name, Shader.Find("DarkForest/FoliageWind"), m =>
        {
            m.SetFloat("_Sway", sway);
            m.SetFloat("_Speed", speed);
            m.SetFloat("_Flutter", flutter);
            m.SetFloat("_Hang", hang ? 1f : 0f);
        });

    /// <summary>
    /// Under every ground run a dark earth body down out of view (no empty space or level "backstage" under the
    /// walkway), and a kill zone in every pit so the boy can never get stranded down there.
    /// </summary>
    static void BuildGroundDepth(Color earth)
    {
        var root = Group("Ground Depth & Pit Kill Zones").transform;
        foreach (var g in _ground)
        {
            float w = g.x1 - g.x0 + 1;
            if (g.h > 0)
            {
                var body = Place("white", new Vector2(g.x0 + w / 2f, g.h - SlabRows - 18f), root, -1, _unlit, earth, 1f);
                body.name = $"Earth x{g.x0}-{g.x1}";
                body.transform.localScale = new Vector3(w + 0.02f, 36f, 1f);
                var fade = Place("glow", new Vector2(g.x0 + w / 2f, g.h - SlabRows - 0.2f), root, -1, _unlit, new Color(earth.r * 2.2f, earth.g * 2.2f, earth.b * 2.2f, 0.6f), 1f);
                fade.name = "Earth Edge";
                fade.transform.localScale = new Vector3(w / 4f * 1.2f, 0.5f, 1f);
                continue;
            }
            int left = _ground.Where(o => o.x1 == g.x0 - 1).Select(o => o.h).DefaultIfEmpty(0).First();
            int right = _ground.Where(o => o.x0 == g.x1 + 1).Select(o => o.h).DefaultIfEmpty(0).First();
            float top = Mathf.Min(left > 0 ? left : 99, right > 0 ? right : 99) - 1.4f;
            if (top > 90f) continue;
            var pit = new GameObject($"Pit Kill Zone x{g.x0}-{g.x1}");
            pit.transform.SetParent(root, false);
            var box = pit.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.offset = new Vector2(g.x0 + w / 2f, (top - 40f) / 2f);
            box.size = new Vector2(w, top + 40f);
            pit.AddComponent<KillZone>();
        }
    }

    /// <summary>A tutorial beat: the game pauses, the camera swings to the target and a line explains what to do.</summary>
    static void TutorialAt(string name, float x, Transform focus, string[] lines)
    {
        var go = new GameObject($"Tutorial: {name} x{x}");
        go.transform.position = new Vector3(x, ASurf(x), 0f);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1f, 14f);
        box.offset = new Vector2(0f, 5f);
        var t = go.AddComponent<DialogueTrigger>();
        t.lines = lines;
        t.focus = focus;
    }

    static AudioClip Clip(string area, string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(area == "ALL" ? $"{Root}/ALL SFx/{file}" : $"{Root}/{area}/SFX/{file}");

    static AudioClip[] Voices(params int[] boySfx) => boySfx
        .Select(n => AssetDatabase.FindAssets($"boy-sfx-{n}", new[] { "Assets/ALL Dialogues" }).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p).EndsWith($"boy-sfx-{n}")))
        .Where(p => p != null).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();

    static GameAudio.Cue Cue(string id, AudioClip clip, float vol = 0.8f, float pitchLo = 0.94f, float pitchHi = 1.06f, float range = 22f, float cooldown = 0.04f) =>
        new GameAudio.Cue { id = id, clips = clip ? new[] { clip } : new AudioClip[0], volume = vol, pitch = new Vector2(pitchLo, pitchHi), range = range, cooldown = cooldown };

    /// <summary>The area's sound: shared combat/footstep cues + its own ambience.</summary>
    static void BuildAudio(char area)
    {
        var ga = new GameObject("Game Audio").AddComponent<GameAudio>();
        var walk = area == 'C' ? Clip("AreaC", "Walking Concrete.mp3") : Clip("AreaB", "Walking-dirt.mp3");
        var run = area == 'C' ? Clip("AreaC", "Walking Concrete.mp3") : Clip("AreaB", "RUN.mp3");
        var crack = area == 'B' ? Clip("AreaB", "Wood snaping-environment.mp3") : Clip("AreaA", "Wooden beam Cracking-environmenr.mp3");
        var cues = new System.Collections.Generic.List<GameAudio.Cue>
        {
            Cue("ember_shot", Clip("AreaA", "EmberShot-sfx.mp3"), 0.55f, 0.95f, 1.08f, 0f, 0.05f),
            Cue("flare_charged", Clip("AreaA", "Charged Flare.mp3"), 0.6f, 1f, 1f, 0f),
            Cue("flare_release", Clip("AreaA", "Flare Release.mp3"), 0.75f, 0.97f, 1.03f, 0f),
            Cue("fire_hit", Clip("AreaA", "FireHit-Impact sounds.mp3"), 0.7f, 0.9f, 1.1f, 24f, 0.05f),
            Cue("shadow_burst", Clip("AreaA", "FireHit-Impact sounds.mp3"), 0.9f, 0.55f, 0.62f, 26f),
            Cue("dash", Clip("ALL", "Player-Dash.mp3"), 0.7f, 0.95f, 1.05f, 0f, 0.1f),
            Cue("jump", Clip("ALL", "Jump-sfx.mp3"), 0.55f, 0.95f, 1.08f, 0f, 0.05f),
            Cue("land", Clip("ALL", "Player Landing-sfx.mp3"), 0.6f, 0.95f, 1.05f, 0f, 0.1f),
            Cue("ember", Clip("ALL", "Ember Pickup- sounch.mp3"), 0.7f),
            Cue("soul", Clip("ALL", "Ember Pickup- sounch.mp3"), 0.55f, 1.3f, 1.45f, 0f, 0.05f),
            Cue("checkpoint", Clip("ALL", "Checkpoint Ignite sound.mp3"), 0.85f, 1f, 1f, 0f),
            Cue("heal", Clip("ALL", "Sister Heal-Player.mp3"), 0.8f, 1f, 1f, 0f),
            Cue("orb_throw", Clip("ALL", "Sister-Orb Throw.mp3"), 0.8f),
            Cue("gasp", Clip("ALL", "Player-Gasp-SFX.mp3"), 0.85f, 0.97f, 1.03f, 0f, 0.3f),
            Cue("breathing", Clip("ALL", "Player-Breathing-sfx.mp3"), 0.5f, 1f, 1f, 0f),
            Cue("level_change", Clip("ALL", "Level Change-sfx.mp3"), 0.85f, 1f, 1f, 0f),
            Cue("step_walk", walk, 0.45f, 1f, 1f, 0f),
            Cue("step_run", run, 0.5f, area == 'C' ? 1.3f : 1f, area == 'C' ? 1.3f : 1f, 0f),
            Cue("wood_crack", crack, 0.8f),
            Cue("tree_fall", Clip("AreaA", "Falling trees-environment.mp3"), 0.9f, 0.95f, 1.05f, 30f),
            Cue("wood_fall", Clip("AreaA", "Falling wood-environment.mp3"), 0.85f),
            Cue("fire_jet", Clip("AreaA", "Fire Jet thrower-Environment.mp3"), 0.6f, 0.95f, 1.05f, 18f, 0.3f),
            Cue("rewind", Clip("AreaC", "Rewind sfx.mp3"), 0.9f, 1f, 1f, 0f),
        };
        if (area == 'C')
        {
            cues.Add(Cue("bell", Clip("AreaC", "Bell-Environment.mp3"), 1f, 1f, 1f, 0f));
            cues.Add(Cue("crusher", Clip("AreaC", "Falling-bg-environmentFinal.mp3"), 0.85f, 0.9f, 1f, 22f, 0.2f));
            cues.Add(Cue("collapse", Clip("AreaC", "Falling-bg-environmentFinal.mp3"), 0.6f, 1.05f, 1.2f, 26f, 0.15f));
            cues.Add(Cue("steam", Clip("AreaC", "Steam-environment.mp3"), 0.45f, 0.95f, 1.1f, 16f, 0.5f));
            cues.Add(Cue("chains", Clip("AreaC", "RattlingChains-Environment.mp3"), 0.5f, 0.95f, 1.05f, 18f, 0.6f));
            cues.Add(Cue("gear", Clip("AreaC", "GearRotating-Environment.mp3"), 0.55f, 1f, 1f, 16f));
            ga.ambienceBed = Clip("AreaC", "ClockTicking-Environment.mp3");
            ga.ambienceVolume = 0.3f;
            ga.ambienceVolume = 0.22f;
            ga.randomAmbient = new[]
            {
                new GameAudio.Ambient { clip = Clip("AreaC", "RattlingChains-Environment.mp3"), volume = 0.25f, interval = new Vector2(14f, 26f) },
                new GameAudio.Ambient { clip = Clip("AreaC", "Steam-environment.mp3"), volume = 0.22f, interval = new Vector2(10f, 22f) },
                new GameAudio.Ambient { clip = Clip("AreaC", "Obstacles-bg-environment2 Final.mp3"), volume = 0.2f, interval = new Vector2(18f, 34f) },
            };
        }
        else if (area == 'B')
        {
            ga.randomAmbient = new[]
            {
                new GameAudio.Ambient { clip = Clip("AreaB", "Wolf howl-environment.mp3"), volume = 0.35f, interval = new Vector2(18f, 35f) },
                new GameAudio.Ambient { clip = Clip("AreaB", "Wood snaping-environment.mp3"), volume = 0.15f, interval = new Vector2(9f, 20f) },
            };
        }
        else
        {
            ga.randomAmbient = new[]
            {
                new GameAudio.Ambient { clip = Clip("AreaA", "Background fireburning sound.mp3"), volume = 0.4f, interval = new Vector2(0.6f, 1.6f) },   // crackles
                new GameAudio.Ambient { clip = Clip("AreaA", "Wooden beam Cracking-environmenr.mp3"), volume = 0.18f, interval = new Vector2(10f, 22f) },
                new GameAudio.Ambient { clip = Clip("AreaA", "Falling wood-environment.mp3"), volume = 0.15f, interval = new Vector2(14f, 30f) },
            };
        }
        // Ilyan's own voice: little grunts on jumps, cries when hit, breaths on charged shots.
        cues.Add(new GameAudio.Cue { id = "grunt_jump", clips = Voices(1, 12, 9), volume = 0.45f, pitch = new Vector2(0.97f, 1.04f), range = 0f, cooldown = 0.8f });
        cues.Add(new GameAudio.Cue { id = "grunt_attack", clips = Voices(10, 2), volume = 0.5f, pitch = new Vector2(0.97f, 1.04f), range = 0f, cooldown = 0.6f });
        cues.Add(new GameAudio.Cue { id = "hurt", clips = Voices(4, 5, 6, 15), volume = 0.7f, pitch = new Vector2(0.97f, 1.03f), range = 0f, cooldown = 0.4f });
        ga.proceduralWind = true;
        ga.musicKey = area == 'A' ? -7 : area == 'B' ? 0 : 3;   // D minor village, A minor forest, C minor tower
        ga.windVolume = area == 'B' ? 0.4f : area == 'A' ? 0.12f : 0.1f;
        ga.cues = cues.ToArray();
    }

    /// <summary>
    /// Dark ground continuing below a painted parallax layer: when the camera rises (high jumps) the layer's
    /// bottom edge never opens onto empty space.
    /// </summary>
    static void LayerBase(Transform layer, Sprite sprite, int order, Color tint)
    {
        float w = sprite.bounds.size.x * 6f, h = 40f;
        var baseSr = Place("white", Vector2.zero, layer, order, _unlit, new Color(tint.r * 0.08f, tint.g * 0.07f, tint.b * 0.12f, 1f), 1f);
        baseSr.name = "Layer Base";
        baseSr.transform.localPosition = new Vector3(0f, -sprite.bounds.size.y / 2f - h / 2f + 0.6f, 0f);
        baseSr.transform.localScale = new Vector3(w, h, 1f);
    }

    static Material GodRayMat() => Neon("M_GodRay", Color.white, 1f, 1f, true, 0f, 0f);

    /// <summary>A slanted beam of light whose foot rests on the ground (angle > 0: light from the upper left).</summary>
    static LightShaft GodRay(Transform parent, Vector2 foot, float height, float width, float angle, Color color, float alpha,
                             int order, float lightIntensity, bool dust = true, bool pool = true)
    {
        var sp = _sprites["light_shaft"];
        float sw = sp.bounds.size.x, sh = sp.bounds.size.y;
        var mat = GodRayMat();
        var go = new GameObject("God Ray");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(foot.x, foot.y, parent ? parent.position.z : 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        var beams = new List<SpriteRenderer>();

        var soft = Place("light_shaft", foot, go.transform, order, mat, new Color(color.r, color.g, color.b, alpha * 0.55f));
        soft.name = "Ray (soft)";
        soft.transform.localPosition = Vector3.zero;
        soft.transform.localRotation = Quaternion.identity;
        soft.transform.localScale = new Vector3(width / sw, height / sh, 1f);
        beams.Add(soft);
        var core = Place("light_shaft", foot, go.transform, order + 1, mat, new Color(color.r, color.g, color.b, alpha));
        core.name = "Ray (core)";
        core.transform.localPosition = new Vector3(width * 0.08f, 0f, 0f);
        core.transform.localRotation = Quaternion.identity;
        core.transform.localScale = new Vector3(width * 0.42f / sw, height * 0.96f / sh, 1f);
        beams.Add(core);

        Light2D light = null;
        if (lightIntensity > 0f)
        {
            var lg = new GameObject("Ray Light");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localScale = new Vector3(width * 1.1f / sw, height / sh, 1f);
            light = lg.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Sprite;
            light.lightCookieSprite = sp;
            light.color = color;
            light.intensity = lightIntensity;
        }
        if (pool)
        {
            var p = Place("glow", foot + new Vector2(0f, 0.2f), parent, -13, mat, new Color(color.r, color.g, color.b, Mathf.Min(1f, alpha * 2.2f)));
            p.name = "Ray Pool";
            p.transform.localScale = new Vector3(width * 1.5f / 4f, 0.7f / 4f, 1f);
            beams.Add(p);
        }
        if (dust)
        {
            var motes = Particles("Ray Motes", foot, go.transform, PixelSquareMaterial(), order + 2, 2.4f, new Vector2(5f, 8f), Vector2.zero,
                                  new Vector2(0.04f, 0.07f), Grad((color, 0f, 0f), (color, 0.3f, 0.75f), (color, 1f, 0f)), 0f, 0f, 0.12f, 30);
            motes.transform.localPosition = new Vector3(0f, height * 0.3f, 0f);
            motes.transform.localRotation = Quaternion.identity;
            var mm = motes.main; mm.simulationSpace = ParticleSystemSimulationSpace.Local;
            var ms = motes.shape; ms.shapeType = ParticleSystemShapeType.Box; ms.rotation = Vector3.zero; ms.scale = new Vector3(width * 0.55f, height * 0.5f, 0.1f);
            var mv = motes.velocityOverLifetime; mv.enabled = true; mv.space = ParticleSystemSimulationSpace.Local;
            mv.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f); mv.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.05f); mv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var msol = motes.sizeOverLifetime; msol.enabled = false;
        }
        var shaft = go.AddComponent<LightShaft>();
        shaft.beams = beams.ToArray();
        shaft.groundLight = light;
        return shaft;
    }

    /// <summary>Rays behind a painted parallax layer (repeat with the layer's tiles, so the wrap stays seamless).</summary>
    static void LayerRays(string layerPrefix, string sprite, Color color, float[] fractions, float footFromCenter, Vector2 alpha, int order, float angle)
    {
        var layer = GameObject.Find("Parallax Backgrounds")?.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith(layerPrefix));
        if (!layer || !_sprites.TryGetValue(sprite, out var sp)) return;
        float bw = sp.bounds.size.x;
        for (int k = -2; k <= 2; k++)
            foreach (var f in fractions)
            {
                var ray = GodRay(layer, Vector2.zero, sp.bounds.size.y * 1.2f, R(2f, 9f), (angle + R(-6f, 6f)) * (_rng.Next(3) == 0 ? -1f : 1f), color, R(alpha.x, alpha.y), order, 0f, false, false);
                ray.transform.localPosition = new Vector3((k + f - 0.5f) * bw, sp.bounds.size.y * footFromCenter, 0f);
                ray.breatheAmount = 0.55f;
            }
    }

    /// <summary>Leaves hanging into the top of the frame, swinging in the wind (stays near the top as the camera climbs).</summary>
    static void Canopy(Color tint, float density, IEnumerable<(float a, float b)> clear)
    {
        var mat = FoliageMat("M_FoliageCanopy", 0.55f, 1.4f, 0.12f, true);
        Vector2 c = _cam.position;
        var group = Group("Canopy (parallax -0.08)");
        group.transform.position = new Vector3(c.x, c.y, -3f);
        float h = _sprites["canopy_leaves"].bounds.size.y;
        for (float x = -16f; x < MapWidth + 16f; x += R(5f, 9f))
        {
            if ((float)_rng.NextDouble() > density || clear.Any(r => x > r.a && x < r.b)) continue;
            float s = R(0.9f, 1.5f);
            var leaf = Place("canopy_leaves", new Vector2(x, c.y + CamSize + R(0.3f, 1.4f) - h * s), group.transform, 31, mat, tint, s, _rng.Next(2) == 0);
            leaf.name = "Canopy";
        }
        Parallax(group, -0.08f, 0.9f);
    }

    static void FallingLeaves(Transform parent, Color color, float topY, float rate)
    {
        var mat = Neon("M_Leaf", Color.white, 1f, 0f, false, 0f, 0f);
        mat.mainTexture = _sprites["leaf_bit"].texture;
        var leaves = Particles("Falling Leaves", new Vector2(MapWidth / 2f, topY), parent, mat, 33, rate, new Vector2(9f, 14f), Vector2.zero,
                               new Vector2(0.16f, 0.26f), Grad((color, 0f, 0f), (color, 0.08f, 0.95f), (color, 0.85f, 0.85f), (color, 1f, 0f)), 0f, 0.035f, 0.9f, 160);
        var sh = leaves.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.rotation = Vector3.zero; sh.scale = new Vector3(MapWidth + 20f, 2f, 0.1f);
        var v = leaves.velocityOverLifetime; v.enabled = true;
        v.x = new ParticleSystem.MinMaxCurve(0.3f, 1.3f); v.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.25f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var rot = leaves.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
        var main = leaves.main; main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var sol = leaves.sizeOverLifetime; sol.enabled = false;
        var n = leaves.noise; n.frequency = 0.5f;

        // Gusts: every few seconds a flurry of leaves tears across the view, fast and tumbling.
        var gust = Particles("Leaf Gusts", new Vector2(MapWidth / 2f, topY - 8f), parent, mat, 34, 0f, new Vector2(3f, 5f), Vector2.zero,
                             new Vector2(0.18f, 0.3f), Grad((color, 0f, 0f), (color, 0.1f, 1f), (color, 0.8f, 0.9f), (color, 1f, 0f)), 0f, 0.15f, 1.6f, 400);
        var gs = gust.shape; gs.shapeType = ParticleSystemShapeType.Box; gs.rotation = Vector3.zero; gs.scale = new Vector3(MapWidth + 20f, 14f, 0.1f);
        var ge = gust.emission;
        ge.SetBursts(new[] { new ParticleSystem.Burst(0f, 70, 70, 0, 4.5f), new ParticleSystem.Burst(0.25f, 50, 50, 0, 4.5f) });
        var gv = gust.velocityOverLifetime; gv.enabled = true;
        gv.x = new ParticleSystem.MinMaxCurve(4f, 8f); gv.y = new ParticleSystem.MinMaxCurve(-1.5f, 0.6f); gv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var gr = gust.rotationOverLifetime; gr.enabled = true; gr.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
        var gm = gust.main; gm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); gm.prewarm = false;
        var gsol = gust.sizeOverLifetime; gsol.enabled = false;
        var gn = gust.noise; gn.frequency = 0.9f;
    }

    // ------------------------------------------------------------------ Area B: moonlight through the canopy

    static void BuildAreaBLight()
    {
        var root = Group("Moonlight (god rays)").transform;
        var moon = new Color(0.72f, 0.93f, 0.84f);
        foreach (var x in new[] { 5.5f, 16f, 31f, 45f, 58.5f, 73f, 94f, 108f, 120f })
            GodRay(root, new Vector2(x, ASurf(x) - 0.3f), 26f, R(2f, 7.5f), R(10f, 24f) * (_rng.Next(3) == 0 ? -1f : 1f), moon, R(0.14f, 0.22f), -36, 0.85f);
        // The king's clearing: one wide, bright pool of moonlight (the stage).
        GodRay(root, new Vector2(145f, ASurf(145f) - 0.3f), 28f, 9f, 16f, new Color(0.78f, 0.9f, 1f), 0.26f, -36, 1.2f);

        LayerRays("AreaB-3", "AreaB-3", moon, new[] { 0.14f, 0.38f, 0.63f, 0.87f }, -0.32f, new Vector2(0.09f, 0.14f), -77, 18f);
        LayerRays("AreaB-2", "AreaB-2", new Color(0.7f, 0.85f, 0.9f), new[] { 0.25f, 0.7f }, -0.3f, new Vector2(0.04f, 0.07f), -84, 18f);

        Canopy(new Color(0.17f, 0.26f, 0.21f), 0.8f, new[] { (127f, 168f) });   // open sky over the boss clearing
        FallingLeaves(GameObject.Find("Atmosphere").transform, new Color(0.34f, 0.48f, 0.34f), 26f, 12f);
    }

    // ------------------------------------------------------------------ Area C: rays, new clockwork hazards, rewind

    static void BuildAreaCLight()
    {
        var root = Group("Tower Light (god rays)").transform;
        SetZ(root, 6f);
        // Cold moonlight through the broken windows (from the upper right), warm lamp light in the halls.
        foreach (var (x, ang, warm) in new[] { (10f, -20f, false), (27f, 16f, true), (40f, -18f, false), (65f, -24f, false), (82f, 15f, true),
                                               (99f, -20f, false), (118f, 18f, true), (135f, -24f, false) })
        {
            var col = warm ? new Color(1f, 0.74f, 0.45f) : new Color(0.74f, 0.72f, 1f);
            GodRay(root, new Vector2(x, ASurf(x) - 0.3f), 24f, warm ? R(2.6f, 3.6f) : R(3.2f, 5f), ang, col, warm ? 0.2f : 0.18f, -38, warm ? 0.9f : 0.75f);
        }
        // Neon lamps hung on chains along the climb (cyan = clockwork, amber = the old keepers' lamps).
        var lamps = Group("Neon Lamps", root).transform;
        int i = 0;
        for (float x = 6f; x < MapWidth - 4f; x += R(9f, 13f), i++)
        {
            bool cyan = i % 3 != 2;
            var col = cyan ? new Color(0.4f, 0.95f, 1f) : new Color(1f, 0.7f, 0.35f);
            float y = AreaH(Mathf.FloorToInt(x)) + R(5.5f, 7f);
            var chain = Chain(lamps, new Vector2(x, y + 9f), 9f, 5, new Color(0.4f, 0.38f, 0.48f), 1, 0.8f);
            var bulb = Place("glow", new Vector2(x, y), lamps, 9, _neonAdd, new Color(col.r, col.g, col.b, 0.95f), 0.12f);
            bulb.name = "Lamp Core";
            LocalGlow("Lamp Halo", bulb.transform, Vector2.zero, new Vector2(18f, 18f), new Color(col.r, col.g, col.b, 0.45f), 8, _neonSoft);
            var l = PointLight("Neon Lamp", new Vector2(x, y), lamps, col, 6.5f, 1.25f);
            l.gameObject.AddComponent<FlickerLight2D>().flickerAmount = 0.12f;
        }

        LayerRays("Area C-2", "Area C-2", new Color(0.72f, 0.7f, 1f), new[] { 0.2f, 0.55f, 0.85f }, -0.25f, new Vector2(0.05f, 0.09f), -85, -20f);
    }

    static TickCrusher CrusherAt(Transform parent, float x, float period, float phase)
    {
        const float drop = 5f;
        float floor = ASurf(x);
        var root = new GameObject($"Tick Crusher x{x}");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(x, floor + 1.2f + drop, 0f);
        var cr = root.AddComponent<TickCrusher>();
        cr.drop = drop;
        cr.period = period;
        cr.phase = phase;
        cr.rod = Chain(root.transform, Vector2.zero, 0.6f, 7, new Color(0.55f, 0.52f, 0.62f), 1, 1.6f);
        if (cr.rod) cr.rod.transform.localPosition = Vector3.zero;
        Gear(root.transform, "b1", Vector2.zero, 1.4f, 8, _unlit, new Color(0.7f, 0.55f, 0.45f), 0f).transform.localPosition = Vector3.zero;
        // Anchor beam up into the dark.
        var beam = Place("white", Vector2.zero, root.transform, 6, _unlit, new Color(0.16f, 0.14f, 0.2f));
        beam.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        beam.transform.localScale = new Vector3(0.5f, 14f, 1f);

        var head = new GameObject("Head").transform;
        head.SetParent(root.transform, false);
        head.localPosition = new Vector3(0f, -0.6f, 0f);
        for (int i = 0; i < 2; i++)
        {
            int col = i == 0 ? 11 : 15;
            var block = Place($"a_{col}_1", Vector2.zero, head, 9, _lit, new Color(0.62f, 0.6f, 0.75f));
            block.transform.localPosition = new Vector3(i - 0.5f, -0.6f, 0f);
            block.transform.localScale = new Vector3(1f, 1.2f, 1f);
        }
        var face = Gear(head, "b2", Vector2.zero, 1.25f, 10, _unlit, new Color(0.85f, 0.62f, 0.45f), 0f);
        face.transform.localPosition = Vector3.zero;
        cr.gear = face.transform;
        // Teeth along the bottom edge.
        for (int i = 0; i < 5; i++)
        {
            var tooth = Place("white", Vector2.zero, head, 9, _unlit, new Color(0.75f, 0.72f, 0.82f));
            tooth.transform.localPosition = new Vector3(-0.8f + i * 0.4f, -0.72f, 0f);
            tooth.transform.localScale = new Vector3(0.18f, 0.22f, 1f);
            tooth.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        cr.head = head;
        cr.warnGlow = LocalGlow("Warn Glow", head, new Vector2(0f, -0.7f), new Vector2(3.4f, 1.4f), new Color(1f, 0.3f, 0.15f, 0f), 11, _neonSoft);
        cr.warnLight = LocalLight("Warn Light", head, new Vector2(0f, -0.8f), new Color(1f, 0.35f, 0.2f), 3.5f, 0f);
        var dust = OneShot("Impact Dust", head, new Vector2(0f, -0.7f), PixelSquareMaterial(), 12, 26, new Vector2(0.4f, 0.8f), new Vector2(2f, 5f),
                           new Vector2(0.05f, 0.1f), Grad((new Color(0.9f, 0.85f, 0.95f), 0f, 1f), (new Color(0.5f, 0.45f, 0.6f), 1f, 0f)), 1f, 180f, 1f, 0.3f);
        var dm = dust.main; dm.playOnAwake = false; dm.simulationSpace = ParticleSystemSimulationSpace.World;
        cr.impactDust = dust;
        root.AddComponent<Rewindable>().trackTransform = false;
        return cr;
    }

    static RollingGear RollingGearAt(Transform parent, float minX, float maxX, float speed)
    {
        const float r = 0.95f;
        float x = (minX + maxX) / 2f;
        var root = new GameObject($"Rolling Saw-Gear x{minX}-{maxX}");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(x, ASurf(x) + r, 0f);
        var g = root.AddComponent<RollingGear>();
        g.minX = minX;
        g.maxX = maxX;
        g.speed = speed;
        g.radius = r;
        var art = new GameObject("Art").transform;
        art.SetParent(root.transform, false);
        var saw = Place("gear_s8_0", Vector2.zero, art, 9, _unlit, new Color(0.85f, 0.85f, 0.95f), r * 2.2f / _sprites["gear_s8_0"].bounds.size.x);
        saw.transform.localPosition = Vector3.zero;
        Gear(art, "b1", Vector2.zero, 0.9f, 10, _unlit, new Color(0.8f, 0.55f, 0.4f), 0f).transform.localPosition = Vector3.zero;
        g.art = art;
        LocalGlow("Danger Glow", root.transform, Vector2.zero, new Vector2(3.4f, 3.4f), new Color(1f, 0.35f, 0.2f, 0.32f), 8, _neonSoft);
        LocalLight("Saw Light", root.transform, Vector2.zero, new Color(1f, 0.5f, 0.3f), 3.2f, 0.8f);
        var sparks = Particles("Sparks", Vector2.zero, root.transform, _emberSquare, 10, 26f, new Vector2(0.2f, 0.45f), new Vector2(2f, 4.5f),
                               new Vector2(0.05f, 0.09f), Grad((FireCore, 0f, 1f), (FireOrange, 1f, 0f)), 0.05f, 1f, 0.2f, 60);
        var sm = sparks.main; sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.prewarm = false;
        var ss = sparks.shape; ss.angle = 35f;
        g.sparks = sparks;
        root.AddComponent<Rewindable>().trackTransform = false;
        return g;
    }

    /// <summary>
    /// New clockwork beats: a tick crusher teaches the rhythm on the first floor, a crusher pair alternates on
    /// the gallery after the gear lift, one more guards the short hall, and a loose saw-gear rolls where the bridge
    /// chase spits you out (a good moment to spend a rewind).
    /// </summary>
    static void BuildAreaCObstacles()
    {
        var mech = GameObject.Find("Mechanisms").transform;
        CrusherAt(mech, 12.5f, 3.2f, 0f);
        CrusherAt(mech, 56.5f, 2.6f, 0.5f);
        CrusherAt(mech, 66f, 2.4f, 0.25f);
        RollingGearAt(mech, 107.6f, 111.4f, 2.4f);
    }

    /// <summary>Time-sand and bell grit: different paths every fall, and a little bounce when they hit stone.</summary>
    static void BouncyFall(ParticleSystem ps, float gravity)
    {
        if (!ps) return;
        var main = ps.main;
        main.gravityModifier = gravity;
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        v.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var n = ps.noise;
        n.enabled = true;
        n.strength = 0.35f;
        n.frequency = 0.6f;
        n.scrollSpeed = 0.7f;
        ps.useAutoRandomSeed = true;
        var col = ps.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.mode = ParticleSystemCollisionMode.Collision2D;
        col.bounce = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        col.dampen = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        col.lifetimeLoss = 0.12f;
        col.quality = ParticleSystemCollisionQuality.Medium;
        col.radiusScale = 0.5f;
        col.enableDynamicColliders = true;
    }

    static void BuildAreaCRewind(GameObject player)
    {
        // Falling things bounce (sand from the shafts, grit from the bell).
        var sand = GameObject.Find("Time Sand")?.GetComponent<ParticleSystem>();
        BouncyFall(sand, 0.22f);
        if (sand) { var m = sand.main; m.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f); }
        BouncyFall(GameObject.Find("Bell Grit")?.GetComponent<ParticleSystem>(), 1.2f);

        BuildRewind(player);
    }

    /// <summary>The hold-Q rewind (3 charges) for any area: post-FX, after-images, and what the past remembers.</summary>
    static void BuildRewind(GameObject player)
    {
        var tr = new GameObject("Time Rewind (hold Q, 3 charges)").AddComponent<TimeRewind>();
        var vol = tr.gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 6f;
        vol.weight = 0f;
        var path = $"{Root}/Rewind_PostFX.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(-55f);
        ca.contrast.Override(25f);
        ca.colorFilter.Override(new Color(1.15f, 0.93f, 0.65f));    // sepia: the past
        profile.Add<LensDistortion>(true).intensity.Override(-0.3f);
        profile.Add<ChromaticAberration>(true).intensity.Override(0.9f);
        var vg = profile.Add<Vignette>(true);
        vg.color.Override(new Color(0.35f, 0.2f, 0.05f));
        vg.intensity.Override(0.5f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        vol.sharedProfile = profile;
        tr.volume = vol;
        var trail = player.GetComponent<AfterImageTrail>();
        if (!trail)
        {
            trail = player.AddComponent<AfterImageTrail>();
            trail.source = player.GetComponent<SpriteRenderer>();
            trail.material = Neon("M_AfterImage", Color.white, 1.6f, 1f, true, 0f, 0f);
            trail.enabled = false;
        }
        tr.trail = trail;
        tr.player = player.transform;
        tr.startFx = SheetFx("FX_RewindRing", "fx_clockring", 30f, 2.4f, _neonAdd, new Color(1f, 0.8f, 0.45f, 0.9f), 18, Vector2.zero, new Color(1f, 0.75f, 0.4f), 6f);

        // What the past remembers: the boy, every enemy, and every mechanism with a clock.
        player.AddComponent<Rewindable>();
        foreach (var hp in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            if (!hp.GetComponent<Rewindable>()) hp.gameObject.AddComponent<Rewindable>();
        foreach (var p in Object.FindObjectsByType<Pendulum>(FindObjectsSortMode.None))
            p.gameObject.AddComponent<Rewindable>().trackTransform = false;
        foreach (var p in Object.FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None))
            p.gameObject.AddComponent<Rewindable>();
        foreach (var b in Object.FindObjectsByType<CollapseBridge>(FindObjectsSortMode.None))
            b.gameObject.AddComponent<Rewindable>().trackTransform = false;
        foreach (var t in Object.FindObjectsByType<TreeTopple>(FindObjectsSortMode.None))
            if (!t.GetComponent<Rewindable>()) t.gameObject.AddComponent<Rewindable>().trackTransform = false;
    }
}
