using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberShooter.EditorTools
{
    /// <summary>
    /// GENERADOR DE NIVELES POR TEXTO (grid-based).
    ///
    /// Menu: Tools > CyberShooter > Construir Mapa
    ///
    /// Leyenda:
    ///   #  Cubo pared       escala (5, 5, 5)
    ///   .  Losa de suelo    escala (5, 0.1, 5)
    ///   C  Cobertura        escala (3, 2, 3)
    ///   P  Mueve a Player12 a esa celda
    ///   E  Instancia el prefab EnemyHumanoid en esa celda
    ///
    /// Idempotente: antes de construir borra el grupo "GeneratedMap".
    /// No guarda la escena automaticamente: la deja marcada como modificada
    /// para que puedas revisarla (o deshacer con Ctrl+Z).
    /// </summary>
    public static class MapBuilder
    {
        // ------------------------------------------------------------------
        //  Configuracion
        // ------------------------------------------------------------------

        private const string GeneratedRootName = "GeneratedMap";
        private const string MaterialFolder = "Assets/_Project/Materials/Environment";
        private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/EnemyHumanoid.prefab";

        /// <summary>Tamano de celda en metros.</summary>
        private const float TileSize = 5f;

        /// <summary>
        /// Si es true, las celdas P y E tambien generan suelo debajo.
        /// Sin esto el jugador y los enemigos caerian al vacio.
        /// Ponlo a false si quieres el comportamiento estrictamente literal.
        /// </summary>
        private const bool FloorUnderMarkers = true;

        /// <summary>
        /// Mapa de prueba. Filas = Z, columnas = X.
        /// Puedes sustituir este array por cualquier otro mapa.
        /// </summary>
        private static readonly string[] TestMap =
        {
            "##########",
            "#........#",
            "#..C..C..#",
            "#........#",
            "#E......E#",
            "#...P....#",
            "##########"
        };

        // ------------------------------------------------------------------
        //  Menu
        // ------------------------------------------------------------------

        [MenuItem("Tools/CyberShooter/Construir Mapa")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MapBuilder] No se puede construir el mapa en Play Mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MapBuilder] No hay una escena valida abierta.");
                return;
            }

            // --- Idempotencia: borrar el mapa anterior ---
            GameObject previous = GameObject.Find(GeneratedRootName);
            if (previous != null)
            {
                Undo.DestroyObjectImmediate(previous);
                Debug.Log("[MapBuilder] Mapa anterior eliminado.");
            }

            var root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Construir Mapa");

            // --- Materiales ---
            Material wallMat = LoadMaterial("M_Env_Wall_Panel");
            Material coverMat = LoadMaterial("M_Env_Cover_Industrial");

            Material floorMat = LoadMaterial("M_Env_Ground_Deck");
            if (floorMat == null) floorMat = LoadMaterial("M_Env_Ground_Base");

            // --- Prefab de enemigo ---
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (enemyPrefab == null)
            {
                Debug.LogError("[MapBuilder] No encontre el prefab en: " + EnemyPrefabPath);
            }

            int walls = 0;
            int floors = 0;
            int covers = 0;
            int enemies = 0;
            bool foundPlayer = false;
            Vector3 playerPosition = Vector3.zero;

            // --- Recorrido fila por fila / columna por columna ---
            for (int row = 0; row < TestMap.Length; row++)
            {
                string line = TestMap[row];

                for (int col = 0; col < line.Length; col++)
                {
                    char cell = line[col];

                    // Centro de la celda en el mundo (X = columna, Z = fila).
                    Vector3 tile = new Vector3(col * TileSize, 0f, row * TileSize);

                    switch (cell)
                    {
                        case '#':
                            SpawnBox(
                                root.transform,
                                "Wall_" + row + "_" + col,
                                tile + Vector3.up * (TileSize * 0.5f),
                                new Vector3(TileSize, TileSize, TileSize),
                                wallMat);
                            walls++;
                            break;

                        case '.':
                            SpawnFloor(root.transform, "Floor_" + row + "_" + col, tile, floorMat);
                            floors++;
                            break;

                        case 'C':
                            SpawnBox(
                                root.transform,
                                "Cover_" + row + "_" + col,
                                tile + Vector3.up * 1f,
                                new Vector3(3f, 2f, 3f),
                                coverMat);
                            covers++;
                            break;

                        case 'P':
                            if (FloorUnderMarkers)
                            {
                                SpawnFloor(root.transform, "Floor_" + row + "_" + col, tile, floorMat);
                                floors++;
                            }
                            playerPosition = tile;
                            foundPlayer = true;
                            break;

                        case 'E':
                            if (FloorUnderMarkers)
                            {
                                SpawnFloor(root.transform, "Floor_" + row + "_" + col, tile, floorMat);
                                floors++;
                            }

                            if (enemyPrefab != null)
                            {
                                SpawnEnemy(root.transform, enemyPrefab, tile, row, col);
                                enemies++;
                            }
                            break;

                        default:
                            // Espacio u otro caracter: celda vacia.
                            break;
                    }
                }
            }

            // --- Mover al jugador ---
            if (foundPlayer)
            {
                MovePlayer(playerPosition);
            }
            else
            {
                Debug.LogWarning("[MapBuilder] El mapa no contiene ninguna 'P': el jugador no se movio.");
            }

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                "[MapBuilder] Mapa generado." +
                " Paredes=" + walls +
                " Suelos=" + floors +
                " Coberturas=" + covers +
                " Enemigos=" + enemies +
                " Jugador=" + (foundPlayer ? playerPosition.ToString("F1") : "sin P") +
                " | Celda=" + TileSize + " m");
        }

        [MenuItem("Tools/CyberShooter/Borrar Mapa Generado")]
        public static void Clear()
        {
            GameObject previous = GameObject.Find(GeneratedRootName);
            if (previous == null)
            {
                Debug.Log("[MapBuilder] No hay ningun mapa generado.");
                return;
            }

            Undo.DestroyObjectImmediate(previous);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[MapBuilder] Mapa generado eliminado.");
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        private static GameObject SpawnBox(Transform parent, string name, Vector3 center,
                                           Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;

            ApplyMaterial(go, material);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            return go;
        }

        private static GameObject SpawnFloor(Transform parent, string name, Vector3 tile, Material material)
        {
            // La losa tiene 0.1 m de grosor y su cara superior queda en y = 0.
            Vector3 center = tile + Vector3.up * -0.05f;
            return SpawnBox(parent, name, center, new Vector3(TileSize, 0.1f, TileSize), material);
        }

        private static void SpawnEnemy(Transform parent, GameObject prefab, Vector3 position, int row, int col)
        {
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.name = "Enemy_" + row + "_" + col;
            enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.identity;
        }

        private static void MovePlayer(Vector3 position)
        {
            GameObject player = GameObject.Find("Player12");
            if (player == null)
            {
                Debug.LogError("[MapBuilder] No encontre a 'Player12' en la escena.");
                return;
            }

            Undo.RecordObject(player.transform, "Mover Player12");
            // 5 cm sobre la losa para evitar z-fighting con la cara superior.
            player.transform.position = position + Vector3.up * 0.05f;
            Physics.SyncTransforms();

            Debug.Log("[MapBuilder] Player12 movido a " + player.transform.position.ToString("F2"));
        }

        private static Material LoadMaterial(string materialName)
        {
            string path = MaterialFolder + "/" + materialName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Debug.LogWarning("[MapBuilder] Material no encontrado: " + path);
            }

            return material;
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            if (material == null) return;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }
    }
}
