# Pit Tycoon M2c — Manga Beat-VFX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Punctuate big musical moments with comic VFX — onomatopoeia pops, stage shockwaves, and a comic restyle of the whirlpool/coins — driven by the existing `BeatDetected`/`AbilityFired` bus events.

**Architecture:** A bus-driven `BeatVfxController` conductor (initialized by `GameBootstrap` like every other system) decides when to fire and spawns self-animating, self-destructing world-space VFX (`OnomatopoeiaVfx`, `ShockwaveVfx`) — the same runtime-spawn pattern `WhirlpoolVfx`/`CoinFlyVfx` already use. Those two get a comic material via a static override the controller sets.

**Tech Stack:** Unity 6 / URP 17.4, C#, the M2a ComicLit material + screen-space outline/halftone (which ink these world-space VFX for free). PC-first.

> **Verification model (read first):** VFX has **no automated tests**. Each task is verified at the **manual in-editor checkpoint** (Task 8). The only automated check is the Domain suite staying 50 green after wiring (Task 5), proving the core is untouched. New C# compiles are verified by Unity reporting no Console errors.

---

## File structure

```
Assets/PitTycoon/Unity/
  OnomatopoeiaVfx.cs    (new) self-spawning TextMesh pop (ink-outlined, punch+rise+fade, billboard)
  ShockwaveVfx.cs       (new) self-spawning expanding LineRenderer ink ring
  BeatVfxController.cs  (new) bus-driven conductor (big-beat + great-hit triggers)
  WhirlpoolVfx.cs       (modify) + static OverrideMaterial
  CoinFlyVfx.cs         (modify) + static OverrideMaterial
  GameBootstrap.cs      (modify) + serialized beatVfx ref + Initialize(Bus)
  Editor/FestivalSceneSetup.cs (modify) + WireBeatVfx() — add controller, wire anchor + materials
SETUP.md                (modify) + M2c section
```

---

### Task 1: OnomatopoeiaVfx

