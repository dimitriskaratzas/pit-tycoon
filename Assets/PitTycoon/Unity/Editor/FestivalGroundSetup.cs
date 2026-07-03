using UnityEditor;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds + wires the festival ground (M4a+M4c): enlarges the ground plane, creates/loads the
    /// open-air VenueLayout with the full 11-spot greybox build-spot roster (M4a originals plus the
    /// M4c grandstand/food court/bar/VIP lounge/sponsor banner/amp stack/strobe rig/speaker wall),
    /// adds BuildSpotController + BuildSystem on the Systems object, seeds survey/live camera poses +
    /// the buildSpots ref on UpgradePreviewController, and wires GameBootstrap. Run AFTER Build HUD +
    /// Build Upgrade Preview. Idempotent.
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
            var abilitySys = Object.FindFirstObjectByType<AbilitySystem>();
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var controller = Object.FindFirstObjectByType<UpgradePreviewController>();
            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            if (venue == null || crowd == null || hype == null || economy == null || abilitySys == null || cam == null)
            {
                Debug.LogError("FestivalGroundSetup: missing VenueController/CrowdController/HypeSystem/" +
                               "EconomySystem/AbilitySystem/Camera. Run Build Greybox + Build Festival Scene + Build HUD + " +
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
            var grandstand = StructureGreyboxPrefabs.EnsureGrandstand();
            var foodCourt = StructureGreyboxPrefabs.EnsureFoodCourt();
            var bar = StructureGreyboxPrefabs.EnsureBar();
            var vipLounge = StructureGreyboxPrefabs.EnsureVipLounge();
            var sponsorBanner = StructureGreyboxPrefabs.EnsureSponsorBanner();
            var ampStack = StructureGreyboxPrefabs.EnsureAmpStack();
            var strobeRig = StructureGreyboxPrefabs.EnsureStrobeRig();
            var speakerWall = StructureGreyboxPrefabs.EnsureSpeakerWall();

            // 3. VenueLayout asset (open-air theme). Positions/costs/effects are tuned in play.
            var layout = LoadOrCreateLayout();
            layout.spots = new[]
            {
                // --- M4a originals (unchanged) ---
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
                // --- M4c roster ---
                MakeSpot("grandstand", "Grandstand", grandstand,
                         pos: new Vector3(-14f, 0f, -26f), euler: new Vector3(0f, 20f, 0f),
                         camPos: new Vector3(-8f, 7f, -36f), camEuler: new Vector3(20f, -30f, 0f),
                         cost: 150, BuildEffectKind.Capacity, 30f),
                MakeSpot("food_court", "Food Court", foodCourt,
                         pos: new Vector3(-26f, 0f, -16f), euler: new Vector3(0f, 60f, 0f),
                         camPos: new Vector3(-18f, 6f, -24f), camEuler: new Vector3(18f, -45f, 0f),
                         cost: 100, BuildEffectKind.PassiveCash, 25f),
                MakeSpot("bar", "Bar", bar,
                         pos: new Vector3(14f, 0f, 14f), euler: new Vector3(0f, -30f, 0f),
                         camPos: new Vector3(8f, 5f, 6f), camEuler: new Vector3(14f, 35f, 0f),
                         cost: 70, BuildEffectKind.PassiveCash, 15f),
                MakeSpot("vip_lounge", "VIP Lounge", vipLounge,
                         pos: new Vector3(-16f, 0f, 12f), euler: new Vector3(0f, 40f, 0f),
                         camPos: new Vector3(-8f, 6f, 4f), camEuler: new Vector3(16f, -45f, 0f),
                         cost: 200, BuildEffectKind.CashMultiplier, 0.15f),
                MakeSpot("sponsor_banner", "Sponsor Banner", sponsorBanner,
                         pos: new Vector3(0f, 0f, -32f), euler: new Vector3(0f, 0f, 0f),
                         camPos: new Vector3(0f, 6f, -22f), camEuler: new Vector3(14f, 180f, 0f),
                         cost: 80, BuildEffectKind.CashMultiplier, 0.1f),
                MakeSpot("amp_stack", "Amp Stack", ampStack,
                         pos: new Vector3(10f, 0f, 5f), euler: new Vector3(0f, -60f, 0f),
                         camPos: new Vector3(5f, 4f, -2f), camEuler: new Vector3(14f, 35f, 0f),
                         cost: 140, BuildEffectKind.AbilitySpike, 0.5f, targetAbilityId: "woofer"),
                MakeSpot("strobe_rig", "Strobe Rig", strobeRig,
                         pos: new Vector3(-10f, 0f, 5f), euler: new Vector3(0f, 60f, 0f),
                         camPos: new Vector3(-5f, 4f, -2f), camEuler: new Vector3(14f, -35f, 0f),
                         cost: 110, BuildEffectKind.AbilitySpike, 0.5f, targetAbilityId: "lightburst"),
                MakeSpot("speaker_wall", "Speaker Wall", speakerWall,
                         pos: new Vector3(30f, 0f, 2f), euler: new Vector3(0f, -90f, 0f),
                         camPos: new Vector3(22f, 5f, -4f), camEuler: new Vector3(14f, 53f, 0f),
                         cost: 160, BuildEffectKind.AbilitySpike, 0.5f, targetAbilityId: "whirlpool"),
            };
            EditorUtility.SetDirty(layout);

            // 4. BuildSpotController + BuildSystem on the Systems object (where VenueController lives).
            var systems = venue.gameObject;
            var spotsCtl = systems.GetComponent<BuildSpotController>();
            if (spotsCtl == null) spotsCtl = systems.AddComponent<BuildSpotController>();
            var buildSys = systems.GetComponent<BuildSystem>();
            if (buildSys == null) buildSys = systems.AddComponent<BuildSystem>();

            var ghost = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
            if (ghost == null)
                Debug.LogWarning("FestivalGroundSetup: GhostMat not found — run Build Upgrade Preview first so build-spot previews render.");

            // 5. Wire BuildSpotController.
            SetRef(spotsCtl, "layout", layout);
            SetRef(spotsCtl, "ghostMaterial", ghost);

            // 6. Wire BuildSystem.
            SetRef(buildSys, "economy", economy);
            SetRef(buildSys, "hype", hype);
            SetRef(buildSys, "crowd", crowd);
            SetRef(buildSys, "spots", spotsCtl);
            SetRef(buildSys, "abilities", abilitySys);

            // 7. UpgradePreviewController: buildSpots ref + live pose (= current camera pose) + survey pose.
            if (controller != null)
            {
                SetRef(controller, "buildSpots", spotsCtl);
                var so = new SerializedObject(controller);
                SetVector3(so, "liveCamPosition", cam.transform.position);
                SetVector3(so, "liveCamEuler", cam.transform.eulerAngles);
                SetVector3(so, "surveyCamPosition", new Vector3(0f, 45f, -55f));
                SetVector3(so, "surveyCamEuler", new Vector3(40f, 0f, 0f));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
            else Debug.LogWarning("FestivalGroundSetup: no UpgradePreviewController found. Run Build Upgrade Preview first.");

            // 8. GameBootstrap refs. (BuildSpotController is reached at runtime via BuildSystem.spots,
            // so GameBootstrap only needs the BuildSystem ref.)
            if (boot != null) SetRef(boot, "builds", buildSys);
            else Debug.LogWarning("FestivalGroundSetup: no GameBootstrap found in scene.");

            // 9. FreeLookController on the Main Camera (M4b): bounds from the ~120u ground, zoom
            //    around the survey pose's distance to the ground centre. Idempotent.
            var freeLook = cam.GetComponent<FreeLookController>();
            if (freeLook == null) freeLook = cam.gameObject.AddComponent<FreeLookController>();

            var camRig = cam.GetComponent<CameraRig>();
            if (camRig != null) SetRef(freeLook, "rig", camRig);
            else Debug.LogWarning("FestivalGroundSetup: no CameraRig on the camera. Run Build Upgrade Preview first.");

            var flSo = new SerializedObject(freeLook);
            SetFloat(flSo, "minX", -58f);
            SetFloat(flSo, "maxX", 58f);
            SetFloat(flSo, "minZ", -58f);
            SetFloat(flSo, "maxZ", 58f);
            SetFloat(flSo, "focusY", 0f);
            SetFloat(flSo, "minDistance", 20f);
            SetFloat(flSo, "maxDistance", 90f);
            SetFloat(flSo, "panSpeedPerDistance", 0.5f);
            SetFloat(flSo, "orbitSpeed", 0.2f);
            SetFloat(flSo, "zoomSpeed", 3f);
            SetFloat(flSo, "edgePanMargin", 12f);
            flSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(freeLook);

            if (boot != null) SetRef(boot, "freeLook", freeLook);

            EditorUtility.SetDirty(spotsCtl);
            EditorUtility.SetDirty(buildSys);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Pit Tycoon: Festival Ground built with the full 11-spot roster (second stage, " +
                      "camping, gate, grandstand, food court, bar, VIP lounge, sponsor banner, amp " +
                      "stack, strobe rig, speaker wall). Tune build-spot poses/costs/effects on " +
                      "OpenAirLayout and survey/live poses on UpgradePreviewController (Systems). " +
                      "Free-look bounds/speeds live on the Main Camera's FreeLookController.");
        }

        private static BuildSpot MakeSpot(string id, string label, GameObject prefab,
                                          Vector3 pos, Vector3 euler, Vector3 camPos, Vector3 camEuler,
                                          int cost, BuildEffectKind effect, float magnitude,
                                          string targetAbilityId = "")
        {
            return new BuildSpot
            {
                id = id, label = label, structurePrefab = prefab,
                position = pos, euler = euler, cameraPosition = camPos, cameraEuler = camEuler,
                cost = cost, effect = effect, effectMagnitude = magnitude,
                targetAbilityId = targetAbilityId, color = Color.white,
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

        private static void SetVector3(SerializedObject so, string field, Vector3 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector3Value = value;
            else Debug.LogWarning($"FestivalGroundSetup: field '{field}' not found on {so.targetObject.GetType().Name}.");
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"FestivalGroundSetup: field '{field}' not found on {so.targetObject.GetType().Name}.");
        }
    }
}
