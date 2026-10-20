using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CyberShooter.EditorTools
{
    /// <summary>
    /// BORRA EL ENTORNO del mapa (suelo, muros, coberturas, props, techo/recinto
    /// y luces de ambiente) conservando TODO lo de gameplay:
    /// Player12, camara, enemigos/spawner, zona de extraccion, pickups, HUD,
    /// scripts, luz direccional global y Volume de post-proceso.
    ///
    /// Menu: Tools > CyberShooter > Map > Borrar entorno
    /// </summary>
    public static class MapWipe
    {
        private const string ScenePath = "Assets/_Project/Scenes/SampleScene.unity";

        // Objetos raiz que forman el "mapa". Se eliminan completos (con hijos).
        private static readonly string[] DeleteRoots =
        {
            "Structures",
            "Ground",
            "CoverObjects",
            "ArtPass_Props",
            "ArtPass_Lights",
            "ArtPass_ReflectionProbe",
            "Enclosure_Walls",
            "Enclosure_Ceiling",
            "Enclosure_Columns",
            "Enclosure_Lights",
            "ZoneMarker",
            "MarkerCube"
        };

        // Lo que debe sobrevivir (solo para el informe final).
        private static readonly string[] KeepRoots =
        {
            "Player12",
            "Main Camera",
            "Enemies",
            "EnemySpawner",
            "ExtractionZone",
            "UIManager",
            "Directional Light",
            "Global Volume"
        };

        [MenuItem("Tools/CyberShooter/Map/Borrar entorno")]
        public static void Wipe()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MapWipe] No se ejecuta en Play Mode.");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[MapWipe] Escena inesperada: " + scene.path + ". Abortado.");
                return;
            }

            var roots = scene.GetRootGameObjects();
            int deletedRoots = 0;
            int deletedChildren = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                if (!IsIn(go.name, DeleteRoots)) continue;

                deletedChildren += go.transform.childCount;
                Debug.Log("[MapWipe] Borrando: " + go.name + " (hijos=" + go.transform.childCount + ")");
                Object.DestroyImmediate(go);
                deletedRoots++;
            }

            // La luz direccional vuelve a los valores del Art Pass (0.30 y
            // shadowDistance 120 eran ajustes del recinto, ya no aplican).
            var dirGo = GameObject.Find("Directional Light");
            if (dirGo != null)
            {
                var L = dirGo.GetComponent<Light>();
                if (L != null)
                {
                    L.color = new Color(0.60f, 0.74f, 0.98f);
                    L.intensity = 1.05f;
                    L.shadows = LightShadows.Soft;
                    L.shadowStrength = 0.80f;
                }
            }

            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                var so = new SerializedObject(urp);
                var prop = so.FindProperty("m_ShadowDistance");
                if (prop != null)
                {
                    prop.floatValue = 50f;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(urp);
                }
            }

            RenderSettings.ambientIntensity = 1.15f;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Informe de lo que queda.
            var remaining = scene.GetRootGameObjects();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[MapWipe] Raices borradas=" + deletedRoots + " hijos=" + deletedChildren);
            sb.AppendLine("[MapWipe] Quedan " + remaining.Length + " raices:");

            for (int i = 0; i < remaining.Length; i++)
            {
                string tag = IsIn(remaining[i].name, KeepRoots) ? "  (gameplay)" : "  (otro)";
                sb.AppendLine("  - " + remaining[i].name + tag);
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/CyberShooter/Map/Verificar escena")]
        public static void Verify()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("[MapWipe] Escena: " + scene.path + "  dirty=" + scene.isDirty);
            sb.AppendLine("  raices=" + roots.Length);

            int env = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsIn(roots[i].name, DeleteRoots)) env++;
                sb.AppendLine("  - " + roots[i].name + " hijos=" + roots[i].transform.childCount);
            }
            sb.AppendLine("  restos de entorno = " + env);

            Debug.Log(sb.ToString());
        }

        private static bool IsIn(string name, string[] list)
        {
            for (int i = 0; i < list.Length; i++)
                if (list[i] == name) return true;
            return false;
        }
    }
}
