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
    /// ART PASS del escenario: convierte el prototipo gris en una instalacion
    /// industrial Sci-Fi oscura.
    ///
    /// Solo toca ENTORNO: materiales de suelo/paredes/coberturas, props
    /// decorativos, iluminacion y post-proceso. No modifica Player12, enemigos,
    /// scripts de gameplay, colliders, ni la geometria del escenario.
    ///
    /// Es idempotente: los grupos de props y luces se recrean, los materiales se
    /// actualizan in-place y los ajustes de render se reaplican.
    ///
    /// Menu: Tools > CyberShooter > Art Pass
    /// </summary>
    public static class EnvironmentArtPass
    {
        // ------------------------------------------------------------------
        //  Rutas y constantes
        // ------------------------------------------------------------------

        private const string ScenePath = "Assets/_Project/Scenes/SampleScene.unity";
        private const string MatFolder = "Assets/_Project/Materials/Environment";
        private const string TextureRoot = "Assets/_ThirdParty/NULLSIGNAL-SURFACES-VOL1-normalfix";
        private const string PropRoot = "Assets/_ThirdParty/Modular SciFi MegaKit[Standard]/FBX (Unity)/Props";

        private const string PropsGroup = "ArtPass_Props";
        private const string LightsGroup = "ArtPass_Lights";

        private const int GroundLayer = 8;

        private static readonly string LogPath =
            @"C:\Users\maico\AppData\Local\Temp\opencode\cybershooter_artpass\artpass_log.txt";

        private static StringBuilder _log;

        // ------------------------------------------------------------------
        //  Definicion de superficies
        // ------------------------------------------------------------------

        private class Surface
        {
            public string Set;          // NS_DECK01
            public string MaterialName; // M_Env_Ground_Deck
            public Color BaseColor;
            public float Metallic;
            public float Smoothness;
            public Vector2 Tiling;
            public float NormalScale;
            public bool Emissive;
            public Color EmissionColor;
            public float EmissionIntensity;
            public bool UseEmissionMap;
        }

        private static Surface[] GroundSurfaces()
        {
            return new[]
            {
                new Surface {
                    Set = "NS_DECK01", MaterialName = "M_Env_Ground_Deck",
                    // El tinte debe ser casi blanco: la textura BaseColor ya aporta
                    // el color. Multiplicar textura oscura x tinte oscuro = negro.
                    // Los tres tintes se mantienen MUY proximos entre si: cada tile
                    // mide 100 m, asi que un contraste alto se lee como parches.
                    BaseColor = new Color(0.82f, 0.85f, 0.90f), Metallic = 0.42f, Smoothness = 0.46f,
                    Tiling = new Vector2(10f, 10f), NormalScale = 0.9f,
                },
                new Surface {
                    Set = "NS_GRATE01", MaterialName = "M_Env_Ground_Grate",
                    // La rejilla tiene el BaseColor mas oscuro del pack: se compensa
                    // con un tinte >1 para que no se lea como un agujero negro.
                    BaseColor = new Color(1.10f, 1.14f, 1.22f), Metallic = 0.34f, Smoothness = 0.30f,
                    Tiling = new Vector2(10f, 10f), NormalScale = 1.0f,
                },
                new Surface {
                    Set = "NS_TREAD01", MaterialName = "M_Env_Ground_Tread",
                    BaseColor = new Color(0.86f, 0.84f, 0.81f), Metallic = 0.38f, Smoothness = 0.42f,
                    Tiling = new Vector2(10f, 10f), NormalScale = 0.85f,
                },
            };
        }

        private static Surface GroundBaseSurface()
        {
            // El objeto raiz 'Ground' tiene un plano propio de 10x10 m superpuesto
            // a los tiles. Necesita tiling 1 (10 m por repeticion) para coincidir
            // con la escala de los tiles (100 m / 10 = 10 m por repeticion).
            return new Surface
            {
                Set = "NS_DECK01", MaterialName = "M_Env_Ground_Base",
                BaseColor = new Color(1.00f, 1.02f, 1.06f), Metallic = 0.42f, Smoothness = 0.46f,
                Tiling = new Vector2(1f, 1f), NormalScale = 0.9f,
            };
        }

        private static Surface WallSurface()
        {
            return new Surface
            {
                Set = "NS_WALL01", MaterialName = "M_Env_Wall_Panel",
                BaseColor = new Color(1.00f, 1.00f, 1.00f), Metallic = 0.30f, Smoothness = 0.36f,
                Tiling = new Vector2(42f, 2.5f), NormalScale = 1.0f,
                // El set WALL01 trae mapa emisivo: se usa para las tiras de neon
                // sin gastar luces dinamicas.
                Emissive = true, UseEmissionMap = true,
                EmissionColor = new Color(1.00f, 0.62f, 0.28f), EmissionIntensity = 2.2f,
            };
        }

        private static Surface CoverSurface()
        {
            return new Surface
            {
                Set = "NS_TECH01", MaterialName = "M_Env_Cover_Industrial",
                // Tinte calido para diferenciar coberturas del suelo y paredes.
                BaseColor = new Color(1.00f, 0.86f, 0.68f), Metallic = 0.35f, Smoothness = 0.40f,
                Tiling = new Vector2(3f, 3f), NormalScale = 0.95f,
            };
        }

        // ------------------------------------------------------------------
        //  Entradas de menu
        // ------------------------------------------------------------------

        [MenuItem("Tools/CyberShooter/Art Pass/Aplicar art pass")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ArtPass] No se ejecuta en Play Mode.");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[ArtPass] Escena activa inesperada: " + scene.path +
                               ". Se esperaba " + ScenePath + ". Abortado por seguridad.");
                return;
            }

            _log = new StringBuilder();
            Log("=== ART PASS ===");
            Log("Escena: " + scene.path);

            EnsureFolder(MatFolder);

            // 1) Importacion correcta de las texturas que vamos a usar.
            FixTextureImports();

            // 2) Materiales.
            var grounds = GroundSurfaces();
            Material[] groundMats = new Material[grounds.Length];
            for (int i = 0; i < grounds.Length; i++)
                groundMats[i] = BuildMaterial(grounds[i]);

            Material wallMat = BuildMaterial(WallSurface());
            Material coverMat = BuildMaterial(CoverSurface());
            Material groundBaseMat = BuildMaterial(GroundBaseSurface());
            Material emissiveMat = BuildEmissiveMaterial();

            // 3) Asignacion al escenario.
            AssignGround(groundMats, groundBaseMat);
            AssignWalls(wallMat);
            AssignCovers(coverMat);
            AssignStructuresCovers(coverMat);

            // 4) Props decorativos.
            PlaceProps(emissiveMat);

            // 5) Iluminacion y atmosfera.
            TuneLighting();

            // 6) Post-proceso.
            TuneVolume();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Log("Escena guardada.");
            Debug.Log("[ArtPass] " + Flatten(_log));
            Save();
        }

        [MenuItem("Tools/CyberShooter/Art Pass/Verificar")]
        public static void Verify()
        {
            _log = new StringBuilder();
            Log("=== VERIFICACION ART PASS ===");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Log("Escena: " + scene.path + "  dirty=" + scene.isDirty);

            // Materiales creados
            Log("-- Materiales --");
            string[] mats = AssetDatabase.FindAssets("t:Material", new[] { MatFolder });
            for (int i = 0; i < mats.Length; i++)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(mats[i]));
                bool ok = m.shader != null && m.shader.name.Contains("Universal Render Pipeline");
                Log("   " + m.name + "  URP=" + ok +
                    (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? "  baseMap=OK" : "  baseMap=NULL") +
                    (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null ? "  normal=OK" : "  normal=NULL"));
            }

            // Suelo
            var ground = GameObject.Find("Ground");
            var matsUsed = new Dictionary<string, int>();
            if (ground != null)
            {
                foreach (Transform c in ground.transform)
                {
                    var mr = c.GetComponent<MeshRenderer>();
                    if (mr == null || mr.sharedMaterial == null) continue;
                    string n = mr.sharedMaterial.name;
                    if (!matsUsed.ContainsKey(n)) matsUsed[n] = 0;
                    matsUsed[n]++;
                }
            }
            Log("-- Suelo (" + (ground != null ? ground.transform.childCount : 0) + " tiles) --");
            foreach (var kv in matsUsed) Log("   " + kv.Key + " x" + kv.Value);

            // Paredes
            var structures = GameObject.Find("Structures");
            if (structures != null)
            {
                var wallMat = structures.transform.Find("Wall_North");
                if (wallMat != null)
                {
                    var mr = wallMat.GetComponent<MeshRenderer>();
                    Log("-- Paredes -- Wall_North mat=" + (mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL"));
                }
            }

            // Coberturas
            var covers = GameObject.Find("CoverObjects");
            if (covers != null && covers.transform.childCount > 0)
            {
                var mr = covers.transform.GetChild(0).GetComponent<MeshRenderer>();
                Log("-- Coberturas -- " + covers.transform.childCount + " objetos, primer mat=" +
                    (mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL"));
            }

            // Props
            var pg = GameObject.Find(PropsGroup);
            Log("-- Props -- grupo=" + (pg != null) + "  hijos=" + (pg != null ? pg.transform.childCount : 0));
            if (pg != null)
            {
                foreach (Transform t in pg.transform)
                    Log("     " + t.name + "  pos=" + t.position.ToString("F1"));
            }

            // Luces
            var lg = GameObject.Find(LightsGroup);
            Log("-- Luces -- grupo=" + (lg != null) + "  hijos=" + (lg != null ? lg.transform.childCount : 0));
            var allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            Log("   total luces en escena=" + allLights.Length);
            for (int i = 0; i < allLights.Length; i++)
                Log("     " + allLights[i].gameObject.name + " type=" + allLights[i].type +
                    " int=" + allLights[i].intensity.ToString("F2") +
                    " range=" + allLights[i].range.ToString("F1") +
                    " color=" + allLights[i].color);

            // Render
            Log("-- Render --");
            Log("   ambient=" + RenderSettings.ambientMode + " intensity=" + RenderSettings.ambientIntensity);
            Log("   fog=" + RenderSettings.fog + " density=" + RenderSettings.fogDensity.ToString("F4"));
            var vol = GameObject.Find("Global Volume");
            if (vol != null)
            {
                var v = vol.GetComponent<Volume>();
                if (v != null && v.sharedProfile != null)
                {
                    for (int i = 0; i < v.sharedProfile.components.Count; i++)
                    {
                        var c = v.sharedProfile.components[i];
                        if (c is Bloom b)
                            Log("   Bloom int=" + b.intensity.value + " thr=" + b.threshold.value);
                        if (c is ColorAdjustments ca)
                            Log("   ColorAdj contrast=" + ca.contrast.value + " sat=" + ca.saturation.value);
                    }
                }
            }

            // Integridad del gameplay
            Log("-- Integridad gameplay --");
            var player = GameObject.Find("Player12");
            Log("   Player12=" + (player != null) +
                (player != null ? "  pos=" + player.transform.position.ToString("F2") : ""));
            if (player != null)
            {
                Log("   componentes=" + player.GetComponents<Component>().Length +
                    "  CC=" + (player.GetComponent<CharacterController>() != null) +
                    "  PlayerMotor=" + (player.GetComponent("PlayerMotor") != null) +
                    "  PlayerShooting=" + (player.GetComponent("PlayerShooting") != null) +
                    "  PlayerHealth=" + (player.GetComponent("PlayerHealth") != null));
            }

            Debug.Log("[ArtPass] " + Flatten(_log));
            Save();
        }

        [MenuItem("Tools/CyberShooter/Art Pass/Revertir art pass")]
        public static void Revert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("[ArtPass] Escena inesperada."); return; }

            _log = new StringBuilder();
            Log("=== REVERTIR ART PASS ===");

            // Materiales originales (siguen existiendo en _Project/Materials).
            var groundOrig = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Ground.mat");
            var structureOrig = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Structure.mat");

            var ground = GameObject.Find("Ground");
            if (ground != null && groundOrig != null)
            {
                var rootMr = ground.GetComponent<MeshRenderer>();
                if (rootMr != null) rootMr.sharedMaterial = groundOrig;

                foreach (Transform c in ground.transform)
                {
                    var mr = c.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = groundOrig;
                }
            }
            Log("Suelo revertido a Ground.mat");

            var structures = GameObject.Find("Structures");
            if (structures != null && structureOrig != null)
                foreach (Transform c in structures.transform)
                {
                    var mr = c.GetComponent<MeshRenderer>();
                    if (mr != null && (c.name.StartsWith("Wall_") || c.name.StartsWith("Cover_")))
                        mr.sharedMaterial = structureOrig;
                }
            Log("Paredes y coberturas de Structures revertidas a Structure.mat");

            var covers = GameObject.Find("CoverObjects");
            if (covers != null)
                foreach (Transform c in covers.transform)
                {
                    var mr = c.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = null;
                }
            Log("Coberturas revertidas a material por defecto");

            DestroyGroup(PropsGroup);
            DestroyGroup(LightsGroup);
            Log("Grupos de props y luces eliminados");

            // Luces y atmosfera originales.
            var dir = GameObject.Find("Directional Light");
            if (dir != null)
            {
                var L = dir.GetComponent<Light>();
                L.intensity = 1.2f;
                L.color = new Color(1f, 0.95f, 0.85f);
                Log("Directional Light restaurada");
            }
            RenderSettings.fog = false;
            RenderSettings.ambientIntensity = 1f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[ArtPass] " + Flatten(_log));
            Save();
        }

        // ------------------------------------------------------------------
        //  Texturas
        // ------------------------------------------------------------------

        private static void FixTextureImports()
        {
            string[] sets = { "DECK01", "GRATE01", "WALL01", "TECH01", "TREAD01" };

            for (int s = 0; s < sets.Length; s++)
            {
                string dir = TextureRoot + "/" + sets[s] + "/1K";
                if (!AssetDatabase.IsValidFolder(dir)) { Log("AVISO: no existe " + dir); continue; }

                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string file = Path.GetFileNameWithoutExtension(path);

                    // El README del pack lo indica: Normal_GL es OpenGL (Y+) para
                    // Unity/Godot/Blender; Normal_DX es DirectX (Y-) para Unreal.
                    // Usar el DX en Unity invierte el relieve (convexo <-> concavo).
                    bool isNormal = file.Contains("_Normal_GL");
                    bool isNormalDX = file.Contains("_Normal_DX");
                    bool isLinear = file.Contains("_Metallic") || file.Contains("_Roughness") ||
                                    file.Contains("_AO") || file.Contains("_Height") ||
                                    file.Contains("_Opacity") || isNormal || isNormalDX;

                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;

                    bool changed = false;

                    if (isNormal && ti.textureType != TextureImporterType.NormalMap)
                    {
                        ti.textureType = TextureImporterType.NormalMap;
                        changed = true;
                    }
                    else if (!isNormal && ti.textureType != TextureImporterType.Default)
                    {
                        ti.textureType = TextureImporterType.Default;
                        changed = true;
                    }

                    if (isLinear && ti.sRGBTexture)
                    {
                        ti.sRGBTexture = false;
                        changed = true;
                    }
                    else if (!isLinear && !isNormal && !ti.sRGBTexture)
                    {
                        ti.sRGBTexture = true;
                        changed = true;
                    }

                    if (ti.mipmapEnabled == false)
                    {
                        ti.mipmapEnabled = true;
                        changed = true;
                    }

                    if (ti.maxTextureSize < 1024)
                    {
                        ti.maxTextureSize = 1024;
                        changed = true;
                    }

                    // Anisotropia: el suelo se ve en angulo rasante y sin esto
                    // las lineas del deck se emborronan a pocos metros.
                    if (ti.anisoLevel != 8)
                    {
                        ti.anisoLevel = 8;
                        changed = true;
                    }

                    if (changed)
                    {
                        ti.SaveAndReimport();
                        Log("Textura corregida: " + Path.GetFileName(path));
                    }
                }
            }
        }

        private static string TexPath(string set, string suffix)
        {
            // Los sets se llaman "NS_DECK01" pero las carpetas del pack son "DECK01".
            string folder = set.StartsWith("NS_") ? set.Substring(3) : set;
            return TextureRoot + "/" + folder + "/1K/" + set + "_1K_" + suffix + ".png";
        }

        private static Texture2D LoadTex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Log("AVISO: textura no encontrada " + path);
            return t;
        }

        // ------------------------------------------------------------------
        //  Materiales
        // ------------------------------------------------------------------

        private static Material BuildMaterial(Surface s)
        {
            string matPath = MatFolder + "/" + s.MaterialName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Log("ERROR: no encuentro el shader URP/Lit");
                    return null;
                }
                mat = new Material(shader);
                mat.name = s.MaterialName;
                AssetDatabase.CreateAsset(mat, matPath);
                Log("Material creado: " + s.MaterialName);
            }
            else
            {
                Log("Material actualizado: " + s.MaterialName);
            }

            mat.SetColor("_BaseColor", s.BaseColor);
            mat.SetFloat("_Metallic", s.Metallic);
            mat.SetFloat("_Smoothness", s.Smoothness);

            var baseMap = LoadTex(TexPath(s.Set, "BaseColor"));
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);

            var normal = LoadTex(TexPath(s.Set, "Normal_GL"));
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", s.NormalScale);
            }

            // OJO: NO asignar los mapas Metallic/Roughness del pack.
            // URP/Lit espera _MetallicGlossMap con metalico en R y smoothness en A.
            // Estos PNG son mapas en escala de grises sin canal alfa: al asignarlos,
            // el alpha vale 1 y la superficie queda como espejo perfecto (negra al
            // no tener entorno). Se usan los escalares _Metallic/_Smoothness.
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetTexture("_SpecGlossMap", null);

            var ao = LoadTex(TexPath(s.Set, "AO"));
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 0.8f);
            }

            // Emision: solo si el set tiene mapa emisivo real.
            if (s.Emissive && s.UseEmissionMap)
            {
                var emi = LoadTex(TexPath(s.Set, "Emissive"));
                if (emi != null)
                {
                    mat.SetTexture("_EmissionMap", emi);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", s.EmissionColor * s.EmissionIntensity);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
            else
            {
                mat.SetTexture("_EmissionMap", null);
                mat.DisableKeyword("_EMISSION");
            }

            mat.SetTextureScale("_BaseMap", s.Tiling);
            mat.SetTextureScale("_BumpMap", s.Tiling);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material BuildEmissiveMaterial()
        {
            const string name = "M_Env_Emissive_Amber";
            string path = MatFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                mat.name = name;
                AssetDatabase.CreateAsset(mat, path);
                Log("Material creado: " + name);
            }

            mat.SetColor("_BaseColor", new Color(0.06f, 0.05f, 0.045f));
            mat.SetFloat("_Metallic", 0.2f);
            mat.SetFloat("_Smoothness", 0.6f);

            // Emision naranja-ambar, HDR para que Bloom la recoja.
            mat.SetColor("_EmissionColor", new Color(2.2f, 0.85f, 0.25f));
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        //  Asignacion al escenario
        // ------------------------------------------------------------------

        private static void AssignGround(Material[] mats, Material baseMat)
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) { Log("ERROR: no existe 'Ground'"); return; }

            // El propio objeto Ground tiene un plano de 10x10 m en el origen.
            var rootRenderer = ground.GetComponent<MeshRenderer>();
            if (rootRenderer != null && baseMat != null)
            {
                rootRenderer.sharedMaterial = baseMat;
                Log("Suelo raiz (10x10 m) texturizado con M_Env_Ground_Base");
            }

            int applied = 0;
            int i = 0;
            foreach (Transform tile in ground.transform)
            {
                var mr = tile.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                // Variacion determinista por indice: rompe la monotonia sin ruido.
                int idx = i % mats.Length;
                if (mats[idx] != null)
                {
                    mr.sharedMaterial = mats[idx];
                    applied++;
                }
                i++;
            }

            Log("Suelo: " + applied + " tiles con " + mats.Length + " variantes");
        }

        private static void AssignWalls(Material mat)
        {
            var structures = GameObject.Find("Structures");
            if (structures == null) { Log("ERROR: no existe 'Structures'"); return; }

            int applied = 0;
            foreach (Transform child in structures.transform)
            {
                if (!child.name.StartsWith("Wall_")) continue;
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && mat != null) { mr.sharedMaterial = mat; applied++; }
            }
            Log("Paredes: " + applied + " muros");
        }

        /// <summary>
        /// Cover_Corner_* y Cover_Small_* son coberturas funcionales pero cuelgan
        /// de 'Structures', no de 'CoverObjects'. Sin esto se quedaban con el
        /// material gris antiguo y rompian la coherencia del pase.
        /// </summary>
        private static void AssignStructuresCovers(Material mat)
        {
            var structures = GameObject.Find("Structures");
            if (structures == null) return;

            int applied = 0;
            foreach (Transform child in structures.transform)
            {
                if (!child.name.StartsWith("Cover_")) continue;
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && mat != null) { mr.sharedMaterial = mat; applied++; }
            }
            Log("Coberturas en Structures: " + applied + " objetos");
        }

        private static void AssignCovers(Material mat)
        {
            var covers = GameObject.Find("CoverObjects");
            if (covers == null) { Log("ERROR: no existe 'CoverObjects'"); return; }

            int applied = 0;
            foreach (Transform child in covers.transform)
            {
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && mat != null) { mr.sharedMaterial = mat; applied++; }
            }
            Log("Coberturas: " + applied + " objetos");
        }

        // ------------------------------------------------------------------
        //  Props
        // ------------------------------------------------------------------

        private class PropSpec
        {
            public string Prefab;
            public Vector3 Position;
            public float Yaw;
            public float Scale;
            public bool Emissive;
        }

        private static PropSpec[] PropSpecs()
        {
            return new[]
            {
                // Zona inicial (spawn en 1.8, 0, 16.5) - mantener el pasillo central libre.
                P("Prop_Barrel_Large", -59f, 27f, 15f, 1f),
                P("Prop_Barrel_Large", -58f, 30f, 70f, 1f),
                P("Prop_Crate3",       39f, 27f, 25f, 1f),
                P("Prop_Crate4",       40.5f, 29.5f, 8f, 1f),

                // Segundo tramo (z ~ 76).
                P("Prop_Crate3",       -25f, 82f, 40f, 1f),
                P("Prop_Barrel_Large", 24f, 82f, 5f, 1f),
                P("Prop_Vent_Big",     -8f, 88f, 0f, 1f),

                // Tercer tramo (z ~ 140).
                P("Prop_Crate4",       -55f, 147f, 12f, 1f),
                P("Prop_Barrel_Large", 28f, 146f, 55f, 1f),

                // Cuarto tramo (z ~ 205).
                P("Prop_Crate3",       -32f, 212f, 30f, 1f),
                P("Prop_Barrel_Large", 25f, 211f, 0f, 1f),

                // Sexto tramo (z ~ 253).
                P("Prop_Crate4",       -55f, 238f, 18f, 1f),
                P("Prop_Barrel_Large", 27f, 236f, 42f, 1f),

                // Equipamiento tecnico: refuerza la lectura de "instalacion".
                P("Prop_Computer",     -46f, 33f, 160f, 1f),
                P("Prop_AccessPoint",   36f, 33f, 200f, 1f),
                P("Prop_Cable_1",      -44f, 36f, 0f, 1f),
                P("Prop_Vent_Big",      31f, 24f, 90f, 1f),

                // Luminarias emisivas junto a coberturas.
                E("Prop_Light_Floor",  -50f, 30f, 0f, 1f),
                E("Prop_Light_Floor",   30f, 30f, 0f, 1f),
                E("Prop_Light_Floor",   -8f, 82f, 0f, 1f),
                E("Prop_Light_Floor",   28f, 146f, 0f, 1f),
                E("Prop_Light_Floor",  -32f, 212f, 0f, 1f),
            };
        }

        private static PropSpec P(string prefab, float x, float z, float yaw, float scale)
        {
            return new PropSpec { Prefab = prefab, Position = new Vector3(x, 0f, z), Yaw = yaw, Scale = scale, Emissive = false };
        }

        private static PropSpec E(string prefab, float x, float z, float yaw, float scale)
        {
            return new PropSpec { Prefab = prefab, Position = new Vector3(x, 0f, z), Yaw = yaw, Scale = scale, Emissive = true };
        }

        private static void PlaceProps(Material emissiveMat)
        {
            DestroyGroup(PropsGroup);

            var group = new GameObject(PropsGroup);
            var specs = PropSpecs();

            int placed = 0;
            int skipped = 0;

            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                string path = PropRoot + "/" + spec.Prefab + ".fbx";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    Log("OMITIDO (prefab no encontrado): " + spec.Prefab);
                    skipped++;
                    continue;
                }

                // Suelo valido bajo el punto propuesto.
                if (!TryGroundPoint(spec.Position, out Vector3 ground))
                {
                    Log("OMITIDO (sin suelo): " + spec.Prefab + " en " + spec.Position.ToString("F1"));
                    skipped++;
                    continue;
                }

                // Huella aproximada del prop para comprobar solapes.
                Bounds localBounds = LocalBounds(prefab);
                Vector3 halfExtents = new Vector3(
                    localBounds.size.x * 0.5f * spec.Scale,
                    localBounds.size.y * 0.5f * spec.Scale,
                    localBounds.size.z * 0.5f * spec.Scale);

                // Altura del prop sobre el suelo (pivote puede no estar en la base).
                float lift = -localBounds.min.y * spec.Scale;
                Vector3 spawnPos = ground + Vector3.up * lift;

                Vector3 center = spawnPos + Vector3.up * halfExtents.y;

                if (Physics.CheckBox(center, halfExtents * 0.92f, Quaternion.Euler(0f, spec.Yaw, 0f),
                                     ~0, QueryTriggerInteraction.Ignore))
                {
                    Log("OMITIDO (solape): " + spec.Prefab + " en " + spec.Position.ToString("F1"));
                    skipped++;
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                instance.name = spec.Prefab + "_" + i;
                instance.transform.position = spawnPos;
                instance.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);
                instance.transform.localScale = Vector3.one * spec.Scale;

                // Sin fisica: son decorativos.
                var colliders = instance.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++) Object.DestroyImmediate(colliders[c]);

                if (spec.Emissive && emissiveMat != null)
                {
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                    for (int r = 0; r < renderers.Length; r++)
                        renderers[r].sharedMaterial = emissiveMat;
                }

                placed++;
            }

            Log("Props colocados: " + placed + "  omitidos: " + skipped);
        }

        private static Bounds LocalBounds(GameObject prefab)
        {
            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            bool first = true;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);

            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null) continue;
                Bounds mb = filters[i].sharedMesh.bounds;
                Vector3 c = filters[i].transform.localPosition + mb.center;
                Vector3 size = Vector3.Scale(mb.size, filters[i].transform.localScale);

                if (first) { b = new Bounds(c, size); first = false; }
                else { b.Encapsulate(c - size * 0.5f); b.Encapsulate(c + size * 0.5f); }
            }

            return b;
        }

        private static bool TryGroundPoint(Vector3 approx, out Vector3 hit)
        {
            hit = Vector3.zero;
            Ray ray = new Ray(approx + Vector3.up * 30f, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit info, 60f, 1 << GroundLayer, QueryTriggerInteraction.Ignore))
            {
                hit = info.point;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        //  Iluminacion
        // ------------------------------------------------------------------

        private static void TuneLighting()
        {
            // --- Direccional: luz fria, tenue, tipo luna industrial ---
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
                    dirGo.transform.rotation = Quaternion.Euler(48f, 32f, 0f);
                    Log("Directional: fria azulada, intensidad 1.05");
                }
            }

            // --- Cielo nocturno industrial en vez del skybox diurno ---
            // Un skybox de dia arruina la atmosfera: se sustituye por un
            // degradado oscuro generado por codigo (sin assets externos).
            var sky = BuildNightSkybox();
            if (sky != null) RenderSettings.skybox = sky;

            // --- Ambiente: mantener legibilidad sin perder atmosfera ---
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.38f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.23f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.09f, 0.11f);
            RenderSettings.ambientIntensity = 1.15f;

            // --- Niebla: profundidad sin ocultar el gameplay ---
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.085f, 0.105f, 0.140f);
            RenderSettings.fogDensity = 0.0035f;

            RenderSettings.reflectionIntensity = 0.85f;

            Log("Skybox nocturno + ambiente Trilight 1.15 + niebla exp2 0.0035");

            // --- Luces de acento: pocas, calidas, puntuales ---
            DestroyGroup(LightsGroup);
            var group = new GameObject(LightsGroup);

            AddPoint(group, "Accent_Entry_Left",  new Vector3(-50f, 0.9f, 30f),  new Color(1f, 0.52f, 0.18f), 18f, 16f);
            AddPoint(group, "Accent_Entry_Right", new Vector3(30f, 0.9f, 30f),   new Color(1f, 0.45f, 0.15f), 16f, 15f);
            AddPoint(group, "Accent_Mid",         new Vector3(-8f, 0.9f, 82f),   new Color(1f, 0.55f, 0.20f), 18f, 17f);
            AddPoint(group, "Accent_Far",         new Vector3(28f, 0.9f, 146f),  new Color(1f, 0.40f, 0.14f), 15f, 15f);
            AddPoint(group, "Accent_Deep",        new Vector3(-32f, 0.9f, 212f), new Color(1f, 0.50f, 0.18f), 15f, 15f);
            AddPoint(group, "Accent_Exit",        new Vector3(2f, 1.6f, 70f),    new Color(0.35f, 0.75f, 1f), 12f, 12f);

            Log("Luces de acento: 6 point lights (unidad Lux)");

            // --- Sonda de reflejos: sin ella el metal solo puede devolver negro ---
            AddReflectionProbe();
        }

        /// <summary>
        /// Genera un skybox nocturno industrial (degradado oscuro azul-gris) y lo
        /// guarda como asset. Sustituye el skybox diurno del template, que rompe
        /// por completo la atmosfera.
        /// </summary>
        private static Material BuildNightSkybox()
        {
            const string name = "M_Env_NightSky";
            string path = MatFolder + "/" + name + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) { Log("AVISO: no encuentro Skybox/Panoramic"); return null; }

            // Textura de degradado vertical generada por codigo.
            const int W = 32, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "T_Env_NightSky";

            Color zenith = new Color(0.035f, 0.055f, 0.090f);
            Color horizon = new Color(0.110f, 0.135f, 0.170f);
            Color below = new Color(0.020f, 0.022f, 0.028f);

            for (int y = 0; y < H; y++)
            {
                float t = (float)y / (H - 1);          // 0 = abajo, 1 = arriba
                Color c;
                if (t < 0.5f)
                {
                    c = Color.Lerp(below, horizon, t * 2f);
                }
                else
                {
                    // Curva suave hacia el cenit.
                    float k = (t - 0.5f) * 2f;
                    c = Color.Lerp(horizon, zenith, k * k);
                }

                for (int x = 0; x < W; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();

            string texPath = MatFolder + "/T_Env_NightSky.asset";
            AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.CreateAsset(tex, texPath);

            var mat = new Material(shader);
            mat.name = name;
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Mapping", 1f);      // Latitude-Longitude
            mat.SetFloat("_ImageType", 0f);    // 2D
            mat.SetFloat("_Exposure", 1.0f);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();

            Log("Skybox nocturno generado: " + name);
            return mat;
        }

        /// <summary>
        /// Sonda de reflejos para que el metal tenga algo que reflejar. Sin ella,
        /// cualquier superficie metalica se ve negra por mucho que suba la luz.
        /// Es una sola sonda, sin horneado (realtime), y cubre la zona jugable.
        /// </summary>
        private static void AddReflectionProbe()
        {
            DestroyGroup("ArtPass_ReflectionProbe");

            var go = new GameObject("ArtPass_ReflectionProbe");
            go.transform.position = new Vector3(0f, 6f, 60f);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(160f, 24f, 220f);
            probe.center = Vector3.zero;
            probe.resolution = 128;
            probe.hdr = true;
            probe.importance = 1;
            probe.boxProjection = false;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;

            // Un unico horneado por ejecucion (idempotente: no acumula).
            probe.RenderProbe();

            Log("Reflection probe realtime 128px en la zona jugable");
        }

        private static void AddPoint(GameObject parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;

            var L = go.AddComponent<Light>();
            L.type = LightType.Point;
            L.color = color;
            // Las luces del proyecto usan unidades fisicas (Candela). En un
            // escenario tan grande eso deja charcos de luz invisibles, asi que
            // se usa el modo Lux (heredado) con valores faciles de controlar.
            L.lightUnit = LightUnit.Lux;
            L.intensity = intensity;
            L.range = range;
            L.shadows = LightShadows.None;   // rendimiento: sin sombras en acentos
            L.lightmapBakeType = LightmapBakeType.Realtime;

            var data = go.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = go.AddComponent<UniversalAdditionalLightData>();
            data.usePipelineSettings = true;
        }

        // ------------------------------------------------------------------
        //  Post-proceso
        // ------------------------------------------------------------------

        private static void TuneVolume()
        {
            var volGo = GameObject.Find("Global Volume");
            if (volGo == null) { Log("AVISO: no hay Global Volume"); return; }

            var vol = volGo.GetComponent<Volume>();
            if (vol == null || vol.sharedProfile == null) { Log("AVISO: Global Volume sin profile"); return; }

            var profile = vol.sharedProfile;

            // Bloom: moderado, para que la emision brille sin lavar la imagen.
            var bloom = profile.TryGet<Bloom>(out var b) ? b : profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.85f));

            // Ajustes de color suaves: mas contraste, ligera desaturacion.
            var ca = profile.TryGet<ColorAdjustments>(out var existing) ? existing : profile.Add<ColorAdjustments>(true);
            ca.active = true;
            ca.contrast.Override(8f);
            ca.saturation.Override(-6f);
            ca.postExposure.Override(0.15f);

            // Vignette: conservar el existente, reforzarlo un poco.
            var vg = profile.TryGet<Vignette>(out var v) ? v : profile.Add<Vignette>(true);
            vg.active = true;
            vg.intensity.Override(0.28f);
            vg.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            Log("Volume: Bloom 0.55/0.95, contraste +8, saturacion -6, vignette 0.28");
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
                Debug.LogWarning("[ArtPass] No pude escribir el log: " + e.Message);
            }
        }
    }
}
