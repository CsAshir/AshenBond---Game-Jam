using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Area B shadow roster (Shade Rat, Hollow Skeleton, Spore Shade, Shade Eye) and the two bosses:
/// The Hollow King (end of Area B) and The Clockwork Eye (top of Area C).
/// </summary>
public static partial class DarkForestBuilder
{
    static readonly Color ShadowBody = new Color(0.62f, 0.54f, 0.8f);
    static readonly Color ShadowRim = new Color(0.65f, 0.35f, 1f, 0.6f);

    /// <summary>The forest's shadow look: a bubbling ink pool underfoot, rising violet smoke, cyan eyes, shadow death.</summary>
    static void Shadowfy(EnemyHealth hp, Vector2 eye, float poolWidth, bool pool = true)
    {
        var root = hp.transform;
        hp.deathFx = _fxShadowHit;
        if (pool)
        {
            var ink = Place("glow", Vector2.zero, root, 3, _unlit, new Color(0.04f, 0.01f, 0.08f, 0.85f), 1f);
            ink.name = "Shadow Pool";
            ink.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ink.transform.localScale = new Vector3(poolWidth / 4f, 0.22f, 1f);
            var rim = Place("glow", Vector2.zero, root, 4, _neonSoft, new Color(0.55f, 0.25f, 1f, 0.35f), 1f);
            rim.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            rim.transform.localScale = new Vector3(poolWidth * 1.2f / 4f, 0.3f, 1f);
        }
        var aura = BackgroundParticles("Shadow Aura", root, Vector2.zero, new Vector2(poolWidth * 0.7f, 0.3f), _smoke, 9, 7f,
            new Vector2(1.2f, 2.2f), new Vector2(0.4f, 1f), new Vector2(0.5f, 1f),
            Grad((new Color(0.45f, 0.2f, 0.85f), 0f, 0f), (new Color(0.25f, 0.1f, 0.5f), 0.3f, 0.55f), (new Color(0.05f, 0.02f, 0.1f), 1f, 0f)), 40);
        aura.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        var am = aura.main; am.simulationSpace = ParticleSystemSimulationSpace.World;
        LocalGlow("Shadow Eye", root, eye, new Vector2(0.55f, 0.55f), new Color(0.4f, 0.95f, 1f, 0.75f), 12, _neonAdd);
        LocalLight("Shadow Light", root, new Vector2(0f, 1f), new Color(0.55f, 0.3f, 1f), 2.6f, 0.7f);
    }

    static EnemyHealth SpawnShadeRat(Transform parent, float x, float min, float max)
    {
        var hp = SpawnRat(parent, x, min, max, 1.1f);                       // GDD: second hound 10% faster
        hp.name = $"Shade Rat x{x}";
        var ai = hp.GetComponent<EmberRatAI>();
        ai.body.color = ShadowBody;
        ai.rim.color = ShadowRim;
        ai.tailFire.GetComponent<ParticleSystemRenderer>().sharedMaterial = _smoke;   // a tail of smoke, not fire
        var col = ai.tailFire.colorOverLifetime;
        col.color = Grad((new Color(0.5f, 0.25f, 0.95f), 0f, 0.7f), (new Color(0.1f, 0.05f, 0.2f), 1f, 0f));
        ai.eyeGlow.color = new Color(0.4f, 0.95f, 1f, 0.5f);
        ai.emberLight.color = new Color(0.55f, 0.3f, 1f);
        Shadowfy(hp, new Vector2(0.98f, 0.72f), 2.2f, false);
        return hp;
    }

    static EnemyHealth SpawnSkeleton(Transform parent, float x, float min = -999f, float max = 999f)
    {
        var (root, body, hp) = EnemyBody(parent, $"Hollow Skeleton x{x}", new Vector2(x, ASurf(x)), "en_skel_attack_0", 1.55f,
                                         ShadowBody, ShadowRim, new Vector2(1.1f, 2f), new Vector2(0f, 1f), 4);
        var ai = root.AddComponent<SkeletonAI>();
        Brain(ai, body, root.GetComponent<BoxCollider2D>());
        ai.attackFrames = Sheet("en_skel_attack");
        ai.swordPrefab = _blade;
        ai.patrolMin = min;
        ai.patrolMax = max;
        hp.blockFx = _fxBlock;
        Shadowfy(hp, new Vector2(0.25f, 1.75f), 2.4f);
        return hp;
    }

