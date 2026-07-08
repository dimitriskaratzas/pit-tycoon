# M5a Rigged Crowd Figures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the identical bobbing crowd clones with 4 rigged body variants animated by a hype-blended 3-clip Mecanim blend tree, with per-member outfit tints.

**Architecture:** One shared Blender armature drives 4 skinned body FBXs each carrying 3 loopable clips (Sway/Groove/HypeJump). A new `CrowdPrefabs` editor builder creates one shared `AnimatorController` (1D blend tree on float `Energy`) and wraps the FBXs into prefabs. `CrowdController` swaps its single `memberPrefab` for a variant array, drops the procedural sine bob (clips own body motion), writes analyzer intensity into each member's Animator, and tints outfits via `MaterialPropertyBlock`. Beat pops (transform scale/offset) remain and stack on top.

**Tech Stack:** Unity 6 URP, Mecanim (AnimatorController + BlendTree via `UnityEditor.Animations`), Blender (via MCP) for rig/bodies/actions, ComicLit cel shader.

## Global Constraints

- **No Domain changes.** `dotnet test PitTycoon.Domain.slnx` stays at **98 passing** after every task.
- Crowd reacts only through `IAudioAnalyzer` (`Intensity01`, `BeatDetected`) — never `AudioSource`.
- Blender working file: `ArtSource/crowd-figures.blend` (repo root — **NEVER** inside `Assets/`, Unity would import it).
- FBX exports: `Assets/PitTycoon/Art/Models/Crowd/<Name>.fbx`; prefabs: `Assets/PitTycoon/Art/Prefabs/Crowd/<Name>.prefab`; controller: `Assets/Settings/CrowdAnimator.controller`.
- Animator parameter is exactly **`Energy`** (float 0..1). Body variant names exactly: **CrowdBase, CrowdChunky, CrowdLanky, CrowdShort**. Clip names contain exactly **Sway, Groove, HypeJump** (Blender exports may prefix `Armature|`).
- Everything degrades gracefully: missing FBXs → builder skips with a log; empty `memberPrefabs` → capsule fallback; member without Animator → skipped, no exception.
- Commits: imperative present tense, **no Co-Authored-By trailer**.

## File Structure

- `Assets/PitTycoon/Unity/CrowdController.cs` (modify) — variant array, Energy writes, tints, bob removal.
- `Assets/PitTycoon/Unity/Editor/CrowdPrefabs.cs` (create) — AnimatorController + blend tree + prefab builder.
- `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs` (modify) — wire variants + palette into CrowdController.
- `SETUP.md` (modify) — M5a section.
- `ArtSource/crowd-figures.blend`, 4 FBXs (created in the Blender tasks — controller-led, not subagent).

---

### Task 1: CrowdController — variant array, Energy, tints, bob removal

**Files:**
- Modify: `Assets/PitTycoon/Unity/CrowdController.cs`

**Interfaces:**
- Consumes: existing `IAudioAnalyzer.Intensity01` (float 0..1, already read in `Update`).
- Produces: serialized fields `memberPrefabs` (GameObject[]) and `outfitPalette` (Color[]) — Task 3 wires them by these exact names via `SerializedObject.FindProperty`.

This is a Unity-only change; there are no unit tests for MonoBehaviours in this project. The test cycle is: Domain suite stays green + the code compiles in Unity at the Task 7 checkpoint.

- [ ] **Step 1: Replace the serialized fields**

In the field block at the top of the class, **delete** these three lines:

```csharp
[SerializeField] private float bounceHeight = 0.6f;
[SerializeField] private float bobSpeed = 7f;
[SerializeField] private GameObject memberPrefab;
```

and add in their place:

