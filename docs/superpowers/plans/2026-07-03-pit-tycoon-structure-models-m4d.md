# Pit Tycoon — Real Structure Models (M4d) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Split execution model:** Tasks 1–3 and 8 are code tasks — dispatch to subagents as usual. Tasks 4–7 are **Blender modelling tasks executed live by the controlling session via the Blender MCP** (they cannot be dispatched: subagents have no MCP access and modelling needs art judgment). Task 9 is the user's manual Editor checkpoint.

**Goal:** Replace the 11 greybox build-spot structures with real Blender-modelled, cel-shaded, mid-detail 3D models — kit-based, palette-material'd, with idle motion on the Sponsor Banner and Strobe Rig.

**Architecture:** A shared 9-part kit is modelled in one Blender file and assembled into 11 structures, exported per-structure as FBX. A new `StructurePrefabs` editor builder wraps each FBX in a prefab with shared ComicLit palette materials assigned by material-slot name, and falls back to the greybox prefab while an FBX doesn't exist yet — so the ground builder works at every stage of the modelling batches. `FestivalGroundSetup` switches its 11 `Ensure*` calls to the new builder. One new MonoBehaviour (`IdleMotion`) animates a child transform on two structures.

**Tech Stack:** Unity 6 URP (editor scripts, prefabs), Blender via MCP (`mcp__Blender__execute_blender_code` etc.), ComicLit cel shader from M2a. No Domain changes; `dotnet test` stays at 96 by construction.

## Global Constraints

- **No Domain changes.** Nothing under `Assets/PitTycoon/Domain/` or `tests/` is touched.
- **No gameplay-code changes** beyond the new `IdleMotion` MonoBehaviour and the `FestivalGroundSetup` prefab-source swap. `BuildSystem`, `BuildSpotController`, `ShopView` are untouched.
- **Footprints:** each model keeps roughly its greybox bounding box (per-structure sizes given in Task 4–7 tables) so `OpenAirLayout.asset` poses and camera fly-tos stay valid.
- **Palette slot names are exact:** Blender materials must be named `Wood`, `MetalDark`, `MetalLight`, `CanvasA`, `CanvasB`, `AccentWarm`, `StrobeGlow` — the prefab builder maps by name.
- **Exact child names for motion:** Sponsor Banner's cloth object is named `BannerCloth`; Strobe Rig's head assembly parent is named `Heads`. The builder targets them by name.
- **Paths:** FBX → `Assets/PitTycoon/Art/Models/Structures/<Name>.fbx`; prefabs → `Assets/PitTycoon/Art/Prefabs/Structures/<Name>.prefab`; palette → `Assets/PitTycoon/Art/Materials/Palette/`; Blender source → `Assets/PitTycoon/Art/Source/festival-structures.blend` (`.blend` files are ignored by Unity import but versioned in git).
- **Structure names (= FBX names = prefab names = Blender collection names):** `SecondStage`, `CampingField`, `EntranceGate`, `Grandstand`, `FoodCourt`, `Bar`, `VipLounge`, `SponsorBanner`, `AmpStack`, `StrobeRig`, `SpeakerWall`.
- Work on branch `feat/structure-models-m4d`.
- Commit messages: imperative present tense, no Co-Authored-By trailer.

---

### Task 1: `IdleMotion` MonoBehaviour

**Files:**
- Create: `Assets/PitTycoon/Unity/IdleMotion.cs`

