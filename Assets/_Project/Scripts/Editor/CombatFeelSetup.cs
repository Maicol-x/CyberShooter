using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Bloque "combate y camara". Deja la escena de una pasada y es idempotente (se puede
/// ejecutar tantas veces como quieras):
///   - Crea las capas Player y Enemy si no existen.
///   - Pone al jugador en la capa Player y a los enemigos en la capa Enemy.
///   - Camara: solo colisiona con geometria (Default|Ground). Antes colisionaba tambien
///     con el propio jugador y con los enemigos (todos estaban en Default), asi que un
///     enemigo acercandose por detras empujaba la camara contra ti.
///   - PlayerMotor: el suelo se detecta en todas las capas menos Player (no puedes
///     hacer de suelo de ti mismo).
///   - EnemyAI: la linea de vision la bloquean tambien los companeros (en un monton,
///     solo disparan los de delante) y los tiros llevan dispersion.
///   - EnemySpawner: mantiene el total en los 12 enemigos que ya hay colocados.
/// Menu: Tools > CyberShooter > Combate y camara (capas y mascaras)
/// </summary>
public static class CombatFeelSetup
{
    private const string PlayerLayerName = "Player";
    private const string EnemyLayerName = "Enemy";
    private const int MaxEnemiesAlive = 12;
    private const float EnemyAimSpreadDegrees = 3f;

    [MenuItem("Tools/CyberShooter/Combate y camara (capas y mascaras)")]
    public static void Setup()
    {
        int playerLayer = EnsureLayer(PlayerLayerName);
        int enemyLayer = EnsureLayer(EnemyLayerName);

        if (playerLayer < 0 || enemyLayer < 0)
        {
            Debug.LogError("[CombatFeelSetup] No hay hueco libre para las capas Player/Enemy (indices 8..31).");
            return;
        }

        int geoMask = LayerMask.GetMask("Default", "Ground");
        int obstacleMask = LayerMask.GetMask("Default", "Ground", PlayerLayerName, EnemyLayerName);
        int playerMask = LayerMask.GetMask(PlayerLayerName);
        int groundMask = ~(1 << playerLayer);

        var scene = EditorSceneManager.GetActiveScene();
        var report = new System.Text.StringBuilder();
        report.Append("[CombatFeelSetup] capa Player=").Append(playerLayer)
              .Append("  capa Enemy=").Append(enemyLayer).AppendLine();

        // ---------- Jugador ----------
        GameObject player = GameObject.Find("Player12");
        if (player == null) player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            SetLayerRecursive(player, playerLayer);

            var motor = player.GetComponent<PlayerMotor>();
            if (motor != null)
            {
                var so = new SerializedObject(motor);
                var prop = so.FindProperty("groundLayer");
                if (prop != null)
                {
                    prop.intValue = groundMask;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(motor);
                    report.Append("  PlayerMotor.groundLayer = ").Append(groundMask)
                          .AppendLine("  (todo menos la capa Player)");
                }
            }

            report.Append("  ").Append(player.name).Append(" -> capa ").Append(playerLayer).AppendLine();
        }
        else
        {
            report.AppendLine("  AVISO: no encontre al jugador (Player12 ni tag Player)");
        }

        // ---------- Enemigos ya colocados en la escena ----------
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            SetLayerRecursive(enemies[i], enemyLayer);

            var ai = enemies[i].GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.playerLayer = playerMask;
                ai.obstacleLayer = obstacleMask;
                if (ai.aimSpreadDegrees <= 0.01f) ai.aimSpreadDegrees = EnemyAimSpreadDegrees;
                EditorUtility.SetDirty(ai);
            }
        }
        report.Append("  enemigos recolocados: ").Append(enemies.Length)
              .Append("  (obstacleLayer=").Append(obstacleMask).AppendLine(")");

        // ---------- Camara ----------
        Camera cam = Camera.main;
        if (cam != null)
        {
            int oldCulling = cam.cullingMask;
            cam.cullingMask |= (1 << playerLayer) | (1 << enemyLayer);
            if (cam.cullingMask != oldCulling)
            {
                EditorUtility.SetDirty(cam);
                report.Append("  camara.cullingMask: ").Append(oldCulling).Append(" -> ")
                      .Append(cam.cullingMask).AppendLine();
            }

            var tpc = cam.GetComponent<ThirdPersonCameraController>();
            if (tpc != null)
            {
                var so = new SerializedObject(tpc);
                var prop = so.FindProperty("collisionLayers");
                if (prop != null)
                {
                    prop.intValue = geoMask;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(tpc);
                    report.Append("  camara.collisionLayers = ").Append(geoMask)
                          .AppendLine("  (solo Default|Ground: ni tu cuerpo ni los enemigos la empujan)");
                }
            }
        }
        else
        {
            report.AppendLine("  AVISO: no hay Camera.main en la escena");
        }

        // ---------- Spawner ----------
        var spawner = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        if (spawner != null)
        {
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("maxEnemiesAlive");
            if (prop != null)
            {
                prop.intValue = MaxEnemiesAlive;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(spawner);
                report.Append("  EnemySpawner.maxEnemiesAlive = ").Append(MaxEnemiesAlive)
                      .AppendLine("  (los 12 de la escena cuentan: no crece sin control)");
            }
        }
        else
        {
            report.AppendLine("  AVISO: no encontre EnemySpawner en la escena");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        report.Append("  escena '").Append(scene.name).Append("' guardada = ").Append(saved);

        Debug.Log(report.ToString());
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;

        Transform[] all = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
    }

    /// <summary>
    /// Devuelve el indice de la capa y la crea en el primer hueco libre (8..31) si no existe.
    /// </summary>
    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return -1;

        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        if (layers == null) return -1;

        for (int i = 8; i < layers.arraySize; i++)
        {
            var element = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("[CombatFeelSetup] capa '" + layerName + "' creada en el indice " + i);
                return i;
            }
        }

        return -1;
    }
}
