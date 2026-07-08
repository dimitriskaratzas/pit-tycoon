# M5b Main-Stage Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the five M2b main-stage objects (Stage/Truss/Banner/PA ×2) with one full Blender stage rig (`MainStage.fbx`) while keeping venue upgrades, ghost previews, accent lights, and beat VFX anchoring working unchanged.

**Architecture:** One FBX with named child parts (`Deck`, `RoofTruss`, `Backwall`, `PAWingL/R`, `SubStackL/R`, `BannerCloth`) wrapped by a new `StructurePrefabs.EnsureMainStage()` (palette by slot name + IdleMotion banner sway). `FestivalSceneSetup` deletes the legacy five, places the rig at the same z=9 spot, re-parents the M2a accent lights onto `RoofTruss`, and re-wires `VenueController` (stage → rig root, paLeft/paRight → wings) and `BeatVfxController.stageAnchor`. Legacy placement remains as fallback while the FBX doesn't exist yet.

**Tech Stack:** Unity 6 URP editor scripting (SerializedObject wiring), Blender via MCP (M4d kit reuse), ComicLit palette.

## Global Constraints

- **No Domain changes.** `dotnet test PitTycoon.Domain.slnx` stays at **98 passing** after every task.
- FBX export: `Assets/PitTycoon/Art/Models/Structures/MainStage.fbx` (Structures dir so the existing `ModelDir` constant is reused — deliberate deviation from the spec's `Art/Models/` path). Prefab: `Assets/PitTycoon/Art/Prefabs/Structures/MainStage.prefab`.
- **M5c hook contract — child object names exactly:** `Deck`, `RoofTruss`, `Backwall`, `PAWingL`, `PAWingR`, `SubStackL`, `SubStackR`, `BannerCloth`.
- Material slots use only existing M4d palette names: Wood, MetalDark, MetalLight, AccentWarm, StrobeGlow, CanvasA (CanvasB allowed). No new palette entries.
- Everything degrades gracefully: missing `MainStage.fbx` → warning + the legacy five-object placement runs exactly as today.
- VenueController serialized field names (verified live): `stage`, `paLeft`, `paRight`, `accentLights`. BeatVfxController: `stageAnchor`.
- Commits: imperative present tense, **no Co-Authored-By trailer**.

## File Structure

- `Assets/PitTycoon/Unity/Editor/StructurePrefabs.cs` (modify) — `EnsureMainStage()` + banner idle-motion case.
- `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs` (modify) — placement swap, light reparent, venue/vfx rewiring.
- `SETUP.md` (modify) — M5b section.
- `ArtSource/festival-structures.blend` + `MainStage.fbx` (Blender task — controller-led).

---

### Task 1: StructurePrefabs.EnsureMainStage()

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/StructurePrefabs.cs`

**Interfaces:**
- Consumes: existing private helpers `EnsurePalette()`, `EnsureDir(string)`, `ConfigureIdle(GameObject, string, Vector3, float, float)` — all already in this file.
- Produces: `public static GameObject EnsureMainStage()` — returns the built prefab, or **null** when `MainStage.fbx` doesn't exist yet (Task 2 branches on null). Adds IdleMotion banner sway via the existing `AddIdleMotion` switch.

- [ ] **Step 1: Add the public entry point**

Below the existing `EnsureSpeakerWall()` line, add:

```csharp
        /// <summary>M5b main stage. No greybox fallback: returns null while the FBX is not
        /// yet exported, and FestivalSceneSetup keeps the legacy M2b placement.</summary>
        public static GameObject EnsureMainStage()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/MainStage.fbx");
            if (fbx == null)
            {
                Debug.LogWarning("StructurePrefabs: MainStage.fbx not found — legacy M2b stage kept.");
                return null;
            }

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
                    else Debug.LogWarning($"StructurePrefabs: MainStage/{r.name} slot '{mats[i].name}' " +
                                          "is not a palette name — left as imported.");
                }
                r.sharedMaterials = mats;
            }

            AddIdleMotion(temp, "MainStage");

            string path = $"{PrefabDir}/MainStage.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }
```

- [ ] **Step 2: Extend the idle-motion switch**

In `AddIdleMotion`, after the `StrobeRig` branch, add:

```csharp
            else if (name == "MainStage")
                ConfigureIdle(root, "BannerCloth", new Vector3(0f, 0f, 1f), rotationAmplitude: 4f, speed: 0.35f);
```

- [ ] **Step 3: Verify Domain suite untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: **98 passed**.

- [ ] **Step 4: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/StructurePrefabs.cs
git commit -m "feat(editor): StructurePrefabs.EnsureMainStage - stage rig prefab with banner sway"
```

---

### Task 2: FestivalSceneSetup — place the rig, keep everything wired

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs`

**Interfaces:**
- Consumes: `StructurePrefabs.EnsureMainStage()` (Task 1, returns null when FBX missing); existing helpers `PlaceModel`, `ReparentLight`, `WireBeatVfx(Shader, GameObject)`, `WireVenue(GameObject, GameObject, GameObject)` — unchanged signatures.
- Produces: scene objects — the rig instance named `MainStage` at `(0, 0, 9)`, or the legacy five objects when falling back.

- [ ] **Step 1: Replace the placement block**

The current block (`// Structure placement …` through `WireVenue(stage, paLeft, paRight);`) reads:

```csharp
            // Structure placement (behind the default 12x7 crowd, which spans z in [-3.6, 3.6]).
            var stage = PlaceModel("Stage.fbx", "Stage", new Vector3(0f, 0f, 9f), structureMat);
            var truss = PlaceModel("Truss.fbx", "Truss", new Vector3(0f, 0f, 9f), structureMat);
            PlaceModel("Banner.fbx", "Banner", new Vector3(0f, 1.2f, 10f), structureMat);
            var paLeft = PlaceModel("PASpeaker.fbx", "PA Left", new Vector3(-6f, 0f, 9f), structureMat);
            var paRight = PlaceModel("PASpeaker.fbx", "PA Right", new Vector3(6f, 0f, 9f), structureMat);

            // Reparent the M2a accent lights onto the truss beam.
            if (truss != null)
            {
                ReparentLight("Accent Amber", truss.transform, new Vector3(-2.5f, 4f, 9f));
                ReparentLight("Accent Magenta", truss.transform, new Vector3(2.5f, 4f, 9f));
                ReparentLight("Accent Cyan", truss.transform, new Vector3(0f, 4f, 9f));
            }

            WireBeatVfx(lit, stage);
            WireVenue(stage, paLeft, paRight);
```

Replace it with:

```csharp
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

            WireBeatVfx(lit, stage);
            WireVenue(stage, paLeft, paRight);
```

- [ ] **Step 2: Add the `FindDeep` helper**

`FestivalSceneSetup` has no deep-find helper (StructurePrefabs' one is private). Add at the bottom of the class:

```csharp
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
```

- [ ] **Step 3: Self-review the fallback path**

Confirm by reading: when `stagePrefab == null` the legacy block is byte-identical in behavior to the current code (same names, positions, materials, and `lightMount` = truss). Confirm `WireVenue` handles null `paLeft`/`paRight` (it does — `SetRef` with null value assigns null, and VenueController null-checks its fields).

- [ ] **Step 4: Verify Domain suite untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: **98 passed**.

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs
git commit -m "feat(editor): place MainStage rig with legacy fallback; rewire venue, lights, beat vfx"
```

---

### Task 3: Blender — MainStage rig + export (controller/Fable — NOT a subagent task)

Done live via the Blender MCP by the session controller, in `ArtSource/festival-structures.blend`, new `MainStage` collection reusing the M4d kit (truss segments, speaker cabinets, poles, canopy sheet):

- **Deck** ~14×6u platform, ~1u high, front skirt panel (Wood + MetalDark).
- **RoofTruss** — 4 truss-leg towers at the deck corners + roof truss grid + slightly sagging canopy (MetalDark + CanvasA).
- **Backwall** — angled LED-style panel behind the performer spot (MetalDark frame, StrobeGlow face).
- **PAWingL/R** — 3-cabinet vertical arrays flanking the deck (MetalDark, MetalLight fittings).
- **SubStackL/R** — 2-cabinet sub stacks at the deck front corners.
- **BannerCloth** — curved banner on the roof front edge (AccentWarm).
- Object names exactly per the hook contract; single mesh per named part; ~6–9k tris total; origins at sensible pivots (BannerCloth pivot at its top edge so IdleMotion swings it like cloth).
- Export selection to `Assets/PitTycoon/Art/Models/Structures/MainStage.fbx`, same axis/scale conventions as M4d (apply unit scale, FBX_SCALE_ALL, no leaf bones — static mesh, no armature, `bake_anim=False`).
- Look-check renders (crowd-level + survey angle) before export.

- [ ] Rig assembled, named per contract, look-check passed
- [ ] Exported + committed: `git add Assets/PitTycoon/Art/Models/Structures/MainStage.fbx ArtSource && git commit -m "feat(art): full main-stage rig"`

---

### Task 4: SETUP.md M5b section

**Files:**
- Modify: `SETUP.md` (append after the M5a section)

- [ ] **Step 1: Append**

```markdown
## M5b — Main-stage refresh

The M2b five-object stage (Stage/Truss/Banner/PA ×2) is now one full rig —
`Assets/PitTycoon/Art/Models/Structures/MainStage.fbx` (source: `ArtSource/festival-structures.blend`,
`MainStage` collection): deck, roof truss + canopy, LED backwall, PA wings, sub stacks, banner.

**Build steps:** pull, let the FBX import, run `Pit Tycoon → Build Festival Scene`. The builder
deletes the legacy objects, places `MainStage` at the old spot, moves the accent lights onto the
roof truss, and re-wires VenueController (stage = rig root, PA = wings) and the beat-VFX anchor.
Without the FBX the legacy placement still runs (safe mid-modelling).

**Verify:** old five objects gone; rig palette-colored; banner sways; Stage upgrade scales the whole
rig (ghost preview included); PA upgrade scales the wings; accent lights sit on the roof; crowd
dances in front at sane scale.

**Tuning:** palette materials (shared, live); IdleMotion on the prefab (BannerCloth);
VenueController stageStep/paStep (existing).

**M5c hook contract:** child names `Deck`, `RoofTruss`, `Backwall`, `PAWingL/R`, `SubStackL/R`,
`BannerCloth` are load-bearing for beat-reactivity — don't rename.
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP M5b main-stage refresh section"
```

---

### Task 5: Manual Unity checkpoint (user)

Not a subagent task. User runs the SETUP.md M5b steps and verifies the checklist above plus M1–M5a regression (set loop, abilities, build spots, crowd). Commit Unity-generated `.meta`/import/scene changes as `chore(unity): checkpoint`, then PR.

- [ ] Checklist passed, metas committed, PR opened