**Interfaces:**
- Consumes: nothing (self-contained).
- Produces: `PitTycoon.Unity.IdleMotion` with serialized fields `target` (Transform), `rotationAxis` (Vector3), `rotationAmplitude` (float, deg), `bobAmplitude` (float), `speed` (float, osc/sec), `phase` (float, sec). Task 2 sets these via `SerializedObject` using those exact field names.

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>Cosmetic idle motion for structure prefabs (M4d): gently sways/pans a child
    /// transform (banner cloth, strobe heads) with a sine oscillation. Purely visual —
    /// no EventBus, no Domain, safe to leave running during live sets.</summary>
    public sealed class IdleMotion : MonoBehaviour
    {
        [SerializeField, Tooltip("Transform to animate; defaults to this transform.")]
        private Transform target;
        [SerializeField, Tooltip("Local rotation axis of the sway/pan.")]
        private Vector3 rotationAxis = Vector3.up;
        [SerializeField, Tooltip("Peak rotation in degrees (0 disables rotation).")]
        private float rotationAmplitude = 10f;
        [SerializeField, Tooltip("Peak vertical bob in local units (0 disables bobbing).")]
        private float bobAmplitude = 0f;
        [SerializeField, Tooltip("Oscillations per second.")]
        private float speed = 0.25f;
        [SerializeField, Tooltip("Phase offset in seconds so neighbours don't sync.")]
        private float phase = 0f;

        private Quaternion _baseRotation;
        private Vector3 _basePosition;

        private void Awake()
        {
            if (target == null) target = transform;
            _baseRotation = target.localRotation;
            _basePosition = target.localPosition;
        }

        private void Update()
        {
            float s = Mathf.Sin((Time.time + phase) * speed * Mathf.PI * 2f);
            if (rotationAmplitude != 0f)
                target.localRotation = _baseRotation * Quaternion.AngleAxis(s * rotationAmplitude, rotationAxis);
            if (bobAmplitude != 0f)
                target.localPosition = _basePosition + Vector3.up * (s * bobAmplitude);
        }
    }
}
```

- [ ] **Step 2: Verify Domain tests unaffected**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 96` (this task adds no Domain code; the run guards against accidental cross-assembly edits).

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/IdleMotion.cs
git commit -m "feat(unity): IdleMotion component for structure sway/pan (M4d)"
```

---

### Task 2: `StructurePrefabs` editor builder + palette materials

**Files:**
- Create: `Assets/PitTycoon/Unity/Editor/StructurePrefabs.cs`

**Interfaces:**
- Consumes: `StructureGreyboxPrefabs.Ensure*()` (existing, same assembly) as fallbacks; `IdleMotion` from Task 1 (field names `target`, `rotationAxis`, `rotationAmplitude`, `speed`); ComicLit shader `PitTycoon/ComicLit` with `_BaseColor` + `_RampTex`; ramp at `Assets/PitTycoon/Art/Ramps/ComicRamp.png`.
- Produces: `StructurePrefabs.EnsureSecondStage()` … `EnsureSpeakerWall()` (11 methods, `GameObject` returns) — Task 3 calls them with these exact names.

- [ ] **Step 1: Write the builder**

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds real-model structure prefabs (M4d) from the Blender FBX exports in
    /// Art/Models/Structures. Wraps each FBX in a prefab under Art/Prefabs/Structures,
    /// assigns the shared ComicLit palette materials by material-slot name, and adds
    /// IdleMotion to the Sponsor Banner (cloth sway) and Strobe Rig (head pan).
    /// Falls back to the greybox prefab while a structure's FBX does not exist yet, so
    /// Build Festival Ground works at any point during the modelling batches.
    /// Idempotent: re-running rebuilds prefabs from the current FBX + palette.
    /// </summary>
    public static class StructurePrefabs
    {
        private const string ModelDir = "Assets/PitTycoon/Art/Models/Structures";
        private const string PrefabDir = "Assets/PitTycoon/Art/Prefabs/Structures";
        private const string PaletteDir = "Assets/PitTycoon/Art/Materials/Palette";
        private const string RampPath = "Assets/PitTycoon/Art/Ramps/ComicRamp.png";

        // Slot name -> base color. Blender material names must match exactly.
        private static readonly (string name, Color color)[] PaletteSpec =
        {
            ("Wood",       new Color(0.55f, 0.36f, 0.20f)),
            ("MetalDark",  new Color(0.16f, 0.17f, 0.20f)),
            ("MetalLight", new Color(0.62f, 0.66f, 0.72f)),
            ("CanvasA",    new Color(0.86f, 0.32f, 0.25f)),
            ("CanvasB",    new Color(0.20f, 0.45f, 0.70f)),
            ("AccentWarm", new Color(0.95f, 0.75f, 0.20f)),
            ("StrobeGlow", new Color(1.00f, 0.95f, 0.70f)),
        };

        public static GameObject EnsureSecondStage()   => Ensure("SecondStage",   StructureGreyboxPrefabs.EnsureSecondStage);
        public static GameObject EnsureCampingField()  => Ensure("CampingField",  StructureGreyboxPrefabs.EnsureCampingField);
        public static GameObject EnsureEntranceGate()  => Ensure("EntranceGate",  StructureGreyboxPrefabs.EnsureEntranceGate);
        public static GameObject EnsureGrandstand()    => Ensure("Grandstand",    StructureGreyboxPrefabs.EnsureGrandstand);
        public static GameObject EnsureFoodCourt()     => Ensure("FoodCourt",     StructureGreyboxPrefabs.EnsureFoodCourt);
        public static GameObject EnsureBar()           => Ensure("Bar",           StructureGreyboxPrefabs.EnsureBar);
        public static GameObject EnsureVipLounge()     => Ensure("VipLounge",     StructureGreyboxPrefabs.EnsureVipLounge);
        public static GameObject EnsureSponsorBanner() => Ensure("SponsorBanner", StructureGreyboxPrefabs.EnsureSponsorBanner);
        public static GameObject EnsureAmpStack()      => Ensure("AmpStack",      StructureGreyboxPrefabs.EnsureAmpStack);
        public static GameObject EnsureStrobeRig()     => Ensure("StrobeRig",     StructureGreyboxPrefabs.EnsureStrobeRig);
        public static GameObject EnsureSpeakerWall()   => Ensure("SpeakerWall",   StructureGreyboxPrefabs.EnsureSpeakerWall);

        private static GameObject Ensure(string name, System.Func<GameObject> greyboxFallback)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            if (fbx == null) return greyboxFallback();

            var palette = EnsurePalette();
            EnsureDir(PrefabDir);

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            foreach (var r in temp.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (palette.TryGetValue(mats[i].name, out var mapped)) mats[i] = mapped;
                    else Debug.LogWarning($"StructurePrefabs: {name}/{r.name} slot '{mats[i].name}' " +
                                          "is not a palette name — left as imported.");
                }
                r.sharedMaterials = mats;
            }

            AddIdleMotion(temp, name);

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static void AddIdleMotion(GameObject root, string name)
        {
            if (name == "SponsorBanner")
                ConfigureIdle(root, "BannerCloth", new Vector3(0f, 0f, 1f), rotationAmplitude: 4f, speed: 0.35f);
            else if (name == "StrobeRig")
                ConfigureIdle(root, "Heads", new Vector3(0f, 1f, 0f), rotationAmplitude: 25f, speed: 0.15f);
        }

        private static void ConfigureIdle(GameObject root, string childName, Vector3 axis,
                                          float rotationAmplitude, float speed)
        {
            var child = FindDeep(root.transform, childName);
            if (child == null)
            {
                Debug.LogWarning($"StructurePrefabs: '{childName}' not found under {root.name} — no idle motion added.");
                return;
            }
            var idle = root.GetComponent<IdleMotion>();
            if (idle == null) idle = root.AddComponent<IdleMotion>();
            var so = new SerializedObject(idle);
            so.FindProperty("target").objectReferenceValue = child;
            so.FindProperty("rotationAxis").vector3Value = axis;
            so.FindProperty("rotationAmplitude").floatValue = rotationAmplitude;
            so.FindProperty("speed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private static Dictionary<string, Material> EnsurePalette()
        {
            EnsureDir(PaletteDir);
            var shader = Shader.Find("PitTycoon/ComicLit");
            var ramp = AssetDatabase.LoadAssetAtPath<Texture2D>(RampPath);
            if (shader == null) Debug.LogError("StructurePrefabs: ComicLit shader missing — run M2a Comic Look first.");
            if (ramp == null) Debug.LogWarning("StructurePrefabs: ComicRamp.png missing — palette materials get no ramp.");

            var dict = new Dictionary<string, Material>();
            foreach (var (name, color) in PaletteSpec)
            {
                string path = $"{PaletteDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null && shader != null)
                {
                    mat = new Material(shader);
                    mat.SetColor("_BaseColor", color);
                    if (ramp != null) mat.SetTexture("_RampTex", ramp);
                    AssetDatabase.CreateAsset(mat, path);
                }
                if (mat != null) dict[name] = mat;
            }
            return dict;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
```