**Files:**
- Create: `Assets/PitTycoon/Unity/OnomatopoeiaVfx.cs`

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Self-spawning comic onomatopoeia pop (e.g. "DON!"). World-space TextMesh with a
    /// black offset copy for an inked drop-shadow; punches in, rises, fades, billboards to
    /// the camera, then self-destructs. Runtime-only, no scene wiring. Font is the builtin
    /// LegacyRuntime font so there's no asset import.
    /// </summary>
    public sealed class OnomatopoeiaVfx : MonoBehaviour
    {
        private const float MaxLife = 0.85f;
        private float _life;
        private TextMesh[] _texts;

        public static void Spawn(string word, Color color, Vector3 pos)
        {
            var root = new GameObject("Onomatopoeia");
            root.transform.position = pos;
            var fx = root.AddComponent<OnomatopoeiaVfx>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            TextMesh MakeText(string name, Color c, Vector3 localOffset)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = localOffset;
                var tm = go.AddComponent<TextMesh>();
                tm.text = word; tm.font = font; tm.fontSize = 96; tm.fontStyle = FontStyle.Bold;
                tm.characterSize = 0.12f; tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center; tm.color = c;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                return tm;
            }

            // shadow sits slightly behind (local +z = away from camera after billboard) and offset.
            var shadow = MakeText("ink", Color.black, new Vector3(0.06f, -0.06f, 0.02f));
            var front = MakeText("front", color, Vector3.zero);
            fx._texts = new[] { shadow, front };
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = _life / MaxLife;
            if (t >= 1f) { Destroy(gameObject); return; }

            if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;

            float punch = t < 0.25f
                ? Mathf.Lerp(0.3f, 1.15f, t / 0.25f)
                : Mathf.Lerp(1.15f, 1f, (t - 0.25f) / 0.75f);
            transform.localScale = Vector3.one * punch;
            transform.position += Vector3.up * (0.6f * Time.deltaTime);

            float a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            foreach (var tm in _texts) { var c = tm.color; c.a = a; tm.color = c; }
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: Unity recompiles `PitTycoon.Unity` with no Console errors. (Visual check at Task 8.)

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/OnomatopoeiaVfx.cs"
git commit -m "feat(vfx): OnomatopoeiaVfx comic pop (M2c)"
```

---

### Task 2: ShockwaveVfx

**Files:**
- Create: `Assets/PitTycoon/Unity/ShockwaveVfx.cs`

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Self-spawning shockwave: a flat ink ring (LineRenderer loop on the XZ plane) that
    /// expands outward from the stage and fades. Runtime-only, no scene wiring.
    /// </summary>
    public sealed class ShockwaveVfx : MonoBehaviour
    {
        private const int Segments = 48;
        private const float MaxLife = 0.6f;
        private static Material _ringMat;

        private LineRenderer _lr;
        private float _life;
        private float _maxRadius = 9f;

        public static void Spawn(Vector3 pos, float maxRadius)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = pos + Vector3.up * 0.05f;
            var fx = go.AddComponent<ShockwaveVfx>();
            fx._maxRadius = maxRadius;

            var lr = go.AddComponent<LineRenderer>();
            if (_ringMat == null) _ringMat = new Material(Shader.Find("Sprites/Default"));
            lr.material = _ringMat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = Segments;
            lr.widthMultiplier = 0.35f;
            lr.numCapVertices = 2;
            lr.startColor = lr.endColor = new Color(0.08f, 0.06f, 0.10f, 1f);
            fx._lr = lr;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = _life / MaxLife;
            if (t >= 1f) { Destroy(gameObject); return; }

            float r = Mathf.Lerp(0.2f, _maxRadius, t);
            for (int i = 0; i < Segments; i++)
            {
                float a = (i / (float)Segments) * Mathf.PI * 2f;
                _lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            _lr.widthMultiplier = Mathf.Lerp(0.35f, 0.02f, t);
            var col = new Color(0.08f, 0.06f, 0.10f, 1f - t);
            _lr.startColor = _lr.endColor = col;
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: no Console errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/ShockwaveVfx.cs"
git commit -m "feat(vfx): ShockwaveVfx expanding ink ring (M2c)"
```

---

### Task 3: Restyle WhirlpoolVfx + CoinFlyVfx

**Files:**
- Modify: `Assets/PitTycoon/Unity/WhirlpoolVfx.cs`
- Modify: `Assets/PitTycoon/Unity/CoinFlyVfx.cs`

- [ ] **Step 1: Add an override material to WhirlpoolVfx**

In `WhirlpoolVfx`, add the static field at the top of the class (after the class declaration line `public sealed class WhirlpoolVfx : MonoBehaviour {`):

```csharp
        /// <summary>Optional comic material applied to spawned whirlpools (set by BeatVfxController).</summary>
        public static Material OverrideMaterial;
```

Then in `Spawn`, immediately after `if (col != null) Destroy(col);`, add:

```csharp
            if (OverrideMaterial != null)
            {
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = OverrideMaterial;
            }
```

- [ ] **Step 2: Add an override material to CoinFlyVfx**

In `CoinFlyVfx`, add the static field after the class declaration:

```csharp
        /// <summary>Optional comic material applied to spawned coins (set by BeatVfxController).</summary>
        public static Material OverrideMaterial;
```

Then in `Burst`, immediately after `if (col != null) Destroy(col);`, add:

```csharp
                if (OverrideMaterial != null)
                {
                    var rend = go.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = OverrideMaterial;
                }
```

- [ ] **Step 3: Verify compile**

Expected: no Console errors. With `OverrideMaterial` null (not yet set), behaviour is identical to today.

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Unity/WhirlpoolVfx.cs" "Assets/PitTycoon/Unity/CoinFlyVfx.cs"
git commit -m "feat(vfx): WhirlpoolVfx/CoinFlyVfx optional comic override material (M2c)"
```

---

### Task 4: BeatVfxController (conductor)

**Files:**
- Create: `Assets/PitTycoon/Unity/BeatVfxController.cs`

- [ ] **Step 1: Write the conductor**

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Bus-driven VFX conductor. On a big beat (strength over threshold, rate-limited) it
    /// throws a shockwave + onomatopoeia from the stage; on a great ability hit (multiplier
    /// over threshold) it throws a "POW!" at the pit. Also sets the comic override materials
    /// on the whirlpool/coin VFX. Live-phase only. Wired by the editor script; Initialize(bus)
    /// called by GameBootstrap.
    /// </summary>
    public sealed class BeatVfxController : MonoBehaviour
    {
        [SerializeField] private Transform stageAnchor;
        [SerializeField] private float strengthThreshold = 0.6f;
        [SerializeField] private float multiplierThreshold = 4f;
        [SerializeField] private float minInterval = 1.5f;
        [SerializeField] private float shockwaveRadius = 9f;
        [SerializeField] private Material whirlpoolMaterial;
        [SerializeField] private Material coinMaterial;

        private static readonly string[] Words = { "DON!", "POW!", "BOOM!", "WHAM!", "BAM!" };
        private static readonly Color[] Accents =
        {
            new Color(1f, 0.69f, 0.24f),
            new Color(0.88f, 0.27f, 0.49f),
            new Color(0.21f, 0.79f, 0.88f),
            Color.white,
        };

        private EventBus _bus;
        private bool _live;
        private double _lastBigBeat = -999.0;

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            WhirlpoolVfx.OverrideMaterial = whirlpoolMaterial;
            CoinFlyVfx.OverrideMaterial = coinMaterial;
            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);
            _bus.Subscribe<BeatDetected>(OnBeat);
            _bus.Subscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
            _bus.Unsubscribe<BeatDetected>(OnBeat);
            _bus.Unsubscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnSetStarted(SetStarted e) => _live = true;
        private void OnSetEnded(SetEnded e) => _live = false;

        private void OnBeat(BeatDetected e)
        {
            if (!_live || e.Beat.Strength < strengthThreshold) return;
            double now = AudioSettings.dspTime;
            if (now - _lastBigBeat < minInterval) return;
            _lastBigBeat = now;

            Vector3 stagePos = stageAnchor != null ? stageAnchor.position : Vector3.zero;
            ShockwaveVfx.Spawn(stagePos, shockwaveRadius);
            SpawnWord(stagePos + Vector3.up * 3f + new Vector3(Random.Range(-2f, 2f), 0f, 0f));
        }

        private void OnAbilityFired(AbilityFired e)
        {
            if (!_live || e.Multiplier < multiplierThreshold) return;
            SpawnWord(Vector3.up * 2.5f + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)));
        }

        private void SpawnWord(Vector3 pos)
        {
            string w = Words[Random.Range(0, Words.Length)];
            Color c = Accents[Random.Range(0, Accents.Length)];
            OnomatopoeiaVfx.Spawn(w, c, pos);
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: no Console errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/BeatVfxController.cs"
git commit -m "feat(vfx): BeatVfxController conductor — big-beat + great-hit triggers (M2c)"
```

---

### Task 5: Wire into GameBootstrap

**Files:**
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`

- [ ] **Step 1: Add the serialized field**

After the `setController` field (line 21):

```csharp
        [SerializeField] private BeatVfxController beatVfx;
```

- [ ] **Step 2: Add to the null-check**

Replace the existing null-check condition so it also covers `beatVfx`:

```csharp
            if (analyzer == null || crowd == null || hype == null || ability == null
                || economy == null || upgrades == null || setController == null || beatVfx == null)
```

- [ ] **Step 3: Initialize it**

After `setController.Initialize(Bus);` (line 44):

```csharp
            beatVfx.Initialize(Bus);
```

- [ ] **Step 4: Verify compile + Domain regression**

Expected: no Console errors. Then:

```bash
dotnet test PitTycoon.Domain.slnx
```
Expected: PASS (50 tests) — M2c hasn't touched the Domain.

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/GameBootstrap.cs"
git commit -m "feat(unity): wire BeatVfxController into the composition root (M2c)"
```

---

### Task 6: Extend FestivalSceneSetup to wire the VFX controller

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs`

- [ ] **Step 1: Call the wiring helper from BuildFestivalScene**

Immediately before the `var active = EditorSceneManager.GetActiveScene();` line in `BuildFestivalScene`, add:

```csharp
            WireBeatVfx(lit, stage);
```

- [ ] **Step 2: Add the helper methods**

Add these methods to the `FestivalSceneSetup` class (e.g. after `BuildFigurePrefab`):

```csharp
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

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }
```

- [ ] **Step 3: Verify compile**

Expected: Editor assembly recompiles with no errors. (`LoadOrCreateMat`, `MatDir`, and `stage` already exist in this file from M2b.)

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs"
git commit -m "feat(editor): wire BeatVfxController + VFX materials in Build Festival Scene (M2c)"
```

