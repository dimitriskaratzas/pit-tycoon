using UnityEditor;
using UnityEngine;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds primitive greybox structure prefabs (Second Stage / Camping Field / Entrance Gate)
    /// for the festival ground. Idempotent: loads the prefab if it already exists, else constructs
    /// it from primitives + StructureMat and saves it. Real art is a later milestone (M4d).
    /// </summary>
    public static class StructureGreyboxPrefabs
    {
        private const string PrefabDir = "Assets/PitTycoon/Art/Prefabs/Greybox";
        private const string StructureMatPath = "Assets/PitTycoon/Art/Materials/StructureMat.mat";

        public static GameObject EnsureSecondStage() => EnsurePrefab("SecondStage", BuildSecondStage);
        public static GameObject EnsureCampingField() => EnsurePrefab("CampingField", BuildCampingField);
        public static GameObject EnsureEntranceGate() => EnsurePrefab("EntranceGate", BuildEntranceGate);

        private static GameObject EnsurePrefab(string name, System.Func<Material, GameObject> build)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            EnsureDir(PrefabDir);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(StructureMatPath);
            GameObject root = build(mat);
            root.name = name;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // A wide low deck + back wall + two side speaker stacks.
        private static GameObject BuildSecondStage(Material mat)
        {
            var root = new GameObject("SecondStage");
            Box(root, mat, "Deck",     new Vector3(0f, 0.4f, 0f),  new Vector3(8f, 0.8f, 5f));
            Box(root, mat, "Backwall", new Vector3(0f, 2.5f, 2.2f), new Vector3(8f, 4f, 0.4f));
            Box(root, mat, "SpkL",     new Vector3(-3.6f, 1.2f, -1.5f), new Vector3(1f, 2.4f, 1f));
            Box(root, mat, "SpkR",     new Vector3(3.6f, 1.2f, -1.5f),  new Vector3(1f, 2.4f, 1f));
            return root;
        }

        // A cluster of tents (cubes rotated 45 deg read as pitched tents) of varied size.
        private static GameObject BuildCampingField(Material mat)
        {
            var root = new GameObject("CampingField");
            var rng = new System.Random(12345);
            for (int i = 0; i < 9; i++)
            {
                float x = (i % 3) * 3f - 3f + (float)(rng.NextDouble() - 0.5);
                float z = (i / 3) * 3f - 3f + (float)(rng.NextDouble() - 0.5);
                var t = Box(root, mat, $"Tent{i}", new Vector3(x, 0.7f, z), new Vector3(1.6f, 1.6f, 1.6f));
                t.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            return root;
        }

        // Two tall pillars + a top lintel = an arch.
        private static GameObject BuildEntranceGate(Material mat)
        {
            var root = new GameObject("EntranceGate");
            Box(root, mat, "PillarL", new Vector3(-2.5f, 2.5f, 0f), new Vector3(0.8f, 5f, 0.8f));
            Box(root, mat, "PillarR", new Vector3(2.5f, 2.5f, 0f),  new Vector3(0.8f, 5f, 0.8f));
            Box(root, mat, "Lintel",  new Vector3(0f, 5.2f, 0f),    new Vector3(6f, 0.8f, 0.8f));
            return root;
        }

        private static GameObject Box(GameObject parent, Material mat, string name, Vector3 localPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (mat != null)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
            return go;
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
