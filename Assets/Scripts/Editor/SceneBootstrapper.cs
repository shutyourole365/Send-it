#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds a fully wired, immediately playable test scene from the Unity menu:
///   Send-it → Setup Test Scene
///
/// What it creates
/// ───────────────
///   Ground         200 × 200 m dark tarmac plane
///   [Managers]     GameManager, SaveSystem, SoundManager + all UGS service managers
///   TestCar        Primitive-body car with VehicleController, BurnoutSystem,
///                  4 WheelColliders + TyrePhysics, smoke particles, skid trails
///   Main Camera    CameraController pre-wired to the car
///   HUD_Canvas     Speed, RPM, gear and burnout-score text labels
///
/// Controls (once you press Play)
/// ───────────────────────────────
///   W / Up arrow     Throttle
///   S / Down arrow   Brake / reverse
///   A/D / Left/Right Steer
///   Space            Handbrake (rear wheel lock)
///   Left Shift       Line lock — front brakes on, rear free → burnout!
///   Q / E            Shift down / up (manual gearbox)
///   R                Toggle reverse
///   C                Toggle chase / first-person camera
/// </summary>
public static class SceneBootstrapper
{
    // ─── Car geometry constants ───────────────────────────────────────────────
    const float BODY_LENGTH  = 4.4f;
    const float BODY_WIDTH   = 1.8f;
    const float BODY_HEIGHT  = 0.52f;
    const float WHEEL_RADIUS = 0.33f;
    const float WHEEL_WIDTH  = 0.22f;   // visual only
    const float HALF_TRACK   = 0.78f;   // half of track width
    const float HALF_WB      = 1.35f;   // half of wheelbase
    const float ROOT_HEIGHT  = 0.58f;   // car root Y above ground (≈ wheel radius + clearance)
    const float WHEEL_LOCAL_Y = -0.30f; // wheel centre Y in local car space