    static EnemyHealth SpawnMushroom(Transform parent, float x)
    {
        var (root, body, hp) = EnemyBody(parent, $"Spore Shade x{x}", new Vector2(x, ASurf(x)), "en_mush_attack_0", 1.6f,
                                         new Color(0.72f, 0.6f, 0.86f), ShadowRim, new Vector2(1.2f, 1.4f), new Vector2(0f, 0.7f), 2);
        var ai = root.AddComponent<MushroomAI>();
        Brain(ai, body, root.GetComponent<BoxCollider2D>());
        ai.attackFrames = Sheet("en_mush_attack");
        ai.sporePrefab = _spore;
        Shadowfy(hp, new Vector2(0.1f, 1.1f), 2f);
        return hp;
    }

    /// <summary>Forest goblin: the bomb thrower, gone to shadow (violet skin, smoke, cyan eye).</summary>
    static EnemyHealth SpawnShadeGoblin(Transform parent, float x, float? y = null)
    {
        var hp = SpawnGoblin(parent, x, y);
        hp.name = $"Shade Goblin x{x}";
        hp.body.color = new Color(0.72f, 0.66f, 0.9f);
        hp.body.transform.Find("Rim").GetComponent<SpriteRenderer>().color = ShadowRim;
        Shadowfy(hp, new Vector2(0.2f, 1.2f), 1.6f, false);
        return hp;
    }

    /// <summary>Clock-tower spore cap: corrupted red (red skin, red smoke, red spores) on the high ledges.</summary>
    static EnemyHealth SpawnRedMushroom(Transform parent, float x, float? y = null)
    {
        var hp = SpawnMushroom(parent, x);
        hp.name = $"Rust Spore x{x}";
        if (y.HasValue) hp.transform.position = new Vector3(x, y.Value, 0f);
        hp.body.color = new Color(0.95f, 0.62f, 0.6f);
        hp.body.transform.Find("Rim").GetComponent<SpriteRenderer>().color = new Color(1f, 0.25f, 0.2f, 0.7f);
        hp.GetComponent<MushroomAI>().sporePrefab = _redSpore ? _redSpore
            : (_redSpore = BuildShotPrefab("RustSpore", "en_mush_shot", 4, new Color(1f, 0.55f, 0.45f), new Color(1f, 0.3f, 0.2f), 1.4f, 9f));
        foreach (var ps in hp.GetComponentsInChildren<ParticleSystem>())
        {
            var col = ps.colorOverLifetime;
            col.color = Grad((new Color(1f, 0.3f, 0.2f), 0f, 0f), (new Color(0.6f, 0.1f, 0.1f), 0.3f, 0.55f), (new Color(0.1f, 0.02f, 0.02f), 1f, 0f));
        }
        foreach (var l in hp.GetComponentsInChildren<Light2D>()) l.color = new Color(1f, 0.3f, 0.25f);
        foreach (var sr in hp.GetComponentsInChildren<SpriteRenderer>())
            if (sr.name is "Shadow Eye" or "Shadow Pool") sr.color = sr.name == "Shadow Eye" ? new Color(1f, 0.35f, 0.25f, 0.8f) : new Color(0.15f, 0.02f, 0.02f, 0.85f);
        return hp;
    }

    static EnemyHealth SpawnShadeEye(Transform parent, float x, float y)
    {
        var hp = SpawnEye(parent, x, y);
        hp.name = $"Shade Eye x{x}";
        var ai = hp.GetComponent<FlyingEyeAI>();
        ai.body.color = new Color(0.6f, 0.5f, 0.8f);
        ai.rim.color = ShadowRim;
        ai.shotPrefab = _spore;
        Shadowfy(hp, Vector2.zero, 1.4f, false);
        return hp;
    }

    // ------------------------------------------------------------------ heavy bosses (placeholders until the final ones are chosen)