---

### Task 7: SETUP.md M2c section

**Files:**
- Modify: `SETUP.md` (append)

- [ ] **Step 1: Append the section**

Append to `SETUP.md`:

```markdown
## Milestone 2c — Manga beat-VFX

Prereq: M2a + M2b applied (comic look + festival scene).

1. Pull M2c, let Unity recompile (new VFX scripts).
2. With `Greybox.unity` open, re-run **Pit Tycoon → Build Festival Scene (M2b)** — it now also
   adds `BeatVfxController` to `Systems`, wires the stage anchor + whirlpool/coin materials, and
   points `GameBootstrap` at it.
3. Press **Play**. Expected: big beats throw a shockwave ring off the stage + an onomatopoeia
   pop (DON!/POW!); a PERFECT whirlpool throws a "POW!"; the whirlpool + coins read comic.

### Tuning (BeatVfxController on the Systems object)
- `Strength Threshold` — how loud a beat must be to fire (lower = more pops).
- `Multiplier Threshold` — how good an ability hit must be for its POW! (lower = more).
- `Min Interval` — seconds between big-beat pops (raise to thin them out).
- `Shockwave Radius` — how far the ring expands.
- `Whirlpool Material` / `Coin Material` — the comic accent materials.
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP M2c — beat-VFX wiring + tuning"
```

---

