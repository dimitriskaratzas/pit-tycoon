using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds real-model structure prefabs (M4d) from the Blender FBX exports in
    /// Art/Models/Structures. Wraps each FBX in a prefab under Art/Prefabs/Structures,
    /// assigns the shared ComicLit palette materials by material-slot name, and adds
    /// IdleMotion to the Sponsor Banner (cloth sway) and Strobe Rig (head pan).
    /// Falls back to the greybox prefab while a structure's FBX does not exist yet, so
    /// Build Festival Ground works at any point during the modelling batches.
    /// Idempotent: re-running rebuilds prefabs from the current FBX + palette.
    /// </summary>
    public static class StructurePrefabs
    {
        private const string ModelDir = "Assets/PitTycoon/Art/Models/Structures";
        private const string PrefabDir = "Assets/PitTycoon/Art/Prefabs/Structures";
        private const string PaletteDir = "Assets/PitTycoon/Art/Materials/Palette";
        private const string RampPath = "Assets/PitTycoon/Art/Ramps/ComicRamp.png";

        // Slot name -> base color. Blender material names must match exactly.
        private static readonly (string name, Color color)[] PaletteSpec =
        {
            ("Wood",       new Color(0.55f, 0.36f, 0.20f)),
            ("MetalDark",  new Color(0.16f, 0.17f, 0.20f)),
            ("MetalLight", new Color(0.62f, 0.66f, 0.72f)),
            ("CanvasA",    new Color(0.86f, 0.32f, 0.25f)),
            ("CanvasB",    new Color(0.20f, 0.45f, 0.70f)),
            ("AccentWarm", new Color(0.95f, 0.75f, 0.20f)),
            ("StrobeGlow", new Color(1.00f, 0.95f, 0.70f)),
        };

        public static GameObject EnsureSecondStage()   => Ensure("SecondStage",   StructureGreyboxPrefabs.EnsureSecondStage);
        public static GameObject EnsureCampingField()  => Ensure("CampingField",  StructureGreyboxPrefabs.EnsureCampingField);
        public static GameObject EnsureEntranceGate()  => Ensure("EntranceGate",  StructureGreyboxPrefabs.EnsureEntranceGate);
        public static GameObject EnsureGrandstand()    => Ensure("Grandstand",    StructureGreyboxPrefabs.EnsureGrandstand);
        public static GameObject EnsureFoodCourt()     => Ensure("FoodCourt",     StructureGreyboxPrefabs.EnsureFoodCourt);
        public static GameObject EnsureBar()           => Ensure("Bar",           StructureGreyboxPrefabs.EnsureBar);
        public static GameObject EnsureVipLounge()     => Ensure("VipLounge",     StructureGreyboxPrefabs.EnsureVipLounge);
        public static GameObject EnsureSponsorBanner() => Ensure("SponsorBanner", StructureGreyboxPrefabs.EnsureSponsorBanner);
        public static GameObject EnsureAmpStack()      => Ensure("AmpStack",      StructureGreyboxPrefabs.EnsureAmpStack);
        public static GameObject EnsureStrobeRig()     => Ensure("StrobeRig",     StructureGreyboxPrefabs.EnsureStrobeRig);
        public static GameObject EnsureSpeakerWall()   => Ensure("SpeakerWall",   StructureGreyboxPrefabs.EnsureSpeakerWall);

        private static GameObject Ensure(string name, System.Func<GameObject> greyboxFallback)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            if (fbx == null) return greyboxFallback();

            var palette = EnsurePalette();
            EnsureDir(PrefabDir);

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in temp.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (palette.TryGetValue(mats[i].name, out var mapped)) mats[i] = mapped;
                    else Debug.LogWarning($"StructurePrefabs: {name}/{r.name} slot '{mats[i].name}' " +
                                          "is not a palette name — left as imported.");
                }
                r.sharedMaterials = mats;
            }

            AddIdleMotion(temp, name);

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static void AddIdleMotion(GameObject root, string name)
        {
            if (name == "SponsorBanner")
                ConfigureIdle(root, "BannerCloth", new Vector3(0f, 0f, 1f), rotationAmplitude: 4f, speed: 0.35f);
            else if (name == "StrobeRig")
                ConfigureIdle(root, "Heads", new Vector3(0f, 1f, 0f), rotationAmplitude: 25f, speed: 0.15f);
        }

        private static void ConfigureIdle(GameObject root, string childName, Vector3 axis,
                                          float rotationAmplitude, float speed)
        {
            var child = FindDeep(root.transform, childName);
            if (child == null)
            {
                Debug.LogWarning($"StructurePrefabs: '{childName}' not found under {root.name} — no idle motion added.");
                return;
            }
            var idle = root.GetComponent<IdleMotion>();
            if (idle == null) idle = root.AddComponent<IdleMotion>();
            var so = new SerializedObject(idle);
            so.FindProperty("target").objectReferenceValue = child;
            so.FindProperty("rotationAxis").vector3Value = axis;
            so.FindProperty("rotationAmplitude").floatValue = rotationAmplitude;
            so.FindProperty("speed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var hit = FindDeep(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Dictionary<string, Material> EnsurePalette()
        {
            EnsureDir(PaletteDir);
            var shader = Shader.Find("PitTycoon/ComicLit");
            var ramp = AssetDatabase.LoadAssetAtPath<Texture2D>(RampPath);
            if (shader == null) Debug.LogError("StructurePrefabs: ComicLit shader missing — run M2a Comic Look first.");
            if (ramp == null) Debug.LogWarning("StructurePrefabs: ComicRamp.png missing — palette materials get no ramp.");

            var dict = new Dictionary<string, Material>();
            foreach (var (name, color) in PaletteSpec)
            {
                string path = $"{PaletteDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null && shader != null)
                {
                    mat = new Material(shader);
                    mat.SetColor("_BaseColor", color);
                    if (ramp != null) mat.SetTexture("_RampTex", ramp);
                    AssetDatabase.CreateAsset(mat, path);
                }
                if (mat != null) dict[name] = mat;
            }
            return dict;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
