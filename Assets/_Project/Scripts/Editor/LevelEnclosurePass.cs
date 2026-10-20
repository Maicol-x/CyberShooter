using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CyberShooter.EditorTools
{
    /// <summary>
    /// ENCIERRA la zona jugable: 4 paredes modulares del MegaKit, techo a 10 m,
    /// columnas, luminarias de techo y arreglo de las coberturas que quedaban
    /// fuera del suelo (Z = 253 y Z = 324).
    ///
    /// No toca Player12, enemigos, scripts de gameplay ni colliders existentes
    /// (solo LEE las coberturas para validar posiciones).
    ///
    /// Idempotente: los grupos Enclosure_* se destruyen y se reconstruyen.
    ///
    /// Menu: Tools > CyberShooter > Enclosure
    /// </summary>
    public static class LevelEnclosurePass
    {
        // ------------------------------------------------------------------
        //  Configuracion del recinto
        // ------------------------------------------------------------------
        //  El suelo real va de -250 a 250, pero las coberturas utiles ocupan
        //  X -57..57 y Z 19..212.  X +/-50 cortaria las coberturas de los
        //  extremos (x=-53.7 y x=51.5), por eso el recinto se abre a +/-60.
        private const float MinX = -60f;
        private const float MaxX = 60f;
        private const float MinZ = -30f;
        private const float MaxZ = 222f;

        private const float CeilingY = 10f;
        private const float ModuleLength = 4f;
        private const float VerticalModule = 5f;

        private const string ScenePath = "Assets/_Project/Scenes/SampleScene.unity";
        private const string KitRoot = "Assets/_ThirdParty/Modular SciFi MegaKit[Standard]/FBX (Unity)/";
        private const string MatFolder = "Assets/_Project/Materials/Environment";

        private const string WallsGroup = "Enclosure_Walls";
        private const string CeilingGroup = "Enclosure_Ceiling";
        private const string ColumnsGroup = "Enclosure_Columns";
        private const string LightsGroup = "Enclosure_Lights";

        private static readonly string LogPath =
            @"C:\Users\maico\AppData\Local\Temp\opencode\cybershooter_enclosure\enclosure_log.txt";

        private static StringBuilder _log;

        // ------------------------------------------------------------------
        //  Menu
        // ------------------------------------------------------------------

        [MenuItem("Tools/CyberShooter/Enclosure/Aplicar recinto")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Enclosure] No se ejecuta en Play Mode.");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[Enclosure] Escena inesperada: " + scene.path + ". Abortado.");
                return;
            }

            _log = new StringBuilder();
            Log("=== RECINTO ===");
            Log("Escena: " + scene.path);

            EnsureFolder(MatFolder);

            // 1) Arreglar coberturas fuera de rango ANTES de levantar paredes,
            //    para que la validacion de solapes vea el escenario definitivo.
            MoveOutOfBoundsCovers();

            // 2) Muros, techo, columnas y luces.
            DestroyGroup(WallsGroup);
            DestroyGroup(CeilingGroup);
            DestroyGroup(ColumnsGroup);
            DestroyGroup(LightsGroup);

            BuildWalls();
            BuildCeiling();
            BuildColumns();
            BuildCeilingLights();
            TuneInteriorLighting();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Log("Escena guardada.");
            Debug.Log("[Enclosure] " + Flatten(_log));
            Save();
        }

        [MenuItem("Tools/CyberShooter/Enclosure/Verificar recinto")]
        public static void Verify()
        {
            _log = new StringBuilder();
            Log("=== VERIFICACION RECINTO ===");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Log("Escena: " + scene.path + "  dirty=" + scene.isDirty);

            // Grupos
            string[] groups = { WallsGroup, CeilingGroup, ColumnsGroup, LightsGroup };
            for (int i = 0; i < groups.Length; i++)
            {
                var go = GameObject.Find(groups[i]);
                Log("  " + groups[i] + " = " + (go != null ? "OK hijos=" + go.transform.childCount : "FALTA"));
            }

            // Rango del recinto
            Log("  rango X " + MinX + ".." + MaxX + "  Z " + MinZ + ".." + MaxZ + "  techo=" + CeilingY);

            // Coberturas fuera de rango
            var covers = GameObject.Find("CoverObjects");
            int outside = 0;
            if (covers != null)
            {
                foreach (Transform c in covers.transform)
                {
                    if (c.position.x < MinX || c.position.x > MaxX ||
                        c.position.z < MinZ || c.position.z > MaxZ)
                    {
                        Log("  COBERTURA FUERA: " + c.name + " " + c.position.ToString("F1"));
                        outside++;
                    }
                }
            }
            Log("  coberturas fuera del recinto = " + outside);

            // Coberturas sin suelo debajo
            int noFloor = 0;
            if (covers != null)
            {
                foreach (Transform c in covers.transform)
                {
                    var mr = c.GetComponent<MeshRenderer>();
                    if (mr == null) continue;
                    var b = mr.bounds;
                    Vector3 p = new Vector3(b.center.x, 5f, b.center.z);
                    if (!Physics.Raycast(p, Vector3.down, 40f, 1 << 8, QueryTriggerInteraction.Ignore))
                    {
                        Log("  SIN SUELO: " + c.name + " " + c.position.ToString("F1"));
                        noFloor++;
                    }
                }
            }
            Log("  coberturas sin suelo = " + noFloor);

            // Jugador y ruta
            var player = GameObject.Find("Player12");
            var ez = GameObject.Find("ExtractionZone");
            if (player != null && ez != null)
            {
                Log("  Player12 " + player.transform.position.ToString("F2") +
                    "  extraccion " + ez.transform.position.ToString("F1"));
            }

            // Luces
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            Log("  luces totales = " + lights.Length);

            // Techo
            var ceiling = GameObject.Find("Ceiling_Slab");
            if (ceiling != null)
            {
                var mr = ceiling.GetComponent<MeshRenderer>();
                Log("  techo bounds=" + (mr != null ? mr.bounds.size.ToString("F1") + " minY=" + mr.bounds.min.y.ToString("F2") : "?"));
            }

            // Shadow distance
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) Log("  shadowDistance = " + urp.shadowDistance);

            // Props con collider
            var props = GameObject.Find("ArtPass_Props");
            if (props != null)
            {
                int withCol = 0;
                foreach (Transform t in props.transform)
                    if (t.GetComponent<Collider>() != null) withCol++;
                Log("  props con collider = " + withCol + "/" + props.transform.childCount);
            }

            Debug.Log("[Enclosure] " + Flatten(_log));
            Save();
        }

        [MenuItem("Tools/CyberShooter/Enclosure/Revertir recinto")]
        public static void Revert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("[Enclosure] Escena inesperada."); return; }

            _log = new StringBuilder();
            Log("=== REVERTIR RECINTO ===");

            DestroyGroup(WallsGroup);
            DestroyGroup(CeilingGroup);
            DestroyGroup(ColumnsGroup);
            DestroyGroup(LightsGroup);

            // Direccional y sombras a los valores previos.
            var dir = GameObject.Find("Directional Light");
            if (dir != null)
            {
                var L = dir.GetComponent<Light>();
                if (L != null) L.intensity = 1.05f;
            }
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) SetShadowDistance(urp, 50f);

            Log("Grupos del recinto eliminados. Direccional 1.05, shadowDistance 50.");
            Debug.Log("[Enclosure] " + Flatten(_log));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Save();
        }

        // ------------------------------------------------------------------
        //  Muros
        // ------------------------------------------------------------------

        private static void BuildWalls()
        {
            var root = new GameObject(WallsGroup);
            var wall = KitRoot + "Walls/WallAstra_Straight.fbx";
            var top = KitRoot + "Walls/TopSimple_Straight.fbx";

            // Mismo material que los 4 muros del Art Pass: paneles oscuros con
            // tiras emisivas ambar, para que las luces ambar resalten.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_Env_Wall_Panel.mat");
            if (mat == null) Log("AVISO: falta M_Env_Wall_Panel");

            int count = 0;

            // Este (X = MaxX): frente hacia -X.
            count += BuildSide(root.transform, wall, top, mat,
                new Vector3(MaxX - 2.1f, 0f, MinZ + 2f), new Vector3(0f, 0f, ModuleLength),
                Mathf.RoundToInt((MaxZ - MinZ) / ModuleLength), Quaternion.Euler(0f, 0f, 0f));

            // Oeste (X = MinX): frente hacia +X.
            count += BuildSide(root.transform, wall, top, mat,
                new Vector3(MinX + 2.1f, 0f, MaxZ - 2f), new Vector3(0f, 0f, -ModuleLength),
                Mathf.RoundToInt((MaxZ - MinZ) / ModuleLength), Quaternion.Euler(0f, 180f, 0f));

            // Norte (Z = MaxZ): frente hacia -Z.
            count += BuildSide(root.transform, wall, top, mat,
                new Vector3(MaxX - 2f, 0f, MaxZ + 2.1f), new Vector3(-ModuleLength, 0f, 0f),
                Mathf.RoundToInt((MaxX - MinX) / ModuleLength), Quaternion.Euler(0f, 270f, 0f));

            // Sur (Z = MinZ): frente hacia +Z.
            count += BuildSide(root.transform, wall, top, mat,
                new Vector3(MinX + 2f, 0f, MinZ - 2.1f), new Vector3(ModuleLength, 0f, 0f),
                Mathf.RoundToInt((MaxX - MinX) / ModuleLength), Quaternion.Euler(0f, 90f, 0f));

            Log("Muros: " + count + " piezas");
        }

        private static int BuildSide(Transform parent, string wallPath, string topPath, Material mat,
                                     Vector3 origin, Vector3 step, int count, Quaternion rot)
        {
            int placed = 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 p = origin + step * i;

                // Dos filas verticales de 5 m para llegar a los 10 m de techo.
                for (int row = 0; row < 2; row++)
                {
                    float y = row * VerticalModule;

                    if (Spawn(wallPath, parent, p + Vector3.up * y, rot, mat)) placed++;
                    if (Spawn(topPath, parent, p + Vector3.up * (y + 3f), rot, mat)) placed++;
                }
            }

            return placed;
        }

        private static bool Spawn(string fbxPath, Transform parent, Vector3 pos, Quaternion rot, Material mat = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null)
            {
                Log("FALTA PIEZA: " + fbxPath);
                return false;
            }

            var go = (GameObject)Object.Instantiate(prefab, pos, rot, parent);
            go.name = prefab.name;

            if (mat != null)
            {
                var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
                for (int r = 0; r < renderers.Length; r++) renderers[r].sharedMaterial = mat;
            }

            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            return true;
        }

        // ------------------------------------------------------------------
        //  Techo
        // ------------------------------------------------------------------

        private static void BuildCeiling()
        {
            var root = new GameObject(CeilingGroup);
            var mat = BuildCeilingMaterial();

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Ceiling_Slab";
            slab.transform.SetParent(root.transform, false);
            slab.transform.position = new Vector3(
                (MinX + MaxX) * 0.5f,
                CeilingY + 0.25f,
                (MinZ + MaxZ) * 0.5f);
            slab.transform.localScale = new Vector3(
                (MaxX - MinX) + 2f,
                0.5f,
                (MaxZ - MinZ) + 2f);

            var collider = slab.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var mr = slab.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;

            GameObjectUtility.SetStaticEditorFlags(slab,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            Log("Techo: " + slab.transform.localScale.ToString("F1") + " a y=" + CeilingY);
        }

        private static Material BuildCeilingMaterial()
        {
            const string name = "M_Env_Ceiling";
            string path = MatFolder + "/" + name + ".mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) { Log("ERROR: sin shader URP/Lit"); return null; }
                mat = new Material(shader);
                mat.name = name;
                AssetDatabase.CreateAsset(mat, path);
                Log("Material creado: " + name);
            }

            string tex = "Assets/_ThirdParty/NULLSIGNAL-SURFACES-VOL1-normalfix/TECH01/1K/NS_TECH01_1K_";
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "BaseColor.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Normal_GL.png");

            mat.SetColor("_BaseColor", new Color(0.40f, 0.43f, 0.48f));
            mat.SetFloat("_Metallic", 0.35f);
            mat.SetFloat("_Smoothness", 0.30f);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            if (normal != null) mat.SetTexture("_BumpMap", normal);
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetTexture("_SpecGlossMap", null);

            // 10 m por repeticion sobre 120x252 m.
            mat.SetTextureScale("_BaseMap", new Vector2(12f, 25f));
            mat.SetTextureScale("_BumpMap", new Vector2(12f, 25f));

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        //  Columnas
        // ------------------------------------------------------------------

        private static void BuildColumns()
        {
            var root = new GameObject(ColumnsGroup);
            var column = KitRoot + "Columns/Column_Simple.fbx";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_Env_Cover_Industrial.mat");
            if (mat == null) Log("AVISO: falta M_Env_Cover_Industrial");

            var positions = new List<Vector3>();

            // Esquinas.
            positions.Add(new Vector3(MaxX - 1.2f, 0f, MaxZ - 1.2f));
            positions.Add(new Vector3(MinX + 1.2f, 0f, MaxZ - 1.2f));
            positions.Add(new Vector3(MaxX - 1.2f, 0f, MinZ + 1.2f));
            positions.Add(new Vector3(MinX + 1.2f, 0f, MinZ + 1.2f));

            // Cada 24 m a lo largo de cada lado.
            for (float z = MinZ + 24f; z < MaxZ - 12f; z += 24f)
            {
                positions.Add(new Vector3(MaxX - 1.2f, 0f, z));
                positions.Add(new Vector3(MinX + 1.2f, 0f, z));
            }
            for (float x = MinX + 24f; x < MaxX - 12f; x += 24f)
            {
                positions.Add(new Vector3(x, 0f, MaxZ - 1.2f));
                positions.Add(new Vector3(x, 0f, MinZ + 1.2f));
            }

            int placed = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 p = positions[i];
                // Column_Simple mide 5 m: dos tramos para llegar al techo.
                if (Spawn(column, root.transform, p, Quaternion.identity, mat)) placed++;
                if (Spawn(column, root.transform, p + Vector3.up * 5f, Quaternion.identity, mat)) placed++;
            }

            Log("Columnas: " + placed + " piezas en " + positions.Count + " puntos");
        }

        // ------------------------------------------------------------------
        //  Luces de techo
        // ------------------------------------------------------------------

        private static void BuildCeilingLights()
        {
            var root = new GameObject(LightsGroup);
            var fixture = KitRoot + "Props/Prop_Light_Wide.fbx";
            var emissive = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_Env_Emissive_Amber.mat");

            float[] zs = { 10f, 55f, 100f, 145f, 190f, 215f };

            for (int i = 0; i < zs.Length; i++)
            {
                Vector3 p = new Vector3(0f, CeilingY - 0.35f, zs[i]);

                // Accesorio emisivo (visual).
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fixture);
                if (prefab != null)
                {
                    var go = (GameObject)Object.Instantiate(prefab, p, Quaternion.Euler(180f, 0f, 0f), root.transform);
                    go.name = "CeilingFixture_" + i;
                    if (emissive != null)
                    {
                        var rends = go.GetComponentsInChildren<MeshRenderer>(true);
                        for (int r = 0; r < rends.Length; r++) rends[r].sharedMaterial = emissive;
                    }
                }

                // Luz real (cool, para que el recinto no quede negro).
                var lightGo = new GameObject("CeilingLight_" + i);
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.position = new Vector3(0f, CeilingY - 0.6f, zs[i]);

                var L = lightGo.AddComponent<Light>();
                L.type = LightType.Point;
                L.color = new Color(0.72f, 0.83f, 1.0f);
                L.lightUnit = LightUnit.Lux;
                L.intensity = 55f;
                L.range = 75f;
                L.shadows = LightShadows.None;
                L.lightmapBakeType = LightmapBakeType.Realtime;

                var data = lightGo.GetComponent<UniversalAdditionalLightData>();
                if (data == null) data = lightGo.AddComponent<UniversalAdditionalLightData>();
                data.usePipelineSettings = true;
            }

            Log("Luces de techo: " + zs.Length);
        }

        // ------------------------------------------------------------------
        //  Iluminacion interior
        // ------------------------------------------------------------------

        private static void TuneInteriorLighting()
        {
            // El techo bloquea la "luna": se baja la direccional para que no
            // inunde el interior alli donde la distancia de sombras no llega.
            var dirGo = GameObject.Find("Directional Light");
            if (dirGo != null)
            {
                var L = dirGo.GetComponent<Light>();
                if (L != null)
                {
                    L.intensity = 0.30f;
                    L.shadows = LightShadows.Soft;
                }
            }

            // La distancia de sombras por defecto (50 m) es menor que el recinto
            // (252 m): sin subirla, el techo no proyectaria sombra en la mitad
            // del nivel y la direccional volveria a iluminar el suelo.
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                SetShadowDistance(urp, 120f);
                Log("shadowDistance -> " + urp.shadowDistance);
            }

            // Ambiente ligeramente mas alto para compensar el techo.
            RenderSettings.ambientIntensity = 1.25f;

            Log("Direccional 0.30, ambiente 1.25");
        }

        private static void SetShadowDistance(UniversalRenderPipelineAsset urp, float value)
        {
            var so = new SerializedObject(urp);
            var prop = so.FindProperty("m_ShadowDistance");
            if (prop != null)
            {
                prop.floatValue = value;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(urp);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Log("AVISO: no encontre m_ShadowDistance en el URP asset");
            }
        }

        // ------------------------------------------------------------------
        //  Arreglo de coberturas fuera de rango
        // ------------------------------------------------------------------

        private static void MoveOutOfBoundsCovers()
        {
            var covers = GameObject.Find("CoverObjects");
            if (covers == null) { Log("AVISO: no existe CoverObjects"); return; }

            int moved = 0;
            int failed = 0;

            foreach (Transform c in covers.transform)
            {
                // Solo las que quedan fuera del recinto.
                if (c.position.z <= MaxZ - 6f) continue;

                float targetZ = c.position.z > 290f ? 175f : 110f;
                bool ok = TryRelocate(c, targetZ);
                if (ok) moved++; else failed++;
            }

            Log("Coberturas reubicadas: " + moved + "  fallidas: " + failed);
        }

        private static bool TryRelocate(Transform cover, float targetZ)
        {
            var col = cover.GetComponent<BoxCollider>();
            var mr = cover.GetComponent<MeshRenderer>();

            Vector3 size;
            if (col != null) size = Vector3.Scale(col.size, cover.lossyScale);
            else if (mr != null) size = mr.bounds.size;
            else return false;

            Vector3 half = size * 0.5f;
            Quaternion rot = cover.rotation;

            float originalX = cover.position.x;
            float[] offsets = { 0f, 8f, -8f, 16f, -16f, 24f, -24f, 32f, -32f };

            for (int i = 0; i < offsets.Length; i++)
            {
                float x = Mathf.Clamp(originalX + offsets[i], MinX + half.x + 2f, MaxX - half.x - 2f);
                Vector3 candidate = new Vector3(x, cover.position.y, targetZ);

                // Excluye el suelo (capa 8) y la propia cobertura (sigue en su sitio).
                if (Physics.CheckBox(candidate + Vector3.up * half.y, half * 0.92f, rot,
                                     ~(1 << 8), QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 delta = candidate - cover.position;
                cover.position = candidate;
                Log("  " + cover.name + " movida " + delta.ToString("F1"));
                return true;
            }

            Log("  " + cover.name + " NO se pudo reubicar (sin hueco)");
            return false;
        }

        // ------------------------------------------------------------------
        //  Utilidades
        // ------------------------------------------------------------------

        private static void DestroyGroup(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
                Log("Grupo eliminado: " + name);
            }
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').Trim('/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            int slash = folder.LastIndexOf('/');
            string parent = slash < 0 ? "Assets" : folder.Substring(0, slash);
            string leaf = slash < 0 ? folder : folder.Substring(slash + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
            Log("Carpeta creada: " + folder);
        }

        private static string Flatten(StringBuilder sb)
        {
            return sb.ToString().Replace("\r\n", " | ").Replace("\n", " | ");
        }

        private static void Log(string line)
        {
            if (_log == null) _log = new StringBuilder();
            _log.AppendLine(line);
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, _log.ToString() + System.Environment.NewLine);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Enclosure] No pude escribir el log: " + e.Message);
            }
        }
    }
}
