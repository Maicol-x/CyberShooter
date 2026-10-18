using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupPhase6
{
    [MenuItem("Tools/CyberShooter/Setup Phase 6 (DOOM Gauntlet)")]
    public static void Setup()
    {
        Debug.Log("=== SETUP FASE 6 (UPDATED V7 - YPLAYER MODEL SETUP): DOOM GAUNTLET ===");

        // 1. Crear o encontrar la zona de meta (ExtractionZone) al final del mapa
        GameObject extractionZone = GameObject.Find("ExtractionZone");
        if (extractionZone == null)
        {
            extractionZone = new GameObject("ExtractionZone");
            extractionZone.transform.position = new Vector3(0, 0.5f, 400f); // Al otro lado del mapa
            extractionZone.tag = "Untagged";
            Debug.Log("Created ExtractionZone at (0, 0.5, 400)");
        }

        // Añadir componente ExtractionZone
        var ez = extractionZone.GetComponent<ExtractionZone>();
        if (ez == null)
        {
            ez = extractionZone.gameObject.AddComponent<ExtractionZone>();
        }

        // Añadir collider trigger si no tiene
        var col = extractionZone.GetComponent<Collider>();
        if (col == null)
        {
            var boxCol = extractionZone.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(10f, 5f, 10f);
        }
        else
        {
            col.isTrigger = true;
        }

        // Añadir Rigidbody para que el trigger funcione bien (kinematic)
        var rb = extractionZone.GetComponent<Rigidbody>();
        if (rb == null)
        {
            var newRB = extractionZone.AddComponent<Rigidbody>();
            newRB.isKinematic = true;
            newRB.useGravity = false;
        }

        Debug.Log("ExtractionZone configured with trigger collider");

        // 2. Crear EnemySpawner
        GameObject spawnerObj = GameObject.Find("EnemySpawner");
        if (spawnerObj == null)
        {
            spawnerObj = new GameObject("EnemySpawner");
            spawnerObj.transform.position = Vector3.zero;
        }

        var spawner = spawnerObj.GetComponent<EnemySpawner>();
        if (spawner == null)
        {
            spawner = spawnerObj.AddComponent<EnemySpawner>();
        }

        var spawnerSO = new SerializedObject(spawner);
        spawnerSO.FindProperty("spawnIntervalMin").floatValue = 2f;
        spawnerSO.FindProperty("spawnIntervalMax").floatValue = 4f;
        spawnerSO.FindProperty("maxEnemiesAlive").intValue = 15;
        spawnerSO.FindProperty("spawnRange").floatValue = 200f;
        spawnerSO.FindProperty("enemyMoveSpeed").floatValue = 4f;
        spawnerSO.FindProperty("enemyMaxHealth").intValue = 30;
        
        // Asignar playerTransform
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            spawnerSO.FindProperty("playerTransform").objectReferenceValue = player.transform;
        }
        spawnerSO.ApplyModifiedProperties();

        Debug.Log("EnemySpawner configured: spawn every 2-4s, max 15 enemies, speed 4m/s");

        // 3. Configurar velocidad de enemigos existentes a 4 m/s
        GameObject enemiesParent = GameObject.Find("Enemies");
        if (enemiesParent != null)
        {
            int count = 0;
            foreach (Transform enemy in enemiesParent.transform)
            {
                var ai = enemy.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    var aiSO = new SerializedObject(ai);
                    aiSO.FindProperty("moveSpeed").floatValue = 4f;
                    aiSO.ApplyModifiedProperties();
                    count++;
                }
            }
            Debug.Log($"Updated {count} existing enemies to speed 4m/s");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("=== FASE 6 COMPLETADA ===");
        Debug.Log("- Zona de meta (ExtractionZone) en (0, 0.5, 400)");
        Debug.Log("- EnemySpawner activo: enemigos aparecen cada 2-4s");
        Debug.Log("- Velocidad de enemigos: 4 m/s (obliga a usar Sprint/Dash)");
    }

    [MenuItem("Tools/CyberShooter/Validate Phase 6")]
    public static void Validate()
    {
        Debug.Log("=== VALIDANDO FASE 6 ===");

        // Zona de meta
        GameObject ez = GameObject.Find("ExtractionZone");
        if (ez != null)
        {
            var ezComp = ez.GetComponent<ExtractionZone>();
            Debug.Log($"ExtractionZone: {(ezComp != null ? "OK" : "MISSING component")}, Position: {ez.transform.position}");
        }
        else
        {
            Debug.LogError("ExtractionZone NOT FOUND!");
        }

        // Spawner
        GameObject spawnerObj = GameObject.Find("EnemySpawner");
        if (spawnerObj != null)
        {
            var spawner = spawnerObj.GetComponent<EnemySpawner>();
            Debug.Log($"EnemySpawner: {(spawner != null ? "OK" : "MISSING component")}");
        }
        else
        {
            Debug.LogError("EnemySpawner NOT FOUND!");
        }

        // Enemigos con velocidad 4m/s
        GameObject enemiesParent = GameObject.Find("Enemies");
        if (enemiesParent != null)
        {
            int total = 0, speed4 = 0;
            foreach (Transform enemy in enemiesParent.transform)
            {
                total++;
                var ai = enemy.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    var aiSO = new SerializedObject(ai);
                    float speed = aiSO.FindProperty("moveSpeed").floatValue;
                    if (speed >= 4f) speed4++;
                }
            }
            Debug.Log($"Enemies: {total} total, {speed4} with speed >= 4m/s");
        }

        Debug.Log("=== VALIDACION FASE 6 COMPLETADA ===");
    }
}