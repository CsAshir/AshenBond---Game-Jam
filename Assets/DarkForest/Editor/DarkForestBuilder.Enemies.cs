using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Enemies (Area A roster: Ember Rat, Flying Eye, Goblin bomber), their projectiles, hit / exposed / death
/// effects and fire-walled ambush arenas. Mushroom and Skeleton are reserved for later areas.
/// </summary>
public static partial class DarkForestBuilder
{
    static Material _enemyMat, _enemyRimMat;
    static GameObject _soul, _redSpore;
    static GameObject _fxFireHit, _fxShadowHit, _fxDeath, _fxSpawn, _fxClear, _emberDrop, _eyeShot, _bomb;
    static GameObject _fxBlock, _fxShadowSpawn, _fxPillar, _fxPillarWarn, _blade, _spore, _timeShot, _fxClockRing;

    static void BuildEnemyAssets()
    {
        _enemyMat = Neon("M_EnemySprite", Color.white, 1.3f, 0f, false, 0f, 0f);          // unlit: always readable
        _enemyRimMat = Neon("M_EnemyRim", Color.white, 2.2f, 1f, true, 0f, 0f);            // flat additive outline

        _fxFireHit = BuildEnemyHitPrefab();
        _soul = BuildSoulPrefab();
        _redSpore = null;
        _fxShadowHit = SheetFx("FX_ShadowFinisher", "fx_midnight", 38f, 1.7f, _neonAdd, new Color(0.85f, 0.65f, 1f), 16, Vector2.zero, new Color(0.6f, 0.35f, 1f), 4.5f);
        _fxDeath = SheetFx("FX_EnemyDeath", "fx_ring", 40f, 1.5f, _neonAdd, new Color(1f, 0.7f, 0.4f), 16, Vector2.zero, new Color(1f, 0.55f, 0.25f), 3.5f);
        _fxSpawn = SheetFx("FX_EnemySpawn", "fx_firespin", 34f, 2.2f, _neonAdd, new Color(1f, 0.6f, 0.35f), 15, Vector2.zero, new Color(1f, 0.45f, 0.15f), 4f);
        _fxClear = SheetFx("FX_ArenaClear", "fx_ring", 30f, 5f, _neonAdd, new Color(1f, 0.85f, 0.5f), 16, Vector2.zero, new Color(1f, 0.75f, 0.4f), 9f);
        _fxBlock = SheetFx("FX_ShieldBlock", "fx_ring", 60f, 0.6f, _neonAdd, new Color(0.75f, 0.9f, 1f), 17, Vector2.zero, new Color(0.6f, 0.8f, 1f), 2f);
        _fxShadowSpawn = SheetFx("FX_ShadowSpawn", "fx_shadowpool", 22f, 2.6f, _neonAdd, new Color(0.7f, 0.45f, 1f, 0.9f), 15, Vector2.zero, new Color(0.55f, 0.3f, 1f), 4f);
        _fxPillar = SheetFx("FX_ShadowPillar", "fx_midnight", 30f, 3.2f, _neonAdd, new Color(0.75f, 0.5f, 1f), 16, Vector2.zero, new Color(0.6f, 0.3f, 1f), 5f);
        _fxPillarWarn = SheetFx("FX_PillarWarn", "fx_shadowpool", 34f, 1.6f, _neonAdd, new Color(0.6f, 0.3f, 1f, 0.8f), 5, Vector2.zero);
        _fxClockRing = SheetFx("FX_ClockRing", "fx_clockring", 26f, 3.4f, _neonAdd, new Color(0.6f, 0.95f, 1f, 0.85f), 18, Vector2.zero, new Color(0.45f, 0.85f, 1f), 6f);
        _blade = BuildBladePrefab();
        _spore = BuildShotPrefab("ShadowSpore", "en_mush_shot", 4, new Color(0.75f, 0.55f, 1f), new Color(0.6f, 0.3f, 1f), 1.4f, 9f);
        _timeShot = BuildShotPrefab("TimeLockBolt", "en_eye_shot", 3, new Color(0.7f, 0.95f, 1f), new Color(0.4f, 0.9f, 1f), 0.8f, 0f);
        _emberDrop = BuildEmberDropPrefab();
        _eyeShot = BuildEyeShotPrefab();
        _bomb = BuildBombPrefab();
    }

