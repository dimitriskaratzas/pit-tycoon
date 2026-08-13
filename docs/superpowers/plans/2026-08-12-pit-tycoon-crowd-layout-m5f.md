# M5f Crowd Layout & Beat Wave Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the pit read as a crowd rather than a lattice — position jitter that grows with distance from the stage, rows that pack tight at the barrier and loosen toward the back, and a beat pop that travels backward through the pit instead of firing everywhere at once.

**Architecture:** All per-member placement moves into a pure `CrowdLayout` type in the Domain assembly, derived from a deterministic hash of the member's index rather than from `Random.Range`. That single change kills the lattice, fixes two existing bugs (rebuilds reshuffling the whole crowd, ghost previews standing somewhere other than where real members land), and makes the maths unit-testable without the Unity Editor. `CrowdController` then consumes it from both its build path and its preview path.

**Tech Stack:** Plain C# (netstandard2.1) Domain + NUnit (net10.0), Unity 6 URP MonoBehaviour.

## Global Constraints

- **You cannot operate the Unity Editor.** Every Editor action is the developer's step. Write the code and the instructions; never mark a visual checkpoint verified yourself.
- **Domain layer is plain C#** — `Assets/PitTycoon/Domain/` must contain **no UnityEngine references**. `Vector3`, `Mathf`, and `Random` are all unavailable there; use `float`, `System.Math`, and the plain structs this plan defines.
- The Domain suite currently reports **98 passing** and must stay green: `dotnet test PitTycoon.Domain.slnx`. This milestone **adds** tests, so the final count is higher — that is expected, unlike M5e.
- Unity C# does **not** compile outside the Editor. The Domain half is genuinely tested; the `CrowdController` half is verified by transcription care plus the developer's Editor checkpoint.
- **Layout must be a pure function of member index.** No `Random` in any code path that decides where a member stands, what body it uses, what colour it wears, or how tall it is — otherwise rebuilds reshuffle the crowd and ghost previews stop matching.
- `FillFraction` / `ICrowdMeter`, the `CrowdFill` model, the Animator blend tree, and all hype maths are untouched.
- Commits: imperative present tense, **no `Co-Authored-By` trailer**.
- Branch: `feat/crowd-layout-m5f` (already created off the M5e tip; spec already committed).

## File Structure

- Create `Assets/PitTycoon/Domain/CrowdLayout.cs` — the pure layout maths: `CrowdSlot`, `CrowdLayoutSettings`, and the static `CrowdLayout`. One file, one responsibility.
- Create `tests/PitTycoon.Domain.Tests/CrowdLayoutTests.cs` — NUnit coverage of that maths.
- Modify `Assets/PitTycoon/Unity/CrowdController.cs` — consume the layout in both `Build()` and `PreviewCapacity()`, and drive the travelling wave.
- Modify `SETUP.md` — M5f section.

---

### Task 1: Domain `CrowdLayout` (TDD)