    static GameObject BuildWispPrefab()
    {
        var (root, body, hp) = EnemyBody(null, "ShadowWisp", Vector2.zero, "en_wisp_idle_0", 2.2f,
                                         new Color(0.55f, 0.45f, 0.75f), new Color(0.65f, 0.35f, 1f, 0.7f), new Vector2(0.9f, 1.4f), Vector2.zero, 1);
        var w = root.AddComponent<ShadowWisp>();
        Brain(w, body, root.GetComponent<BoxCollider2D>());
        w.appearFrames = Sheet("en_wisp_appear");
        w.idleFrames = Sheet("en_wisp_idle");
        w.deathFrames = Sheet("en_wisp_death");
        w.spawnGrace = 0.7f;
        hp.embers = 0;
        hp.soulPrefab = null;              // summons are not souls
        hp.deathFx = _fxShadowHit;
        LocalGlow("Wisp Glow", root.transform, Vector2.zero, new Vector2(2f, 2f), new Color(0.6f, 0.3f, 1f, 0.45f), 8, _neonSoft);
        LocalGlow("Wisp Eye", root.transform, new Vector2(0.15f, 0.3f), new Vector2(0.4f, 0.4f), new Color(0.4f, 0.95f, 1f, 0.8f), 12, _neonAdd);
        var trail = Particles("Smoke", Vector2.zero, root.transform, _smoke, 7, 0f, new Vector2(0.4f, 0.8f), Vector2.zero, new Vector2(0.3f, 0.6f),
                              Grad((new Color(0.5f, 0.3f, 0.9f), 0f, 0.6f), (new Color(0.1f, 0.05f, 0.2f), 1f, 0f)), 0.1f, -0.05f, 0.3f, 60);
        var te = trail.emission; te.rateOverDistance = 5f; te.rateOverTime = 4f;
        var tm = trail.main; tm.simulationSpace = ParticleSystemSimulationSpace.World; tm.prewarm = false;
        root.AddComponent<Rewindable>().persistent = false;
        return SavePrefab(root, "ShadowWisp");
    }

    static GameObject BuildWavePrefab()
    {
        var root = new GameObject("BeastShockwave");
        var frames = Sheet("en_beast_wave");
        var art = Place("white", Vector2.zero, root.transform, 14, _neonTex, new Color(0.55f, 0.95f, 1f), 1.8f);
        art.name = "Art";
        art.sprite = frames.Length > 0 ? frames[0] : art.sprite;
        art.transform.localPosition = Vector3.zero;
        LocalGlow("Wave Glow", root.transform, new Vector2(0f, 0.5f), new Vector2(2.4f, 1.6f), new Color(0.4f, 0.9f, 1f, 0.55f), 13, _neonSoft);
        LocalLight("Wave Light", root.transform, new Vector2(0f, 0.5f), new Color(0.45f, 0.9f, 1f), 2.6f, 1.2f);
        var dust = Particles("Grit", Vector2.zero, root.transform, PixelSquareMaterial(), 15, 30f, new Vector2(0.3f, 0.6f), new Vector2(1.5f, 3f),
                             new Vector2(0.05f, 0.09f), Grad((new Color(0.7f, 0.95f, 1f), 0f, 1f), (new Color(0.3f, 0.5f, 0.8f), 1f, 0f)), 0.3f, 0.8f, 0.2f, 60);
        var dm = dust.main; dm.simulationSpace = ParticleSystemSimulationSpace.World; dm.prewarm = false;
        var sw = root.AddComponent<ShockWave>();
        sw.art = art;
        sw.frames = frames.Skip(4).Take(8).ToArray();      // the risen arch while it runs
        root.AddComponent<Rewindable>().persistent = false;
        return SavePrefab(root, "BeastShockwave");
    }