- [ ] **Step 2: Verify Domain tests unaffected**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 96`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/StructurePrefabs.cs
git commit -m "feat(editor): StructurePrefabs builder — FBX wrap, palette materials, idle motion, greybox fallback"
```

---

### Task 3: `FestivalGroundSetup` prefab-source swap

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs:45-56`

**Interfaces:**
- Consumes: `StructurePrefabs.Ensure*()` from Task 2 (exact names listed there).
- Produces: no new interfaces; `Build()` behaviour is unchanged except prefab source.

- [ ] **Step 1: Swap the Ensure calls**

Replace the step-2 block (currently `// 2. Greybox structure prefabs.` and the 11 `StructureGreyboxPrefabs.Ensure*()` calls) with:

```csharp
            // 2. Structure prefabs: real models where an FBX exists (M4d), greybox fallback otherwise.
            var secondStage = StructurePrefabs.EnsureSecondStage();
            var camping = StructurePrefabs.EnsureCampingField();
            var gate = StructurePrefabs.EnsureEntranceGate();
            var grandstand = StructurePrefabs.EnsureGrandstand();
            var foodCourt = StructurePrefabs.EnsureFoodCourt();
            var bar = StructurePrefabs.EnsureBar();
            var vipLounge = StructurePrefabs.EnsureVipLounge();
            var sponsorBanner = StructurePrefabs.EnsureSponsorBanner();
            var ampStack = StructurePrefabs.EnsureAmpStack();
            var strobeRig = StructurePrefabs.EnsureStrobeRig();
            var speakerWall = StructurePrefabs.EnsureSpeakerWall();
```

