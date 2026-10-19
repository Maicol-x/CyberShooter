using UnityEngine;

/// <summary>
/// Posiciona la cámara apuntando hacia la zona de meta (ExtractionZone)
/// para que el jugador sepa hacia dónde ir al iniciar el juego.
/// </summary>
public class CameraTargetSetup : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 10, -20);
    [SerializeField] private bool lookAtTarget = true;

    private void Awake()
    {
        if (target == null)
        {
            // OJO: GameObject.FindWithTag LANZA una excepción si el tag no existe en
            // el proyecto; buscamos por nombre (el objeto se llama igual).
            var extractionZone = GameObject.Find("ExtractionZone");
            if (extractionZone != null)
                target = extractionZone.transform;
        }

        if (target != null)
        {
            // Posicionar la cámara mirando hacia la zona de meta
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = target.position + cameraOffset;

                // lookAtTarget estaba sin usar (CS0414): ahora decide de verdad si
                // además orientamos la cámara hacia la meta.
                if (lookAtTarget)
                    cam.transform.LookAt(target.position + Vector3.up * 5f);

                Debug.Log($"[CameraTargetSetup] Cámara posicionada mirando hacia ExtractionZone en {target.position} (lookAt={lookAtTarget})");
            }
        }
    }
}