    /// <summary>Area B boss (placeholder): the Executioner, a floating reaper of the shadow forest.</summary>
    static EnemyHealth SpawnExecutioner(Transform parent, float x, float arenaMin, float arenaMax)
    {
        var (root, body, hp) = EnemyBody(parent, "BOSS The Executioner", new Vector2(x, ASurf(x)), "en_exec_idle_0", 1.9f,
                                         new Color(0.82f, 0.76f, 0.98f), new Color(0.7f, 0.35f, 1f, 0.8f), new Vector2(2.2f, 4.6f), new Vector2(0.4f, 3.4f), 18);
        var boss = root.AddComponent<HeavyBoss>();
        Brain(boss, body, root.GetComponent<BoxCollider2D>());
        boss.intro = HeavyBoss.Intro.RiseFromShadow;
        boss.bodyOffset = new Vector3(0f, -0.6f, 0f);              // hovers just above its shadow pool
        boss.idleFrames = Sheet("en_exec_idle");
        boss.walkFrames = boss.idleFrames;
        boss.meleeFrames = Sheet("en_exec_attack");
        boss.meleeHitFrames = new Vector2Int(2, 5);
        boss.meleeRange = 3.6f;
        boss.meleeBox = new Vector2(4f, 4.4f);
        boss.spikeFrames = Sheet("en_exec_skill");
        boss.spikeHitFrame = 6;
        boss.spikeRadius = 3f;
        boss.summonFrames = Sheet("en_exec_summon");
        boss.deathFrames = Sheet("en_exec_death");
        boss.phase1 = new[] { HeavyBoss.Move.Melee, HeavyBoss.Move.Summon, HeavyBoss.Move.Melee, HeavyBoss.Move.Spikes };
        boss.phase2 = new[] { HeavyBoss.Move.Melee, HeavyBoss.Move.Pillars, HeavyBoss.Move.Summon, HeavyBoss.Move.Spikes, HeavyBoss.Move.Melee };
        boss.walkSpeed = 2.2f;
        boss.minionPrefab = BuildWispPrefab();
        boss.pillarFx = _fxPillar;
        boss.pillarWarnFx = _fxPillarWarn;
        boss.ringFx = _fxShadowSpawn;
        boss.arenaMin = arenaMin;
        boss.arenaMax = arenaMax;
        boss.spawnGrace = 1.8f;
        hp.embers = 4;
        hp.exposedShadowMultiplier = 2;
        Shadowfy(hp, new Vector2(0.6f, 4.6f), 5f);
        boss.glare = LocalLight("Glare", root.transform, new Vector2(0.6f, 4.6f), new Color(1f, 0.25f, 0.3f), 4f, 0.7f);
        boss.aura = root.transform.Find("Shadow Aura").GetComponent<ParticleSystem>();
        var bar = root.AddComponent<BossBar>();
        bar.bossName = "The Executioner";
        bar.barColor = new Color(0.7f, 0.35f, 1f);
        return hp;
    }

    /// <summary>Area C boss (placeholder): the Horned Beast that guards the top of the tower.</summary>
    static EnemyHealth SpawnBeast(Transform parent, float x, float arenaMin, float arenaMax)
    {
        var (root, body, hp) = EnemyBody(parent, "BOSS The Horned Beast", new Vector2(x, ASurf(x)), "en_beast_idle_0", 3.2f,
                                         new Color(0.74f, 0.74f, 0.88f), new Color(0.4f, 0.95f, 1f, 0.75f), new Vector2(3.2f, 4.6f), new Vector2(0.3f, 2.3f), 20);
        var boss = root.AddComponent<HeavyBoss>();
        Brain(boss, body, root.GetComponent<BoxCollider2D>());
        boss.intro = HeavyBoss.Intro.DropFromAbove;
        boss.bodyOffset = new Vector3(0f, -8f / 24f * 3.2f, 0f);   // the art stands 8 px above the bottom of its frame
        boss.idleFrames = Sheet("en_beast_idle");
        boss.walkFrames = Sheet("en_beast_walk");
        boss.meleeFrames = Sheet("en_beast_slam");            // rears up, crashes down (used by the hop-slam)
        boss.deathFrames = Sheet("en_beast_death");
        boss.phase1 = new[] { HeavyBoss.Move.Slam, HeavyBoss.Move.Charge, HeavyBoss.Move.Slam };
        boss.phase2 = new[] { HeavyBoss.Move.Slam, HeavyBoss.Move.TimeLock, HeavyBoss.Move.Charge };
        boss.walkSpeed = 2f;
        boss.chargeSpeed = 12f;
        boss.wavePrefab = BuildWavePrefab();
        boss.timeShotPrefab = _timeShot;
        boss.ringFx = _fxClockRing;
        boss.slamFx = _beamImpact;
        boss.arenaMin = arenaMin;
        boss.arenaMax = arenaMax;
        boss.spawnGrace = 2f;
        hp.embers = 4;
        hp.exposedShadowMultiplier = 2;
        LocalGlow("Clock Glow", root.transform, new Vector2(0f, 2.4f), new Vector2(7f, 7f), new Color(0.45f, 0.9f, 1f, 0.22f), 7, _neonSoft);
        // The glare rides on the body (it hops with it); the art's own red eyes carry the look.
        boss.glare = LocalLight("Glare", body.transform, new Vector2(0f, 3.4f / 3.2f), new Color(1f, 0.3f, 0.25f), 5f, 0.8f);
        LocalLight("Beast Light", root.transform, new Vector2(0f, 2.5f), new Color(0.45f, 0.9f, 1f), 6f, 0.9f);
        var bar = root.AddComponent<BossBar>();
        bar.bossName = "The Horned Beast";
        bar.barColor = new Color(0.45f, 0.9f, 1f);
        return hp;
    }

