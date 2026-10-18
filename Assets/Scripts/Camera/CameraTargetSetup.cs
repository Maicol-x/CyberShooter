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
            var ez = GameObject.FindWithTag("ExtractionZone");
            if (ez == null)
            {
                var extractionZone = GameObject.Find("ExtractionZone");
                if (extractionZone != null)
                    target = extractionZone.transform;
            }
            else
            {
                target = ez.transform;
            }
        }

        if (target != null)
        {
            // Posicionar la cámara mirando hacia la zona de meta
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = target.position + cameraOffset;
                cam.transform.LookAt(target.position + Vector3.up * 5f);
                Debug.Log($"[CameraTargetSetup] Cámara posicionada mirando hacia ExtractionZone en {target.position}");
            }
        }
    }
}