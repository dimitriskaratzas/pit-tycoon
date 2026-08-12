using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// M2a: applies the mature graphic-novel comic look to the open Greybox scene.
    /// Generates the lighting ramp, materials, and ComicLook Volume profile, then wires
    /// the scene (ground/crowd materials, global Volume, accent lights, sky color).
    /// Render features are attached manually per SETUP.md (one-time, reliable by hand).
    /// Menu: "Pit Tycoon/Apply Comic Look (M2a)".
    /// </summary>
    public static class ComicLookSetup
    {
        private const string RampPath = "Assets/PitTycoon/Art/Ramps/ComicRamp.png";
        private const string MatDir = "Assets/PitTycoon/Art/Materials";
        private const string VolumePath = "Assets/Settings/ComicLook.asset";

        // Palette (mature graphic-novel, color-pop variant).
        private static readonly Color PitFloor = new Color(0.29f, 0.25f, 0.22f);
        private static readonly Color Crowd = new Color(0.16f, 0.13f, 0.18f);
        private static readonly Color Sky = new Color(0.24f, 0.23f, 0.32f);
        private static readonly Color Amber = new Color(1.00f, 0.69f, 0.24f);
        private static readonly Color Magenta = new Color(0.88f, 0.27f, 0.49f);
        private static readonly Color Cyan = new Color(0.21f, 0.79f, 0.88f);
        private static readonly Color DuskHorizon = new Color(0.98f, 0.55f, 0.28f);
        private static readonly Color DuskZenith = new Color(0.24f, 0.26f, 0.48f);
        private static readonly Vector3 PitCenter = new Vector3(0f, 0.5f, 0f);

        [MenuItem("Pit Tycoon/Apply Comic Look (M2a)")]
        public static void ApplyComicLook()
        {
            EnsureFolder("Assets/PitTycoon/Art/Ramps");
            EnsureFolder(MatDir);
            EnsureFolder("Assets/Settings");

            var ramp = CreateRamp(RampPath, 3);

            var litShader = Shader.Find("PitTycoon/ComicLit");
            var halftoneShader = Shader.Find("PitTycoon/Halftone");
            var outlineShader = Shader.Find("PitTycoon/Outline");
            var skyShader = Shader.Find("PitTycoon/ComicSky");
            if (litShader == null || halftoneShader == null || outlineShader == null)
            {
                EditorUtility.DisplayDialog("Pit Tycoon",
                    "Shaders not found. Ensure ComicLit/Halftone/Outline are imported (no compile errors) before applying.", "OK");
                return;
            }

            var groundMat = CreateMaterial($"{MatDir}/GroundMat.mat", litShader, m => { m.SetColor("_BaseColor", PitFloor); m.SetTexture("_RampTex", ramp); });
            var crowdMat = CreateMaterial($"{MatDir}/CrowdMat.mat", litShader, m => { m.SetColor("_BaseColor", Crowd); m.SetTexture("_RampTex", ramp); });
            CreateMaterial($"{MatDir}/HalftoneMat.mat", halftoneShader, null);
            CreateMaterial($"{MatDir}/OutlineMat.mat", outlineShader, m =>
            {
                m.SetFloat("_FadeStart", 30f);
                m.SetFloat("_FadeEnd", 70f);
            });

            Material skyMat = null;
            if (skyShader != null)
            {
                skyMat = CreateMaterial($"{MatDir}/ComicSky.mat", skyShader, m =>
                {
                    m.SetColor("_HorizonColor", DuskHorizon);
                    m.SetColor("_ZenithColor", DuskZenith);
                    m.SetFloat("_StarStrength", 0f);
                });
            }
            else Debug.LogWarning("ComicLookSetup: PitTycoon/ComicSky not found — sky left as camera clear color.");

            CreateVolumeProfile(VolumePath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);

            // ---- Scene wiring (operates on the currently open scene) ----
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                var mr = ground.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = groundMat;
            }

            var crowd = Object.FindAnyObjectByType<CrowdController>();
            if (crowd != null)
            {
                var so = new SerializedObject(crowd);
                var prop = so.FindProperty("memberMaterial");
                if (prop != null) { prop.objectReferenceValue = crowdMat; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(crowd); }
            }

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = skyMat != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cam.backgroundColor = Sky;
            }
            if (skyMat != null) RenderSettings.skybox = skyMat;

            // Flat ambient so one color drives it deterministically. Under the default Skybox
            // ambient mode the value is derived from the custom sky's spherical harmonics and
            // would need a DynamicGI.UpdateEnvironment() every time the sky changes.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.28f, 0.34f);

            // Fog: on and exponential-squared. AtmosphereController drives color and density
            // per set; these are the dusk (set 1) values so the scene reads right without it.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.42f, 0.34f, 0.36f);
            RenderSettings.fogDensity = 0.010f;

            // The sun is now a cool fill, not a daylight key. AtmosphereController animates
            // these per set; these are the dusk values.
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo != null)
            {
                var sun = sunGo.GetComponent<Light>();
                if (sun != null)
                {
                    sun.color = new Color(1f, 0.78f, 0.52f);
                    sun.intensity = 0.9f;
                }
            }
            else Debug.LogWarning("ComicLookSetup: 'Directional Light' not found — sun left unchanged.");

            EnsureGlobalVolume(profile);
            EnsureAccentLight("Accent Amber", Amber, new Vector3(-5f, 6f, 1f));
            EnsureAccentLight("Accent Magenta", Magenta, new Vector3(5f, 6f, 1f));
            EnsureAccentLight("Accent Cyan", Cyan, new Vector3(0f, 7f, -4f));

            var active = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Pit Tycoon",
                "Comic look applied to the open scene.\n\n" +
                "ONE manual step remains (see SETUP.md M2a): add the Halftone and Outline\n" +
                "Full Screen Pass features to Assets/Settings/PC_Renderer.asset, then press Play.", "OK");
        }

        private static Texture2D CreateRamp(string path, int steps)
        {
            const int w = 256;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false, true);
            for (int x = 0; x < w; x++)
            {
                float t = (float)x / (w - 1);
                int s = Mathf.Clamp(Mathf.FloorToInt(t * steps), 0, steps - 1);
                float b = Mathf.Lerp(0.28f, 1f, steps == 1 ? 1f : s / (float)(steps - 1));
                tex.SetPixel(x, 0, new Color(b, b, b, 1f));
            }
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.filterMode = FilterMode.Point;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateMaterial(string path, Shader shader, System.Action<Material> configure)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else { mat.shader = shader; }
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreateVolumeProfile(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f); bloom.threshold.Override(0.9f); bloom.scatter.Override(0.6f);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(22f); ca.contrast.Override(14f);

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.Neutral);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.28f); vig.smoothness.Override(0.4f);

            EditorUtility.SetDirty(profile);
        }

        private static void EnsureGlobalVolume(VolumeProfile profile)
        {
            var go = GameObject.Find("Global Volume");
            if (go == null) go = new GameObject("Global Volume");
            var vol = go.GetComponent<Volume>(); if (vol == null) vol = go.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 1f; vol.profile = profile;
        }

        /// <summary>Rig light: a spot aimed down at the pit, so the beam cones (M5e) have a
        /// direction to follow and the crowd gets pools of colour instead of flat point fill.</summary>
        private static void EnsureAccentLight(string name, Color color, Vector3 pos)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation((PitCenter - pos).normalized, Vector3.up);
            var light = go.GetComponent<Light>(); if (light == null) light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = 8f;
            light.range = 34f;
            light.spotAngle = 46f;
            light.innerSpotAngle = 18f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