    // ------------------------------------------------------------------ bosses (previous; kept for later)

    static EnemyHealth SpawnHollowKing(Transform parent, float x, float arenaMin, float arenaMax)
    {
        var (root, body, hp) = EnemyBody(parent, "BOSS The Hollow King", new Vector2(x, ASurf(x)), "en_skel_attack_0", 3.8f,
                                         new Color(0.62f, 0.54f, 0.82f), new Color(0.7f, 0.35f, 1f, 0.75f), new Vector2(2.6f, 5f), new Vector2(0f, 2.5f), 18);
        var boss = root.AddComponent<HollowKingBoss>();
        Brain(boss, body, root.GetComponent<BoxCollider2D>());
        boss.attackFrames = Sheet("en_skel_attack");
        boss.bladePrefab = _blade;
        boss.pillarFx = _fxPillar;
        boss.pillarWarnFx = _fxPillarWarn;
        boss.arenaMin = arenaMin;
        boss.arenaMax = arenaMax;
        boss.spawnGrace = 1.8f;
        hp.blockFx = _fxBlock;
        hp.embers = 4;
        hp.exposedShadowMultiplier = 2;                                   // a boss takes the combo, but is not one-shot by it
        Shadowfy(hp, new Vector2(0.6f, 4.3f), 6f);
        boss.glare = LocalLight("Glare", root.transform, new Vector2(0.6f, 4.3f), new Color(1f, 0.25f, 0.3f), 4f, 0.6f);
        boss.aura = root.transform.Find("Shadow Aura").GetComponent<ParticleSystem>();
        var bar = root.AddComponent<BossBar>();
        bar.bossName = "The Hollow King";
        bar.barColor = new Color(0.7f, 0.35f, 1f);
        return hp;
    }

    static EnemyHealth SpawnClockworkEye(Transform parent, float x, float floorY, float hoverY, float arenaMin, float arenaMax)
    {
        var (root, body, hp) = EnemyBody(parent, "BOSS The Clockwork Eye", new Vector2(x, hoverY + 4f), "en_eye_fly_3", 4.2f,
                                         new Color(0.85f, 0.8f, 0.9f), new Color(0.4f, 0.95f, 1f, 0.6f), new Vector2(3.2f, 3f), Vector2.zero, 16);
        var boss = root.AddComponent<ClockworkEyeBoss>();
        Brain(boss, body, root.GetComponent<BoxCollider2D>());
        var f = Sheet("en_eye_fly");
        boss.flyFrames = f.Length >= 6 ? new[] { f[3], f[4], f[5], f[4] } : f;
        boss.curlFrames = f.Take(3).ToArray();
        boss.shotPrefab = _eyeShot;
        boss.timeLockShotPrefab = _timeShot;
        boss.clockRingFx = _fxClockRing;
        boss.arenaMin = arenaMin;
        boss.arenaMax = arenaMax;
        boss.floorY = floorY;
        boss.hoverY = hoverY;
        boss.spawnGrace = 2f;
        hp.embers = 4;
        hp.exposedShadowMultiplier = 2;
        // Brass gear halo turning behind the eye: the tower's clockwork made flesh.
        var halo = new GameObject("Gear Halo");
        halo.transform.SetParent(root.transform, false);
        var gframes = GearFrames("b11");
        if (gframes.Length > 0)
        {
            var g = halo.AddComponent<SpriteRenderer>();
            g.sprite = gframes[0];
            g.sharedMaterial = _unlit;
            g.color = new Color(0.85f, 0.6f, 0.4f);
            g.sortingOrder = 8;
            float s = 6.5f / gframes[0].bounds.size.x;
            halo.transform.localScale = new Vector3(s, s, 1f);
        }
        boss.halo = halo.transform;
        LocalGlow("Clock Glow", root.transform, Vector2.zero, new Vector2(8f, 8f), new Color(0.45f, 0.9f, 1f, 0.25f), 7, _neonSoft);
        boss.eyeLight = LocalLight("Eye Light", root.transform, new Vector2(0.4f, 0f), new Color(1f, 0.35f, 0.3f), 6f, 1f);
        var bar = root.AddComponent<BossBar>();
        bar.bossName = "The Clockwork Eye";
        bar.barColor = new Color(0.45f, 0.9f, 1f);
        return hp;
    }
}
