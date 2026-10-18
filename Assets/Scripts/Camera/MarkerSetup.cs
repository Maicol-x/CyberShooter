using UnityEngine;

/// <summary>
/// Configura el marcador azul de la zona de meta.
/// Se añade al GameObject ZoneMarker bajo ExtractionZone.
/// </summary>
public class MarkerSetup : MonoBehaviour
{
    [SerializeField] private float height = 30f;
    [SerializeField] private float width = 5f;
    [SerializeField] private Color markerColor = Color.blue;

    private void Awake()
    {
        // Configurar escala del cubo
        transform.localScale = new Vector3(width, height, width);
        transform.localPosition = new Vector3(0, height * 0.5f, 0);

        // Material azul brillante
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = markerColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", markerColor * 2f);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 1);
            mat.SetFloat("_ZWrite", 0);
            mat.renderQueue = 3000;
            renderer.material = mat;
        }

        Debug.Log($"[MarkerSetup] Marcador azul creado: {width}x{height}x{width} en {transform.position}");
    }
}