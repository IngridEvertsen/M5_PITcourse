using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        BuildScoreboard(arena.transform);
        RestyleExisting();
        WireGame(goalZone, crowd);

        RenderSettings.ambientLight = new Color(0.35f, 0.33f, 0.32f);
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

        var dark = Mat("HoopMetal", new Color(0.15f, 0.15f, 0.17f), 0.6f);
        var orange = Mat("Rim", new Color(1f, 0.35f, 0.05f), 0.7f);
        var glass = Mat("Backboard", new Color(0.95f, 0.97f, 1f), 0.9f);

        // pole + arm
        Cyl("Pole", hoop, new Vector3(0, (RimHeight + 0.6f) / 2f, BaselineZ + 0.6f),
            new Vector3(0.18f, (RimHeight + 0.6f) / 2f, 0.18f), dark);
        Box("Arm", hoop, new Vector3(0, RimHeight + 0.45f, (BaselineZ + 0.6f + BackboardZ) / 2f),
            new Vector3(0.15f, 0.15f, BaselineZ + 0.6f - BackboardZ), dark);
        Box("Base", hoop, new Vector3(0, 0.3f, BaselineZ + 0.6f), new Vector3(1.2f, 0.6f, 1.2f),
            Mat("HoopPad", new Color(0.12f, 0.32f, 0.62f), 0.2f));

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
        main.startLifetime = 1.8f; main.startSpeed = 4f; main.startSize = 0.08f;
        main.gravityModifier = 0.8f; main.maxParticles = 300;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.1f), new Color(0.2f, 0.6f, 1f));
        var em = fx.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 150) });
        var sh = fx.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.1f;
        fx.transform.rotation = Quaternion.Euler(-90, 0, 0);
        fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("Confetti", Color.white, 0f, true);

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

        var seatMat = Mat("TribuneSeats", new Color(0.12f, 0.32f, 0.62f), 0.3f);
        var stepMat = Mat("TribuneConcrete", new Color(0.5f, 0.5f, 0.53f), 0.1f);
        Color[] shirts = {
            new Color(0.12f,0.32f,0.62f), new Color(0.95f,0.95f,0.95f), new Color(1f,0.35f,0.05f),
            new Color(0.85f,0.15f,0.2f), new Color(0.15f,0.15f,0.18f), new Color(0.95f,0.8f,0.2f),
            new Color(0.2f,0.6f,0.35f) };
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
        Box("Banner_1", shell, new Vector3(-4f, 6.5f, centerZ + halfL - 0.2f), new Vector3(2.5f, 3f, 0.05f), Mat("BannerBlue", new Color(0.12f, 0.32f, 0.62f), 0.1f));
        Box("Banner_2", shell, new Vector3(4f, 6.5f, centerZ + halfL - 0.2f), new Vector3(2.5f, 3f, 0.05f), Mat("BannerOrange", new Color(1f, 0.35f, 0.05f), 0.1f));

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
                l.type = LightType.Spot; l.spotAngle = 85f; l.range = 16f; l.intensity = 3f;
                l.color = new Color(1f, 0.96f, 0.9f);
                l.shadows = i == 0 ? LightShadows.Soft : LightShadows.None; // one shadow near the hoop only
            }
        }
    }

    // ---------------- Scoreboard ----------------

    static void BuildScoreboard(Transform root)
    {
        var board = new GameObject("Scoreboard").transform;
        board.SetParent(root, false);
        Box("ScoreboardPanel", board, new Vector3(0, 5f, BaselineZ + 0.8f), new Vector3(3.6f, 1.6f, 0.2f),
            Mat("ScoreboardBlack", new Color(0.03f, 0.03f, 0.04f), 0.6f));
        Box("ScoreboardFrame", board, new Vector3(0, 5f, BaselineZ + 0.85f), new Vector3(3.8f, 1.8f, 0.15f),
            Mat("BannerOrange", new Color(1f, 0.35f, 0.05f), 0.1f));
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
            if (t != null) { t.fontSize = 1.4f; t.alignment = TextAlignmentOptions.Center; t.color = new Color(1f, 0.75f, 0.1f);
                t.rectTransform.sizeDelta = new Vector2(3.4f, 1.4f); }
        }
        var msg = GameObject.Find("MessageText");
        if (msg != null)
        {
            msg.transform.position = new Vector3(0, 3.95f, BaselineZ + 0.68f);
            msg.transform.rotation = Quaternion.identity;
            var t = msg.GetComponent<TMP_Text>();
            if (t != null) { t.fontSize = 0.9f; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
                t.rectTransform.sizeDelta = new Vector2(6f, 0.8f); }
        }

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            var l = sun.GetComponent<Light>();
            l.intensity = 0.35f; l.color = new Color(1f, 0.95f, 0.88f);
            sun.transform.rotation = Quaternion.Euler(60, -30, 0);
        }
    }

    static void WireGame(Transform goalZone, Transform crowdTransform)
    {
        var manager = Object.FindFirstObjectByType<ScoreManager>();
        if (manager == null) { Debug.LogWarning("[ArenaBuilder] No ScoreManager found - add one to GameManager."); return; }

        goalZone.GetComponent<GoalTrigger>().scoreManager = manager;

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
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white * 2f);
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static Material WoodMaterial()
    {
        var m = Mat("CourtWood", Color.white, 0.75f);
        m.mainTexture = MakeTexture("CourtWood", 512, 512, (x, y) =>
        {
            // planks running along Z, with grain and slight colour variation per plank
            int plank = x / 32;
            float seed = Mathf.Sin(plank * 12.9898f) * 43758.5453f; seed -= Mathf.Floor(seed);
            float grain = Mathf.PerlinNoise(x * 0.05f + plank * 3f, y * 0.004f) * 0.12f;
            float joint = (x % 32 == 0 || (y + plank * 97) % 256 == 0) ? 0.6f : 1f;
            var c = Color.Lerp(new Color(0.78f, 0.55f, 0.32f), new Color(0.88f, 0.66f, 0.42f), seed);
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
