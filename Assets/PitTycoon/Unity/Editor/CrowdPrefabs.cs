using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds the rigged crowd-member prefabs (M5a) from the Blender FBX exports in
    /// Art/Models/Crowd. Configures each FBX import (generic rig, looping clips), creates the
    /// shared CrowdAnimator controller (1D blend tree: Sway -> Groove -> HypeJump on 'Energy'),
    /// and wraps each body FBX in a prefab with the ComicLit crowd material and the controller.
    /// Missing FBXs are skipped with a warning so wiring works mid-modelling.
    /// Idempotent: re-running rebuilds prefabs + controller from the current FBXs.
    /// </summary>
    public static class CrowdPrefabs
    {
        private const string ModelDir = "Assets/PitTycoon/Art/Models/Crowd";
        private const string PrefabDir = "Assets/PitTycoon/Art/Prefabs/Crowd";
        private const string ControllerPath = "Assets/Settings/CrowdAnimator.controller";
        private const string CrowdMatPath = "Assets/PitTycoon/Art/Materials/CrowdMat.mat";

        private static readonly string[] Variants = { "CrowdBase", "CrowdChunky", "CrowdLanky", "CrowdShort" };
        private static readonly string[] ClipNames = { "Sway", "Groove", "HypeJump" };
        private static readonly float[] ClipThresholds = { 0f, 0.5f, 1f };

        /// <summary>Build (or rebuild) all available crowd variant prefabs. Missing FBXs are skipped.</summary>
        public static GameObject[] EnsureCrowdVariants()
        {
            var result = new List<GameObject>();
            AnimatorController controller = null;

            foreach (var name in Variants)
            {
                string fbxPath = $"{ModelDir}/{name}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null)
                {
                    Debug.LogWarning($"CrowdPrefabs: {fbxPath} not found - skipped (export it, then re-run).");
                    continue;
                }

                ConfigureImport(fbxPath);
                // Controller needs clips; build it from the first FBX that exists (all carry the same 3).
                if (controller == null) controller = EnsureController(fbxPath);
                if (controller == null) { Debug.LogError("CrowdPrefabs: no controller - aborting."); break; }

                result.Add(BuildPrefab(fbx, name, controller));
            }

            AssetDatabase.SaveAssets();
            return result.ToArray();
        }

        /// <summary>Generic rig + loop time on the three clips. Reimports only when settings change.</summary>
        private static void ConfigureImport(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            bool dirty = false;

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                dirty = true;
            }

            var clips = importer.defaultClipAnimations;   // what the FBX actually contains
            var wanted = new List<ModelImporterClipAnimation>();
            foreach (var clip in clips)
            {
                if (!ClipNames.Any(n => clip.name.Contains(n))) continue;
                clip.loopTime = true;
                wanted.Add(clip);
            }
            // Apply when the importer has no explicit clip list yet, or loop flags drifted.
            var current = importer.clipAnimations;
            if (wanted.Count > 0 && (current.Length != wanted.Count || current.Any(c => !c.loopTime)))
            {
                importer.clipAnimations = wanted.ToArray();
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        /// <summary>Create (or rebuild) the shared controller: one state holding a 1D blend tree on 'Energy'.</summary>
        private static AnimatorController EnsureController(string fbxPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();

            var motions = new AnimationClip[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++)
            {
                motions[i] = clips.FirstOrDefault(c => c.name.Contains(ClipNames[i]));
                if (motions[i] == null)
                {
                    Debug.LogError($"CrowdPrefabs: clip containing '{ClipNames[i]}' not found in {fbxPath}. " +
                                   "Check the Blender action names.");
                    return null;
                }
            }

            AssetDatabase.DeleteAsset(ControllerPath);   // rebuild clean; scene refs are by builder, not GUID-fragile here
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Energy", AnimatorControllerParameterType.Float);

            var tree = new BlendTree
            {
                name = "DanceBlend",
                blendParameter = "Energy",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            for (int i = 0; i < motions.Length; i++) tree.AddChild(motions[i], ClipThresholds[i]);

            var state = controller.layers[0].stateMachine.AddState("Dance");
            state.motion = tree;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject BuildPrefab(GameObject fbx, string name, AnimatorController controller)
        {
            EnsureDir(PrefabDir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CrowdMatPath);
            if (mat == null)
                Debug.LogWarning($"CrowdPrefabs: {CrowdMatPath} not found - run Build Festival Scene first; " +
                                 "prefab keeps imported materials.");

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (mat != null)
                foreach (var r in temp.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }

            var anim = temp.GetComponent<Animator>();
            if (anim == null) anim = temp.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(dir));
        }
    }
}
