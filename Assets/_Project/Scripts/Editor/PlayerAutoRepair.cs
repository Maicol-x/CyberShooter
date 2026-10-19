#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Repara automáticamente al jugador (Player12) en la escena activa.
///
/// ¿Por qué existe esto?
/// Player12 solo tenía Transform + Animator. Le faltaban el CharacterController y
/// TODOS los scripts (PlayerMotor, PlayerShooting, PlayerHealth,
/// PlayerAnimationController). Sin PlayerMotor el jugador NO puede moverse.
///
/// Este script añade lo que falte de forma idempotente y segura.
/// Se ejecuta:
///   - Al terminar de compilar / recargar el dominio.
///   - Justo ANTES de entrar en Play Mode (garantiza que funcione sin guardar).
///   - Manualmente desde: Tools > CyberShooter > Reparar Player12
/// </summary>
[InitializeOnLoad]
public static class PlayerAutoRepair
{
    private const string PlayerObjectName = "Player12";

    static PlayerAutoRepair()
    {
        EditorApplication.delayCall += AutoRepair;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            AutoRepair();
    }

    [MenuItem("Tools/CyberShooter/Reparar Player12")]
    public static void AutoRepair()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject player = GameObject.Find(PlayerObjectName);
        if (player == null)
            player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        bool changed = false;

        // 1) CharacterController (PlayerMotor lo requiere, pero lo aseguramos primero).
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = player.AddComponent<CharacterController>();
            changed = true;
        }

        if (cc != null)
        {
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.08f;
            cc.minMoveDistance = 0f;
        }

        // 2) Scripts del jugador (cada uno se auto-asigna sus referencias en Awake).
        if (player.GetComponent<PlayerMotor>() == null) { player.AddComponent<PlayerMotor>(); changed = true; }
        if (player.GetComponent<PlayerShooting>() == null) { player.AddComponent<PlayerShooting>(); changed = true; }
        if (player.GetComponent<PlayerHealth>() == null) { player.AddComponent<PlayerHealth>(); changed = true; }
        if (player.GetComponent<PlayerAnimationController>() == null) { player.AddComponent<PlayerAnimationController>(); changed = true; }

        // 3) Punto de disparo (origen del trazador). Si no existe, PlayerShooting usa la cámara.
        if (player.transform.Find("MuzzlePoint") == null)
        {
            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(player.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.1f, 0.6f);
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PlayerAutoRepair] Player12 reparado: CharacterController + PlayerMotor + " +
                      "PlayerShooting + PlayerHealth + PlayerAnimationController (+ MuzzlePoint).");
        }
    }
}
#endif