    // ─── Menu entries ─────────────────────────────────────────────────────────
    [MenuItem("Send-it/Setup Test Scene", false, 1)]
    public static void SetupTestScene()
    {
        if (GameObject.Find("[Managers]") != null)
        {
            bool rebuild = EditorUtility.DisplayDialog(
                "Send-it Scene Bootstrapper",
                "[Managers] already exists. Rebuild the test scene?",
                "Yes, rebuild", "Cancel");
            if (!rebuild) return;

            foreach (string name in new[] { "[Managers]", "TestCar_VECommodore",
                                            "HUD_Canvas", "Ground" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        Undo.SetCurrentGroupName("Send-it: Setup Test Scene");
        int undoGroup = Undo.GetCurrentGroup();

        CreateGround();
        CreateManagers();
        GameObject car       = CreateCar();
        GameObject cameraObj = CreateCamera(car.transform);
        CreateHUD(car);

        Selection.activeGameObject = car;
        SceneView.FrameLastActiveSceneView();

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            "[Send-it] Test scene ready — press Play!\n" +
            "Drive: WASD/Arrows  |  Handbrake: Space  |  Line Lock (burnout): Left Shift\n" +
            "Gear: Q/E  |  Reverse: R  |  Camera: C");
    }

    [MenuItem("Send-it/Clear Test Scene", false, 2)]
    public static void ClearTestScene()
    {
        bool confirm = EditorUtility.DisplayDialog("Send-it",
            "Remove all Send-it test objects from the scene?", "Remove", "Cancel");
        if (!confirm) return;

        foreach (string name in new[] { "[Managers]", "TestCar_VECommodore",
                                        "HUD_Canvas", "Ground" })
        {
            var go = GameObject.Find(name);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }
    }

    // ─── Ground ───────────────────────────────────────────────────────────────
    static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position   = Vector3.zero;
        ground.transform.localScale = new Vector3(20f, 1f, 20f);   // 200 × 200 m

        ApplyLitMaterial(ground, new Color(0.08f, 0.08f, 0.08f));
        Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
    }

    // ─── Managers ─────────────────────────────────────────────────────────────
    static void CreateManagers()
    {
        GameObject root = new GameObject("[Managers]");
        Undo.RegisterCreatedObjectUndo(root, "Create Managers");

        root.AddComponent<GameManager>();
        root.AddComponent<SaveSystem>();
        root.AddComponent<SoundManager>();
        root.AddComponent<UGSManager>();
        root.AddComponent<CloudSyncManager>();
        root.AddComponent<LeaderboardManager>();
        root.AddComponent<EconomyManager>();
    }

    // ─── Car ──────────────────────────────────────────────────────────────────
    static GameObject CreateCar()
    {
        // Root
        var root = new GameObject("TestCar_VECommodore");
        root.transform.position = new Vector3(0f, ROOT_HEIGHT, 0f);
        Undo.RegisterCreatedObjectUndo(root, "Create Car");

        var rb = root.AddComponent<Rigidbody>();
        rb.mass             = 1600f;
        rb.linearDamping    = 0.05f;
        rb.angularDamping   = 0.25f;
        rb.interpolation    = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass     = new Vector3(0f, -0.35f, 0.1f);

        // Body mesh
        CreateBox(root, "Body",
            new Vector3(0f, 0.08f, 0f),
            new Vector3(BODY_WIDTH, BODY_HEIGHT, BODY_LENGTH),
            new Color(0.65f, 0.07f, 0.04f));

        // Cabin mesh
        CreateBox(root, "Cabin",
            new Vector3(0f, BODY_HEIGHT * 0.5f + 0.36f, -0.15f),
            new Vector3(BODY_WIDTH * 0.88f, 0.68f, BODY_LENGTH * 0.44f),
            new Color(0.52f, 0.05f, 0.03f));

        // Driver head anchor (first-person camera position)
        var head = new GameObject("DriverHead");
        head.transform.SetParent(root.transform);
        head.transform.localPosition = new Vector3(0.28f, BODY_HEIGHT * 0.5f + 0.58f, 0.35f);

        // Wheel colliders
        var wcFL = MakeWheelCollider(root, "WC_FL", new Vector3(-HALF_TRACK, WHEEL_LOCAL_Y,  HALF_WB));
        var wcFR = MakeWheelCollider(root, "WC_FR", new Vector3( HALF_TRACK, WHEEL_LOCAL_Y,  HALF_WB));
        var wcRL = MakeWheelCollider(root, "WC_RL", new Vector3(-HALF_TRACK, WHEEL_LOCAL_Y, -HALF_WB));
        var wcRR = MakeWheelCollider(root, "WC_RR", new Vector3( HALF_TRACK, WHEEL_LOCAL_Y, -HALF_WB));

        // Wheel visual meshes (cylinders)
        var meshFL = MakeWheelMesh(root, "Mesh_FL", new Vector3(-HALF_TRACK, WHEEL_LOCAL_Y,  HALF_WB));
        var meshFR = MakeWheelMesh(root, "Mesh_FR", new Vector3( HALF_TRACK, WHEEL_LOCAL_Y,  HALF_WB));
        var meshRL = MakeWheelMesh(root, "Mesh_RL", new Vector3(-HALF_TRACK, WHEEL_LOCAL_Y, -HALF_WB));
        var meshRR = MakeWheelMesh(root, "Mesh_RR", new Vector3( HALF_TRACK, WHEEL_LOCAL_Y, -HALF_WB));

        // Tyre smoke particles (rear wheels)
        var smokeRL = MakeSmokeSystem(root, "Smoke_RL",
            new Vector3(-HALF_TRACK, WHEEL_LOCAL_Y + WHEEL_RADIUS * 0.5f, -HALF_WB));
        var smokeRR = MakeSmokeSystem(root, "Smoke_RR",
            new Vector3( HALF_TRACK, WHEEL_LOCAL_Y + WHEEL_RADIUS * 0.5f, -HALF_WB));

        // Skid mark trail renderers (at tyre contact patch)
        var skidRL = MakeSkidTrail(root, "Skid_RL",
            new Vector3(-HALF_TRACK, -(ROOT_HEIGHT - 0.02f), -HALF_WB));
        var skidRR = MakeSkidTrail(root, "Skid_RR",
            new Vector3( HALF_TRACK, -(ROOT_HEIGHT - 0.02f), -HALF_WB));

        // Audio sources
        var engineAudio = MakeAudioSource(root, "Audio_Engine");
        var screechAudio = MakeAudioSource(root, "Audio_TyreScreech");
        screechAudio.loop = true;

        // ── VehicleController ──
        var vc = root.AddComponent<VehicleController>();
        vc.wheelFL = wcFL;    vc.wheelFR = wcFR;
        vc.wheelRL = wcRL;    vc.wheelRR = wcRR;
        vc.meshFL  = meshFL;  vc.meshFR  = meshFR;
        vc.meshRL  = meshRL;  vc.meshRR  = meshRR;
        vc.driveType = VehicleController.DriveType.RearWheelDrive;
        vc.peakTorque = 530f;
        vc.redlineRPM = 6500f;

        // ── BurnoutSystem ──
        var burnout = root.AddComponent<BurnoutSystem>();
        burnout.smokeRL          = smokeRL;
        burnout.smokeRR          = smokeRR;
        burnout.skidMarkRL       = skidRL;
        burnout.skidMarkRR       = skidRR;
        burnout.engineAudio      = engineAudio;
        burnout.tyreScreechAudio = screechAudio;

        // ── TyrePhysics (on each WheelCollider child) ──
        wcFL.gameObject.AddComponent<TyrePhysics>();
        wcFR.gameObject.AddComponent<TyrePhysics>();
        wcRL.gameObject.AddComponent<TyrePhysics>();
        wcRR.gameObject.AddComponent<TyrePhysics>();

        // ── DamageSystem ──
        root.AddComponent<DamageSystem>();

        // ── EngineSimulator ──
        root.AddComponent<EngineSimulator>();

        return root;
    }

    // ─── Camera ───────────────────────────────────────────────────────────────
    static GameObject CreateCamera(Transform carTransform)
    {
        // Reuse the existing Main Camera if present; otherwise create one
        Camera existingCam = Object.FindFirstObjectByType<Camera>();
        var camGo = existingCam != null
            ? existingCam.gameObject
            : new GameObject("Main Camera");

        camGo.tag = "MainCamera";
        if (!camGo.GetComponent<Camera>())       camGo.AddComponent<Camera>();
        if (!camGo.GetComponent<AudioListener>()) camGo.AddComponent<AudioListener>();

        var ctrl = camGo.GetComponent<CameraController>()
                   ?? camGo.AddComponent<CameraController>();

        ctrl.vehicleTransform    = carTransform;
        ctrl.driverHeadTransform = carTransform.Find("DriverHead");

        camGo.transform.position = carTransform.position + new Vector3(0f, 2.5f, -7f);
        camGo.transform.LookAt(carTransform.position + Vector3.up * 0.5f);

        if (existingCam == null)
            Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");

        return camGo;
    }

    // ─── HUD Canvas ───────────────────────────────────────────────────────────
    static void CreateHUD(GameObject car)
    {
        var canvasGo = new GameObject("HUD_Canvas");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var hud    = canvasGo.AddComponent<HUD>();
        hud.vehicle = car.GetComponent<VehicleController>();
        hud.burnout = car.GetComponent<BurnoutSystem>();
        hud.engine  = car.GetComponent<EngineSimulator>();
        hud.damage  = car.GetComponent<DamageSystem>();

        // Wire the TyrePhysics components
        hud.tyreFL = car.transform.Find("WC_FL")?.GetComponent<TyrePhysics>();
        hud.tyreFR = car.transform.Find("WC_FR")?.GetComponent<TyrePhysics>();
        hud.tyreRL = car.transform.Find("WC_RL")?.GetComponent<TyrePhysics>();
        hud.tyreRR = car.transform.Find("WC_RR")?.GetComponent<TyrePhysics>();

        // ── Minimal HUD labels ──────────────────────────────────────────────────
        // Bottom-left cluster: speedo
        hud.speedText = MakeLabel(canvasGo, "SpeedText",
            new Vector2(-800f, -440f), "000", 28);
        MakeLabel(canvasGo, "SpeedUnit",
            new Vector2(-800f, -470f), "km/h", 14);

        // Bottom-right cluster: RPM
        hud.rpmText = MakeLabel(canvasGo, "RPMText",
            new Vector2(800f, -440f), "0000", 28);
        MakeLabel(canvasGo, "RPMUnit",
            new Vector2(800f, -470f), "RPM", 14);

        // Bottom-centre: gear
        hud.gearText = MakeLabel(canvasGo, "GearText",
            new Vector2(0f, -460f), "1", 52, new Color(1f, 0.4f, 0f));

        // Top-centre: burnout score (hidden until a burnout starts)
        var scoreRoot = new GameObject("BurnoutScoreRoot");
        scoreRoot.transform.SetParent(canvasGo.transform, false);
        var scoreRootRT = scoreRoot.AddComponent<RectTransform>();
        scoreRootRT.anchoredPosition = new Vector2(0f, 420f);
        scoreRoot.SetActive(false);
        hud.burnoutScoreRoot = scoreRoot;

        hud.burnoutScoreText = MakeLabel(scoreRoot, "ScoreText",
            Vector2.zero, "0", 48, Color.white);
        hud.sessionScoreText = MakeLabel(scoreRoot, "SessionText",
            new Vector2(0f, -52f), "SESSION 0", 18, new Color(1f, 0.7f, 0f));

        // ── Controls reminder (top-left) ────────────────────────────────────────
        var hint = MakeLabel(canvasGo, "ControlsHint",
            new Vector2(-820f, 460f),
            "WASD Drive  |  Space Handbrake\nLeft Shift LINE LOCK  |  Q/E Gear  |  C Camera",
            13, new Color(0.6f, 0.6f, 0.6f));
        hint.alignment = TextAlignmentOptions.TopLeft;
    }

    // ─── Primitive helpers ────────────────────────────────────────────────────

    static void CreateBox(GameObject parent, string name,
                          Vector3 localPos, Vector3 localScale, Color colour)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        ApplyLitMaterial(go, colour);
    }