    /// <summary>Compact impact: a quick white-gold ring + sparks, small sunburst core.</summary>
    static GameObject BuildEnemyHitPrefab()
    {
        var root = new GameObject("FX_EnemyHit");
        var ring = root.AddComponent<SpriteRenderer>();
        var rf = Sheet("fx_ring");
        ring.sprite = rf.Length > 0 ? rf[0] : null;
        ring.sharedMaterial = _neonAdd;
        ring.color = new Color(1f, 0.85f, 0.6f, 0.8f);
        ring.sortingOrder = 17;
        root.transform.localScale = Vector3.one * 0.6f;
        var one = root.AddComponent<SpriteOneShot>();
        one.frames = rf;
        one.fps = 70f;
        one.linger = 0.5f;
        // Soft core flash + two crossed light streaks (the old sunburn frame read as a hard-edged cut-out).
        var core = Place("glow", Vector2.zero, root.transform, 18, _neonSoft, new Color(1f, 0.75f, 0.45f, 0.95f), 1f);
        core.name = "Flash";
        core.transform.localPosition = Vector3.zero;
        var hf = core.gameObject.AddComponent<HitFlash>();
        hf.from = Vector3.one * 0.25f;
        hf.to = Vector3.one * 0.75f;
        hf.life = 0.18f;
        foreach (var (ang, len) in new[] { (20f, 1.1f), (-55f, 0.8f) })
        {
            var streak = Place("glow", Vector2.zero, root.transform, 19, _neonAdd, new Color(1f, 0.9f, 0.7f, 0.9f), 1f);
            streak.name = "Streak";
            streak.transform.localPosition = Vector3.zero;
            streak.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            var sh = streak.gameObject.AddComponent<HitFlash>();
            sh.from = new Vector3(0.3f * len, 0.07f, 1f);
            sh.to = new Vector3(0.9f * len, 0.035f, 1f);
            sh.life = 0.12f;
        }
        OneShot("Sparks", root.transform, Vector2.zero, _emberSquare, 19, 16, new Vector2(0.15f, 0.35f), new Vector2(4f, 9f), new Vector2(0.07f, 0.12f),
                Grad((FireCore, 0f, 1f), (FireOrange, 0.5f, 1f), (FireMagenta, 1f, 0f)), 0.1f, 360f, 0.5f, 0.1f);
        LocalLight("Hit Light", root.transform, Vector2.zero, new Color(1f, 0.6f, 0.3f), 3f, 2.2f);
        return SavePrefab(root, "FX_EnemyHit");
    }

