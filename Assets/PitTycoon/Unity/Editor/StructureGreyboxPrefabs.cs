using UnityEditor;
using UnityEngine;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds primitive greybox structure prefabs (Second Stage / Camping Field / Entrance Gate /
    /// Grandstand / Food Court / Bar / VIP Lounge / Sponsor Banner / Amp Stack / Strobe Rig / Speaker Wall)
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
        public static GameObject EnsureGrandstand() => EnsurePrefab("Grandstand", BuildGrandstand);
        public static GameObject EnsureFoodCourt() => EnsurePrefab("FoodCourt", BuildFoodCourt);
        public static GameObject EnsureBar() => EnsurePrefab("Bar", BuildBar);
        public static GameObject EnsureVipLounge() => EnsurePrefab("VipLounge", BuildVipLounge);
        public static GameObject EnsureSponsorBanner() => EnsurePrefab("SponsorBanner", BuildSponsorBanner);
        public static GameObject EnsureAmpStack() => EnsurePrefab("AmpStack", BuildAmpStack);
        public static GameObject EnsureStrobeRig() => EnsurePrefab("StrobeRig", BuildStrobeRig);
        public static GameObject EnsureSpeakerWall() => EnsurePrefab("SpeakerWall", BuildSpeakerWall);

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

        // Three stepped seating tiers facing the stage.
        private static GameObject BuildGrandstand(Material mat)
        {
            var root = new GameObject("Grandstand");
            Box(root, mat, "Tier1", new Vector3(0f, 0.5f, 0f),  new Vector3(10f, 1f, 2f));
            Box(root, mat, "Tier2", new Vector3(0f, 1f, -2f),   new Vector3(10f, 2f, 2f));
            Box(root, mat, "Tier3", new Vector3(0f, 1.5f, -4f), new Vector3(10f, 3f, 2f));
            return root;
        }

        // A row of three roofed stalls.
        private static GameObject BuildFoodCourt(Material mat)
        {
            var root = new GameObject("FoodCourt");
            for (int i = 0; i < 3; i++)
            {
                float x = i * 3.5f - 3.5f;
                Box(root, mat, $"Stall{i}", new Vector3(x, 1f, 0f),    new Vector3(2.6f, 2f, 2.6f));
                Box(root, mat, $"Roof{i}",  new Vector3(x, 2.3f, 0f),  new Vector3(3.2f, 0.3f, 3.2f));
            }
            return root;
        }

        // A long counter with a back shelf under a post-held canopy.
        private static GameObject BuildBar(Material mat)
        {
            var root = new GameObject("Bar");
            Box(root, mat, "Counter", new Vector3(0f, 0.6f, 0f),       new Vector3(6f, 1.2f, 1.2f));
            Box(root, mat, "Shelf",   new Vector3(0f, 1.5f, 1.8f),     new Vector3(6f, 3f, 0.5f));
            Box(root, mat, "Canopy",  new Vector3(0f, 3.2f, 0.8f),     new Vector3(7f, 0.3f, 3.5f));
            Box(root, mat, "PostL",   new Vector3(-3.2f, 1.6f, -0.6f), new Vector3(0.3f, 3.2f, 0.3f));
            Box(root, mat, "PostR",   new Vector3(3.2f, 1.6f, -0.6f),  new Vector3(0.3f, 3.2f, 0.3f));
            return root;
        }

        // A raised platform with a front rail and two sofa blocks.
        private static GameObject BuildVipLounge(Material mat)
        {
            var root = new GameObject("VipLounge");
            Box(root, mat, "Platform", new Vector3(0f, 0.75f, 0f),     new Vector3(6f, 1.5f, 5f));
            Box(root, mat, "Rail",     new Vector3(0f, 1.9f, -2.3f),   new Vector3(6f, 0.8f, 0.2f));
            Box(root, mat, "SofaL",    new Vector3(-1.6f, 1.9f, 1.4f), new Vector3(2f, 0.8f, 1f));
            Box(root, mat, "SofaR",    new Vector3(1.6f, 1.9f, 1.4f),  new Vector3(2f, 0.8f, 1f));
            return root;
        }

        // Two poles holding a wide thin banner panel.
        private static GameObject BuildSponsorBanner(Material mat)
        {
            var root = new GameObject("SponsorBanner");
            Box(root, mat, "PoleL",  new Vector3(-4f, 3f, 0f),   new Vector3(0.4f, 6f, 0.4f));
            Box(root, mat, "PoleR",  new Vector3(4f, 3f, 0f),    new Vector3(0.4f, 6f, 0.4f));
            Box(root, mat, "Banner", new Vector3(0f, 4.5f, 0f),  new Vector3(8.4f, 2.4f, 0.15f));
            return root;
        }

        // A 2x3 stack of speaker cabinets.
        private static GameObject BuildAmpStack(Material mat)
        {
            var root = new GameObject("AmpStack");
            for (int col = 0; col < 2; col++)
                for (int row = 0; row < 3; row++)
                    Box(root, mat, $"Amp{col}{row}",
                        new Vector3(col * 1.3f - 0.65f, row * 1.1f + 0.55f, 0f),
                        new Vector3(1.2f, 1f, 1f));
            return root;
        }

        // Two poles + a crossbar hung with four strobe boxes.
        private static GameObject BuildStrobeRig(Material mat)
        {
            var root = new GameObject("StrobeRig");
            Box(root, mat, "PoleL", new Vector3(-2.5f, 2.25f, 0f), new Vector3(0.3f, 4.5f, 0.3f));
            Box(root, mat, "PoleR", new Vector3(2.5f, 2.25f, 0f),  new Vector3(0.3f, 4.5f, 0.3f));
            Box(root, mat, "Bar",   new Vector3(0f, 4.4f, 0f),     new Vector3(5.6f, 0.3f, 0.3f));
            for (int i = 0; i < 4; i++)
                Box(root, mat, $"Strobe{i}", new Vector3(i * 1.3f - 1.95f, 3.9f, 0f), new Vector3(0.5f, 0.5f, 0.5f));
            return root;
        }

        // A 4x3 wall of speaker cabinets.
        private static GameObject BuildSpeakerWall(Material mat)
        {
            var root = new GameObject("SpeakerWall");
            for (int col = 0; col < 4; col++)
                for (int row = 0; row < 3; row++)
                    Box(root, mat, $"Spk{col}{row}",
                        new Vector3(col * 1.6f - 2.4f, row * 1.4f + 0.7f, 0f),
                        new Vector3(1.5f, 1.3f, 1f));
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