**Files:**
- Create: `Assets/PitTycoon/Domain/CrowdLayout.cs`
- Test: `tests/PitTycoon.Domain.Tests/CrowdLayoutTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces, all in namespace `PitTycoon.Domain`:
  - `readonly struct CrowdSlot` with `float X, Z, RotationY, Scale, PopScale` (get-only) and a 5-arg constructor in that order.
  - `readonly struct CrowdLayoutSettings` with a 7-arg constructor `(int columns, float spacing, int startRows, float positionJitter, float rowSpacingFalloff, float rotationJitter, float scaleJitter)`.
  - `static class CrowdLayout` with `CrowdSlot Slot(int index, CrowdLayoutSettings s)`, `float RowDepth(int row, float spacing, float falloff)`, `float PopHeight(int row, float secondsSinceBeat, float strength, float rowDelay, float decay)`, `int VariantIndex(int index, int count)`, `int OutfitIndex(int index, int count)`.

Task 2 calls every one of those.

- [ ] **Step 1: Write the failing tests**

Create `tests/PitTycoon.Domain.Tests/CrowdLayoutTests.cs`:

```csharp
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class CrowdLayoutTests
    {
        // columns 12, spacing 1.2, startRows 7 — the shipping CrowdController defaults.
        private static CrowdLayoutSettings Settings(float positionJitter = 0.3f, float falloff = 0.06f)
            => new CrowdLayoutSettings(12, 1.2f, 7, positionJitter, falloff, 18f, 0.12f);

        [Test]
        public void Slot_IsDeterministic_SameIndexSamePlace()
        {
            var s = Settings();
            var a = CrowdLayout.Slot(37, s);
            var b = CrowdLayout.Slot(37, s);
            Assert.That(b.X, Is.EqualTo(a.X));
            Assert.That(b.Z, Is.EqualTo(a.Z));
            Assert.That(b.Scale, Is.EqualTo(a.Scale));
            Assert.That(b.RotationY, Is.EqualTo(a.RotationY));
            Assert.That(b.PopScale, Is.EqualTo(a.PopScale));
        }

        [Test]
        public void RowDepth_FrontRowIsZero()
        {
            Assert.That(CrowdLayout.RowDepth(0, 1.2f, 0.06f), Is.EqualTo(0f));
        }

        [Test]
        public void RowDepth_NoFalloff_MatchesUniformGrid()
        {
            Assert.That(CrowdLayout.RowDepth(5, 1.2f, 0f), Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void RowDepth_GapWidensWithDistanceFromStage()
        {
            float gapNear = CrowdLayout.RowDepth(2, 1.2f, 0.06f) - CrowdLayout.RowDepth(1, 1.2f, 0.06f);
            float gapFar = CrowdLayout.RowDepth(9, 1.2f, 0.06f) - CrowdLayout.RowDepth(8, 1.2f, 0.06f);
            Assert.That(gapFar, Is.GreaterThan(gapNear));
        }

        [Test]
        public void Slot_JitterStaysWithinBounds()
        {
            var s = Settings(positionJitter: 0.3f);
            float maxOffset = 0.3f * 1.2f;      // jitter is a fraction of spacing, max multiplier is 1
            float offsetX = (12 - 1) * 1.2f * 0.5f;

            for (int i = 0; i < 400; i++)
            {
                var slot = CrowdLayout.Slot(i, s);
                float gridX = (i % 12) * 1.2f - offsetX;
                Assert.That(System.Math.Abs(slot.X - gridX), Is.LessThanOrEqualTo(maxOffset + 1e-4f));
            }
        }

        [Test]
        public void Slot_BackRowsScatterMoreThanTheFrontRow()
        {
            var s = Settings();
            float offsetX = (12 - 1) * 1.2f * 0.5f;

            float FrontRowSpread()
            {
                float total = 0f;
                for (int col = 0; col < 12; col++)
                    total += System.Math.Abs(CrowdLayout.Slot(col, s).X - (col * 1.2f - offsetX));
                return total;
            }

            float BackRowSpread()
            {
                float total = 0f;
                for (int col = 0; col < 12; col++)
                {
                    int i = 10 * 12 + col;
                    total += System.Math.Abs(CrowdLayout.Slot(i, s).X - (col * 1.2f - offsetX));
                }
                return total;
            }

            Assert.That(BackRowSpread(), Is.GreaterThan(FrontRowSpread()));
        }

        [Test]
        public void Slot_NeighbouringIndicesDoNotShareJitter()
        {
            // Adjacent indices stand next to each other in the pit, so a weak hash shows up as
            // visible diagonal banding. Neighbours must not land on the same offset.
            var s = Settings();
            int identical = 0;
            for (int i = 0; i < 200; i++)
            {
                var a = CrowdLayout.Slot(i, s);
                var b = CrowdLayout.Slot(i + 1, s);
                float aOff = a.X - (i % 12) * 1.2f;
                float bOff = b.X - ((i + 1) % 12) * 1.2f;
                if (System.Math.Abs(aOff - bOff) < 1e-6f) identical++;
            }
            Assert.That(identical, Is.Zero);
        }

        [Test]
        public void Slot_ScaleStaysWithinJitter()
        {
            var s = Settings();
            for (int i = 0; i < 200; i++)
            {
                float scale = CrowdLayout.Slot(i, s).Scale;
                Assert.That(scale, Is.InRange(1f - 0.12f - 1e-4f, 1f + 0.12f + 1e-4f));
            }
        }

        [Test]
        public void Slot_PopScaleStaysInTheDesignedRange()
        {
            var s = Settings();
            for (int i = 0; i < 200; i++)
                Assert.That(CrowdLayout.Slot(i, s).PopScale, Is.InRange(0.55f, 1.2f));
        }

        [Test]
        public void PopHeight_IsZeroBeforeTheWaveArrives()
        {
            Assert.That(CrowdLayout.PopHeight(row: 4, secondsSinceBeat: 0.05f,
                strength: 1f, rowDelay: 0.03f, decay: 2.5f), Is.EqualTo(0f));
        }

        [Test]
        public void PopHeight_IsFullStrengthExactlyOnArrival()
        {
            Assert.That(CrowdLayout.PopHeight(row: 4, secondsSinceBeat: 0.12f,
                strength: 1f, rowDelay: 0.03f, decay: 2.5f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void PopHeight_FrontRowLeadsTheBackRow()
        {
            float front = CrowdLayout.PopHeight(0, 0.05f, 1f, 0.03f, 2.5f);
            float back = CrowdLayout.PopHeight(6, 0.05f, 1f, 0.03f, 2.5f);
            Assert.That(front, Is.GreaterThan(back));
        }

        [Test]
        public void PopHeight_DecaysToZero()
        {
            Assert.That(CrowdLayout.PopHeight(0, 10f, 1f, 0.03f, 2.5f), Is.EqualTo(0f));
        }

        [Test]
        public void PopHeight_ZeroRowDelay_FiresEveryRowTogether()
        {
            // rowDelay 0 must reproduce the pre-M5f behaviour exactly, so the wave can be
            // A/B'd from the Inspector.
            float front = CrowdLayout.PopHeight(0, 0.05f, 1f, 0f, 2.5f);
            float back = CrowdLayout.PopHeight(9, 0.05f, 1f, 0f, 2.5f);
            Assert.That(back, Is.EqualTo(front));
        }

        [Test]
        public void VariantIndex_StaysInRangeAndIsDeterministic()
        {
            for (int i = 0; i < 200; i++)
            {
                int v = CrowdLayout.VariantIndex(i, 4);
                Assert.That(v, Is.InRange(0, 3));
                Assert.That(CrowdLayout.VariantIndex(i, 4), Is.EqualTo(v));
            }
        }

        [Test]
        public void OutfitIndex_StaysInRangeAndDiffersFromVariantChoice()
        {
            int sameCount = 0;
            for (int i = 0; i < 200; i++)
            {
                int o = CrowdLayout.OutfitIndex(i, 6);
                Assert.That(o, Is.InRange(0, 5));
                if (o == CrowdLayout.VariantIndex(i, 6)) sameCount++;
            }
            // Different salts: outfit and body must not be locked together across the pit.
            Assert.That(sameCount, Is.LessThan(200));
        }

        [Test]
        public void PickIndex_HandlesEmptyCollections()
        {
            Assert.That(CrowdLayout.VariantIndex(5, 0), Is.Zero);
            Assert.That(CrowdLayout.OutfitIndex(5, 0), Is.Zero);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: FAIL — compile errors, `CrowdLayout` / `CrowdSlot` / `CrowdLayoutSettings` do not exist.

- [ ] **Step 3: Write the implementation**

Create `Assets/PitTycoon/Domain/CrowdLayout.cs`:

```csharp
namespace PitTycoon.Domain
{
    /// <summary>Where one crowd member stands and how it is shaped. Plain floats, not a
    /// Vector3: the Domain assembly has no UnityEngine reference.</summary>
    public readonly struct CrowdSlot
    {
        public float X { get; }
        public float Z { get; }
        public float RotationY { get; }
        public float Scale { get; }
        public float PopScale { get; }

        public CrowdSlot(float x, float z, float rotationY, float scale, float popScale)
        {
            X = x;
            Z = z;
            RotationY = rotationY;
            Scale = scale;
            PopScale = popScale;
        }
    }

    /// <summary>Layout parameters, passed as one value so the real pit and its ghost preview
    /// cannot drift apart on arguments.</summary>
    public readonly struct CrowdLayoutSettings
    {
        public int Columns { get; }
        public float Spacing { get; }
        public int StartRows { get; }
        public float PositionJitter { get; }
        public float RowSpacingFalloff { get; }
        public float RotationJitter { get; }
        public float ScaleJitter { get; }

        public CrowdLayoutSettings(int columns, float spacing, int startRows,
            float positionJitter, float rowSpacingFalloff, float rotationJitter, float scaleJitter)
        {
            Columns = columns < 1 ? 1 : columns;
            Spacing = spacing;
            StartRows = startRows < 1 ? 1 : startRows;
            PositionJitter = positionJitter;
            RowSpacingFalloff = rowSpacingFalloff;
            RotationJitter = rotationJitter;
            ScaleJitter = scaleJitter;
        }
    }

    /// <summary>
    /// Pure pit layout. Every per-member value is derived from a hash of the member's index
    /// rather than drawn randomly, which is what makes the crowd stable across rebuilds (a
    /// capacity upgrade calls Build() again) and makes a ghost preview land exactly where its
    /// real member will. No UnityEngine — fully unit-tested.
    /// </summary>
    public static class CrowdLayout
    {
        // Salts keep the derived values independent. Reusing one would lock, say, body shape
        // to outfit colour across the whole pit.
        private const int SaltJitterX = 1;
        private const int SaltJitterZ = 2;
        private const int SaltRotation = 3;
        private const int SaltScale = 4;
        private const int SaltPop = 5;
        private const int SaltVariant = 6;
        private const int SaltOutfit = 7;

        public static CrowdSlot Slot(int index, CrowdLayoutSettings s)
        {
            if (index < 0) index = 0;

            int row = index / s.Columns;
            int col = index % s.Columns;

            // Front row stays pinned near the stage, so the pit does not drift off the barrier
            // as capacity grows.
            float frontZ = (s.StartRows - 1) * s.Spacing * 0.5f;
            float offsetX = (s.Columns - 1) * s.Spacing * 0.5f;

            // Scatter grows with distance from the stage: packed at the barrier, loose at the
            // back. Expressed as a fraction of spacing so it stays proportional when tuned.
            float rowT = s.StartRows > 1 ? row / (float)(s.StartRows - 1) : 1f;
            if (rowT > 1f) rowT = 1f;
            float amount = s.PositionJitter * (0.35f + 0.65f * rowT) * s.Spacing;

            float x = col * s.Spacing - offsetX + Signed(index, SaltJitterX) * amount;
            float z = frontZ - RowDepth(row, s.Spacing, s.RowSpacingFalloff)
                      + Signed(index, SaltJitterZ) * amount;

            float rotationY = Signed(index, SaltRotation) * s.RotationJitter;
            float scale = 1f + Signed(index, SaltScale) * s.ScaleJitter;
            float popScale = 0.55f + Hash01(index, SaltPop) * 0.65f;   // 0.55 .. 1.20

            return new CrowdSlot(x, z, rotationY, scale, popScale);
        }

        /// <summary>Cumulative depth of a row from the front. The gap between consecutive rows
        /// is spacing * (1 + falloff * r), so the sum has a closed form and needs no loop.</summary>
        public static float RowDepth(int row, float spacing, float falloff)
        {
            if (row <= 0) return 0f;
            return spacing * (row + falloff * row * (row - 1) * 0.5f);
        }

        /// <summary>Height of the beat pop for a row, given how long ago the beat landed.
        /// Zero until the wave reaches that row — that guard is what makes the pop travel
        /// instead of firing across the whole pit on one frame.</summary>
        public static float PopHeight(int row, float secondsSinceBeat, float strength,
            float rowDelay, float decay)
        {
            float age = secondsSinceBeat - row * rowDelay;
            if (age < 0f) return 0f;
            float height = strength - age * decay;
            return height > 0f ? height : 0f;
        }

        /// <summary>Which body variant this member uses. Stable across rebuilds.</summary>
        public static int VariantIndex(int index, int count) => PickIndex(index, count, SaltVariant);

        /// <summary>Which outfit tint this member wears. Stable across rebuilds.</summary>
        public static int OutfitIndex(int index, int count) => PickIndex(index, count, SaltOutfit);

        private static int PickIndex(int index, int count, int salt)
        {
            if (count <= 0) return 0;
            int picked = (int)(Hash01(index, salt) * count);
            return picked >= count ? count - 1 : picked;
        }

        /// <summary>Deterministic [0,1) hash. Must decorrelate neighbouring indices: adjacent
        /// members stand next to each other, so a weak hash reads as diagonal banding rather
        /// than scatter.</summary>
        private static float Hash01(int index, int salt)
        {
            unchecked
            {
                uint h = (uint)(index * 374761393 + salt * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        private static float Signed(int index, int salt) => Hash01(index, salt) * 2f - 1f;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS, with the total above the previous 98 (17 tests are added by this task).

If `Slot_NeighbouringIndicesDoNotShareJitter` fails, the hash is not mixing enough — fix the hash, do not weaken the test. It is guarding against the exact artefact (banding) that this milestone exists to remove.

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/CrowdLayout.cs tests/PitTycoon.Domain.Tests/CrowdLayoutTests.cs
git commit -m "feat(domain): add deterministic crowd layout and travelling pop maths"
```

---

### Task 2: `CrowdController` consumes the layout

**Files:**
- Modify: `Assets/PitTycoon/Unity/CrowdController.cs`

**Interfaces:**
- Consumes: `CrowdLayout.Slot(int, CrowdLayoutSettings)`, `CrowdLayout.VariantIndex(int, int)`, `CrowdLayout.OutfitIndex(int, int)`, `CrowdSlot`, `CrowdLayoutSettings` from Task 1.
- Produces: a private `CrowdLayoutSettings LayoutSettings()` helper used by both `Build()` and `PreviewCapacity()`. Task 3 adds the wave on top of this file.

- [ ] **Step 1: Add the new serialized knobs and the settings helper**

In `CrowdController.cs`, add these two fields immediately after the existing `scaleJitter` field:

```csharp
        [Tooltip("Per-member scatter off the grid, as a fraction of spacing. 0 = the old rigid lattice.")]
        [SerializeField] private float positionJitter = 0.3f;
        [Tooltip("How much wider each row's gap gets further from the stage. 0 = uniform rows.")]
        [SerializeField] private float rowSpacingFalloff = 0.06f;
```

Add this private helper next to `SpawnMember` — it is the single place layout parameters are assembled, so the build path and the preview path cannot disagree:

```csharp
        /// <summary>Layout parameters for CrowdLayout. Shared by Build and PreviewCapacity so a
        /// ghost lands exactly where its real member will.</summary>
        private CrowdLayoutSettings LayoutSettings()
        {
            int startRows = Mathf.CeilToInt((float)startingCapacity / Mathf.Max(1, columns));
            return new CrowdLayoutSettings(columns, spacing, startRows,
                positionJitter, rowSpacingFalloff, rotationJitter, scaleJitter);
        }
```

- [ ] **Step 2: Rewrite the member loop in `Build()`**

Replace the block that starts with `int startRows = Mathf.CeilToInt(...)` and runs to the end of the `for (int i = 0; i < n; i++)` loop with:

```csharp
            var layout = LayoutSettings();

            for (int i = 0; i < n; i++)
            {
                var slot = CrowdLayout.Slot(i, layout);

                GameObject go = SpawnMember(i);
                _animators[i] = StyleMember(go, i);

                go.name = $"Crowd_{i / columns}_{i % columns}";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(slot.X, 0f, slot.Z);
                go.transform.localRotation = Quaternion.Euler(0f, slot.RotationY, 0f);

                _popJitter[i] = slot.PopScale;
                float cur = (i < active) ? 1f : 0f;             // active members appear immediately
                _fullScale[i] = slot.Scale;
                _curScale[i] = cur;
                go.transform.localScale = Vector3.one * (slot.Scale * cur);
                _members[i] = go.transform;
            }
```

- [ ] **Step 3: Rewrite the member loop in `PreviewCapacity()`**

Replace the block that starts with `int startRows = Mathf.CeilToInt(...)` and runs to the end of that method's `for` loop with:

```csharp
            var layout = LayoutSettings();

            for (int i = from; i < to; i++)
            {
                var slot = CrowdLayout.Slot(i, layout);

                GameObject go = SpawnMember(i);
                var ghostAnim = go.GetComponentInChildren<Animator>();
                if (ghostAnim != null) Destroy(ghostAnim);
                go.name = $"GhostCrowd_{i / columns}_{i % columns}";
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                if (ghostMaterial != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;

                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(slot.X, 0f, slot.Z);
                go.transform.localRotation = Quaternion.Euler(0f, slot.RotationY, 0f);
                go.transform.localScale = Vector3.one * slot.Scale;
                _ghosts.Add(go);
            }
```

The ghost now also matches the real member's rotation and scale, not just its position — the whole point is that the preview shows what you are buying.

- [ ] **Step 4: Make body variant and outfit tint index-derived**

Both currently call `Random.Range`, so every rebuild reshuffles which body and which shirt colour each member has. A capacity purchase visibly re-clothes the entire crowd. Replace `SpawnMember` and `StyleMember` with index-taking versions:

```csharp
        /// <summary>Instantiate this member's body variant, or the capsule fallback when none are
        /// wired. The variant is index-derived, so a rebuild does not re-roll everyone's body.</summary>
        private GameObject SpawnMember(int index)
        {
            if (memberPrefabs != null && memberPrefabs.Length > 0)
            {
                var prefab = memberPrefabs[CrowdLayout.VariantIndex(index, memberPrefabs.Length)];
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

        /// <summary>Index-derived outfit tint (stable across rebuilds) plus a desynced animator
        /// start. The animator offset stays random on purpose: its phase drifts anyway, so
        /// re-rolling it on a rebuild is invisible.</summary>
        private Animator StyleMember(GameObject go, int index)
        {
            if (outfitPalette != null && outfitPalette.Length > 0)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(BaseColorProp, outfitPalette[CrowdLayout.OutfitIndex(index, outfitPalette.Length)]);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mpb);
            }
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.CullCompletely;
                anim.speed = Random.Range(0.85f, 1.15f);
                // Target the state BY NAME: hash 0 ("current state") is a no-op before the
                // Animator's first frame, which left every member at frame 0 = lockstep.
                anim.Play(DanceState, 0, Random.value);
            }
            return anim;
        }
```

- [ ] **Step 5: Verify the Domain suite still passes**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS, same count as at the end of Task 1. (This task changes no Domain code; the suite is a regression gate.)

- [ ] **Step 6: Developer Editor checkpoint**

Hand these to the developer — do not mark them done yourself:

1. Let Unity recompile; confirm no errors in the Console.
2. Press Play. The pit should no longer read as a grid: members sit off-axis, and the crowd is denser near the stage and thinner at the back.
3. Buy a **capacity (Grounds)** upgrade. The existing crowd must **not** resettle — no jumping, no bodies changing shape, no shirts changing colour. Only the new members appear.
4. Open the shop and select the Grounds upgrade to show the ghost preview. Ghost members must stand exactly where real members appear after purchase, at the same rotation and scale.
5. Set `positionJitter` to 0 in the Inspector; the old rigid lattice should return, confirming the knob is wired.

- [ ] **Step 7: Commit**

```bash
git add Assets/PitTycoon/Unity/CrowdController.cs
git commit -m "feat: derive crowd placement from member index instead of random draws"
```

---

### Task 3: The travelling beat wave

**Files:**
- Modify: `Assets/PitTycoon/Unity/CrowdController.cs`
- Modify: `SETUP.md`

**Interfaces:**
- Consumes: `CrowdLayout.PopHeight(int, float, float, float, float)` from Task 1; the `_popJitter` array populated in Task 2.
- Produces: nothing consumed by later tasks — this is the closing task.

- [ ] **Step 1: Replace the single pop value with a beat timestamp**

In `CrowdController.cs`, add the wave knob after the `rowSpacingFalloff` field added in Task 2:

```csharp
        [Tooltip("Seconds of delay per row, so the pop travels back from the stage. 0 = every row pops at once (the pre-M5f behaviour).")]
        [SerializeField] private float waveRowDelay = 0.035f;
```

Replace the `private float _pop;` field with:

```csharp
        private float _beatTime = -999f;   // Time.time of the beat that started the current wave
        private float _beatStrength;
        private float _prevBeatTime = -999f;
        private float _prevBeatStrength;
```

- [ ] **Step 2: Retrigger the wave from beats and abilities**

Replace `OnBeat` and `Pop` with:

```csharp
        private void OnBeat(BeatInfo beat)
        {
            TriggerWave(beatPop * Mathf.Clamp01(0.4f + beat.Strength));
        }

        /// <summary>One-shot crowd jolt (an ability fired). Visible on the next Update.</summary>
        public void Pop(float strength)
        {
            TriggerWave(strength);
        }

        /// <summary>Start a new pop wave at the barrier, keeping the in-flight one alive beside it.
        /// Replacing it outright would reset secondsSinceBeat to 0, which makes PopHeight return 0
        /// for every row the old wave had already reached — their arrival is back in the future —
        /// so the whole pit would snap to the ground on each retrigger and then re-rise.</summary>
        private void TriggerWave(float strength)
        {
            _prevBeatTime = _beatTime;
            _prevBeatStrength = _beatStrength;
            _beatTime = Time.time;
            _beatStrength = strength;
        }
```

- [ ] **Step 3: Drive per-member height from the wave in `Update()`**

Remove this line from `Update()`:

```csharp
            _pop = Mathf.MoveTowards(_pop, 0f, popDecayPerSecond * Time.deltaTime);
```

Add this immediately before the `for` loop over members:

```csharp
            float sinceBeat = Time.time - _beatTime;
            float sincePrevBeat = Time.time - _prevBeatTime;
```

Then replace the three lines inside the loop that compute the member's Y:

```csharp
                bool visible = _curScale[i] > 0.05f;
                Vector3 p = tr.localPosition;
                float jitter = _popJitter != null && i < _popJitter.Length ? _popJitter[i] : 1f;
                p.y = visible ? _pop * jitter : 0f;     // clips own body motion; beat pops lift the root
                tr.localPosition = p;
```

with:

```csharp
                bool visible = _curScale[i] > 0.05f;
                Vector3 p = tr.localPosition;
                float jitter = _popJitter != null && i < _popJitter.Length ? _popJitter[i] : 1f;
                // Row-delayed: the pop rolls back through the pit from the stage rather than
                // firing on every member in the same frame. Clips own body motion; lifts the root.
                float wave = CrowdLayout.PopHeight(i / columns, sinceBeat, _beatStrength,
                    waveRowDelay, popDecayPerSecond);
                float prevWave = CrowdLayout.PopHeight(i / columns, sincePrevBeat, _prevBeatStrength,
                    waveRowDelay, popDecayPerSecond);
                if (prevWave > wave) wave = prevWave;
                p.y = visible ? wave * jitter : 0f;
                tr.localPosition = p;
```

- [ ] **Step 4: Verify the Domain suite still passes**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS, same count as at the end of Task 1.

- [ ] **Step 5: Add the M5f section to SETUP.md**

Append, following the formatting of the existing per-milestone sections:

```markdown
## M5f — Crowd Layout & Beat Wave

Breaks the pit out of its grid and makes beat pops travel back through the crowd instead of
firing everywhere at once.

### Build steps

None. This milestone is pure code — no builder run and no new assets. Unity recompiles on
focus and the change is live in Play mode.

### Verification

- The pit no longer reads as a lattice: members sit off-axis, denser at the barrier and
  thinner toward the back.
- A beat pop visibly travels backward from the stage. Set `waveRowDelay` to 0 to get the old
  everyone-at-once behaviour for comparison.
- Firing an ability sends the same travelling pulse.
- Buying a capacity (Grounds) upgrade adds new members **without** the existing crowd
  resettling — no jumping, no bodies changing shape, no shirts changing colour.
- Ghost preview members stand exactly where the real members appear after purchase, at the
  same rotation and scale.

### Tuning knobs

All on `CrowdController` in the scene, live in Play mode:

- `positionJitter` — scatter off the grid, as a fraction of `spacing`. 0 removes the scatter, but
  rows stay unevenly spaced until `rowSpacingFalloff` is zeroed too — the two knobs are
  independent. Set **both** to 0 to get the exact pre-M5f lattice back for comparison.
- `rowSpacingFalloff` — how much wider each row's gap gets further from the stage. 0 = uniform.
- `waveRowDelay` — seconds of delay per row. Higher travels slower and reads more like a wave;
  0 fires the whole pit at once.
- Existing: `spacing`, `columns`, `rotationJitter`, `scaleJitter`, `beatPop`, and
  `popDecayPerSecond` (which also sets how fast the wave decays as it travels).

Placement is derived from each member's index, not drawn randomly, so the crowd is stable
across rebuilds and ghost previews match what you get. Changing `columns`, `spacing`,
`positionJitter`, or `rowSpacingFalloff` re-derives every position — that is expected.
```

- [ ] **Step 6: Developer Editor checkpoint**

1. Press Play and watch the pit on a strong beat: the pop should start at the barrier and roll backward, not fire as one wall.
2. Fire an ability — same travelling pulse.
3. Set `waveRowDelay` to 0 and confirm the whole pit pops together (the old behaviour), then set it back.
4. Confirm a weak beat landing during a big wave does not visibly cut the wave short.
5. M1–M5e regression: hype builds, abilities fire on-beat, upgrades and build spots work, and the M5e night lighting, fog, and beams are unaffected.

- [ ] **Step 7: Commit**

```bash
git add Assets/PitTycoon/Unity/CrowdController.cs SETUP.md
git commit -m "feat: make beat pops travel back through the pit"
```

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| `CrowdSlot` / `CrowdLayoutSettings` plain structs, no UnityEngine | 1 |
| `Slot(index, settings)` deterministic, hash-derived | 1 |
| Row depth with falloff, closed form | 1 |
| Jitter scaling `0.35 + 0.65 * rowT`, fraction of spacing | 1 |
| `PopHeight` with arrival delay, zero before arrival, linear decay | 1 |
| Hash decorrelates neighbours | 1 (test + implementation) |
| Unit tests: determinism, spread, depth, bounds, wave ordering | 1 |
| `Build()` and `PreviewCapacity()` share one layout source | 2 |
| Duplicated preview layout maths deleted | 2 |
| `Random.Range` removed from placement paths | 2 |
| Ghosts match real members' position, rotation, scale | 2 |
| New knobs `positionJitter`, `rowSpacingFalloff` | 2 |
| New knob `waveRowDelay`; `popDecayPerSecond` reused as decay | 3 |
| Beat timestamp replaces single `_pop` | 3 |
| `Pop()` (ability jolt) triggers the same wave | 3 |
| `FillFraction`, `CrowdFill`, Animator, hype maths untouched | 2, 3 (no edits to those paths) |
| SETUP.md section | 3 |

**Beyond the spec, deliberately:** the spec's bug section names rotation, scale, and pop jitter as the values to make index-derived. Task 2 also covers **body variant** and **outfit tint**, which have the identical defect — `Random.Range` at build time means a capacity purchase re-rolls every member's body shape and shirt colour. Fixing placement stability while leaving the crowd visibly re-clothing itself on every upgrade would be a half-fix.

**Placeholders:** none — every step carries the code to write.

**Type consistency:** `CrowdSlot` members (`X`, `Z`, `RotationY`, `Scale`, `PopScale`) are defined in Task 1 Step 3 and read with those exact names in Task 2 Steps 2-3. `CrowdLayout.PopHeight(int, float, float, float, float)` is defined in Task 1 and called with that argument order in Task 3 Steps 2-3. `SpawnMember(int)` and `StyleMember(GameObject, int)` gain their index parameter in Task 2 Step 4 and are called with it in Steps 2-3 of the same task — both call sites and both definitions change together.
