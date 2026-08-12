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
        private static readonly Vector3 PitCenter = new Vector3(0f, 0.5f, 0f);

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

            // Crowd members (M5a): rigged variant prefabs when the Crowd FBXs exist, else the
            // static M2b figure as a one-element array. CrowdController falls back to capsules
            // only when the array ends up empty.
            var figurePrefab = BuildFigurePrefab(crowdMat);
            var variants = CrowdPrefabs.EnsureCrowdVariants();
            if (variants.Length == 0 && figurePrefab != null) variants = new[] { figurePrefab };

            var crowd = Object.FindAnyObjectByType<CrowdController>();
            if (crowd != null && variants.Length > 0)
            {
                var so = new SerializedObject(crowd);
                var prop = so.FindProperty("memberPrefabs");
                if (prop != null)
                {
                    prop.arraySize = variants.Length;
                    for (int i = 0; i < variants.Length; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = variants[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(crowd);
                }
                else Debug.LogWarning("FestivalSceneSetup: 'memberPrefabs' not found on CrowdController.");
            }

            // Structure placement (behind the default 12x7 crowd, which spans z in [-3.6, 3.6]).
            // M5b: one full stage rig replaces the M2b Stage/Truss/Banner/PA set. While
            // MainStage.fbx is not yet exported, the legacy five-object placement still runs.
            GameObject stage, lightMount, paLeft, paRight;
            var stagePrefab = StructurePrefabs.EnsureMainStage();
            if (stagePrefab != null)
            {
                foreach (var legacy in new[] { "Stage", "Truss", "Banner", "PA Left", "PA Right" })
                {
                    var old = GameObject.Find(legacy);
                    if (old != null) Object.DestroyImmediate(old);
                }
                var existing = GameObject.Find("MainStage");
                if (existing != null) Object.DestroyImmediate(existing);

                var rig = (GameObject)PrefabUtility.InstantiatePrefab(stagePrefab);
                rig.name = "MainStage";
                rig.transform.position = new Vector3(0f, 0f, 9f);

                Transform Find(string n) { var t = FindDeep(rig.transform, n); return t; }
                stage = rig;
                lightMount = Find("RoofTruss") != null ? Find("RoofTruss").gameObject : rig;
                paLeft = Find("PAWingL") != null ? Find("PAWingL").gameObject : null;
                paRight = Find("PAWingR") != null ? Find("PAWingR").gameObject : null;
                if (paLeft == null || paRight == null)
                    Debug.LogWarning("FestivalSceneSetup: PAWingL/PAWingR not found on MainStage — " +
                                     "PA upgrade scaling has no target until the rig exports these children.");
            }
            else
            {
                stage = PlaceModel("Stage.fbx", "Stage", new Vector3(0f, 0f, 9f), structureMat);
                var truss = PlaceModel("Truss.fbx", "Truss", new Vector3(0f, 0f, 9f), structureMat);
                PlaceModel("Banner.fbx", "Banner", new Vector3(0f, 1.2f, 10f), structureMat);
                paLeft = PlaceModel("PASpeaker.fbx", "PA Left", new Vector3(-6f, 0f, 9f), structureMat);
                paRight = PlaceModel("PASpeaker.fbx", "PA Right", new Vector3(6f, 0f, 9f), structureMat);
                lightMount = truss != null ? truss : stage;
            }

            // Reparent the M2a accent lights onto the stage's light mount (roof truss).
            if (lightMount != null)
            {
                ReparentLight("Accent Amber", lightMount.transform, new Vector3(-2.5f, 4f, 9f));
                ReparentLight("Accent Magenta", lightMount.transform, new Vector3(2.5f, 4f, 9f));
                ReparentLight("Accent Cyan", lightMount.transform, new Vector3(0f, 4f, 9f));
            }

            var beams = EnsureBeams();

            WireBeatVfx(lit, stage);
            WireVenue(stage, paLeft, paRight);
            WireAtmosphere(beams);

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

        private static void WireAtmosphere(LightBeam[] beams)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            var ctrl = systems.GetComponent<AtmosphereController>();
            if (ctrl == null) ctrl = systems.AddComponent<AtmosphereController>();

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/ComicSky.mat");
            if (skyMat == null)
                Debug.LogWarning("FestivalSceneSetup: ComicSky.mat missing — run Apply Comic Look (M2a) first.");

            var sunGo = GameObject.Find("Directional Light");
            var sun = sunGo != null ? sunGo.GetComponent<Light>() : null;

            var aso = new SerializedObject(ctrl);
            SetRef(aso, "skyMaterial", skyMat);
            SetRef(aso, "sun", sun);
            var arr = aso.FindProperty("beams");
            if (arr != null)
            {
                arr.arraySize = beams.Length;
                for (int i = 0; i < beams.Length; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = beams[i];
            }
            aso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);

            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            if (boot != null)
            {
                var bso = new SerializedObject(boot);
                SetRef(bso, "atmosphere", ctrl);
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(boot);
            }
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void WireVenue(GameObject stage, GameObject paLeft, GameObject paRight)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            var venue = systems.GetComponent<VenueController>();
            if (venue == null) venue = systems.AddComponent<VenueController>();

            var lights = new System.Collections.Generic.List<Light>();
            foreach (var name in new[] { "Accent Amber", "Accent Magenta", "Accent Cyan" })
            {
                var go = GameObject.Find(name);
                if (go != null) { var l = go.GetComponent<Light>(); if (l != null) lights.Add(l); }
            }

            var vso = new SerializedObject(venue);
            SetRef(vso, "stage", stage != null ? stage.transform : null);
            SetRef(vso, "paLeft", paLeft != null ? paLeft.transform : null);
            SetRef(vso, "paRight", paRight != null ? paRight.transform : null);
            var arr = vso.FindProperty("accentLights");
            arr.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            vso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(venue);

            var upgrades = Object.FindAnyObjectByType<UpgradeSystem>();
            if (upgrades != null)
            {
                var uso = new SerializedObject(upgrades);
                SetRef(uso, "venue", venue);
                uso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(upgrades);
            }
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

        /// <summary>Move a rig light onto the stage's light mount. Re-aims after moving: these
        /// are spots (M5e), so the rotation baked at the authored position in ComicLookSetup
        /// points from the wrong place once the light lands on the truss.</summary>
        private static void ReparentLight(string name, Transform parent, Vector3 worldPos)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.LookRotation((PitCenter - worldPos).normalized, Vector3.up);
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

        private const string BeamMeshPath = "Assets/PitTycoon/Art/Models/BeamCone.asset";
        private const string BeamMatPath = "Assets/PitTycoon/Art/Materials/LightBeamMat.mat";
        private const string BeamPrefabPath = "Assets/PitTycoon/Art/Prefabs/LightBeam.prefab";

        /// <summary>Open-ended cone: apex at the origin, opening along -Y to radius/length.
        /// uv.y runs 0 at the apex to 1 at the open end, which the beam shader fades along.
        /// No caps — a capped cone reads as a solid object, not a shaft of light.</summary>
        private static Mesh BuildBeamCone(int segments, float radius, float length)
        {
            var verts = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 3];

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Cos(a) * radius, -length, Mathf.Sin(a) * radius);
                verts[i * 2] = Vector3.zero;      // apex, duplicated per segment for clean normals
                verts[i * 2 + 1] = ring;
                uvs[i * 2] = new Vector2(i / (float)segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1;
                int b1 = ((i + 1) % segments) * 2 + 1;
                int t = i * 3;
                // One triangle per segment: apex + this segment's ring vertex + the next one.
                // A second triangle would span two apex vertices that share the origin — zero area.
                tris[t] = a0; tris[t + 1] = a1; tris[t + 2] = b1;
            }

            var mesh = new Mesh { name = "BeamCone" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject EnsureBeamPrefab()
        {
            var beamShader = Shader.Find("PitTycoon/LightBeam");
            if (beamShader == null)
            {
                Debug.LogWarning("FestivalSceneSetup: PitTycoon/LightBeam shader missing — beams skipped.");
                return null;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
            if (mesh == null)
            {
                mesh = BuildBeamCone(24, 3.2f, 16f);
                AssetDatabase.CreateAsset(mesh, BeamMeshPath);
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(BeamMatPath);
            if (mat == null) { mat = new Material(beamShader); AssetDatabase.CreateAsset(mat, BeamMatPath); }
            mat.shader = beamShader;
            EditorUtility.SetDirty(mat);

            // Idempotent: an existing prefab is kept, not rebuilt. Re-running the builder must
            // not revert per-beam tuning (sweepAxis, sweepDegrees) the developer set on it.
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BeamPrefabPath);
            if (existing != null) return existing;

            var temp = new GameObject("LightBeam");
            temp.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = temp.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            temp.AddComponent<LightBeam>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, BeamPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>Hang a beam cone under each accent light, aligned to the light's aim.
        /// The prefab's cone opens along -Y, so a local -90 X rotation points it down the
        /// parent light's +Z (forward) axis. Phases are staggered so they never sweep in unison.</summary>
        private static LightBeam[] EnsureBeams()
        {
            var prefab = EnsureBeamPrefab();
            if (prefab == null) return new LightBeam[0];

            var beams = new System.Collections.Generic.List<LightBeam>();
            string[] lightNames = { "Accent Amber", "Accent Magenta", "Accent Cyan" };

            for (int i = 0; i < lightNames.Length; i++)
            {
                var lightGo = GameObject.Find(lightNames[i]);
                if (lightGo == null)
                {
                    Debug.LogWarning($"FestivalSceneSetup: '{lightNames[i]}' not found — beam skipped.");
                    continue;
                }

                var old = lightGo.transform.Find("LightBeam");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var beamGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                beamGo.name = "LightBeam";
                beamGo.transform.SetParent(lightGo.transform, false);
                beamGo.transform.localPosition = Vector3.zero;
                beamGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                var beam = beamGo.GetComponent<LightBeam>();
                var bso = new SerializedObject(beam);
                var phase = bso.FindProperty("phaseOffset");
                if (phase != null) phase.floatValue = i / (float)lightNames.Length;
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(beam);

                beams.Add(beam);
            }
            return beams.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
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
    }
}