```csharp
[Tooltip("Rigged body-variant prefabs (M5a). Each member picks one at random. Empty = capsule fallback.")]
[SerializeField] private GameObject[] memberPrefabs;
[Tooltip("Outfit tints assigned per member at random (MaterialPropertyBlock on _BaseColor).")]
[SerializeField] private Color[] outfitPalette =
{
    new Color(0.86f, 0.32f, 0.25f),   // red jacket
    new Color(0.20f, 0.45f, 0.70f),   // blue denim
    new Color(0.95f, 0.75f, 0.20f),   // yellow hoodie
    new Color(0.35f, 0.65f, 0.35f),   // green tee
    new Color(0.55f, 0.35f, 0.60f),   // purple flannel
    new Color(0.16f, 0.13f, 0.18f),   // black metal shirt
};
```

- [ ] **Step 2: Add animator state + helpers**

Next to the other private state (`_members`, `_fullScale`, ...), add:

```csharp
private Animator[] _animators;    // per-member; null where the prefab has no Animator
private static readonly int EnergyParam = Animator.StringToHash("Energy");
private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
```

Below `ClearPreview()`, add two private helpers:

```csharp
/// <summary>Instantiate a random body variant, or the capsule fallback when none are wired.</summary>
private GameObject SpawnMember()
{
    if (memberPrefabs != null && memberPrefabs.Length > 0)
    {
        var prefab = memberPrefabs[Random.Range(0, memberPrefabs.Length)];
        if (prefab != null) return Instantiate(prefab);
    }
    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    if (memberMaterial != null)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = memberMaterial;
    }
    return go;
}

/// <summary>Random outfit tint + desynced animator start (offset + speed jitter).</summary>
private Animator StyleMember(GameObject go)
{
    if (outfitPalette != null && outfitPalette.Length > 0)
    {
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(BaseColorProp, outfitPalette[Random.Range(0, outfitPalette.Length)]);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mpb);
    }
    var anim = go.GetComponentInChildren<Animator>();
    if (anim != null)
    {
        anim.cullingMode = AnimatorCullingMode.CullCompletely;
        anim.speed = Random.Range(0.9f, 1.1f);
        anim.Play(0, 0, Random.value);   // random normalized start time: no lockstep
    }
    return anim;
}
```

- [ ] **Step 3: Rework `Build()` to use the helpers**

In `Build()`, after `_curScale = new float[n];` add `_animators = new Animator[n];`. Then replace the member-creation block

```csharp
GameObject go;
if (memberPrefab != null)
{
    go = Instantiate(memberPrefab);
}
else
{
    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    if (memberMaterial != null)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = memberMaterial;
    }
}
```

with:

```csharp
GameObject go = SpawnMember();
_animators[i] = StyleMember(go);
```

- [ ] **Step 4: Rework `PreviewCapacity()` ghosts**

Ghosts are static previews — they must not animate. Replace

```csharp
GameObject go = memberPrefab != null
    ? Instantiate(memberPrefab)
    : GameObject.CreatePrimitive(PrimitiveType.Capsule);
```

with:

```csharp
GameObject go = SpawnMember();
var ghostAnim = go.GetComponentInChildren<Animator>();
if (ghostAnim != null) Destroy(ghostAnim);
```

- [ ] **Step 5: Rework `Update()` — bob out, Energy in**

Replace the per-member loop body's motion section

```csharp
bool visible = _curScale[i] > 0.05f;
float bob = visible ? Mathf.Abs(Mathf.Sin(t * bobSpeed + i * 0.6f)) * bounceHeight * intensity : 0f;
Vector3 p = tr.localPosition;
p.y = bob + (visible ? _pop : 0f);
tr.localPosition = p;
```

with:

```csharp
bool visible = _curScale[i] > 0.05f;
Vector3 p = tr.localPosition;
p.y = visible ? _pop : 0f;              // clips own body motion now; only beat pops lift the root
tr.localPosition = p;

var anim = _animators != null && i < _animators.Length ? _animators[i] : null;
if (anim != null && visible) anim.SetFloat(EnergyParam, intensity);
```

Then delete the now-unused `float t = Time.time;` line above the loop (the compiler will flag it as unused otherwise). `intensity` stays — it now feeds `Energy`. Update the class XML doc comment's motion sentence to say members are animated by a hype-blended Animator (Energy = analyzer intensity) with beat pops on the transform.