Also update the class summary comment's first line to mention M4d real models, e.g. append to the `<summary>`: `M4d: structure prefabs come from StructurePrefabs (real models with greybox fallback).`

- [ ] **Step 2: Verify Domain tests unaffected**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 96`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs
git commit -m "feat(editor): festival ground uses StructurePrefabs (real models, greybox fallback)"
```

---

### Task 4: Blender kit — 9 parts *(controller session, Blender MCP)*

**Files:**
- Create (on disk, via Blender save): `Assets/PitTycoon/Art/Source/festival-structures.blend`

**Interfaces:**
- Produces: a `Kit` collection with 9 part objects and the 7 palette materials (exact names from Global Constraints) that Tasks 5–7 assemble from.

- [ ] **Step 1: Clean the default scene** — delete default Cube/Light/Camera; set unit scale metric, 1.0.
- [ ] **Step 2: Create the 7 palette materials** — names exactly `Wood`, `MetalDark`, `MetalLight`, `CanvasA`, `CanvasB`, `AccentWarm`, `StrobeGlow`; viewport base colors matching the palette spec in Task 2 (colors only matter for modelling readability — Unity replaces the materials by name).
- [ ] **Step 3: Model the 9 kit parts** in a `Kit` collection, each at origin, real-world scale (meters = Unity units), chunky bevels (bevel modifier ~0.02–0.04m, 2 segments):

| Part object | Size guide | Materials | Notes |
|---|---|---|---|
| `Kit_SpeakerCabinet` | 1.2×1.0×1.0 | MetalDark + MetalLight corners | recessed grille face (inset + extrude −0.05) |
| `Kit_Pole` | 0.15Ø × 3.0 | MetalLight | 8-sided cylinder + clamp ring |
| `Kit_TrussSegment` | 0.4×0.4×2.0 | MetalLight | 4 rails + diagonal lattice, tileable ends |
| `Kit_Canopy` | 3.5×3.5×0.4 | CanvasA | subdivided plane, slight center sag, rim border |
| `Kit_Counter` | 2.0×1.1×0.7 | Wood | inset front panel |
| `Kit_Tent` | 1.8×1.6×1.8 | CanvasB | pitched profile, door-flap inset |
| `Kit_Railing` | 2.0×1.0×0.1 | MetalLight | 2 posts + 2 rails, tileable |
| `Kit_LightHead` | 0.5×0.6×0.5 | MetalDark + StrobeGlow lens | boxy head + yoke bracket |
| `Kit_Sofa` | 2.0×0.8×1.0 | AccentWarm | rounded seat + back (bevel heavy) |

