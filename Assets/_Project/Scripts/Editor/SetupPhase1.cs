using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// Editor script to automatically set up Phase 1: Third-Person Movement & Camera
/// Run this via Tools > CyberShooter > Setup Phase 1 (TPS Core)
/// </summary>
public class SetupPhase1 : EditorWindow
{
    [MenuItem("Tools/CyberShooter/Setup Phase 1 (TPS Core)")]
    public static void Setup()
    {
        Debug.Log("=== Setting up Phase 1: TPS Core ===");

        try
        {
            EnsureGroundLayer();
            CreateGroundPlane();
            GameObject player = CreatePlayer();
            SetupCamera(player);
            AddPlayerScripts(player);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("=== Phase 1 Setup Complete! ===");
            Debug.Log("Press Play to test. Use WASD to move, Mouse to look, Space to jump.");
            Debug.Log("Camera orbits player independently. Body rotates toward movement direction.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Setup failed: " + e.Message + "\n" + e.StackTrace);
        }
    }

    private static void EnsureGroundLayer()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        bool groundExists = false;
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (layer.stringValue == "Ground")
            {
                groundExists = true;
                break;
            }
        }

        if (!groundExists)
        {
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = "Ground";
                    tagManager.ApplyModifiedProperties();
                    Debug.Log("Created 'Ground' layer at index " + i);
                    break;
                }
            }
        }
        else
        {
            Debug.Log("'Ground' layer already exists");
        }
    }

    private static GameObject CreateGroundPlane()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10, 1, 10);
        ground.layer = LayerMask.NameToLayer("Ground");

        Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.color = new Color(0.2f, 0.2f, 0.25f);
        ground.GetComponent<Renderer>().material = groundMat;

        string matPath = "Assets/_Project/Materials/Ground.mat";
        System.IO.Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.CreateAsset(groundMat, matPath);

        Debug.Log("Created Ground plane at (0, 0, 0) scale 10x10");
        return ground;
    }

    private static GameObject CreatePlayer()
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0, 1.5f, 0);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.radius = 0.3f;
        controller.height = 1f;
        controller.center = new Vector3(0, 0.5f, 0);
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.08f;
        controller.minMoveDistance = 0f;

        GameObject cubeVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeVisual.name = "BodyVisual";
        cubeVisual.transform.SetParent(player.transform);
        cubeVisual.transform.localPosition = new Vector3(0, 0.5f, 0);
        cubeVisual.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
        DestroyImmediate(cubeVisual.GetComponent<BoxCollider>());

        Material bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        bodyMat.color = new Color(0.2f, 0.7f, 1f);
        cubeVisual.GetComponent<Renderer>().material = bodyMat;

        GameObject muzzlePoint = new GameObject("MuzzlePoint");
        muzzlePoint.transform.SetParent(cubeVisual.transform);
        muzzlePoint.transform.localPosition = new Vector3(0, 0, 0.35f);

        Debug.Log("Created Player (cube) with CharacterController at (0, 1.5, 0)");
        return player;
    }

    private static void SetupCamera(GameObject player)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            Debug.Log("Created new Main Camera");
        }
        else
        {
            Debug.Log("Using existing Main Camera");
        }

        mainCam.transform.SetParent(null);
        mainCam.transform.position = new Vector3(0.5f, 2f, -3f);
        mainCam.transform.rotation = Quaternion.identity;

        mainCam.clearFlags = CameraClearFlags.Skybox;
        mainCam.fieldOfView = 60f;
        mainCam.nearClipPlane = 0.3f;
        mainCam.farClipPlane = 1000f;
        mainCam.allowHDR = true;
        mainCam.allowMSAA = true;

        var urpCamData = mainCam.gameObject.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (urpCamData == null)
            urpCamData = mainCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        urpCamData.renderPostProcessing = true;
        urpCamData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
        urpCamData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;

        if (mainCam.GetComponent<AudioListener>() == null)
            mainCam.gameObject.AddComponent<AudioListener>();

        var camController = mainCam.gameObject.GetComponent<ThirdPersonCameraController>();
        if (camController == null)
            camController = mainCam.gameObject.AddComponent<ThirdPersonCameraController>();

        SerializedObject camSO = new SerializedObject(camController);
        camSO.FindProperty("target").objectReferenceValue = player.transform;
        camSO.FindProperty("offset").vector3Value = new Vector3(0.5f, 1.5f, -3f);
        camSO.FindProperty("yawSensitivity").floatValue = 2.5f;
        camSO.FindProperty("pitchSensitivity").floatValue = 2.5f;
        camSO.FindProperty("minPitch").floatValue = -40f;
        camSO.FindProperty("maxPitch").floatValue = 70f;
        camSO.FindProperty("cameraRadius").floatValue = 0.3f;
        camSO.FindProperty("minDistance").floatValue = 0.5f;
        camSO.FindProperty("collisionSmoothTime").floatValue = 0.1f;
        camSO.FindProperty("collisionLayers").intValue = LayerMask.GetMask("Default", "Ground", "TransparentFX");
        camSO.ApplyModifiedProperties();

        string[] guids = AssetDatabase.FindAssets("InputSystem_Actions");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
            if (inputActions != null)
            {
                camSO.FindProperty("inputActions").objectReferenceValue = inputActions;
                camSO.ApplyModifiedProperties();
                Debug.Log("Assigned InputSystem_Actions to camera controller");
            }
            else
            {
                Debug.LogWarning("InputSystem_Actions asset found but could not load");
            }
        }
        else
        {
            Debug.LogWarning("InputSystem_Actions not found in project - camera will use fallback mouse input");
        }

        Debug.Log("Configured Main Camera with ThirdPersonCameraController (orbits player)");
    }

    private static void AddPlayerScripts(GameObject player)
    {
        var motor = player.GetComponent<PlayerMotor>();
        if (motor == null)
            motor = player.AddComponent<PlayerMotor>();

        SerializedObject motorSO = new SerializedObject(motor);
        motorSO.FindProperty("characterController").objectReferenceValue = player.GetComponent<CharacterController>();
        
        Camera cam = Camera.main;
        if (cam != null)
            motorSO.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
        else
            Debug.LogWarning("Camera.main is null when configuring PlayerMotor");

        motorSO.FindProperty("walkSpeed").floatValue = 4f;
        motorSO.FindProperty("sprintSpeed").floatValue = 8f;
        motorSO.FindProperty("rotationSpeed").floatValue = 12f;
        motorSO.FindProperty("gravity").floatValue = -9.81f;
        motorSO.FindProperty("jumpHeight").floatValue = 1.5f;
        motorSO.FindProperty("groundedStickForce").floatValue = -2f;
        motorSO.FindProperty("coyoteTime").floatValue = 0.1f;
        motorSO.FindProperty("jumpBufferTime").floatValue = 0.1f;
        motorSO.FindProperty("groundLayer").intValue = LayerMask.GetMask("Ground", "Default");
        motorSO.FindProperty("actionMapName").stringValue = "Player";
        motorSO.FindProperty("moveActionName").stringValue = "Move";
        motorSO.FindProperty("jumpActionName").stringValue = "Jump";
        motorSO.FindProperty("sprintActionName").stringValue = "Sprint";

        string[] guids = AssetDatabase.FindAssets("InputSystem_Actions");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
            if (inputActions != null)
            {
                motorSO.FindProperty("inputActions").objectReferenceValue = inputActions;
            }
        }
        motorSO.ApplyModifiedProperties();

        var shooting = player.GetComponent<PlayerShooting>();
        if (shooting == null)
            shooting = player.AddComponent<PlayerShooting>();

        SerializedObject shootingSO = new SerializedObject(shooting);
        Transform muzzlePoint = player.transform.Find("BodyVisual/MuzzlePoint");
        if (muzzlePoint != null)
            shootingSO.FindProperty("muzzlePoint").objectReferenceValue = muzzlePoint;
        else
            Debug.LogWarning("MuzzlePoint not found at BodyVisual/MuzzlePoint");

        if (cam != null)
            shootingSO.FindProperty("playerCamera").objectReferenceValue = cam.transform;
        
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
            if (inputActions != null)
            {
                shootingSO.FindProperty("inputActions").objectReferenceValue = inputActions;
            }
        }
        shootingSO.FindProperty("fireRate").floatValue = 0.15f;
        shootingSO.FindProperty("maxDistance").floatValue = 100f;
        shootingSO.FindProperty("hitLayers").intValue = -1;
        shootingSO.FindProperty("tracerDuration").floatValue = 0.05f;
        shootingSO.FindProperty("tracerWidth").floatValue = 0.05f;
        shootingSO.FindProperty("tracerColor").colorValue = Color.yellow;
        shootingSO.FindProperty("actionMapName").stringValue = "Player";
        shootingSO.FindProperty("attackActionName").stringValue = "Attack";
        shootingSO.ApplyModifiedProperties();

        Debug.Log("Added PlayerMotor and PlayerShooting to Player");
    }

    [MenuItem("Tools/CyberShooter/Validate Phase 1 Setup")]
    public static void Validate()
    {
        Debug.Log("=== Validating Phase 1 Setup ===");

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found! Run Setup Phase 1 first.");
            return;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null)
            Debug.LogError("Player missing CharacterController");
        else
            Debug.Log("CharacterController: radius=" + cc.radius + ", height=" + cc.height + ", center=" + cc.center);

        PlayerMotor motor = player.GetComponent<PlayerMotor>();
        if (motor == null)
            Debug.LogError("Player missing PlayerMotor");
        else
            Debug.Log("PlayerMotor: OK");

        PlayerShooting shooting = player.GetComponent<PlayerShooting>();
        if (shooting == null)
            Debug.LogError("Player missing PlayerShooting");
        else
            Debug.Log("PlayerShooting: OK");

        Camera cam = Camera.main;
        if (cam == null)
            Debug.LogError("Main Camera not found");
        else
        {
            var camController = cam.GetComponent<ThirdPersonCameraController>();
            if (camController == null)
                Debug.LogError("Camera missing ThirdPersonCameraController");
            else
            {
                SerializedObject camSO = new SerializedObject(camController);
                float yawSens = camSO.FindProperty("yawSensitivity").floatValue;
                float pitchSens = camSO.FindProperty("pitchSensitivity").floatValue;
                Debug.Log("ThirdPersonCameraController: target=" + (camController.target != null ? camController.target.name : "null") + ", yawSens=" + yawSens + ", pitchSens=" + pitchSens);
            }

            if (cam.transform.parent != null)
                Debug.LogWarning("Camera is parented to something! Should be unparented for orbit camera.");
            else
                Debug.Log("Camera correctly unparented (orbits independently)");
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer == -1)
            Debug.LogWarning("'Ground' layer not found in TagManager!");
        else
            Debug.Log("Ground layer: " + groundLayer);

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
            Debug.LogWarning("Ground plane not found!");
        else if (ground.layer != groundLayer)
            Debug.LogWarning("Ground plane layer is " + LayerMask.LayerToName(ground.layer) + ", expected Ground");
        else
            Debug.Log("Ground plane: OK");

        Debug.Log("=== Validation Complete ===");
    }

    [MenuItem("Tools/CyberShooter/Reset Phase 1 (Clean Scene)")]
    public static void Reset()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) DestroyImmediate(player);

        GameObject ground = GameObject.Find("Ground");
        if (ground != null) DestroyImmediate(ground);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.SetParent(null);
            cam.transform.position = new Vector3(0, 1, -10);
            cam.transform.rotation = Quaternion.identity;
            var camController = cam.GetComponent<ThirdPersonCameraController>();
            if (camController != null) DestroyImmediate(camController);
        }

        Debug.Log("Phase 1 reset complete. Run Setup again.");
    }
}