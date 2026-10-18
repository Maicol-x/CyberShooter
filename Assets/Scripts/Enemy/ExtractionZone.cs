using UnityEngine;

/// <summary>
/// Zona de meta del nivel tipo "Gauntlet" de DOOM.
/// Cuando el jugador entra, muestra "NIVEL COMPLETADO" y congela el tiempo.
/// </summary>
public class ExtractionZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private Color zoneColor = Color.green;
    [SerializeField] private float zoneSize = 5f;
    [SerializeField] private string levelCompleteMessage = "NIVEL COMPLETADO";

    [Header("Marker Settings")]
    [SerializeField] private Color markerColor = Color.blue;
    [SerializeField] private float markerHeight = 30f;
    [SerializeField] private float markerWidth = 5f;

    private bool _triggered = false;

    private void Awake()
    {
        // Crear visual del cubo verde si no hay mesh
        var meshFilter = GetComponent<MeshFilter>();
        var meshRenderer = GetComponent<MeshRenderer>();
        var collider = GetComponent<Collider>();

        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = zoneColor;
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 1);
            mat.SetFloat("_ZWrite", 0);
            mat.renderQueue = 3000;
            meshRenderer.material = mat;
        }

        if (collider == null)
        {
            var boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(zoneSize, zoneSize, zoneSize);
        }

        // Asegurar que el collider es trigger
        var existingCollider = GetComponent<Collider>();
        if (existingCollider != null)
        {
            existingCollider.isTrigger = true;
        }

        transform.localScale = Vector3.one;

        // Crear estructura alta azul como marcador visual de la meta
        CreateMarker();
    }

    private void CreateMarker()
    {
        // Crear objeto padre para la estructura del marcador
        GameObject markerObj = new GameObject("ZoneMarker");
        markerObj.transform.SetParent(transform);
        markerObj.transform.localPosition = Vector3.zero;
        markerObj.transform.localRotation = Quaternion.identity;

        // Cubo alto azul (la estructura visible desde lejos)
        GameObject markerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        markerCube.name = "MarkerCube";
        markerCube.transform.SetParent(markerObj.transform);
        markerCube.transform.localPosition = new Vector3(0, markerHeight * 0.5f, 0);
        markerCube.transform.localScale = new Vector3(markerWidth, markerHeight, markerWidth);

        // Remover collider del marcador para que no interfiera con gameplay
        var markerCollider = markerCube.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Object.Destroy(markerCollider);
        }

        // Material azul brillante
        var renderer = markerCube.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = markerColor;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", markerColor * 1.5f);
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 1);
        mat.SetFloat("_ZWrite", 0);
        mat.renderQueue = 3000;
        renderer.material = mat;

        // Añadir un pequeño cilindro en la base para que se vea más estable
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.name = "MarkerBase";
        baseObj.transform.SetParent(markerObj.transform);
        baseObj.transform.localPosition = new Vector3(0, markerHeight * 0.5f, 0);
        baseObj.transform.localScale = new Vector3(markerWidth * 1.5f, 0.5f, markerWidth * 1.5f);
        baseObj.transform.rotation = Quaternion.Euler(90, 0, 0);

        var baseCollider = baseObj.GetComponent<Collider>();
        if (baseCollider != null)
        {
            Object.Destroy(baseCollider);
        }

        var baseRenderer = baseObj.GetComponent<Renderer>();
        var baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        baseMat.color = markerColor;
        baseMat.EnableKeyword("_EMISSION");
        baseMat.SetColor("_EmissionColor", markerColor * 1.5f);
        baseMat.SetFloat("_Surface", 1);
        baseMat.SetFloat("_Blend", 1);
        baseMat.SetFloat("_ZWrite", 0);
        baseMat.renderQueue = 3000;
        baseRenderer.material = baseMat;

        Debug.Log($"[ExtractionZone] Marcador azul creado: altura {markerHeight}m, ancho {markerWidth}m");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;

        if (other.CompareTag("Player"))
        {
            _triggered = true;
            Debug.Log("[ExtractionZone] NIVEL COMPLETADO!");

            // Mostrar UI de nivel completado
            var ui = Object.FindAnyObjectByType<UIManager>();
            if (ui != null)
            {
                ui.ShowLevelComplete();
            }

            // Congelar el tiempo
            Time.timeScale = 0f;

            Debug.Log($"[ExtractionZone] {levelCompleteMessage} - Tiempo congelado");
        }
    }
}