    static WheelCollider MakeWheelCollider(GameObject parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;

        var wc = go.AddComponent<WheelCollider>();
        wc.radius            = WHEEL_RADIUS;
        wc.suspensionDistance = 0.2f;
        wc.mass              = 25f;

        var spring = wc.suspensionSpring;
        spring.spring          = 35000f;
        spring.damper          = 4500f;
        spring.targetPosition  = 0.5f;
        wc.suspensionSpring    = spring;

        wc.forwardFriction  = MakeFrictionCurve(0.4f, 1.0f, 0.8f, 0.5f, 1.6f);
        wc.sidewaysFriction = MakeFrictionCurve(0.2f, 1.0f, 0.5f, 0.75f, 1.6f);

        return wc;
    }

    static WheelFrictionCurve MakeFrictionCurve(float extSlip, float extVal,
                                                  float asymSlip, float asymVal, float stiffness)
    {
        return new WheelFrictionCurve
        {
            extremumSlip   = extSlip,
            extremumValue  = extVal,
            asymptoteSlip  = asymSlip,
            asymptoteValue = asymVal,
            stiffness      = stiffness,
        };
    }

    static Transform MakeWheelMesh(GameObject parent, string name, Vector3 localPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);  // lie on side
        go.transform.localScale    = new Vector3(WHEEL_RADIUS * 2f,
                                                  WHEEL_WIDTH * 0.5f,
                                                  WHEEL_RADIUS * 2f);
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        ApplyLitMaterial(go, new Color(0.1f, 0.1f, 0.1f));
        return go.transform;
    }

    static ParticleSystem MakeSmokeSystem(GameObject parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime    = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed       = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize        = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
        main.startColor       = new ParticleSystem.MinMaxGradient(
                                    new Color(0.75f, 0.75f, 0.75f, 0.0f),
                                    new Color(0.85f, 0.85f, 0.85f, 0.55f));
        main.maxParticles     = 600;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.loop             = true;
        main.playOnAwake      = false;

        var emission = ps.emission;
        emission.enabled        = true;
        emission.rateOverTime   = 0f;  // BurnoutSystem drives this at runtime

        var shape = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Sphere;
        shape.radius     = 0.12f;

        ps.Stop();
        return ps;
    }

    static TrailRenderer MakeSkidTrail(GameObject parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;

        var tr = go.AddComponent<TrailRenderer>();
        tr.time               = 10f;
        tr.startWidth         = 0.24f;
        tr.endWidth           = 0.24f;
        tr.minVertexDistance  = 0.04f;
        tr.autodestruct       = false;
        tr.emitting           = false;
        tr.receiveShadows     = false;
        tr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.04f, 0.04f, 0.04f, 0.9f);
        tr.sharedMaterial = mat;

        return tr;
    }

    static AudioSource MakeAudioSource(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = Vector3.zero;

        var src = go.AddComponent<AudioSource>();
        src.spatialBlend = 0.85f;
        src.rolloffMode  = AudioRolloffMode.Logarithmic;
        src.minDistance  = 4f;
        src.maxDistance  = 80f;
        src.playOnAwake  = false;
        return src;
    }

    // ─── UI helpers ───────────────────────────────────────────────────────────

    static TextMeshProUGUI MakeLabel(GameObject parent, string name,
                                      Vector2 anchoredPos, string text,
                                      int fontSize = 32,
                                      Color? colour = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(400f, 70f);
        rt.anchoredPosition = anchoredPos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = colour ?? Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return tmp;
    }

    // ─── Material helper ──────────────────────────────────────────────────────
    static void ApplyLitMaterial(GameObject go, Color colour)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        // Try URP first, fall back to Standard
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
        if (shader == null) return;
        r.sharedMaterial = new Material(shader) { color = colour };
    }
}
#endif
