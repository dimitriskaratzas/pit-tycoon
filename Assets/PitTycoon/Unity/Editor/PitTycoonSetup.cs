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

            // Analyzer config asset (created once, then reused/tunable in the Inspector).
            var config = AssetDatabase.LoadAssetAtPath<AudioAnalyzerConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<AudioAnalyzerConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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
            var analyzer = audioGo.AddComponent<FftAudioAnalyzer>();
            SetSerializedRef(analyzer, "config", config);
            SetSerializedRef(analyzer, "source", src);

            // Crowd.
            var crowdGo = new GameObject("Crowd");
            var crowd = crowdGo.AddComponent<CrowdController>();

            // Bootstrap.
            var bootGo = new GameObject("Bootstrap");
            var boot = bootGo.AddComponent<GameBootstrap>();
            SetSerializedRef(boot, "analyzer", analyzer);
            SetSerializedRef(boot, "crowd", crowd);

            // Debug HUD.
            var hudGo = new GameObject("DebugHud");
            var hud = hudGo.AddComponent<DebugHud>();
            SetSerializedRef(hud, "analyzer", analyzer);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.DisplayDialog(
                "Pit Tycoon",
                "Greybox scene built and wired at:\n" + ScenePath +
                "\n\nNext:\n1. Put an audio file in Assets/Audio.\n" +
                "2. Select the 'Audio' object and drag your clip into AudioSource > AudioClip.\n" +
                "3. Press Play. The crowd should bob with intensity and pop on beats.",
                "OK");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetSerializedRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