### Task 8: In-editor checkpoint (user)

**No file changes.** Manual visual gate.

- [ ] **Step 1: Run it**

Pull `feat/m2c-manga-beat-vfx`; let Unity recompile. Open `Greybox.unity`, re-run **Pit Tycoon → Build Festival Scene (M2b)**.

- [ ] **Step 2: Verify against the checklist**

Press Play and confirm:
- Big beats throw a shockwave ring from the stage + an onomatopoeia pop, **rate-limited** (not every beat).
- A PERFECT whirlpool throws a "POW!" at the pit.
- Whirlpool + coins read comic (accent-colored, inked by the existing passes).
- Onomatopoeia is readable at the camera distance and billboards to face it.
- **M1 loop intact**; no spam, frame rate fine.

- [ ] **Step 3: Tune & report**

Adjust the `BeatVfxController` dials to taste, grab a screenshot/clip, and report.

---

### Task 9: Commit generated assets + open PR #6

**After Task 8 confirms.** Re-running the menu created the VFX materials and re-saved the scene.

- [ ] **Step 1: Stage**

```bash
git status --short
git add "Assets/PitTycoon/Art/Materials/WhirlpoolMat.mat" "Assets/PitTycoon/Art/Materials/WhirlpoolMat.mat.meta" \
        "Assets/PitTycoon/Art/Materials/CoinMat.mat" "Assets/PitTycoon/Art/Materials/CoinMat.mat.meta" \
        "Assets/PitTycoon/Unity/OnomatopoeiaVfx.cs.meta" "Assets/PitTycoon/Unity/ShockwaveVfx.cs.meta" \
        "Assets/PitTycoon/Unity/BeatVfxController.cs.meta" "Assets/Scenes/Greybox.unity"
git status --short
```
Discard any line-ending-only churn (e.g. `ProjectSettings/ShaderGraphSettings.asset`, untouched materials) with `git checkout -- <file>`.

- [ ] **Step 2: Commit**

```bash
git commit -m "feat(vfx): generated comic VFX materials + scene wiring (M2c)"
```

- [ ] **Step 3: Push and open PR #6**

```bash
git push -u origin feat/m2c-manga-beat-vfx
gh pr create --base master --head feat/m2c-manga-beat-vfx \
  --title "M2c: manga beat-VFX (onomatopoeia, shockwaves, comic whirlpool/coins)" \
  --body "Punctuates big beats with comic VFX off the existing bus events: a BeatVfxController conductor throws onomatopoeia pops + stage shockwaves on big beats and a POW! on a PERFECT whirlpool, and restyles the whirlpool/coins to the comic look. All world-space, code-only, self-destructing — same pattern as the existing VFX. No gameplay change; Domain still 50 green; M1 loop intact. Speed lines + full-frame impact frames are parked in the backlog (spec §8). Spec: docs/superpowers/specs/2026-06-18-pit-tycoon-m2c-manga-beat-vfx-design.md."
```

---

## Self-Review

**1. Spec coverage:**
- Onomatopoeia pop → Task 1. ✓
- Shockwave ring off the stage → Task 2. ✓
- Restyle whirlpool/coins → Task 3. ✓
- Conductor with big-beat (strength + cooldown) and great-hit (multiplier) triggers, live-gated → Task 4. ✓
- Wiring (GameBootstrap Initialize + editor stage-anchor/material wiring) → Tasks 5, 6. ✓
- World-space, code-only, no font/art import (builtin LegacyRuntime font, LineRenderer, primitives) → Tasks 1, 2. ✓
- Verification: no automated tests, manual checkpoint, Domain stays 50 green → header, Tasks 5 & 8. ✓
- Backlog (speed lines, impact frames) → not implemented, recorded in spec §8. ✓

**2. Placeholder scan:** No TBD/TODO. Every code step has complete content.

**3. Type/name consistency:** `BeatVfxController.Initialize(EventBus)` matches the call in Task 5. `WhirlpoolVfx.OverrideMaterial` / `CoinFlyVfx.OverrideMaterial` defined in Task 3, set in Task 4. `OnomatopoeiaVfx.Spawn(string, Color, Vector3)` and `ShockwaveVfx.Spawn(Vector3, float)` signatures match their callers in Task 4. Serialized field names (`stageAnchor`, `whirlpoolMaterial`, `coinMaterial`, `beatVfx`) match the `FindProperty` calls in Task 6. `LoadOrCreateMat`/`MatDir`/`stage`/`SetRef` reuse the M2b `FestivalSceneSetup` (SetRef added in Task 6). Bus event types (`BeatDetected`, `AbilityFired`, `SetStarted`, `SetEnded`) match `Events.cs`; `BeatInfo.Strength` and `AbilityFired.Multiplier` match their payloads.
