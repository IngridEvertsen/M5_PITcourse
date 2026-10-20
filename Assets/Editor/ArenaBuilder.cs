using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Tools > Ball Game > Build Basketball Arena
/// Builds an indoor basketball arena around the player: wooden court with lines, a hoop with rim and net
/// (wired to ScoreManager), tribunes with a cheering crowd, walls, ceiling lights and a scoreboard.
/// Running it again deletes the old "Arena" and builds a fresh one.
/// </summary>
public static class ArenaBuilder
{
    const string MatFolder = "Assets/Materials/Arena";

    // Layout (player starts at 0,0,0 looking towards +Z)
    const float CourtHalfWidth = 7.5f;   // court is 15 m wide
    const float BaselineZ = 4.7f;        // baseline behind the hoop
    const float CourtLength = 28f;       // real court length
    const float RimHeight = 2.6f;        // real is 3.05 m - lowered so it's easier to hit in the simulator
    const float BackboardZ = 3.5f;
    const float RimRadius = 0.23f;
    const int Rows = 5;                  // tribune rows per side

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    [MenuItem("Tools/Ball Game/Build Basketball Arena")]
    public static void Build()
    {
        mats.Clear();
        EnsureFolder(MatFolder);

        var old = GameObject.Find("Arena");
        if (old != null) Object.DestroyImmediate(old);

        var arena = new GameObject("Arena");
        Undo.RegisterCreatedObjectUndo(arena, "Build Arena");

        float courtCenterZ = BaselineZ - CourtLength / 2f;

        BuildCourt(arena.transform, courtCenterZ);
        BuildLines(arena.transform, courtCenterZ);
        Transform goalZone = BuildHoop(arena.transform);
        Transform crowd = BuildTribunes(arena.transform, courtCenterZ);
        BuildWallsAndCeiling(arena.transform, courtCenterZ);
        BuildDramaticLights(arena.transform, courtCenterZ);
        BuildScoreboard(arena.transform);
        RestyleExisting();
        WireGame(goalZone, crowd);
        ApplyBallSettings();

        // dark, dramatic hall: very little ambient light, the court is lit by spots
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.23f);
        // the court is one big object lit by many spots - allow enough per-pixel lights or most spots disappear
        if (QualitySettings.pixelLightCount < 16) QualitySettings.pixelLightCount = 16;
        // indoor hall: don't reflect the outdoor blue sky in shiny/metal surfaces
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        RenderSettings.reflectionIntensity = 0.3f;
        EditorSceneManager.MarkSceneDirty(arena.scene);
        Selection.activeGameObject = arena;
        Debug.Log("[ArenaBuilder] Arena built. Save the scene (Cmd+S).");
    }

    // ---------------- Court ----------------

    static void BuildCourt(Transform root, float centerZ)
    {
        var court = Box("Court", root, new Vector3(0, -0.05f, centerZ),
            new Vector3(CourtHalfWidth * 2 + 4f, 0.1f, CourtLength + 4f), WoodMaterial());
        court.isStatic = true;

        // darker run-off area around the court
        var outer = Box("Floor_Outer", root, new Vector3(0, -0.06f, centerZ),
            new Vector3(60f, 0.1f, 60f), Mat("ArenaFloor", new Color(0.18f, 0.2f, 0.26f), 0.3f));
        outer.isStatic = true;

        // paint the key (lane) in team colour
        var key = Box("Key_Paint", root, new Vector3(0, 0.002f, BaselineZ - 2.9f),
            new Vector3(4.9f, 0.004f, 5.8f), Mat("KeyPaint", new Color(0.12f, 0.32f, 0.62f), 0.5f));
        Object.DestroyImmediate(key.GetComponent<Collider>());

        var circle = Disc("CenterCircle_Paint", root, new Vector3(0, 0.002f, centerZ), 1.8f,
            Mat("KeyPaint", new Color(0.12f, 0.32f, 0.62f), 0.5f));
        circle.transform.SetParent(root, true);
    }

    static void BuildLines(Transform root, float centerZ)
    {
        var lines = new GameObject("Lines").transform;
        lines.SetParent(root, false);
        var white = Mat("Lines", Color.white, 0.4f);
        float y = 0.006f, w = 0.06f;
        float farZ = BaselineZ - CourtLength;

        Line(lines, white, new Vector3(-CourtHalfWidth, y, centerZ), new Vector3(w, 0.004f, CourtLength)); // sidelines
        Line(lines, white, new Vector3(CourtHalfWidth, y, centerZ), new Vector3(w, 0.004f, CourtLength));
        Line(lines, white, new Vector3(0, y, BaselineZ), new Vector3(CourtHalfWidth * 2, 0.004f, w));     // baselines
        Line(lines, white, new Vector3(0, y, farZ), new Vector3(CourtHalfWidth * 2, 0.004f, w));
        Line(lines, white, new Vector3(0, y, centerZ), new Vector3(CourtHalfWidth * 2, 0.004f, w));       // half-court

        // key outline
        float keyFront = BaselineZ - 5.8f;
        Line(lines, white, new Vector3(-2.45f, y, BaselineZ - 2.9f), new Vector3(w, 0.004f, 5.8f));
        Line(lines, white, new Vector3(2.45f, y, BaselineZ - 2.9f), new Vector3(w, 0.004f, 5.8f));
        Line(lines, white, new Vector3(0, y, keyFront), new Vector3(4.9f, 0.004f, w));

        Ring(lines, white, new Vector3(0, y, centerZ), 1.8f, 40);                 // centre circle
        Ring(lines, white, new Vector3(0, y, keyFront), 1.8f, 40, 0f, 180f, true); // free-throw half circle
        // three-point arc around the rim
        Ring(lines, white, new Vector3(0, y, BackboardZ - 0.4f), 6.75f, 60, 15f, 165f, true);
    }

    // ---------------- Hoop ----------------

    static Transform BuildHoop(Transform root)
    {
        var hoop = new GameObject("Hoop").transform;
        hoop.SetParent(root, false);

        var dark = Mat("HoopMetal", new Color(0.06f, 0.06f, 0.07f), 0.6f);   // metallic black
        var orange = Mat("Rim", new Color(1f, 0.35f, 0.05f), 0.7f);
        var glass = Mat("Backboard", new Color(0.95f, 0.97f, 1f), 0.9f);

        // pole + arm
        Cyl("Pole", hoop, new Vector3(0, (RimHeight + 0.6f) / 2f, BaselineZ + 0.6f),
            new Vector3(0.18f, (RimHeight + 0.6f) / 2f, 0.18f), dark);
        Box("Arm", hoop, new Vector3(0, RimHeight + 0.45f, (BaselineZ + 0.6f + BackboardZ) / 2f),
            new Vector3(0.15f, 0.15f, BaselineZ + 0.6f - BackboardZ), dark);
        Box("Base", hoop, new Vector3(0, 0.3f, BaselineZ + 0.6f), new Vector3(1.2f, 0.6f, 1.2f), dark); // same metallic black as the pole

        // backboard with orange target square
        Box("Backboard", hoop, new Vector3(0, RimHeight + 0.4f, BackboardZ), new Vector3(1.8f, 1.05f, 0.05f), glass);
        float sqY = RimHeight + 0.3f, sqZ = BackboardZ - 0.03f;
        Box("Square_Top", hoop, new Vector3(0, sqY + 0.225f, sqZ), new Vector3(0.59f, 0.05f, 0.01f), orange);
        Box("Square_Bottom", hoop, new Vector3(0, sqY - 0.225f, sqZ), new Vector3(0.59f, 0.05f, 0.01f), orange);
        Box("Square_Left", hoop, new Vector3(-0.27f, sqY, sqZ), new Vector3(0.05f, 0.45f, 0.01f), orange);
        Box("Square_Right", hoop, new Vector3(0.27f, sqY, sqZ), new Vector3(0.05f, 0.45f, 0.01f), orange);

        // rim (ring of small capsules so the ball can bounce off it)
        Vector3 rimCenter = new Vector3(0, RimHeight, BackboardZ - 0.38f);
        var rim = new GameObject("Rim").transform;
        rim.SetParent(hoop, false);
        int segs = 20;
        float segLen = 2f * Mathf.PI * RimRadius / segs;
        for (int i = 0; i < segs; i++)
        {
            float a = i * Mathf.PI * 2f / segs;
            var p = rimCenter + new Vector3(Mathf.Cos(a) * RimRadius, 0, Mathf.Sin(a) * RimRadius);
            var c = Cyl("RimSeg", rim, p, new Vector3(0.025f, segLen * 0.6f, 0.025f), orange);
            // orient the segment along the ring's tangent
            c.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
        }
        Box("RimBracket", hoop, new Vector3(0, RimHeight, BackboardZ - 0.08f), new Vector3(0.12f, 0.03f, 0.12f), orange);

        // net: white lines from the rim down to a smaller ring
        var net = new GameObject("Net").transform;
        net.SetParent(hoop, false);
        var netMat = Mat("Net", new Color(1f, 1f, 1f), 0f);
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2f / 16;
            float a2 = a + Mathf.PI / 8f;
            var top = rimCenter + new Vector3(Mathf.Cos(a) * RimRadius, 0, Mathf.Sin(a) * RimRadius);
            var bottom = rimCenter + new Vector3(Mathf.Cos(a2) * RimRadius * 0.6f, -0.42f, Mathf.Sin(a2) * RimRadius * 0.6f);
            var lr = new GameObject("NetCord").AddComponent<LineRenderer>();
            lr.transform.SetParent(net, false);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPositions(new[] { top, bottom });
            lr.startWidth = lr.endWidth = 0.012f;
            lr.sharedMaterial = netMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // the point the ball's aim assist aims for
        var aim = new GameObject("AimTarget").transform;
        aim.SetParent(hoop, false);
        aim.position = rimCenter + new Vector3(0, 0.05f, 0);

        // where the ball comes back after leaving the court (centre of the court, dropped from 1.5 m)
        var respawn = new GameObject("BallRespawn").transform;
        respawn.SetParent(root, false);
        respawn.position = new Vector3(0, 1.5f, BaselineZ - CourtLength / 2f);

        // invisible score trigger just under the rim
        var goal = Box("GoalZone", hoop, rimCenter + new Vector3(0, -0.18f, 0), new Vector3(0.3f, 0.12f, 0.3f), null);
        goal.GetComponent<BoxCollider>().isTrigger = true;
        goal.GetComponent<MeshRenderer>().enabled = false;
        goal.AddComponent<GoalTrigger>();

        // confetti effect above the rim
        var fx = new GameObject("ScoreConfetti").AddComponent<ParticleSystem>();
        fx.transform.SetParent(hoop, false);
        fx.transform.position = rimCenter + Vector3.up * 0.3f;
        fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = fx.main;
        main.loop = false; main.playOnAwake = false; main.duration = 1f;
        main.startLifetime = 2f; main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.gravityModifier = 0.8f; main.maxParticles = 300;

        // gold and silver: pick one of the two colours per particle (no in-between tones)
        var goldSilver = new Gradient();
        goldSilver.mode = GradientMode.Fixed;
        goldSilver.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.76f, 0.25f), 0.5f),     // gold
                    new GradientColorKey(new Color(0.86f, 0.88f, 0.92f), 1f) },  // silver
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(goldSilver) { mode = ParticleSystemGradientMode.RandomColor };

        var em = fx.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 150) });
        var sh = fx.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.1f;
        fx.transform.rotation = Quaternion.Euler(-90, 0, 0);

        // shrink away at the end instead of popping out
        var sol = fx.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

        // render as small shiny 3D balls instead of flat squares
        var pr = fx.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Mesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pr.mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        pr.sharedMaterial = ConfettiMaterial();

        return goal.transform;
    }

    // ---------------- Tribunes & crowd ----------------

    static Transform BuildTribunes(Transform root, float centerZ)
    {
        var tribunes = new GameObject("Tribunes").transform;
        tribunes.SetParent(root, false);
        var crowdRoot = new GameObject("Crowd").transform;
        crowdRoot.SetParent(root, false);
        var crowd = crowdRoot.gameObject.AddComponent<Crowd>();

        var seatMat = Mat("TribuneSeats", new Color(0.02f, 0.02f, 0.02f), 0.35f);   // black seats
        var stepMat = Mat("TribuneConcrete", new Color(0.08f, 0.08f, 0.09f), 0.1f);  // almost black
        Color[] shirts = {
            new Color(0.94f, 0.92f, 0.87f),  // off-white
            new Color(0.72f, 0.84f, 0.94f),  // pale light blue
            new Color(0.42f, 0.53f, 0.64f),  // deeper ashy blue
            new Color(0.96f, 0.80f, 0.84f),  // pale baby pink
            new Color(0.30f, 0.18f, 0.12f),  // deep brown
            new Color(0.55f, 0.07f, 0.20f),  // berry red
            new Color(0.07f, 0.11f, 0.25f),  // deep navy
            new Color(0.74f, 0.45f, 0.45f),  // rustic pink
            new Color(0.97f, 0.91f, 0.68f),  // cream yellow
        };
        Color[] skins = { new Color(0.96f,0.8f,0.69f), new Color(0.87f,0.67f,0.5f),
                          new Color(0.6f,0.42f,0.3f), new Color(0.36f,0.24f,0.17f) };

        var rng = new System.Random(42);
        int rows = Rows;
        float rowDepth = 0.9f, rowRise = 0.45f;
        float length = CourtLength - 2f;

        foreach (int side in new[] { -1, 1 })
        {
            for (int r = 0; r < rows; r++)
            {
                float x = side * (CourtHalfWidth + 2.5f + r * rowDepth);
                float top = 0.4f + r * rowRise;
                var step = Box($"Step_{(side < 0 ? "L" : "R")}{r}", tribunes,
                    new Vector3(x, top / 2f, centerZ), new Vector3(rowDepth, top, length), stepMat);
                step.isStatic = true;
                var seat = Box($"Seats_{(side < 0 ? "L" : "R")}{r}", tribunes,
                    new Vector3(x - side * 0.2f, top + 0.12f, centerZ), new Vector3(0.45f, 0.25f, length), seatMat);
                seat.isStatic = true;

                // spectators
                for (float z = centerZ - length / 2f + 0.5f; z < centerZ + length / 2f - 0.5f; z += 0.75f)
                {
                    if (rng.NextDouble() < 0.15) continue; // a few empty seats
                    var person = new GameObject("Fan").transform;
                    person.SetParent(crowdRoot, false);
                    person.position = new Vector3(x - side * 0.2f, top + 0.25f, z + (float)(rng.NextDouble() * 0.15));
                    person.rotation = Quaternion.LookRotation(new Vector3(-side, 0, 0));

                    int si = rng.Next(shirts.Length);
                    var shirt = Mat($"Shirt{si}", shirts[si], 0.2f);
                    int ki = rng.Next(skins.Length);
                    var skin = Mat($"Skin{ki}", skins[ki], 0.3f);

                    var body = Prim(PrimitiveType.Capsule, "Body", person, new Vector3(0, 0.45f, 0), new Vector3(0.38f, 0.42f, 0.3f), shirt);
                    var head = Prim(PrimitiveType.Sphere, "Head", person, new Vector3(0, 1.0f, 0), Vector3.one * 0.24f, skin);
                    Object.DestroyImmediate(body.GetComponent<Collider>());
                    Object.DestroyImmediate(head.GetComponent<Collider>());
                    // raised arm on some fans
                    if (rng.NextDouble() < 0.3)
                    {
                        var arm = Prim(PrimitiveType.Capsule, "Arm", person, new Vector3(0.22f, 0.95f, 0), new Vector3(0.09f, 0.3f, 0.09f), shirt);
                        arm.transform.localRotation = Quaternion.Euler(0, 0, -20);
                        Object.DestroyImmediate(arm.GetComponent<Collider>());
                    }
                }
            }
        }
        return crowd.transform;
    }

    // ---------------- Walls, ceiling, lights ----------------

    static void BuildWallsAndCeiling(Transform root, float centerZ)
    {
        var shell = new GameObject("Hall").transform;
        shell.SetParent(root, false);
        var wall = Mat("HallWall", new Color(0.16f, 0.18f, 0.24f), 0.2f);
        float halfW = CourtHalfWidth + 2.5f + Rows * 0.9f + 0.5f;
        float halfL = CourtLength / 2f + 5f;
        float h = 10f;

        Box("Wall_Left", shell, new Vector3(-halfW, h / 2, centerZ), new Vector3(0.3f, h, halfL * 2), wall).isStatic = true;
        Box("Wall_Right", shell, new Vector3(halfW, h / 2, centerZ), new Vector3(0.3f, h, halfL * 2), wall).isStatic = true;
        Box("Wall_Front", shell, new Vector3(0, h / 2, centerZ + halfL), new Vector3(halfW * 2, h, 0.3f), wall).isStatic = true;
        Box("Wall_Back", shell, new Vector3(0, h / 2, centerZ - halfL), new Vector3(halfW * 2, h, 0.3f), wall).isStatic = true;
        Box("Ceiling", shell, new Vector3(0, h, centerZ), new Vector3(halfW * 2, 0.3f, halfL * 2),
            Mat("Ceiling", new Color(0.08f, 0.08f, 0.1f), 0f)).isStatic = true;
        // the hall must not throw shadows from the sun onto the court
        foreach (var r in shell.GetComponentsInChildren<MeshRenderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // banners on the end wall
        Box("Banner_1", shell, new Vector3(-4f, 6.5f, centerZ + halfL - 0.2f), new Vector3(2.5f, 3f, 0.05f), Mat("BannerBlue", new Color(0.04f, 0.09f, 0.24f), 0.1f));  // dark blue
        Box("Banner_2", shell, new Vector3(4f, 6.5f, centerZ + halfL - 0.2f), new Vector3(2.5f, 3f, 0.05f), Mat("BannerBerry", new Color(0.55f, 0.07f, 0.20f), 0.1f));  // berry red

        // team texts on the banners: HOME (berry, shows your score) and GUEST (blue)
        float bannerZ = centerZ + halfL - 0.2f - 0.04f;
        BannerText("HomeLabel", shell, "HOME", new Vector3(4f, 7.55f, bannerZ), new Vector2(2.2f, 0.6f), new Color(0.97f, 0.91f, 0.68f));
        BannerText("HomeScore", shell, "0", new Vector3(4f, 6.1f, bannerZ), new Vector2(2.2f, 2f), Color.white);
        BannerText("GuestLabel", shell, "GUEST", new Vector3(-4f, 7.55f, bannerZ), new Vector2(2.2f, 0.6f), new Color(0.72f, 0.84f, 0.94f));
        BannerText("GuestScore", shell, "0", new Vector3(-4f, 6.1f, bannerZ), new Vector2(2.2f, 2f), Color.white);

        // light the end wall behind the hoop so the banners are visible
        foreach (float bx in new[] { -4f, 4f })
        {
            var bl = new GameObject("BannerSpot").AddComponent<Light>();
            bl.transform.SetParent(shell, false);
            bl.transform.position = new Vector3(bx, h - 0.8f, centerZ + halfL - 6f);
            bl.transform.LookAt(new Vector3(bx, 6.5f, centerZ + halfL));
            bl.type = LightType.Spot; bl.spotAngle = 32f; bl.range = 14f; bl.intensity = 2.5f;
            bl.color = new Color(1f, 0.95f, 0.88f); bl.shadows = LightShadows.None;
        }
        var wash = new GameObject("EndWallWash").AddComponent<Light>();
        wash.transform.SetParent(shell, false);
        wash.transform.position = new Vector3(0, h - 0.8f, centerZ + halfL - 7f);
        wash.transform.LookAt(new Vector3(0, 4f, centerZ + halfL));
        wash.type = LightType.Spot; wash.spotAngle = 80f; wash.range = 16f; wash.intensity = 1.2f;
        wash.color = new Color(0.85f, 0.88f, 1f); wash.shadows = LightShadows.None;

        // ceiling lights: bright fixtures + spot lights over the court
        var lights = new GameObject("Lights").transform;
        lights.SetParent(root, false);
        var lamp = Mat("LampGlow", Color.white, 0f, false, true);
        for (int i = 0; i < 4; i++)
        {
            float z = BaselineZ - 3f - i * 7f;
            foreach (float x in new[] { -4f, 4f })
            {
                Box("LampFixture", lights, new Vector3(x, h - 0.4f, z), new Vector3(1.5f, 0.1f, 0.6f), lamp);
                var l = new GameObject("SpotLight").AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.position = new Vector3(x, h - 0.6f, z);
                l.transform.rotation = Quaternion.Euler(90, 0, 0);
                l.type = LightType.Spot; l.spotAngle = 70f; l.innerSpotAngle = 40f; l.range = 20f; l.intensity = 2.2f; // tight cones on the court
                l.color = new Color(1f, 0.96f, 0.9f);
                l.shadows = i <= 1 ? LightShadows.Soft : LightShadows.None; // shadows on the half near the hoop
            }
        }
    }

    static void BuildDramaticLights(Transform root, float centerZ)
    {
        var lights = root.Find("Lights");
        // hero spot on the hoop and backboard
        var hero = new GameObject("HoopSpot").AddComponent<Light>();
        hero.transform.SetParent(lights, false);
        hero.transform.position = new Vector3(0, 9.3f, BackboardZ - 4f);
        hero.transform.LookAt(new Vector3(0, RimHeight, BackboardZ));
        hero.type = LightType.Spot; hero.spotAngle = 28f; hero.innerSpotAngle = 15f;
        hero.range = 16f; hero.intensity = 2.5f; hero.color = new Color(1f, 0.93f, 0.82f);
        hero.shadows = LightShadows.Soft;

        // very dim, cool fill over the stands so the crowd is visible but in shadow
        float x = CourtHalfWidth + 2.5f + Rows * 0.45f;
        foreach (int side in new[] { -1, 1 })
            for (int i = 0; i < 3; i++)
            {
                var fill = new GameObject("CrowdFill").AddComponent<Light>();
                fill.transform.SetParent(lights, false);
                fill.transform.position = new Vector3(side * x, 6f, centerZ + (i - 1) * 9f);
                fill.type = LightType.Point; fill.range = 12f; fill.intensity = 1.2f;
                fill.color = new Color(0.55f, 0.62f, 0.85f);
                fill.shadows = LightShadows.None;
            }
    }

    // ---------------- Scoreboard ----------------

    static void BuildScoreboard(Transform root)
    {
        var board = new GameObject("Scoreboard").transform;
        board.SetParent(root, false);
        Box("ScoreboardPanel", board, new Vector3(0, 5f, BaselineZ + 0.8f), new Vector3(3.6f, 1.6f, 0.2f),
            Mat("ScoreboardBlack", new Color(0.03f, 0.03f, 0.04f), 0.6f));
        // neon tube frame around the screen
        var neon = NeonMaterial(new Color(1f, 0.2f, 0.55f));   // neon pink
        float fz = BaselineZ + 0.68f, cy = 5f;
        Box("Neon_Top", board, new Vector3(0, cy + 0.86f, fz), new Vector3(3.76f, 0.06f, 0.06f), neon);
        Box("Neon_Bottom", board, new Vector3(0, cy - 0.86f, fz), new Vector3(3.76f, 0.06f, 0.06f), neon);
        Box("Neon_Left", board, new Vector3(-1.85f, cy, fz), new Vector3(0.06f, 1.78f, 0.06f), neon);
        Box("Neon_Right", board, new Vector3(1.85f, cy, fz), new Vector3(0.06f, 1.78f, 0.06f), neon);
        foreach (var r in board.GetComponentsInChildren<MeshRenderer>())
            if (r.name.StartsWith("Neon")) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var glow = new GameObject("NeonGlow").AddComponent<Light>();
        glow.transform.SetParent(board, false);
        glow.transform.position = new Vector3(0, cy, BaselineZ + 0.3f);
        glow.type = LightType.Point; glow.range = 3.5f; glow.intensity = 1.5f;
        glow.color = new Color(1f, 0.2f, 0.55f); glow.shadows = LightShadows.None;
        Cyl("Cable_L", board, new Vector3(-1.5f, 7.5f, BaselineZ + 0.8f), new Vector3(0.03f, 1.7f, 0.03f), Mat("HoopMetal", Color.gray, 0.6f));
        Cyl("Cable_R", board, new Vector3(1.5f, 7.5f, BaselineZ + 0.8f), new Vector3(0.03f, 1.7f, 0.03f), Mat("HoopMetal", Color.gray, 0.6f));
    }

    // ---------------- Existing objects + wiring ----------------

    static void RestyleExisting()
    {
        // move the old box-basket out of the way (kept, just switched off)
        var basket = GameObject.Find("Basket");
        if (basket != null) basket.SetActive(false);
        var plane = GameObject.Find("Floor");
        if (plane != null) plane.SetActive(false);

        // the table becomes a ball rack
        var table = GameObject.Find("Cube");
        if (table != null)
        {
            table.name = "BallRack";
            table.GetComponent<Renderer>().sharedMaterial = Mat("HoopMetal", new Color(0.15f, 0.15f, 0.17f), 0.6f);
        }

        // basketball look
        var ball = GameObject.Find("Ball");
        if (ball != null) ball.GetComponent<Renderer>().sharedMaterial = BasketballMaterial();

        // score texts onto the scoreboard
        var score = GameObject.Find("ScoreText");
        if (score != null)
        {
            score.transform.position = new Vector3(0, 5.15f, BaselineZ + 0.68f);
            score.transform.rotation = Quaternion.identity;
            var t = score.GetComponent<TMP_Text>();
            if (t != null) { t.enableAutoSizing = true; t.fontSizeMin = 0.1f; t.fontSizeMax = 20f; t.alignment = TextAlignmentOptions.Center; t.color = new Color(1f, 0.75f, 0.1f);
                t.rectTransform.sizeDelta = new Vector2(3.4f, 1.4f); }
        }
        // MessageText: keep wherever you placed it, just style it - one line, auto-shrinks to fit
        var msg = GameObject.Find("MessageText");
        if (msg != null)
        {
            var t = msg.GetComponent<TMP_Text>();
            if (t != null)
            {
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.enableAutoSizing = true; t.fontSizeMin = 0.1f; t.fontSizeMax = 2.5f;
                t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
                EditorUtility.SetDirty(t);
            }
        }

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            var l = sun.GetComponent<Light>();
            l.intensity = 0.2f; l.color = new Color(0.85f, 0.88f, 0.95f); // indoor hall: the "sun" is almost off
            sun.transform.rotation = Quaternion.Euler(60, -30, 0);
        }
    }

    static void WireGame(Transform goalZone, Transform crowdTransform)
    {
        var manager = Object.FindFirstObjectByType<ScoreManager>();
        if (manager == null) { Debug.LogWarning("[ArenaBuilder] No ScoreManager found - add one to GameManager."); return; }

        goalZone.GetComponent<GoalTrigger>().scoreManager = manager;
        var home = GameObject.Find("HomeScore");
        if (home != null) manager.bannerScoreText = home.GetComponent<TMP_Text>();

        var fx = goalZone.parent.Find("ScoreConfetti");
        if (fx != null) manager.scoreEffect = fx.GetComponent<ParticleSystem>();

        // applause sound
        if (manager.scoreSound != null && manager.scoreSound.clip == null)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Applause.wav");
            if (clip != null) manager.scoreSound.clip = clip;
        }

        // crowd cheers on every score (remove old Crowd listeners first so re-building doesn't stack them)
        var crowd = crowdTransform.GetComponent<Crowd>();
        if (manager.onScore == null) manager.onScore = new UnityEngine.Events.UnityEvent();
        for (int i = manager.onScore.GetPersistentEventCount() - 1; i >= 0; i--)
            if (manager.onScore.GetPersistentMethodName(i) == nameof(Crowd.Cheer))
                UnityEventTools.RemovePersistentListener(manager.onScore, i);
        UnityEventTools.AddPersistentListener(manager.onScore, crowd.Cheer);

        EditorUtility.SetDirty(manager);
    }

    // ---------------- Ball physics ----------------

    [MenuItem("Tools/Ball Game/Apply Ball Settings")]
    public static void ApplyBallSettings()
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/BasketballBounce.asset";
        var bounce = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (bounce == null)
        {
            bounce = new PhysicsMaterial("BasketballBounce");
            AssetDatabase.CreateAsset(bounce, path);
        }
        bounce.bounciness = 0.82f;                               // a real basketball bounces back ~75-85 %
        bounce.dynamicFriction = 0.5f;
        bounce.staticFriction = 0.6f;
        bounce.bounceCombine = PhysicsMaterialCombine.Maximum;   // the ball's bounce wins over the floor's
        bounce.frictionCombine = PhysicsMaterialCombine.Average;
        EditorUtility.SetDirty(bounce);

        var aim = GameObject.Find("AimTarget");
        var respawn = GameObject.Find("BallRespawn");
        float courtCenterZ = BaselineZ - CourtLength / 2f;

        var balls = Object.FindObjectsByType<Ball>(FindObjectsSortMode.None);
        foreach (var ball in balls)
        {
            Undo.RecordObject(ball, "Ball settings");
            ball.aimTarget = aim != null ? aim.transform : null;
            ball.respawnPoint = respawn != null ? respawn.transform : null;
            ball.courtMinX = -CourtHalfWidth; ball.courtMaxX = CourtHalfWidth;
            ball.courtMinZ = courtCenterZ - CourtLength / 2f; ball.courtMaxZ = BaselineZ;
            EditorUtility.SetDirty(ball);

            var col = ball.GetComponent<SphereCollider>();
            if (col != null) { Undo.RecordObject(col, "Ball settings"); col.sharedMaterial = bounce; EditorUtility.SetDirty(col); }

            var rb = ball.GetComponent<Rigidbody>();
            Undo.RecordObject(rb, "Ball settings");
            rb.mass = 0.6f;                 // a basketball weighs ~600 g
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            EditorUtility.SetDirty(rb);

            var grab = ball.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                Undo.RecordObject(grab, "Ball settings");
                grab.throwOnDetach = true;
                grab.throwVelocityScale = 1f;  // was 1.5 - the ball flew too far
                EditorUtility.SetDirty(grab);
            }
        }

        // the rim and backboard should give a little bounce as well
        foreach (var n in new[] { "Backboard", "RimSeg" })
            foreach (var go in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (go.name == n) go.sharedMaterial = bounce;

        if (aim == null || respawn == null)
            Debug.LogWarning("[ArenaBuilder] AimTarget/BallRespawn not found - run 'Build Basketball Arena' first.");
        Debug.Log($"[ArenaBuilder] Ball settings applied to {balls.Length} ball(s).");
        EditorSceneManager.MarkAllScenesDirty();
    }

    // ---------------- Helpers ----------------

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var go = Prim(PrimitiveType.Cube, name, parent, Vector3.zero, scale, m);
        go.transform.position = pos;
        return go;
    }

    static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var go = Prim(PrimitiveType.Cylinder, name, parent, Vector3.zero, scale, m);
        go.transform.position = pos;
        return go;
    }

    static GameObject Disc(string name, Transform parent, Vector3 pos, float radius, Material m)
    {
        var go = Cyl(name, parent, pos, new Vector3(radius * 2, 0.002f, radius * 2), m);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void Line(Transform parent, Material m, Vector3 pos, Vector3 scale)
    {
        var go = Box("Line", parent, pos, scale, m);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static void Ring(Transform parent, Material m, Vector3 center, float radius, int segs,
                     float fromDeg = 0f, float toDeg = 360f, bool towardsPlayer = false)
    {
        float span = toDeg - fromDeg;
        float segLen = 2f * Mathf.PI * radius * (span / 360f) / segs * 1.05f;
        for (int i = 0; i < segs; i++)
        {
            float a = (fromDeg + span * (i + 0.5f) / segs) * Mathf.Deg2Rad;
            float dirZ = towardsPlayer ? -1f : 1f; // arcs open towards -Z (the player)
            var p = center + new Vector3(Mathf.Cos(a) * radius, 0, dirZ * Mathf.Sin(a) * radius);
            var go = Box("Arc", parent, p, new Vector3(0.06f, 0.004f, segLen), m);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, dirZ * Mathf.Cos(a)));
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }

    static Material Mat(string name, Color color, float smoothness, bool particle = false, bool emissive = false)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = particle ? Shader.Find("Particles/Standard Unlit") : Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        if (!particle) m.SetFloat("_Glossiness", smoothness);
        if (!particle) m.SetFloat("_Metallic", name == "HoopMetal" ? 0.6f : 0f);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white * 2f);
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static void BannerText(string name, Transform parent, string text, Vector3 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.rectTransform.sizeDelta = size;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.identity;   // readable from the court (facing -Z)
        t.enableAutoSizing = true; t.fontSizeMin = 0.1f; t.fontSizeMax = 100f;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.color = color;
    }

    static Material NeonMaterial(Color color)
    {
        var m = Mat("NeonPink", color, 0.9f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", color * 4f);       // strong glow = neon tube
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ConfettiMaterial()
    {
        string path = $"{MatFolder}/ConfettiBalls.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Particles/Standard Surface"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = Color.white;               // the particle's own gold/silver colour is multiplied in
        m.SetFloat("_Metallic", 0.6f);
        m.SetFloat("_Glossiness", 0.9f);
        m.EnableKeyword("_EMISSION");        // a little glow so they sparkle even in the darker hall
        m.SetColor("_EmissionColor", new Color(0.25f, 0.22f, 0.15f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WoodMaterial()
    {
        var m = Mat("CourtWood", Color.white, 0.45f);
        m.mainTexture = MakeTexture("CourtWood", 512, 512, (x, y) =>
        {
            // planks running along Z, with grain and slight colour variation per plank
            int plank = x / 32;
            float seed = Mathf.Sin(plank * 12.9898f) * 43758.5453f; seed -= Mathf.Floor(seed);
            float grain = Mathf.PerlinNoise(x * 0.05f + plank * 3f, y * 0.004f) * 0.12f;
            float joint = (x % 32 == 0 || (y + plank * 97) % 256 == 0) ? 0.6f : 1f;
            var c = Color.Lerp(new Color(0.80f, 0.70f, 0.58f), new Color(0.89f, 0.81f, 0.70f), seed); // cool, pale maple
            return (c - new Color(grain, grain, grain, 0)) * joint;
        });
        m.mainTextureScale = new Vector2(6, 10);
        return m;
    }

    static Material BasketballMaterial()
    {
        var m = Mat("Basketball", Color.white, 0.25f);
        m.mainTexture = MakeTexture("Basketball", 512, 256, (x, y) =>
        {
            float u = x / 512f, v = y / 256f;
            float d = Mathf.Min(Mathf.Abs(v - 0.5f),
                      Mathf.Min(Mathf.Abs(Mathf.Repeat(u, 0.25f)), Mathf.Abs(0.25f - Mathf.Repeat(u, 0.25f))) * 0.5f);
            // two curved seams
            float curve = Mathf.Abs(v - 0.5f - 0.3f * Mathf.Sin(u * Mathf.PI * 4f));
            d = Mathf.Min(d, curve * 0.6f);
            float noise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.08f;
            var orange = new Color(0.85f - noise, 0.38f - noise, 0.1f);
            return d < 0.008f ? new Color(0.08f, 0.05f, 0.04f) : orange;
        });
        return m;
    }

    static Texture2D MakeTexture(string name, int w, int h, System.Func<int, int, Color> pixel)
    {
        string path = $"{MatFolder}/{name}.png";
        var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
