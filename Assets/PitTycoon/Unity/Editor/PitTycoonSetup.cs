using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// One-click greybox scene builder so the human does minimal manual wiring.
    /// Menu: "Pit Tycoon/Build Greybox Scene".
    /// </summary>
    public static class PitTycoonSetup
    {
        private const string ConfigPath = "Assets/Settings/AudioAnalyzerConfig.asset";
        private const string ScenePath = "Assets/Scenes/Greybox.unity";

        [MenuItem("Pit Tycoon/Build Greybox Scene")]
        public static void BuildGreyboxScene()
        {
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Audio");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Analyzer config asset (created once, then reused/tunable in the Inspector).
            // IMPORTANT: create/load this AFTER NewScene. NewScene runs an unused-asset
            // unload pass; a config created beforehand isn't referenced by anything yet, so
            // it gets unloaded into a destroyed-object "fake null" and wires as None.
            var config = AssetDatabase.LoadAssetAtPath<AudioAnalyzerConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<AudioAnalyzerConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            // Camera (pulled-back, RTS-ish).
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 12f, -14f), Quaternion.Euler(35f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            camGo.AddComponent<AudioListener>();

            // Directional light.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground reference.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 3f);

            // Audio + analyzer.
            var audioGo = new GameObject("Audio");
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = true;
            src.loop = false;

            // Auto-assign a clip if one exists in Assets/Audio (e.g. the bundled sample).
            bool clipAssigned = false;
            string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            if (clipGuids.Length > 0)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(clipGuids[0]));
                src.clip = clip;
                clipAssigned = clip != null;
            }

            var analyzer = audioGo.AddComponent<FftAudioAnalyzer>();
            WireRefs(analyzer, ("config", config), ("source", src));

            // Crowd.
            var crowdGo = new GameObject("Crowd");
            var crowd = crowdGo.AddComponent<CrowdController>();

            // Bootstrap.
            var bootGo = new GameObject("Bootstrap");
            var boot = bootGo.AddComponent<GameBootstrap>();
            WireRefs(boot, ("analyzer", analyzer), ("crowd", crowd));

            // Debug HUD.
            var hudGo = new GameObject("DebugHud");
            var hud = hudGo.AddComponent<DebugHud>();
            WireRefs(hud, ("analyzer", analyzer));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            string next = clipAssigned
                ? "A clip from Assets/Audio was auto-assigned.\n\nJust press Play: the crowd should bob with intensity and pop on beats."
                : "No clip found in Assets/Audio.\n\nNext:\n1. Put an audio file in Assets/Audio.\n" +
                  "2. Select the 'Audio' object and drag your clip into AudioSource > AudioClip.\n" +
                  "3. Press Play.";
            EditorUtility.DisplayDialog("Pit Tycoon", "Greybox scene built and wired at:\n" + ScenePath + "\n\n" + next, "OK");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void WireRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var entry in refs)
            {
                var prop = so.FindProperty(entry.field);
                if (prop == null)
                {
                    Debug.LogError($"[PitTycoonSetup] Serialized field '{entry.field}' not found on {target.GetType().Name}.");
                    continue;
                }
                prop.objectReferenceValue = entry.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