- [ ] **Step 4: Look-check** — render a viewport screenshot of the kit laid out in a row (`mcp__Blender__render_viewport_to_path` or screenshot tool); verify silhouettes read at a glance; fix anything mushy.
- [ ] **Step 5: Save** the .blend to `Assets/PitTycoon/Art/Source/festival-structures.blend` and commit:

```bash
git add "Assets/PitTycoon/Art/Source/festival-structures.blend"
git commit -m "feat(art): M4d kit — 9 Blender parts + palette materials"
```

---

### Task 5: Batch 1 — audio gear (AmpStack, SpeakerWall, StrobeRig) *(controller session, Blender MCP)*

**Interfaces:**
- Consumes: `Kit` parts + palette materials from Task 4.
- Produces: `Assets/PitTycoon/Art/Models/Structures/{AmpStack,SpeakerWall,StrobeRig}.fbx`.

- [ ] **Step 1: Assemble `AmpStack`** in its own collection: 6 linked-duplicate cabinets, 2×3 grid (matches greybox ~2.6×3.3×1.0 footprint), slight per-cabinet rotation jitter (±2°) for life.
- [ ] **Step 2: Pipeline check (FIRST export — verify before batch-exporting).** Export `AmpStack` collection only → `Assets/PitTycoon/Art/Models/Structures/AmpStack.fbx` (selected objects, apply transforms/scale). In Unity: run `Pit Tycoon → Build Festival Ground`, then in the Project window open `Art/Prefabs/Structures/AmpStack.prefab` — verify: (a) scale matches the greybox footprint (compare against `Greybox/AmpStack.prefab`), (b) palette materials mapped (no gray "imported" materials), (c) no rotation offset. Fix export settings in Blender until clean — **the settings that pass become the settings for every later export.**
- [ ] **Step 3: Assemble `SpeakerWall`**: 12 cabinets 4×3 (greybox ~6.4×4.9×1.0), two flanking poles, simple strap boxes across rows.
- [ ] **Step 4: Assemble `StrobeRig`**: 2 poles + truss crossbar + 4 light heads. **The 4 heads are parented under an empty/object named exactly `Heads`** (Global Constraints — IdleMotion targets it).
- [ ] **Step 5: Export both** to `SpeakerWall.fbx` / `StrobeRig.fbx` with the settings proven in Step 2.
- [ ] **Step 6: User look-check** — user runs Build Festival Ground, buys/inspects the three structures in Play (survey + preview close-up), confirms silhouettes/palette/outline post. Iterate on feedback.
- [ ] **Step 7: Save .blend + commit**

```bash
git add "Assets/PitTycoon/Art/Source/festival-structures.blend" "Assets/PitTycoon/Art/Models/Structures/"
git commit -m "feat(art): M4d batch 1 — amp stack, speaker wall, strobe rig models"
```

---

### Task 6: Batch 2 — hospitality (Bar, FoodCourt, VipLounge) *(controller session, Blender MCP)*

**Interfaces:** as Task 5; produces `{Bar,FoodCourt,VipLounge}.fbx`.

- [ ] **Step 1: Assemble `Bar`** (greybox ~7×3.4×3.5): counter + back shelf with bottle cluster (simple cylinders, AccentWarm/CanvasB), canopy on 2 poles.
- [ ] **Step 2: Assemble `FoodCourt`** (greybox ~10.5×2.5×3.2): 3 stalls = counter + canopy each, canopies alternating CanvasA/CanvasB, small menu-board planes (AccentWarm).
- [ ] **Step 3: Assemble `VipLounge`** (greybox ~6×2.3×5): platform deck (Wood), railing runs, 2 sofas, rope posts (Pole scaled down + AccentWarm rope curve).
- [ ] **Step 4: Export all three** with the proven settings; run Build Festival Ground; quick self-check of scale/materials in the Editor via the user.
- [ ] **Step 5: User look-check** in Play (as Task 5 Step 6).
- [ ] **Step 6: Save .blend + commit**