    /// <summary>A fallen enemy's soul: bright core, soft halo, light and a long fading tail of light.</summary>
    static GameObject BuildSoulPrefab()
    {
        var root = new GameObject("SoulOrb");
        var core = Place("glow", Vector2.zero, root.transform, 20, _neonAdd, new Color(0.85f, 1f, 1f, 1f), 0.16f);
        core.transform.localPosition = Vector3.zero;
        LocalGlow("Halo", root.transform, Vector2.zero, new Vector2(2.4f, 2.4f), new Color(0.45f, 0.85f, 1f, 0.6f), 19, _neonSoft);
        var orb = root.AddComponent<SoulOrb>();
        orb.glow = LocalLight("Soul Light", root.transform, Vector2.zero, new Color(0.5f, 0.9f, 1f), 2.6f, 1.6f);
        var tg = new GameObject("Tail");
        tg.transform.SetParent(root.transform, false);
        var tr = tg.AddComponent<TrailRenderer>();
        tr.sharedMaterial = _neonAdd;
        tr.time = 0.7f;
        tr.minVertexDistance = 0.05f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.4f, 0.24f), new Keyframe(1f, 0f));
        tr.colorGradient = Grad((new Color(0.9f, 1f, 1f), 0f, 1f), (new Color(0.4f, 0.85f, 1f), 0.35f, 0.75f), (new Color(0.55f, 0.3f, 1f), 1f, 0f));
        tr.sortingOrder = 18;
        var sparks = Particles("Sparkle", Vector2.zero, root.transform, PixelSquareMaterial(), 19, 0f, new Vector2(0.3f, 0.6f), new Vector2(0.2f, 0.6f),
                               new Vector2(0.05f, 0.09f), Grad((new Color(0.8f, 1f, 1f), 0f, 1f), (new Color(0.5f, 0.4f, 1f), 1f, 0f)), 0.1f, 0f, 0.4f, 60);
        var se = sparks.emission; se.rateOverDistance = 10f;
        var sm = sparks.main; sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.prewarm = false;
        // Arrival: a soft cyan pop at the lantern.
        var pop = new GameObject("FX_SoulArrive");
        var ps = Place("glow", Vector2.zero, pop.transform, 21, _neonSoft, new Color(0.5f, 0.95f, 1f, 0.9f), 1f);
        ps.transform.localPosition = Vector3.zero;
        var hf = ps.gameObject.AddComponent<HitFlash>();
        hf.from = Vector3.one * 0.2f; hf.to = Vector3.one * 0.9f; hf.life = 0.3f;
        LocalLight("Pop Light", pop.transform, Vector2.zero, new Color(0.5f, 0.95f, 1f), 3.5f, 2f);
        pop.AddComponent<SelfDestruct>().seconds = 0.4f;
        orb.arriveFx = SavePrefab(pop, "FX_SoulArrive");
        return SavePrefab(root, "SoulOrb");
    }

    static GameObject BuildBladePrefab()
    {
        var root = new GameObject("ShadowBlade");
        var frames = Sheet("en_skel_sword");
        var art = Place("white", Vector2.zero, root.transform, 14, _enemyMat, new Color(0.8f, 0.7f, 1f), 1.3f);
        art.name = "Art";
        art.sprite = frames.Length > 0 ? frames[0] : art.sprite;
        art.transform.localPosition = Vector3.zero;
        LocalGlow("Shadow Glow", root.transform, Vector2.zero, new Vector2(2f, 2f), new Color(0.6f, 0.3f, 1f, 0.5f), 13, _neonSoft);
        var trail = Particles("Shadow Trail", Vector2.zero, root.transform, _smoke, 12, 0f, new Vector2(0.4f, 0.8f), Vector2.zero, new Vector2(0.4f, 0.7f),
                              Grad((new Color(0.5f, 0.3f, 0.9f), 0f, 0.6f), (new Color(0.1f, 0.05f, 0.2f), 1f, 0f)), 0.1f, 0f, 0.3f, 80);
        var tem = trail.emission; tem.rateOverDistance = 6f;
        var tm = trail.main; tm.simulationSpace = ParticleSystemSimulationSpace.World; tm.prewarm = false;
        var b = root.AddComponent<SpinningBlade>();
        b.art = art;
        b.spinFrames = frames.Take(4).ToArray();
        b.breakFrames = frames.Skip(4).ToArray();
        root.AddComponent<Rewindable>().persistent = false;   // vanishes if time runs back past its launch
        return SavePrefab(root, "ShadowBlade");
    }

    static GameObject BuildShotPrefab(string name, string sheet, int flyCount, Color tint, Color glow, float scale, float gravity)
    {
        var root = new GameObject(name);
        var rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = root.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.25f;
        var frames = Sheet(sheet);
        var art = Place("white", Vector2.zero, root.transform, 14, _enemyMat, tint, scale);
        art.name = "Art";
        art.sprite = frames.Length > 0 ? frames[0] : art.sprite;
        art.transform.localPosition = Vector3.zero;
        LocalGlow("Glow", root.transform, Vector2.zero, new Vector2(1.3f, 1.3f), new Color(glow.r, glow.g, glow.b, 0.55f), 13, _neonSoft);
        LocalLight("Shot Light", root.transform, Vector2.zero, glow, 2f, 1f);
        var es = root.AddComponent<EnemyShot>();
        es.art = art;
        es.flyFrames = frames.Take(flyCount).ToArray();
        es.burstFrames = frames.Skip(flyCount).ToArray();
        es.gravity = gravity;
        es.lifetime = 4f;
        if (gravity == 0f)
        {
            // Time-lock bolts: a cyan halo while they hang frozen.
            var hg = LocalGlow("Frozen Halo", root.transform, Vector2.zero, new Vector2(1.6f, 1.6f), new Color(0.4f, 0.95f, 1f, 0.8f), 15, _neonAdd);
            hg.enabled = false;
            es.holdGlow = hg;
        }
        root.AddComponent<Rewindable>().persistent = false;   // vanishes if time runs back past its launch
        return SavePrefab(root, name);
    }

    static GameObject BuildEmberDropPrefab()
    {
        var go = new GameObject("EmberDrop");
        var gem = Place("ember_gem", Vector2.zero, go.transform, 15, _neonTex, new Color(1f, 0.55f, 0.2f), 1.1f);
        gem.transform.localPosition = Vector3.zero;
        LocalGlow("Glow", go.transform, Vector2.zero, new Vector2(2.2f, 2.2f), new Color(1f, 0.5f, 0.15f, 0.6f), 14, _neonSoft);
        LocalLight("Ember Light", go.transform, Vector2.zero, new Color(1f, 0.55f, 0.2f), 2.2f, 1.1f).gameObject.AddComponent<PulseLight2D>();
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.7f;
        go.AddComponent<FloatBob>().amplitude = 0.18f;
        go.AddComponent<EmberPickup>().collectBurst = _emberBurst;
        return SavePrefab(go, "EmberDrop");
    }

    static GameObject BuildEyeShotPrefab()
    {
        var root = new GameObject("EyeShot");
        var rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = root.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.28f;
        var frames = Sheet("en_eye_shot");
        var art = Place("white", Vector2.zero, root.transform, 14, _enemyMat, new Color(1f, 0.85f, 0.85f), 0.7f);
        art.name = "Art";
        art.sprite = frames.Length > 0 ? frames[0] : art.sprite;
        art.transform.localPosition = Vector3.zero;
        LocalGlow("Glow", root.transform, Vector2.zero, new Vector2(1.4f, 1.4f), new Color(1f, 0.25f, 0.2f, 0.55f), 13, _neonSoft);
        LocalLight("Shot Light", root.transform, Vector2.zero, new Color(1f, 0.3f, 0.25f), 2.2f, 1.2f);
        var trail = Particles("Trail", Vector2.zero, root.transform, PixelSquareMaterial(), 13, 0f, new Vector2(0.25f, 0.45f), Vector2.zero,
                              new Vector2(0.06f, 0.1f), Grad((new Color(1f, 0.5f, 0.4f), 0f, 1f), (new Color(0.6f, 0.15f, 0.2f), 1f, 0f)), 0.05f, 0f, 0.3f, 60);
        var em = trail.emission; em.rateOverDistance = 14f;
        var tm = trail.main; tm.simulationSpace = ParticleSystemSimulationSpace.World; tm.prewarm = false;
        var es = root.AddComponent<EnemyShot>();
        es.art = art;
        es.flyFrames = frames.Take(3).ToArray();
        es.burstFrames = frames.Skip(3).ToArray();
        root.AddComponent<Rewindable>().persistent = false;   // vanishes if time runs back past its launch
        return SavePrefab(root, "EyeShot");
    }

    static GameObject BuildBombPrefab()
    {
        var root = new GameObject("GoblinBomb");
        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1.6f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.22f;
        var bouncePath = $"{PrefabDir}/BombBounce.physicsMaterial2D";
        var bounce = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(bouncePath);
        if (!bounce)
        {
            bounce = new PhysicsMaterial2D("BombBounce");
            AssetDatabase.CreateAsset(bounce, bouncePath);
        }
        bounce.bounciness = 0.35f;
        bounce.friction = 0.6f;
        col.sharedMaterial = bounce;
        var frames = Sheet("en_bomb");
        var art = Place("white", Vector2.zero, root.transform, 14, _enemyMat, Color.white, 0.95f);
        art.name = "Art";
        art.sprite = frames.Length > 0 ? frames[0] : art.sprite;
        art.transform.localPosition = Vector3.zero;
        var fuse = LocalLight("Fuse Light", root.transform, new Vector2(0f, 0.2f), new Color(1f, 0.7f, 0.3f), 1.8f, 1.2f);
        var blast = LocalLight("Blast Light", root.transform, Vector2.zero, new Color(1f, 0.5f, 0.2f), 6f, 0f);
        var sparks = Particles("Fuse Sparks", new Vector2(0f, 0.2f), root.transform, _emberSquare, 15, 30f, new Vector2(0.15f, 0.35f), new Vector2(0.5f, 1.5f),
                               new Vector2(0.05f, 0.08f), Grad((FireCore, 0f, 1f), (FireOrange, 1f, 0f)), 0.05f, 0.2f, 0.2f, 40);
        var sm = sparks.main; sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.prewarm = false;
        sparks.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        var b = root.AddComponent<GoblinBomb>();
        b.frames = frames;
        b.art = art;
        b.fuseLight = fuse;
        b.blastLight = blast;
        b.sparks = sparks;
        b.blastFx = _beamImpact;
        root.AddComponent<Rewindable>().persistent = false;   // vanishes if time runs back past its launch
        return SavePrefab(root, "GoblinBomb");
    }

    // ------------------------------------------------------------------ shared enemy body

    static (GameObject root, SpriteRenderer body, EnemyHealth hp) EnemyBody(Transform parent, string name, Vector2 pos, string firstSprite,
                                                                          float scale, Color tint, Color rimColor, Vector2 hurtSize, Vector2 hurtOffset, int hp)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(pos.x, pos.y, 0f);
        var rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var hurt = root.AddComponent<BoxCollider2D>();
        hurt.isTrigger = true;
        hurt.size = hurtSize;
        hurt.offset = hurtOffset;

        var body = new GameObject("Body").AddComponent<SpriteRenderer>();
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(scale, scale, 1f);
        body.sprite = _sprites.TryGetValue(firstSprite, out var s) ? s : null;
        body.sharedMaterial = _enemyMat;
        body.color = tint;
        body.sortingOrder = 10;

        var rim = new GameObject("Rim").AddComponent<SpriteRenderer>();
        rim.transform.SetParent(body.transform, false);
        rim.transform.localScale = new Vector3(1.09f, 1.09f, 1f);
        rim.sprite = body.sprite;
        rim.sharedMaterial = _enemyRimMat;
        rim.color = rimColor;
        rim.sortingOrder = 9;

        var health = root.AddComponent<EnemyHealth>();
        health.maxHp = hp;
        health.body = body;
        health.fireHitFx = _fxFireHit;
        health.shadowHitFx = _fxShadowHit;
        health.deathFx = _fxDeath;
        health.emberDrop = _emberDrop;
        health.soulPrefab = _soul;
        health.fxOffset = hurtOffset;

        // Cyan "exposed" marker (GDD combat grammar step 2): call the sister now.
        var marker = new GameObject("Exposed Marker");
        marker.transform.SetParent(root.transform, false);
        marker.transform.localPosition = new Vector3(0f, hurtOffset.y, 0f);
        var msr = marker.AddComponent<SpriteRenderer>();
        var freeze = Sheet("fx_freeze");
        msr.sprite = freeze.Length > 0 ? freeze[0] : null;
        msr.sharedMaterial = _neonAdd;
        msr.color = new Color(0.45f, 0.95f, 1f, 0.9f);
        msr.sortingOrder = 15;
        marker.transform.localScale = Vector3.one * (1.8f / Mathf.Max(0.01f, msr.sprite ? msr.sprite.bounds.size.x : 1f));   // ~1.8 units: a clear cue, not a cloud
        var anim = marker.AddComponent<SpriteOneShot>();
        anim.frames = freeze;
        anim.fps = 30f;
        anim.loop = true;
        var ml = LocalLight("Exposed Light", marker.transform, Vector2.zero, new Color(0.35f, 0.9f, 1f), 2.6f, 1.6f);
        ml.transform.localPosition = Vector3.zero;
        health.exposedMarker = marker;

        return (root, body, health);
    }

    static void Brain(EnemyBrain brain, SpriteRenderer body, BoxCollider2D hurt)
    {
        brain.body = body;
        brain.rim = body.transform.Find("Rim").GetComponent<SpriteRenderer>();
        brain.hurtBox = hurt;
    }

    // ------------------------------------------------------------------ the roster

    static EnemyHealth SpawnRat(Transform parent, float x, float patrolMin, float patrolMax, float speedBoost = 1f)
    {
        var (root, body, hp) = EnemyBody(parent, $"Ember Rat x{x}", new Vector2(x, ASurf(x)), "en_rat_idle_0", 2.4f,
                                         new Color(0.64f, 0.57f, 0.64f), new Color(1f, 0.38f, 0.12f, 0.6f), new Vector2(2.1f, 1.55f), new Vector2(0f, 0.78f), 3);
        var ai = root.AddComponent<EmberRatAI>();
        Brain(ai, body, root.GetComponent<BoxCollider2D>());
        ai.idleFrames = Sheet("en_rat_idle");
        ai.runFrames = Sheet("en_rat_run");
        ai.attackFrames = Sheet("en_rat_attack");
        ai.hurtFrames = Sheet("en_rat_hurt");
        ai.deathFrames = Sheet("en_rat_death");
        ai.patrolMin = patrolMin;
        ai.patrolMax = patrolMax;
        ai.rushSpeed *= speedBoost;

        // Burning tail, ember eyes, a little warm light: the black body with orange cracks from the GDD.
        var tail = Particles("Tail Fire", Vector2.zero, root.transform, _fireSquare, 11, 22f, new Vector2(0.25f, 0.5f), new Vector2(0.6f, 1.6f),
                             new Vector2(0.12f, 0.24f), FireGradient(), 0.12f, -0.1f, 0.4f, 60);
        tail.transform.localPosition = new Vector3(-1.15f, 0.75f, 0f);
        var tm = tail.main; tm.simulationSpace = ParticleSystemSimulationSpace.World;
        ai.tailFire = tail;
        ai.eyeGlow = LocalGlow("Eye Glow", root.transform, new Vector2(0.98f, 0.72f), new Vector2(0.7f, 0.7f), new Color(1f, 0.45f, 0.15f, 0.5f), 12, _neonSoft);
        ai.emberLight = LocalLight("Ember Light", root.transform, new Vector2(0f, 0.6f), new Color(1f, 0.5f, 0.2f), 2.6f, 0.8f);
        var sparks = OneShot("Telegraph Sparks", root.transform, new Vector2(0.4f, 0.4f), _emberSquare, 12, 14, new Vector2(0.2f, 0.45f), new Vector2(1.5f, 3.5f),
                             new Vector2(0.05f, 0.09f), Grad((FireCore, 0f, 1f), (FireOrange, 1f, 0f)), 0.3f, 160f, 0.6f, 0.2f);
        var sm = sparks.main; sm.playOnAwake = false;
        ai.telegraphSparks = sparks;
        return hp;
    }

    static EnemyHealth SpawnEye(Transform parent, float x, float y)
    {
        var (root, body, hp) = EnemyBody(parent, $"Flying Eye x{x}", new Vector2(x, y), "en_eye_fly_3", 1.45f,
                                         new Color(0.85f, 0.78f, 0.85f), new Color(1f, 0.2f, 0.35f, 0.5f), new Vector2(1.2f, 1.1f), Vector2.zero, 2);
        var ai = root.AddComponent<FlyingEyeAI>();
        Brain(ai, body, root.GetComponent<BoxCollider2D>());
        var f = Sheet("en_eye_fly");
        ai.flyFrames = f.Length >= 6 ? new[] { f[3], f[4], f[5], f[4] } : f;
        ai.curlFrames = f.Take(3).ToArray();
        ai.shotPrefab = _eyeShot;
        ai.chargeGlow = LocalGlow("Charge Glow", root.transform, new Vector2(0.35f, 0f), new Vector2(1f, 1f), new Color(1f, 0.25f, 0.2f, 0f), 12, _neonAdd);
        ai.eyeLight = LocalLight("Eye Light", root.transform, Vector2.zero, new Color(1f, 0.3f, 0.3f), 2.4f, 0.8f);
        return hp;
    }

    static EnemyHealth SpawnGoblin(Transform parent, float x, float? y = null)
    {
        var (root, body, hp) = EnemyBody(parent, $"Goblin Bomber x{x}", new Vector2(x, y ?? ASurf(x)), "en_gob_attack_0", 1.35f,
                                         new Color(0.82f, 0.8f, 0.78f), new Color(0.5f, 1f, 0.3f, 0.45f), new Vector2(0.9f, 1.5f), new Vector2(0f, 0.75f), 3);
        var ai = root.AddComponent<GoblinAI>();
        Brain(ai, body, root.GetComponent<BoxCollider2D>());
        ai.attackFrames = Sheet("en_gob_attack");
        ai.bombPrefab = _bomb;
        LocalLight("Goblin Light", root.transform, new Vector2(0f, 1f), new Color(0.7f, 1f, 0.5f), 2.2f, 0.5f);
        return hp;
    }

    // ------------------------------------------------------------------ ambush arena

    static ArenaLock ArenaAt(Transform parent, float x0, float x1, float groundY, string title, EnemyHealth[] wave1, EnemyHealth[] wave2, bool lockBehind = false)
    {
        var root = new GameObject($"Arena: {title} (x{x0:0}-{x1:0})");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3((x0 + x1) / 2f, groundY, 0f);
        var trig = root.AddComponent<BoxCollider2D>();
        trig.isTrigger = true;
        trig.size = new Vector2(x1 - x0 - 5f, 6f);
        trig.offset = new Vector2(0f, 3f);
        var arena = root.AddComponent<ArenaLock>();
        arena.title = title;

        var walls = new List<Collider2D>();
        var fires = new List<ParticleSystem>();
        var lights = new List<Light2D>();
        // Fire wall ahead only: you may fall back and the enemies follow you. Boss arenas seal both sides.
        foreach (var wx in lockBehind ? new[] { x0, x1 } : new[] { x1 })
        {
            var w = new GameObject("Fire Wall");
            w.transform.SetParent(root.transform, false);
            w.transform.position = new Vector3(wx, groundY, 0f);
            var box = w.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 14f);
            box.offset = new Vector2(0f, 7f);
            walls.Add(box);
            var fire = Particles("Wall Fire", new Vector2(wx, groundY), w.transform, _fireSquare, 12, 90f, new Vector2(0.5f, 1.1f), new Vector2(2f, 4.5f),
                                 new Vector2(0.25f, 0.55f), FireGradient(), 0f, -0.2f, 0.5f, 400);
            var fs = fire.shape; fs.shapeType = ParticleSystemShapeType.Box; fs.scale = new Vector3(0.9f, 0.4f, 0.1f); fs.rotation = new Vector3(-90f, 0f, 0f);
            var fm = fire.main; fm.prewarm = false; fm.playOnAwake = false;
            fires.Add(fire);
            var l = LocalLight("Wall Light", w.transform, new Vector2(0f, 2.5f), new Color(1f, 0.45f, 0.15f), 6f, 2.2f);
            l.gameObject.AddComponent<FlickerLight2D>();
            lights.Add(l);
        }
        arena.walls = walls.ToArray();
        arena.wallFire = fires.ToArray();
        arena.wallLights = lights.ToArray();
        arena.wave1 = wave1;
        arena.wave2 = wave2;
        foreach (var e in wave1.Concat(wave2))
        {
            if (!e) continue;
            if (e.TryGetComponent<EmberRatAI>(out var rat)) { rat.patrolMin = 0f; rat.patrolMax = MapWidth; }
            if (e.TryGetComponent<SkeletonAI>(out var sk)) { sk.patrolMin = 0f; sk.patrolMax = MapWidth; }
            if (e.TryGetComponent<FlyingEyeAI>(out var eye)) eye.leash = new Vector2(40f, 6f);
        }
        arena.spawnFx = _fxSpawn;
        arena.clearFx = _fxClear;
        return arena;
    }
}
