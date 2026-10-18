using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupLargeScenario
{
    [MenuItem("Tools/CyberShooter/Setup Large Scenario")]
    public static void Setup()
    {
        Debug.Log("=== Setting up Large Scenario ===");

        CreateLargeGround();
        CreateWallsAndStructures();
        UpdatePlayerToCube();
        SetupLighting();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("=== Large Scenario Setup Complete! ===");
    }

    private static void CreateLargeGround()
    {
        GameObject oldGround = GameObject.Find("Ground");
        if (oldGround != null) Object.DestroyImmediate(oldGround);

        GameObject groundParent = new GameObject("Ground");
        groundParent.transform.position = Vector3.zero;

        int gridSize = 5;
        float tileSize = 100f;
        float halfSize = (gridSize * tileSize) * 0.5f;

        Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.color = new Color(0.15f, 0.15f, 0.2f);
        string matPath = "Assets/Materials/GroundLarge.mat";
        System.IO.Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(groundMat, matPath);

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Plane);
                tile.name = $"GroundTile_{x}_{z}";
                tile.transform.SetParent(groundParent.transform);
                tile.transform.localPosition = new Vector3(
                    (x * tileSize) - halfSize + tileSize * 0.5f,
                    0f,
                    (z * tileSize) - halfSize + tileSize * 0.5f
                );
                tile.transform.localScale = new Vector3(10, 1, 10);
                tile.layer = LayerMask.NameToLayer("Ground");
                tile.GetComponent<Renderer>().material = groundMat;
            }
        }

        Debug.Log($"Created large ground: {gridSize}x{gridSize} tiles = {gridSize * tileSize}x{gridSize * tileSize} units");
    }

    private static void CreateWallsAndStructures()
    {
        GameObject structuresParent = new GameObject("Structures");
        structuresParent.transform.position = Vector3.zero;

        Material structMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        structMat.color = new Color(0.3f, 0.3f, 0.35f);
        string matPath = "Assets/Materials/Structure.mat";
        AssetDatabase.CreateAsset(structMat, matPath);

        float wallHeight = 20f;
        float wallThickness = 5f;
        float worldSize = 500f;
        float halfWorld = worldSize * 0.5f;

        CreateWall(structuresParent, structMat, "Wall_North", 
            new Vector3(0, wallHeight * 0.5f, halfWorld), 
            new Vector3(worldSize + wallThickness * 2, wallHeight, wallThickness));
        
        CreateWall(structuresParent, structMat, "Wall_South", 
            new Vector3(0, wallHeight * 0.5f, -halfWorld), 
            new Vector3(worldSize + wallThickness * 2, wallHeight, wallThickness));

        CreateWall(structuresParent, structMat, "Wall_East", 
            new Vector3(halfWorld, wallHeight * 0.5f, 0), 
            new Vector3(wallThickness, wallHeight, worldSize));

        CreateWall(structuresParent, structMat, "Wall_West", 
            new Vector3(-halfWorld, wallHeight * 0.5f, 0), 
            new Vector3(wallThickness, wallHeight, worldSize));

        CreateCoverStructures(structuresParent, structMat);

        Debug.Log("Created boundary walls and cover structures");
    }

    private static void CreateWall(GameObject parent, Material mat, string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent.transform);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.layer = LayerMask.NameToLayer("Default");
        wall.GetComponent<Renderer>().material = mat;
    }

    private static void CreateCoverStructures(GameObject parent, Material mat)
    {
        GameObject center = GameObject.CreatePrimitive(PrimitiveType.Cube);
        center.name = "Cover_Center";
        center.transform.SetParent(parent.transform);
        center.transform.localPosition = new Vector3(0, 5f, 0);
        center.transform.localScale = new Vector3(15f, 10f, 15f);
        center.GetComponent<Renderer>().material = mat;

        float offset = 150f;
        Vector3[] positions = {
            new Vector3(offset, 3f, offset),
            new Vector3(-offset, 3f, offset),
            new Vector3(offset, 3f, -offset),
            new Vector3(-offset, 3f, -offset)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = $"Cover_Corner_{i}";
            cover.transform.SetParent(parent.transform);
            cover.transform.localPosition = positions[i];
            cover.transform.localScale = new Vector3(10f, 6f, 10f);
            cover.GetComponent<Renderer>().material = mat;
        }

        for (int i = 0; i < 12; i++)
        {
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = $"Cover_Small_{i}";
            cover.transform.SetParent(parent.transform);
            float angle = (360f / 12) * i * Mathf.Deg2Rad;
            float radius = 80f;
            cover.transform.localPosition = new Vector3(
                Mathf.Sin(angle) * radius,
                2f,
                Mathf.Cos(angle) * radius
            );
            cover.transform.localScale = new Vector3(4f, 4f, 4f);
            cover.GetComponent<Renderer>().material = mat;
        }
    }

    private static void UpdatePlayerToCube()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found!");
            return;
        }

        Transform bodyVisual = player.transform.Find("BodyVisual");
        if (bodyVisual != null) Object.DestroyImmediate(bodyVisual.gameObject);

        Transform muzzlePoint = player.transform.Find("MuzzlePoint");
        if (muzzlePoint != null) Object.DestroyImmediate(muzzlePoint.gameObject);

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.radius = 0.5f;
            cc.height = 2f;
            cc.center = new Vector3(0, 1f, 0);
        }

        GameObject cubeVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeVisual.name = "BodyVisual";
        cubeVisual.transform.SetParent(player.transform);
        cubeVisual.transform.localPosition = new Vector3(0, 1f, 0);
        cubeVisual.transform.localScale = new Vector3(1f, 2f, 1f);
        Object.DestroyImmediate(cubeVisual.GetComponent<BoxCollider>());

        Material cubeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        cubeMat.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        cubeMat.SetFloat("_Surface", 1);
        cubeMat.SetFloat("_Blend", 1);
        cubeMat.SetFloat("_ZWrite", 0);
        cubeMat.renderQueue = 3000;
        cubeVisual.GetComponent<Renderer>().material = cubeMat;

        GameObject newMuzzlePoint = new GameObject("MuzzlePoint");
        newMuzzlePoint.transform.SetParent(player.transform);
        newMuzzlePoint.transform.localPosition = new Vector3(0, 1f, 0.6f);

        PlayerShooting shooting = player.GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            var so = new SerializedObject(shooting);
            so.FindProperty("muzzlePoint").objectReferenceValue = newMuzzlePoint.transform;
            so.ApplyModifiedProperties();
        }

        Debug.Log("Player updated to Cube with new MuzzlePoint");
    }

    private static void SetupLighting()
    {
        Light dirLight = Object.FindAnyObjectByType<Light>();
        if (dirLight == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }
        
        dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);
        dirLight.intensity = 1.2f;
        dirLight.color = new Color(1f, 0.95f, 0.85f);
        dirLight.shadows = LightShadows.Soft;
        dirLight.shadowStrength = 0.8f;

        GameObject globalVolume = GameObject.Find("Global Volume");
        if (globalVolume == null)
        {
            globalVolume = new GameObject("Global Volume");
            var volume = globalVolume.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            volume.profile = profile;
            
            string profilePath = "Assets/Settings/ScenarioVolumeProfile.asset";
            System.IO.Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        Debug.Log("Lighting configured");
    }

    [MenuItem("Tools/CyberShooter/Reset Scenario")]
    public static void Reset()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground != null) Object.DestroyImmediate(ground);

        GameObject structures = GameObject.Find("Structures");
        if (structures != null) Object.DestroyImmediate(structures);

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Transform bodyVisual = player.transform.Find("BodyVisual");
            if (bodyVisual != null) Object.DestroyImmediate(bodyVisual.gameObject);

            Transform muzzlePoint = player.transform.Find("MuzzlePoint");
            if (muzzlePoint != null) Object.DestroyImmediate(muzzlePoint.gameObject);

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.radius = 0.3f;
                cc.height = 1.8f;
                cc.center = new Vector3(0, 0.9f, 0);
            }

            GameObject capsuleVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsuleVisual.name = "BodyVisual";
            capsuleVisual.transform.SetParent(player.transform);
            capsuleVisual.transform.localPosition = new Vector3(0, 0.9f, 0);
            capsuleVisual.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            Object.DestroyImmediate(capsuleVisual.GetComponent<CapsuleCollider>());

            Material bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMat.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            bodyMat.SetFloat("_Surface", 1);
            bodyMat.SetFloat("_Blend", 1);
            bodyMat.SetFloat("_ZWrite", 0);
            bodyMat.renderQueue = 3000;
            capsuleVisual.GetComponent<Renderer>().material = bodyMat;

            GameObject newMuzzlePoint = new GameObject("MuzzlePoint");
            newMuzzlePoint.transform.SetParent(player.transform);
            newMuzzlePoint.transform.localPosition = new Vector3(0, 1.5f, 0.5f);

            PlayerShooting shooting = player.GetComponent<PlayerShooting>();
            if (shooting != null)
            {
                var so = new SerializedObject(shooting);
                so.FindProperty("muzzlePoint").objectReferenceValue = newMuzzlePoint.transform;
                so.ApplyModifiedProperties();
            }
        }

        Debug.Log("Scenario reset complete");
    }
}