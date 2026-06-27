using UnityEditor;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds + wires the festival ground (M4a): enlarges the ground plane, creates/loads the
    /// open-air VenueLayout with greybox build spots, adds BuildSpotController + BuildSystem on the
    /// Systems object, seeds survey/live camera poses + the buildSpots ref on
    /// UpgradePreviewController, and wires GameBootstrap. Run AFTER Build HUD + Build Upgrade
    /// Preview. Idempotent.
    /// </summary>
    public static class FestivalGroundSetup
    {
        private const string LayoutPath = "Assets/Settings/OpenAirLayout.asset";
        private const string GhostMatPath = "Assets/PitTycoon/Art/Materials/GhostMat.mat";

        [MenuItem("Pit Tycoon/Build Festival Ground")]
        public static void Build()
        {
            var venue = Object.FindFirstObjectByType<VenueController>();
            var crowd = Object.FindFirstObjectByType<CrowdController>();
            var hype = Object.FindFirstObjectByType<HypeSystem>();
            var economy = Object.FindFirstObjectByType<EconomySystem>();
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var controller = Object.FindFirstObjectByType<UpgradePreviewController>();
            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            if (venue == null || crowd == null || hype == null || economy == null || cam == null)
            {
                Debug.LogError("FestivalGroundSetup: missing VenueController/CrowdController/HypeSystem/" +
                               "EconomySystem/Camera. Run Build Greybox + Build Festival Scene + Build HUD + " +
                               "Build Upgrade Preview first.");
                return;
            }

            // 1. Enlarge the ground plane (Plane = 10u; scale 12 -> ~120u open field).
            var ground = GameObject.Find("Ground");
            if (ground != null) ground.transform.localScale = new Vector3(12f, 1f, 12f);
            else Debug.LogWarning("FestivalGroundSetup: no 'Ground' object found to enlarge.");

            // 2. Greybox structure prefabs.
            var secondStage = StructureGreyboxPrefabs.EnsureSecondStage();
            var camping = StructureGreyboxPrefabs.EnsureCampingField();
            var gate = StructureGreyboxPrefabs.EnsureEntranceGate();

            // 3. VenueLayout asset (open-air theme). Positions/costs/effects are tuned in play.
            var layout = LoadOrCreateLayout();
            layout.spots = new[]
            {
                MakeSpot("second_stage", "Second Stage", secondStage,
                         pos: new Vector3(-22f, 0f, 0f),  euler: new Vector3(0f, 35f, 0f),
                         camPos: new Vector3(-14f, 6f, -8f), camEuler: new Vector3(18f, 30f, 0f),
                         cost: 120, BuildEffectKind.HypeRate, 3f),
                MakeSpot("camping", "Camping Field", camping,
                         pos: new Vector3(16f, 0f, -22f), euler: new Vector3(0f, -20f, 0f),
                         camPos: new Vector3(10f, 7f, -34f), camEuler: new Vector3(22f, -15f, 0f),
                         cost: 90, BuildEffectKind.Capacity, 40f),
                MakeSpot("gate", "Entrance Gate", gate,
                         pos: new Vector3(24f, 0f, -10f), euler: new Vector3(0f, -50f, 0f),
                         camPos: new Vector3(16f, 5f, -18f), camEuler: new Vector3(12f, -40f, 0f),
                         cost: 60, BuildEffectKind.HypeRate, 1.5f),
            };
            EditorUtility.SetDirty(layout);

            // 4. BuildSpotController + BuildSystem on the Systems object (where VenueController lives).
            var systems = venue.gameObject;
            var spotsCtl = systems.GetComponent<BuildSpotController>();
            if (spotsCtl == null) spotsCtl = systems.AddComponent<BuildSpotController>();
            var buildSys = systems.GetComponent<BuildSystem>();
            if (buildSys == null) buildSys = systems.AddComponent<BuildSystem>();

            var ghost = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);

            // 5. Wire BuildSpotController.
            SetRef(spotsCtl, "layout", layout);
            SetRef(spotsCtl, "ghostMaterial", ghost);

            // 6. Wire BuildSystem.
            SetRef(buildSys, "economy", economy);
            SetRef(buildSys, "hype", hype);
            SetRef(buildSys, "crowd", crowd);
            SetRef(buildSys, "spots", spotsCtl);

            // 7. UpgradePreviewController: buildSpots ref + live pose (= current camera pose) + survey pose.
            if (controller != null)
            {
                SetRef(controller, "buildSpots", spotsCtl);
                var so = new SerializedObject(controller);
                so.FindProperty("liveCamPosition").vector3Value = cam.transform.position;
                so.FindProperty("liveCamEuler").vector3Value = cam.transform.eulerAngles;
                so.FindProperty("surveyCamPosition").vector3Value = new Vector3(0f, 45f, -55f);
                so.FindProperty("surveyCamEuler").vector3Value = new Vector3(40f, 0f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
            else Debug.LogWarning("FestivalGroundSetup: no UpgradePreviewController found. Run Build Upgrade Preview first.");

            // 8. GameBootstrap refs.
            if (boot != null)
            {
                SetRef(boot, "buildSpots", spotsCtl);
                SetRef(boot, "builds", buildSys);
            }
            else Debug.LogWarning("FestivalGroundSetup: no GameBootstrap found in scene.");

            EditorUtility.SetDirty(spotsCtl);
            EditorUtility.SetDirty(buildSys);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Pit Tycoon: Festival Ground built. Tune build-spot poses/costs/effects on " +
                      "OpenAirLayout and survey/live poses on UpgradePreviewController (Systems).");
        }

        private static BuildSpot MakeSpot(string id, string label, GameObject prefab,
                                          Vector3 pos, Vector3 euler, Vector3 camPos, Vector3 camEuler,
                                          int cost, BuildEffectKind effect, float magnitude)
        {
            return new BuildSpot
            {
                id = id, label = label, structurePrefab = prefab,
                position = pos, euler = euler, cameraPosition = camPos, cameraEuler = camEuler,
                cost = cost, effect = effect, effectMagnitude = magnitude, color = Color.white,
            };
        }

        private static VenueLayout LoadOrCreateLayout()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VenueLayout>(LayoutPath);
            if (existing != null) return existing;
            var layout = ScriptableObject.CreateInstance<VenueLayout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
            return layout;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"FestivalGroundSetup: field '{field}' not found on {target.GetType().Name}.");
        }
    }
}
