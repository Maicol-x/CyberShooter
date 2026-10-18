using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupPhase7Marker
{
    [MenuItem("Tools/CyberShooter/Setup Phase 7 (Visible Marker)")]
    public static void Setup()
    {
        Debug.Log("=== SETUP FASE 7: MARCAR ZONA DE META ===");

        // 1. Configurar ExtractionZone con estructura azul visible
        GameObject ez = GameObject.Find("ExtractionZone");
        if (ez == null)
        {
            Debug.LogError("ExtractionZone no encontrada. Ejecuta primero 'Setup Phase 6'");
            return;
        }

        var ezComponent = ez.GetComponent<ExtractionZone>();
        if (ezComponent == null)
        {
            ezComponent = ez.AddComponent<ExtractionZone>();
        }

        // Configurar marker más visible
        var so = new SerializedObject(ezComponent);
        so.FindProperty("markerColor").colorValue = Color.blue;
        so.FindProperty("markerHeight").floatValue = 30f;
        so.FindProperty("markerWidth").floatValue = 5f;
        so.ApplyModifiedProperties();

        Debug.Log($"ExtractionZone configurada con marcador azul en {ez.transform.position}");

        // 2. Añadir CameraTargetSetup a la cámara
        Camera cam = Camera.main;
        if (cam != null)
        {
            var camTarget = cam.GetComponent<CameraTargetSetup>();
            if (camTarget == null)
            {
                camTarget = cam.gameObject.AddComponent<CameraTargetSetup>();
            }

            var camTargetSO = new SerializedObject(camTarget);
            camTargetSO.FindProperty("target").objectReferenceValue = ez.transform;
            camTargetSO.FindProperty("cameraOffset").vector3Value = new Vector3(0, 10, -25);
            camTargetSO.ApplyModifiedProperties();

            Debug.Log("CameraTargetSetup añadido a Main Camera - apuntará hacia la zona de meta");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("=== FASE 7 COMPLETADA: Marcador azul visible ===");
    }

    [MenuItem("Tools/CyberShooter/Validate Phase 7 Marker")]
    public static void Validate()
    {
        Debug.Log("=== VALIDANDO FASE 7 (MARCADOR) ===");

        GameObject ez = GameObject.Find("ExtractionZone");
        if (ez != null)
        {
            var ezComp = ez.GetComponent<ExtractionZone>();
            Debug.Log($"ExtractionZone: OK, Posición: {ez.transform.position}");
            if (ezComp != null)
            {
                var so = new SerializedObject(ezComp);
                float height = so.FindProperty("markerHeight").floatValue;
                float width = so.FindProperty("markerWidth").floatValue;
                Debug.Log($"  Marcador azul: altura={height}m, ancho={width}m");
            }
        }
        else
        {
            Debug.LogError("ExtractionZone NO ENCONTRADA");
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            var camTarget = cam.GetComponent<CameraTargetSetup>();
            Debug.Log($"CameraTargetSetup: {(camTarget != null ? "OK" : "FALTANTE")}");
        }

        Debug.Log("=== VALIDACIÓN COMPLETADA ===");
    }
}