```bash
git add "Assets/PitTycoon/Art/Source/festival-structures.blend" "Assets/PitTycoon/Art/Models/Structures/"
git commit -m "feat(art): M4d batch 2 — bar, food court, VIP lounge models"
```

---

### Task 7: Batch 3 — remainder (SponsorBanner, EntranceGate, Grandstand, CampingField, SecondStage) *(controller session, Blender MCP)*

**Interfaces:** as Task 5; produces the last five FBXs.

- [ ] **Step 1: Assemble `SponsorBanner`** (greybox ~8.4×6×0.4): 2 poles + curved banner cloth (subdivided, slight S-curve). **Cloth object named exactly `BannerCloth`** (IdleMotion target).
- [ ] **Step 2: Assemble `EntranceGate`** (greybox ~6×5.6×0.8): pole pillars + truss lintel + arched sign panel (AccentWarm).
- [ ] **Step 3: Assemble `Grandstand`** (greybox ~10×4.5×6): 3 stepped tiers (Wood) + seat-row insets + railing across the top tier.
- [ ] **Step 4: Assemble `CampingField`** (greybox ~7×1.6×7): 9 tents (3 scale variants, rotation jitter) + campfire ring (stones + AccentWarm ember disc).
- [ ] **Step 5: Assemble `SecondStage`** (greybox ~8×4.5×5): deck (Wood), truss backwall frame, canopy roof, 2 cabinets flanking.
- [ ] **Step 6: Export all five**; run Build Festival Ground.
- [ ] **Step 7: User look-check of the full ground** — all 11 real models, survey + close-ups.
- [ ] **Step 8: Save .blend + commit**

```bash
git add "Assets/PitTycoon/Art/Source/festival-structures.blend" "Assets/PitTycoon/Art/Models/Structures/"
git commit -m "feat(art): M4d batch 3 — banner, gate, grandstand, camping, second stage models"
```

---

### Task 8: SETUP.md M4d section

**Files:**
- Modify: `SETUP.md` (append a new `## M4d — Real structure models` section after the M4c section)

**Interfaces:**
- Consumes: names/paths from Tasks 1–7 (verbatim below).

- [ ] **Step 1: Append the section**

```markdown
## M4d — Real structure models

The 11 build-spot structures are real Blender models (source:
`Assets/PitTycoon/Art/Source/festival-structures.blend`), exported per-structure to
`Assets/PitTycoon/Art/Models/Structures/<Name>.fbx`.

**Build steps**
1. Pull; let Unity import the FBXs and compile.
2. Run `Pit Tycoon → Build Festival Ground`. This now routes structure prefabs through
   `StructurePrefabs`: any structure with an FBX gets a real-model prefab at
   `Assets/PitTycoon/Art/Prefabs/Structures/`, with the shared cel palette
   (`Assets/PitTycoon/Art/Materials/Palette/`) assigned by material-slot name; structures
   without an FBX fall back to the greybox prefab automatically.

**Verification**
- Enter Play → intermission → buy any structure: the real model rises at the spot with
  palette colors (no flat gray cubes).
- Sponsor Banner's cloth sways gently; Strobe Rig's heads pan slowly (IdleMotion).
- Preview fly-to close-ups hold up at mid detail; outline/halftone post reads cleanly.

**Tuning**
- Recolor the whole festival: edit the 7 materials in `Art/Materials/Palette/` (live).
- Idle motion amplitude/speed: `IdleMotion` component on the SponsorBanner / StrobeRig
  prefabs.
- Model edits: change the .blend, re-export the structure's FBX, re-run
  `Pit Tycoon → Build Festival Ground`.
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP M4d — real structure models, palette, idle motion"
```

---

### Task 9: Manual Unity checkpoint *(user)*

- [ ] All 11 structures rise with real models on purchase; footprints match their spots.
- [ ] Palette colors present; no gray import materials (Console has no `StructurePrefabs` slot warnings).
- [ ] Banner sways; strobe heads pan.
- [ ] Preview ghost + fly-to close-ups look right; comic post (outline/halftone) reads well.
- [ ] Regression: hype/economy/abilities/build effects unchanged; `dotnet test` 96 passing.
- [ ] Commit generated prefabs/.meta + scene; open PR `feat/structure-models-m4d` → `master`; merge.