- [ ] **Step 6: Verify Domain suite untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: **98 passed** (this task must not touch anything under `Assets/PitTycoon/Domain/`).

- [ ] **Step 7: Commit**

```bash
git add Assets/PitTycoon/Unity/CrowdController.cs
git commit -m "feat(crowd): body-variant prefabs, Animator Energy drive, outfit tints; drop sine bob"
```

---

### Task 2: CrowdPrefabs editor builder (AnimatorController + prefabs)

**Files:**
- Create: `Assets/PitTycoon/Unity/Editor/CrowdPrefabs.cs`

**Interfaces:**
- Consumes: FBXs at `Assets/PitTycoon/Art/Models/Crowd/{CrowdBase,CrowdChunky,CrowdLanky,CrowdShort}.fbx` (may not exist yet — skip missing with a warning, return only what exists).
- Produces: `public static GameObject[] EnsureCrowdVariants()` — Task 3 calls exactly this. Also creates `Assets/Settings/CrowdAnimator.controller` with float param `Energy` and a 1D blend tree (Sway@0, Groove@0.5, HypeJump@1).

Editor-assembly code (`PitTycoon.Unity.Editor.asmdef` compiles this folder); mirror the `StructurePrefabs` idempotent load-or-build style.

- [ ] **Step 1: Write the builder**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds the rigged crowd-member prefabs (M5a) from the Blender FBX exports in
    /// Art/Models/Crowd. Configures each FBX import (generic rig, looping clips), creates the
    /// shared CrowdAnimator controller (1D blend tree: Sway -> Groove -> HypeJump on 'Energy'),
    /// and wraps each body FBX in a prefab with the ComicLit crowd material and the controller.
    /// Missing FBXs are skipped with a warning so wiring works mid-modelling.
    /// Idempotent: re-running rebuilds prefabs + controller from the current FBXs.
    /// </summary>
    public static class CrowdPrefabs
    {
        private const string ModelDir = "Assets/PitTycoon/Art/Models/Crowd";
        private const string PrefabDir = "Assets/PitTycoon/Art/Prefabs/Crowd";
        private const string ControllerPath = "Assets/Settings/CrowdAnimator.controller";
        private const string CrowdMatPath = "Assets/PitTycoon/Art/Materials/CrowdMat.mat";

        private static readonly string[] Variants = { "CrowdBase", "CrowdChunky", "CrowdLanky", "CrowdShort" };
        private static readonly string[] ClipNames = { "Sway", "Groove", "HypeJump" };
        private static readonly float[] ClipThresholds = { 0f, 0.5f, 1f };

        /// <summary>Build (or rebuild) all available crowd variant prefabs. Missing FBXs are skipped.</summary>
        public static GameObject[] EnsureCrowdVariants()
        {
            var result = new List<GameObject>();
            AnimatorController controller = null;

            foreach (var name in Variants)
            {
                string fbxPath = $"{ModelDir}/{name}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null)
                {
                    Debug.LogWarning($"CrowdPrefabs: {fbxPath} not found - skipped (export it, then re-run).");
                    continue;
                }

                ConfigureImport(fbxPath);
                // Controller needs clips; build it from the first FBX that exists (all carry the same 3).
                if (controller == null) controller = EnsureController(fbxPath);
                if (controller == null) { Debug.LogError("CrowdPrefabs: no controller - aborting."); break; }

                result.Add(BuildPrefab(fbx, name, controller));
            }

            AssetDatabase.SaveAssets();
            return result.ToArray();
        }

        /// <summary>Generic rig + loop time on the three clips. Reimports only when settings change.</summary>
        private static void ConfigureImport(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            bool dirty = false;

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                dirty = true;
            }

            var clips = importer.defaultClipAnimations;   // what the FBX actually contains
            var wanted = new List<ModelImporterClipAnimation>();
            foreach (var clip in clips)
            {
                if (!ClipNames.Any(n => clip.name.Contains(n))) continue;
                clip.loopTime = true;
                wanted.Add(clip);
            }
            // Apply when the importer has no explicit clip list yet, or loop flags drifted.
            var current = importer.clipAnimations;
            if (wanted.Count > 0 && (current.Length != wanted.Count || current.Any(c => !c.loopTime)))
            {
                importer.clipAnimations = wanted.ToArray();
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        /// <summary>Create (or rebuild) the shared controller: one state holding a 1D blend tree on 'Energy'.</summary>
        private static AnimatorController EnsureController(string fbxPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();

            var motions = new AnimationClip[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++)
            {
                motions[i] = clips.FirstOrDefault(c => c.name.Contains(ClipNames[i]));
                if (motions[i] == null)
                {
                    Debug.LogError($"CrowdPrefabs: clip containing '{ClipNames[i]}' not found in {fbxPath}. " +
                                   "Check the Blender action names.");
                    return null;
                }
            }

            AssetDatabase.DeleteAsset(ControllerPath);   // rebuild clean; scene refs are by builder, not GUID-fragile here
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Energy", AnimatorControllerParameterType.Float);

            var tree = new BlendTree
            {
                name = "DanceBlend",
                blendParameter = "Energy",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            for (int i = 0; i < motions.Length; i++) tree.AddChild(motions[i], ClipThresholds[i]);

            var state = controller.layers[0].stateMachine.AddState("Dance");
            state.motion = tree;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject BuildPrefab(GameObject fbx, string name, AnimatorController controller)
        {
            EnsureDir(PrefabDir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CrowdMatPath);
            if (mat == null)
                Debug.LogWarning($"CrowdPrefabs: {CrowdMatPath} not found - run Build Festival Scene first; " +
                                 "prefab keeps imported materials.");

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (mat != null)
                foreach (var r in temp.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }

            var anim = temp.GetComponent<Animator>();
            if (anim == null) anim = temp.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(dir));
        }
    }
}
```

- [ ] **Step 2: Verify Domain suite untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: **98 passed**.

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/CrowdPrefabs.cs
git commit -m "feat(editor): CrowdPrefabs builder - CrowdAnimator blend tree + rigged member prefabs"
```

---

### Task 3: FestivalSceneSetup wires variants + palette

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs`

**Interfaces:**
- Consumes: `CrowdPrefabs.EnsureCrowdVariants()` (Task 2) returning `GameObject[]`; `CrowdController` fields `memberPrefabs` / `outfitPalette` (Task 1).
- Produces: nothing new for later tasks.

- [ ] **Step 1: Replace the single-prefab wiring**

In `Build()`, the current block wires one prefab:

```csharp
// Crowd-figure prefab (figure mesh + CrowdMat), wired into CrowdController.
var figurePrefab = BuildFigurePrefab(crowdMat);
var crowd = Object.FindAnyObjectByType<CrowdController>();
if (crowd != null && figurePrefab != null)
{
    var so = new SerializedObject(crowd);
    var prop = so.FindProperty("memberPrefab");
    if (prop != null) { prop.objectReferenceValue = figurePrefab; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(crowd); }
}
```

Replace it with (keep `BuildFigurePrefab` — it is the fallback when no rigged FBX is exported yet):

```csharp
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
```

(The outfit palette ships as the field's default initializer in Task 1; the builder does not overwrite it, so Inspector tuning survives re-runs.)

- [ ] **Step 2: Verify Domain suite untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: **98 passed**.

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs
git commit -m "feat(editor): wire rigged crowd variant prefabs into CrowdController"
```

---

### Task 4: Blender — armature, CrowdBase body, 3 actions, pipeline check (controller/Fable — NOT a subagent task)

Done live via the Blender MCP by the session controller. Summary of the locked spec values:

- New file `ArtSource/crowd-figures.blend`.
- Armature `CrowdRig`, ~10 bones: hips, spine, head, upperarm.L/R, forearm.L/R, leg.L/R (+feet optional). No IK in export, no fingers.
- Body `CrowdBase`: chibi comic proportions matching the current `CrowdFigure.fbx` scale (~1.5u tall in-scene), ~1–2k tris, single material slot named `Crowd`.
- Actions `Sway` (weight shift, arms low), `Groove` (knee bounce, arm movement), `HypeJump` (jump, arms up) — all loop cleanly (first key == last key), ~1–2s each.
- Export `CrowdBase.fbx` to `Assets/PitTycoon/Art/Models/Crowd/` with the M2b/M4d axis + scale conventions, baked animation on, NLA/all-actions on so all 3 clips land in the FBX.
- **Pipeline check before Task 5:** in Unity run `Pit Tycoon → Build Festival Scene`, press Play — CrowdBase members dance, blend follows hype, cel shading + outline read correctly on the skinned mesh. Commit the FBX + generated assets at this checkpoint.

- [ ] Rig + body + 3 actions built and exported
- [ ] Editor pipeline check passed (import scale, clip names, loop, blend, shading)
- [ ] Commit: `git add Assets/PitTycoon/Art/Models/Crowd ArtSource && git commit -m "feat(art): rigged CrowdBase figure with Sway/Groove/HypeJump actions"`

---

### Task 5: Blender — remaining 3 bodies, batch export (controller/Fable — NOT a subagent task)

- Duplicate `CrowdBase`, reshape to `CrowdChunky` (wide/heavy), `CrowdLanky` (tall/thin), `CrowdShort` (small/round); all stay skinned to the same `CrowdRig` (re-use weights, adjust where reshaping distorts them).
- Export each to `Assets/PitTycoon/Art/Models/Crowd/<Name>.fbx` with the same settings as Task 4.
- Re-run `Pit Tycoon → Build Festival Scene`; all 4 variants appear mixed through the pit.

- [ ] 3 bodies reshaped + exported
- [ ] Editor look-check passed (variants mixed, silhouettes distinct at survey distance)
- [ ] Commit: `git add Assets/PitTycoon/Art/Models/Crowd ArtSource && git commit -m "feat(art): chunky/lanky/short crowd body variants"`

---

### Task 6: SETUP.md M5a section

**Files:**
- Modify: `SETUP.md` (append a new `## M5a — Rigged crowd figures` section after the M4d section)

**Interfaces:** none — documentation of Tasks 1–5.

- [ ] **Step 1: Append the section**

```markdown
## M5a — Rigged crowd figures

The pit is now 4 rigged body variants (base/chunky/lanky/short) skinned to one armature,
dancing a 3-clip blend (Sway → Groove → HypeJump) driven by music intensity, with random
outfit tints per member. Source: `ArtSource/crowd-figures.blend`.

**Build steps (after pulling FBXs):**
1. Let Unity import `Assets/PitTycoon/Art/Models/Crowd/` (4 FBXs).
2. Run `Pit Tycoon → Build Festival Scene` — this now also builds
   `Assets/Settings/CrowdAnimator.controller`, the 4 prefabs under
   `Assets/PitTycoon/Art/Prefabs/Crowd/`, and fills CrowdController's *Member Prefabs*.
3. Press Play.

**Verify:**
- Low hype: crowd sways in place. Mid: grooves. High: jumps with arms up.
- No two neighbours move in lockstep (random clip offset/speed).
- Outfit colors vary; beat pops still lift the crowd on kicks.
- Capacity upgrades still add rows; ghost previews are static (no dancing ghosts).

**Tuning knobs:**
- Outfit palette: CrowdController *Outfit Palette* (Inspector, live).
- Blend feel: `Assets/Settings/CrowdAnimator.controller` blend tree thresholds/clip speeds.
- Desync: `StyleMember`'s speed jitter range in `CrowdController.cs`.
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP M5a rigged crowd figures section"
```

---

### Task 7: Manual Unity checkpoint (user)

Not a subagent task. The user runs the SETUP.md M5a steps and verifies the checklist (blend states, no lockstep, tints, beat pops, ghost previews static, frame rate at max capacity, M1–M4 regression). Commit any Unity-generated `.meta`/import changes as a `chore(unity): checkpoint` commit, then finish the branch (PR to master).

- [ ] User checklist passed
- [ ] Checkpoint commit + PR
