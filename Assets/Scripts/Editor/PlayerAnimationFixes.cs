using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberShooter.EditorTools
{
    /// <summary>
    /// Arregla el motivo de que las animaciones se vean rotas y "reboten":
    ///
    /// Walking, Running y Barrida estaban importadas como GENERIC, mientras que el
    /// personaje usa un Avatar HUMANOID (el de Ch44). Unity NO permite que un clip
    /// generico anime transforms que ya estan bindeados por un avatar humanoide.
    /// El propio Unity lo avisa en el Animator:
    ///   "Some generic clip(s) animate transforms that are already bound by a
    ///    Humanoid avatar. These transforms can only be changed by Humanoid clips."
    /// Resultado: pose rota / a saltos que se arregla sola en cuanto entra un clip
    /// humanoide (por eso Shoot "curaba" al personaje).
    ///
    /// Este script:
    ///   1) Pasa esos 3 FBX a Humanoid (Avatar: Create From This Model).
    ///   2) Activa Loop Time + Loop Pose en los clips de ciclo.
    ///   3) Se asegura de que Apply Root Motion este OFF (el movimiento lo lleva
    ///      PlayerMotor + CharacterController; con root motion el personaje
    ///      "rebota" y atraviesa el suelo).
    ///
    /// Se ejecuta solo si detecta el problema, y a mano desde el menu:
    ///   Tools > CyberShooter > Reparar Animaciones (Humanoid + Loop)
    ///
    /// Nunca se ejecuta en Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerAnimationFixes
    {
        // FBX que DEBEN ser Humanoid (los otros tres ya lo son).
        private static readonly string[] MustBeHumanoid =
        {
            "Assets/Models/Walking.fbx",
            "Assets/Models/Running.fbx",
            "Assets/Models/Barrida.fbx",
        };

        // FBX con clips que deben ciclar sin costura.
        private static readonly string[] MustLoop =
        {
            "Assets/Models/Ch44_nonPBR@Breathing Idle.fbx",
            "Assets/Models/Walking.fbx",
            "Assets/Models/Running.fbx",
        };

        private const string LogTag = "[AnimFix] ";
        private const string SessionKey = "CyberShooter.AnimFix.Intentado";

        static PlayerAnimationFixes()
        {
            EditorApplication.delayCall += AutoFix;
        }

        private static void AutoFix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!NeedsFix()) return;
            if (SessionState.GetBool(SessionKey, false)) return;   // ya lo intentamos en esta sesion
            SessionState.SetBool(SessionKey, true);
            Apply();
        }

        // true si queda algo por arreglar (tipo de rig, loop de los clips o root motion).
        private static bool NeedsFix()
        {
            for (int i = 0; i < MustBeHumanoid.Length; i++)
            {
                var mi = AssetImporter.GetAtPath(MustBeHumanoid[i]) as ModelImporter;
                if (mi != null && mi.animationType != ModelImporterAnimationType.Human) return true;
            }

            for (int i = 0; i < MustLoop.Length; i++)
            {
                var mi = AssetImporter.GetAtPath(MustLoop[i]) as ModelImporter;
                if (mi == null) continue;
                var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;
                if (!clips[0].loopTime || !clips[0].loopPose) return true;
            }

            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i].applyRootMotion) return true;
            }

            return false;
        }

        [MenuItem("Tools/CyberShooter/Reparar Animaciones (Humanoid + Loop)")]
        private static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(LogTag + "No toco las importaciones en Play Mode. Para el juego primero.");
                return;
            }
            Apply();
        }

        private static void Apply()
        {
            var hecho = new List<string>();

            // ---- 1) Generic -> Humanoid ----
            for (int i = 0; i < MustBeHumanoid.Length; i++)
            {
                var mi = AssetImporter.GetAtPath(MustBeHumanoid[i]) as ModelImporter;
                if (mi == null) continue;

                bool changed = false;

                if (mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    changed = true;
                }
                if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    changed = true;
                }
                if (!mi.autoGenerateAvatarMappingIfUnspecified)
                {
                    mi.autoGenerateAvatarMappingIfUnspecified = true;
                    changed = true;
                }

                if (changed)
                {
                    mi.SaveAndReimport();
                    hecho.Add("Humanoid -> " + MustBeHumanoid[i]);
                }
            }

            // ---- 2) Loop Time + Loop Pose en los clips de ciclo ----
            for (int i = 0; i < MustLoop.Length; i++)
            {
                var mi = AssetImporter.GetAtPath(MustLoop[i]) as ModelImporter;
                if (mi == null) continue;

                var clips = mi.clipAnimations;
                if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;

                bool changed = false;
                for (int c = 0; c < clips.Length; c++)
                {
                    if (!clips[c].loopTime) { clips[c].loopTime = true; changed = true; }
                    if (!clips[c].loopPose) { clips[c].loopPose = true; changed = true; }
                }

                if (changed)
                {
                    mi.clipAnimations = clips;
                    mi.SaveAndReimport();
                    hecho.Add("Loop -> " + MustLoop[i]);
                }
            }

            // ---- 3) Apply Root Motion = OFF ----
            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            bool tocoEscena = false;
            for (int i = 0; i < animators.Length; i++)
            {
                if (!animators[i].applyRootMotion) continue;
                animators[i].applyRootMotion = false;
                EditorUtility.SetDirty(animators[i]);
                tocoEscena = true;
                hecho.Add("RootMotion OFF -> " + animators[i].gameObject.name);
            }

            if (hecho.Count == 0)
            {
                Debug.Log(LogTag + "Nada que arreglar: las animaciones ya estaban bien.");
                return;
            }

            AssetDatabase.SaveAssets();
            if (tocoEscena) EditorSceneManager.SaveOpenScenes();

            Debug.Log(LogTag + "Reparado:\n  " + string.Join("\n  ", hecho.ToArray()));
        }
    }
}
