using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// M2b: drops the imported festival FBX assets into the open Greybox scene.
    /// Creates the crowd-figure prefab + structure material, places stage/truss/PA/banner,
    /// reparents the M2a accent lights onto the truss, and points CrowdController at the
    /// crowd-figure prefab. Run after the Comic Look (M2a) and after the FBX have imported.
    /// Menu: "Pit Tycoon/Build Festival Scene (M2b)".
    /// </summary>
    public static class FestivalSceneSetup
    {
        private const string ModelDir = "Assets/PitTycoon/Art/Models";
        private const string MatDir = "Assets/PitTycoon/Art/Materials";
        private const string PrefabDir = "Assets/PitTycoon/Art/Prefabs";

        private static readonly Color Structure = new Color(0.14f, 0.12f, 0.16f);
        private static readonly Color Crowd = new Color(0.16f, 0.13f, 0.18f);

        [MenuItem("Pit Tycoon/Build Festival Scene (M2b)")]
        public static void BuildFestivalScene()
        {
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            var lit = Shader.Find("PitTycoon/ComicLit");
            if (lit == null)
            {
                EditorUtility.DisplayDialog("Pit Tycoon", "ComicLit shader missing — run M2a first.", "OK");
                return;
            }

            var structureMat = LoadOrCreateMat($"{MatDir}/StructureMat.mat", lit, Structure);
            var crowdMat = LoadOrCreateMat($"{MatDir}/CrowdMat.mat", lit, Crowd);

            // Crowd-figure prefab (figure mesh + CrowdMat), wired into CrowdController.
            var figurePrefab = BuildFigurePrefab(crowdMat);
            var crowd = Object.FindAnyObjectByType<CrowdController>();
            if (crowd != null && figurePrefab != null)
            {
                var so = new SerializedObject(crowd);
                var prop = so.FindProperty("memberPrefab");
                if (prop != null) { prop.objectReferenceValue = figurePrefab; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(crowd); }
            }

            // Structure placement (behind the default 12x7 crowd, which spans z in [-3.6, 3.6]).
            var stage = PlaceModel("Stage.fbx", "Stage", new Vector3(0f, 0f, 9f), structureMat);
            var truss = PlaceModel("Truss.fbx", "Truss", new Vector3(0f, 0f, 9f), structureMat);
            PlaceModel("Banner.fbx", "Banner", new Vector3(0f, 1.2f, 10f), structureMat);
            PlaceModel("PASpeaker.fbx", "PA Left", new Vector3(-6f, 0f, 9f), structureMat);
            PlaceModel("PASpeaker.fbx", "PA Right", new Vector3(6f, 0f, 9f), structureMat);

            // Reparent the M2a accent lights onto the truss beam.
            if (truss != null)
            {
                ReparentLight("Accent Amber", truss.transform, new Vector3(-2.5f, 4f, 9f));
                ReparentLight("Accent Magenta", truss.transform, new Vector3(2.5f, 4f, 9f));
                ReparentLight("Accent Cyan", truss.transform, new Vector3(0f, 4f, 9f));
            }

            WireBeatVfx(lit, stage);

            var active = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Pit Tycoon",
                "Festival scene built. Press Play — the crowd should be figures that hop, " +
                "with stage/truss/PA/banner behind them. Tune crowd hop on CrowdController if needed.", "OK");
        }

        private static void WireBeatVfx(Shader lit, GameObject stage)
        {
            var whirlMat = LoadOrCreateMat($"{MatDir}/WhirlpoolMat.mat", lit, new Color(0.21f, 0.79f, 0.88f));
            var coinMat = LoadOrCreateMat($"{MatDir}/CoinMat.mat", lit, new Color(1f, 0.69f, 0.24f));

            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            var ctrl = systems.GetComponent<BeatVfxController>();
            if (ctrl == null) ctrl = systems.AddComponent<BeatVfxController>();

            var cso = new SerializedObject(ctrl);
            SetRef(cso, "stageAnchor", stage != null ? stage.transform : null);
            SetRef(cso, "whirlpoolMaterial", whirlMat);
            SetRef(cso, "coinMaterial", coinMat);
            cso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);

            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            if (boot != null)
            {
                var bso = new SerializedObject(boot);
                SetRef(bso, "beatVfx", ctrl);
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(boot);
            }
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static GameObject BuildFigurePrefab(Material crowdMat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/CrowdFigure.fbx");
            if (fbx == null) return null;
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in temp.GetComponentsInChildren<Renderer>()) r.sharedMaterial = crowdMat;
            string path = $"{PrefabDir}/CrowdFigure.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static GameObject PlaceModel(string fbxName, string instanceName, Vector3 pos, Material mat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{fbxName}");
            if (fbx == null) return null;
            var existing = GameObject.Find(instanceName);
            if (existing != null) Object.DestroyImmediate(existing);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            inst.name = instanceName;
            inst.transform.position = pos;
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            return inst;
        }

        private static void ReparentLight(string name, Transform parent, Vector3 worldPos)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
        }

        private static Material LoadOrCreateMat(string path, Shader shader, Color baseColor)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColor);
            var ramp = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PitTycoon/Art/Ramps/ComicRamp.png");
            if (ramp != null) mat.SetTexture("_RampTex", ramp);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
