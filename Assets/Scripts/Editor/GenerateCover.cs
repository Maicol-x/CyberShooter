using UnityEngine;
using UnityEditor;

/// <summary>
/// Genera cubos de cobertura (muros/columnas) a lo largo del mapa
/// para que el jugador pueda romper la línea de visión de los enemigos.
/// Se distribuyen desde Z=0 hasta Z=400 (donde está la zona de meta).
/// </summary>
public class GenerateCover
{
    [MenuItem("Tools/CyberShooter/Generate Cover Cubes")]
    public static void Generate()
    {
        Debug.Log("=== GENERANDO CUBOS DE COBERTURA ===");

        // Encontrar o crear contenedor
        GameObject coverParent = GameObject.Find("CoverObjects");
        if (coverParent == null)
        {
            coverParent = new GameObject("CoverObjects");
            coverParent.transform.position = Vector3.zero;
        }

        // Limpiar cubos existentes
        foreach (Transform child in coverParent.transform)
        {
            Object.DestroyImmediate(child.gameObject);
        }

        // Material gris para muros
        Material wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        wallMat.color = new Color(0.4f, 0.4f, 0.45f);

        int count = 0;
        float zStart = 20f;   // Empezar un poco después del jugador
        float zEnd = 380f;    // Terminar antes de la zona de meta
        int columns = 8;      // Columnas a lo largo del eje X
        int rows = 6;         // Filas a lo largo del eje Z

        float zStep = (zEnd - zStart) / rows;

        System.Random rand = new System.Random(42); // Semilla fija para reproducibilidad

        for (int z = 0; z < rows; z++)
        {
            float zPos = zStart + (z * zStep) + (float)(rand.NextDouble() * zStep * 0.3f - zStep * 0.15f);

            for (int x = 0; x < columns; x++)
            {
                // Solo generar algunos cubos (probabilidad de 35%)
                if ((float)rand.NextDouble() > 0.35f)
                    continue;

                float xPos = -50f + (x * (100f / (columns - 1))) + (float)(rand.NextDouble() * 10f - 5f);

                // No generar muy cerca del jugador (0,0,0)
                if (Mathf.Abs(xPos) < 15f && zPos < 30f)
                    continue;

                // No generar muy cerca de la zona de meta
                if (zPos > 360f && Mathf.Abs(xPos) < 20f)
                    continue;

                // Tamaño aleatorio del cubo
                float width = Random.Range(4f, 12f);
                float height = Random.Range(3f, 8f);
                float depth = Random.Range(4f, 12f);

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Cover_{z}_{x}";
                cube.transform.position = new Vector3(xPos, height * 0.5f, zPos);
                cube.transform.localScale = new Vector3(width, height, depth);
                cube.tag = "Untagged";
                cube.layer = LayerMask.NameToLayer("Default");

                // Material gris
                cube.GetComponent<Renderer>().material = wallMat;

                // Rigidbody kinematic para que no caiga
                var rb = cube.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                cube.transform.SetParent(coverParent.transform);
                count++;
            }
        }

        Debug.Log($"Generados {count} cubos de cobertura desde Z={zStart} hasta Z={zEnd}");
        Debug.Log("=== COBERTURA GENERADA ===");
    }

    [MenuItem("Tools/CyberShooter/Clear Cover Cubes")]
    public static void ClearCover()
    {
        GameObject coverParent = GameObject.Find("CoverObjects");
        if (coverParent != null)
        {
            Object.DestroyImmediate(coverParent);
            Debug.Log("Cubos de cobertura eliminados");
        }
        else
        {
            Debug.Log("No hay cubos de cobertura que eliminar");
        }
    }

    [MenuItem("Tools/CyberShooter/Validate Phase 7")]
    public static void Validate()
    {
        Debug.Log("=== VALIDANDO FASE 7 ===");

        // Spawner
        GameObject spawnerObj = GameObject.Find("EnemySpawner");
        if (spawnerObj != null)
        {
            var spawner = spawnerObj.GetComponent<EnemySpawner>();
            if (spawner != null)
            {
                int enemiesAlive = GameObject.FindGameObjectsWithTag("Enemy").Length;
                Debug.Log($"EnemySpawner: OK, Enemies alive: {enemiesAlive}, Max: {spawner.GetMaxEnemies()}");
            }
        }

        // Zona de meta
        GameObject ez = GameObject.Find("ExtractionZone");
        if (ez != null)
            Debug.Log($"ExtractionZone: OK at {ez.transform.position}");
        else
            Debug.LogError("ExtractionZone NOT FOUND!");

        // Cobertura
        GameObject coverParent = GameObject.Find("CoverObjects");
        if (coverParent != null)
        {
            int coverCount = coverParent.transform.childCount;
            Debug.Log($"CoverObjects: {coverCount} cubos de cobertura");
        }
        else
        {
            Debug.LogWarning("CoverObjects not found - generate cover cubes first");
        }

        Debug.Log("=== VALIDACIÓN FASE 7 COMPLETADA ===");
    }
}