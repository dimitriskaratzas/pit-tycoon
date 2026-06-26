using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds and wires the upgrade ghost-preview (sub-project B): a ghost material, a CameraRig
    /// on the Main Camera, an UpgradePreviewController on the Systems object with default
    /// waypoints, the venue/crowd ghost-material refs, and GameBootstrap.preview (which delivers
    /// the controller to ShopView at runtime via hud.Initialize). Run AFTER Build HUD so the shop
    /// rows have Buy/Cancel. Idempotent.
    /// </summary>
    public static class PreviewSetup
    {
        private const string GhostMatPath = "Assets/PitTycoon/Art/Materials/GhostMat.mat";

        [MenuItem("Pit Tycoon/Build Upgrade Preview")]
        public static void Build()
        {
            Material ghost = LoadOrCreateGhostMaterial();

            var venue = Object.FindFirstObjectByType<VenueController>();
            var crowd = Object.FindFirstObjectByType<CrowdController>();
            var abilities = Object.FindFirstObjectByType<AbilitySystem>();
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            if (venue == null || crowd == null || abilities == null || cam == null)
            {
                Debug.LogError("PreviewSetup: missing VenueController/CrowdController/AbilitySystem/Camera. " +
                               "Run Build Greybox + Build Festival Scene first.");
                return;
            }

            // CameraRig on the Main Camera.
            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();

            // UpgradePreviewController on the Systems object (where VenueController lives).
            var controller = venue.GetComponent<UpgradePreviewController>();
            if (controller == null) controller = venue.gameObject.AddComponent<UpgradePreviewController>();

            // Ghost material into venue + crowd.
            SetRef(venue, "ghostMaterial", ghost);
            SetRef(crowd, "ghostMaterial", ghost);

            // Controller refs + default waypoints.
            WireController(controller, rig, venue, crowd, abilities);

            // GameBootstrap.preview — delivered to HudController/ShopView at runtime via
            // hud.Initialize (ShopView's preview ref is not serialized). Run Build HUD first so
            // the shop rows carry Buy/Cancel.
            if (boot != null) SetRef(boot, "preview", controller);
            else Debug.LogWarning("PreviewSetup: no GameBootstrap found in scene.");

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(venue);
            EditorUtility.SetDirty(crowd);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Pit Tycoon: Upgrade Preview built. Tune camera waypoints on " +
                      "UpgradePreviewController (Systems) and ghost alpha on GhostMat.");
        }

        private static void WireController(UpgradePreviewController c, CameraRig rig,
                                           VenueController venue, CrowdController crowd, AbilitySystem abilities)
        {
            var so = new SerializedObject(c);
            so.FindProperty("rig").objectReferenceValue = rig;
            so.FindProperty("venue").objectReferenceValue = venue;
            so.FindProperty("crowd").objectReferenceValue = crowd;
            so.FindProperty("abilities").objectReferenceValue = abilities;
            so.FindProperty("flyDuration").floatValue = 0.6f;

            // Default waypoints (world poses derived from the M2b scene; tune in play).
            // Stage z=9, PA at x=+/-6 z=9, truss lights y=4 z=9, crowd in front of stage.
            var wps = so.FindProperty("waypoints");
            wps.arraySize = 4;
            SetWaypoint(wps.GetArrayElementAtIndex(0), (int)UpgradeKind.Stage,    new Vector3(0f, 6f, 2f),   new Vector3(20f, 0f, 0f));
            SetWaypoint(wps.GetArrayElementAtIndex(1), (int)UpgradeKind.PA,       new Vector3(4f, 4f, 3f),   new Vector3(16f, -22f, 0f));
            SetWaypoint(wps.GetArrayElementAtIndex(2), (int)UpgradeKind.Lighting, new Vector3(0f, 5f, 2f),   new Vector3(-4f, 0f, 0f));
            SetWaypoint(wps.GetArrayElementAtIndex(3), (int)UpgradeKind.Grounds,  new Vector3(0f, 7f, -10f), new Vector3(28f, 0f, 0f));

            so.FindProperty("abilityCamPosition").vector3Value = new Vector3(0f, 9f, -11f);
            so.FindProperty("abilityCamEuler").vector3Value = new Vector3(30f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWaypoint(SerializedProperty wp, int kind, Vector3 pos, Vector3 euler)
        {
            wp.FindPropertyRelative("kind").enumValueIndex = kind;
            wp.FindPropertyRelative("position").vector3Value = pos;
            wp.FindPropertyRelative("euler").vector3Value = euler;
        }

        private static Material LoadOrCreateGhostMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
            if (existing != null) return existing;

            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh) { name = "GhostMat" };
            m.SetColor("_BaseColor", new Color(0.45f, 0.85f, 1f, 0.32f));
            m.SetFloat("_Surface", 1f);                       // 0 opaque, 1 transparent
            m.SetFloat("_Blend", 0f);                         // alpha blend
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, GhostMatPath);
            AssetDatabase.SaveAssets();
            return m;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"PreviewSetup: field '{field}' not found on {target.GetType().Name}.");
        }
    }
}
