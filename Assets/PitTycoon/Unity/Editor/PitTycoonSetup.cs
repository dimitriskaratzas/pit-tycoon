using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// One-click greybox scene builder for the full M1 loop. Creates the SO assets,
    /// builds the GameObjects, and wires every serialized reference automatically.
    /// Menu: "Pit Tycoon/Build Greybox Scene".
    /// </summary>
    public static class PitTycoonSetup
    {
        private const string AudioConfigPath = "Assets/Settings/AudioAnalyzerConfig.asset";
        private const string AbilityPath = "Assets/Settings/AbilityDefinition.asset";
        private const string UpgradePath = "Assets/Settings/UpgradeDefinition.asset";
        private const string ScenePath = "Assets/Scenes/Greybox.unity";

        [MenuItem("Pit Tycoon/Build Greybox Scene")]
        public static void BuildGreyboxScene()
        {
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Audio");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Assets created AFTER NewScene (NewScene's unused-asset unload would otherwise
            // destroy a freshly-created asset before anything references it -> wires as None).
            var audioConfig = LoadOrCreate<AudioAnalyzerConfig>(AudioConfigPath);
            var abilityDef = LoadOrCreate<AbilityDefinition>(AbilityPath);
            var upgradeDef = LoadOrCreate<UpgradeDefinition>(UpgradePath);

            // Camera.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 12f, -14f), Quaternion.Euler(35f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            camGo.AddComponent<AudioListener>();

            // Light.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 3f);

            // Audio + analyzer (SetController owns playback, so playOnAwake = false).
            var audioGo = new GameObject("Audio");
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;

            bool clipAssigned = false;
            string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            if (clipGuids.Length > 0)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(clipGuids[0]));
                src.clip = clip;
                clipAssigned = clip != null;
            }

            var analyzer = audioGo.AddComponent<FftAudioAnalyzer>();
            WireRefs(analyzer, ("config", audioConfig), ("source", src));

            // Crowd.
            var crowdGo = new GameObject("Crowd");
            var crowd = crowdGo.AddComponent<CrowdController>();

            // Systems hub.
            var sysGo = new GameObject("Systems");
            var hype = sysGo.AddComponent<HypeSystem>();
            var ability = sysGo.AddComponent<WhirlpoolAbility>();
            var economy = sysGo.AddComponent<EconomySystem>();
            var upgrades = sysGo.AddComponent<UpgradeSystem>();
            var setController = sysGo.AddComponent<SetController>();

            WireRefs(hype, ("analyzer", analyzer));
            WireRefs(ability, ("definition", abilityDef), ("vfxAnchor", crowdGo.transform));
            WireRefs(upgrades,
                ("capacityUpgrade", upgradeDef), ("economy", economy), ("hype", hype), ("crowd", crowd));
            WireRefs(setController,
                ("source", src), ("hype", hype), ("economy", economy), ("crowd", crowd));

            // HUD (expanded DebugHud).
            var hudGo = new GameObject("DebugHud");
            var hud = hudGo.AddComponent<DebugHud>();
            WireRefs(hud,
                ("analyzer", analyzer), ("hype", hype), ("ability", ability),
                ("economy", economy), ("setController", setController), ("upgrades", upgrades));

            // Composition root.
            var bootGo = new GameObject("Bootstrap");
            var boot = bootGo.AddComponent<GameBootstrap>();
            WireRefs(boot,
                ("analyzer", analyzer), ("crowd", crowd), ("hype", hype), ("ability", ability),
                ("economy", economy), ("upgrades", upgrades), ("setController", setController));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            string next = clipAssigned
                ? "A clip from Assets/Audio was auto-assigned.\n\nPress Play:\n" +
                  "- Crowd bobs with intensity, pops on beats.\n" +
                  "- Hype meter fills; press SPACE on-beat to spike it (whirlpool).\n" +
                  "- When the track ends, hype banks to cash (coins fly), then the\n" +
                  "  intermission shop appears — buy capacity and Start Next Set."
                : "No clip found in Assets/Audio. Put a .wav/.ogg/.mp3 there and rebuild.";
            EditorUtility.DisplayDialog("Pit Tycoon", "Greybox loop scene built at:\n" + ScenePath + "\n\n" + next, "OK");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }
            return asset;
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
