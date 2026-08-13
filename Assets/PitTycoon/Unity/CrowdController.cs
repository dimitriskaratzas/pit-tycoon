using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Organic pit: a pre-built pool of members that scale in front-to-back (from the
    /// stage outward) as the CrowdFill's Active count rises with hype. Owns one CrowdFill;
    /// the persistent Following ratchets up across sets (banked at set end). Reacts only
    /// through IAudioAnalyzer (never touches AudioSource) and reads hype via IHypeMeter.
    /// Members are animated by a hype-blended Animator (Energy = analyzer intensity) with
    /// beat pops on the transform. Exposes FillFraction (ICrowdMeter) so HypeSystem can scale
    /// its rate by how full it is.
    /// </summary>
    public sealed class CrowdController : MonoBehaviour, ICrowdMeter
    {
        [SerializeField] private int columns = 12;
        [SerializeField] private int startingCapacity = 84;
        [SerializeField] private int startingFollowing = 24;
        [SerializeField] private float spacing = 1.2f;
        [SerializeField] private float beatPop = 0.7f;
        [SerializeField] private float popDecayPerSecond = 2.5f;
        [Tooltip("How fast a member pops in/out as the pit fills (scale units/sec).")]
        [SerializeField] private float scaleInPerSecond = 3f;
        [SerializeField] private Material memberMaterial;
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
        [SerializeField] private float rotationJitter = 18f;
        [SerializeField] private float scaleJitter = 0.12f;
        [Tooltip("Per-member scatter off the grid, as a fraction of spacing. 0 = the old rigid lattice.")]
        [SerializeField] private float positionJitter = 0.3f;
        [Tooltip("How much wider each row's gap gets further from the stage. 0 = uniform rows.")]
        [SerializeField] private float rowSpacingFalloff = 0.06f;
        [Tooltip("Translucent material for ghost-preview members (wired by Build Upgrade Preview).")]
        [SerializeField] private Material ghostMaterial;

        private IAudioAnalyzer _analyzer;
        private IHypeMeter _hype;
        private EventBus _bus;
        private CrowdFill _fill;

        private Transform[] _members;
        private float[] _fullScale;   // per-member uniform scale (with jitter)
        private float[] _curScale;    // per-member 0..1 scale-in progress
        private float _pop;
        private bool _live;
        private readonly System.Collections.Generic.List<GameObject> _ghosts =
            new System.Collections.Generic.List<GameObject>();

        private Animator[] _animators;    // per-member; null where the prefab has no Animator
        private static readonly int EnergyParam = Animator.StringToHash("Energy");
        private static readonly int DanceState = Animator.StringToHash("Dance");
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private float[] _popJitter;  // per-member beat-pop height factor, so hops aren't a unison wave

        /// <summary>ICrowdMeter: how full the pit is, 0..1.</summary>
        public float FillFraction => _fill?.FillFraction ?? 0f;

        /// <summary>Wire dependencies (called once by GameBootstrap before the first set).</summary>
        public void Initialize(IAudioAnalyzer analyzer, IHypeMeter hype, EventBus bus)
        {
            if (_analyzer != null) _analyzer.BeatDetected -= OnBeat;
            _analyzer = analyzer;
            if (_analyzer != null) _analyzer.BeatDetected += OnBeat;

            _hype = hype;

            if (_bus != null)
            {
                _bus.Unsubscribe<SetStarted>(OnSetStarted);
                _bus.Unsubscribe<SetEnded>(OnSetEnded);
            }
            _bus = bus;
            if (_bus != null)
            {
                _bus.Subscribe<SetStarted>(OnSetStarted);
                _bus.Subscribe<SetEnded>(OnSetEnded);
            }

            _fill ??= new CrowdFill(startingCapacity, startingFollowing);
        }

        private void OnDestroy()
        {
            ClearPreview();
            if (_analyzer != null) _analyzer.BeatDetected -= OnBeat;
            if (_bus != null)
            {
                _bus.Unsubscribe<SetStarted>(OnSetStarted);
                _bus.Unsubscribe<SetEnded>(OnSetEnded);
            }
        }

        private void OnSetStarted(SetStarted e) { _fill.BeginSet(); _live = true; }
        private void OnSetEnded(SetEnded e) { _live = false; _fill.BankSet(); }

        private void OnBeat(BeatInfo beat)
        {
            _pop = Mathf.Max(_pop, beatPop * Mathf.Clamp01(0.4f + beat.Strength));
        }

        /// <summary>One-shot crowd jolt (an ability fired). Visible on the next Update.</summary>
        public void Pop(float strength)
        {
            _pop = Mathf.Max(_pop, strength);
        }

        /// <summary>Grounds upgrade: raise capacity, then rebuild so the new (empty) room shows.</summary>
        public void RaiseCapacity(int delta)
        {
            if (_fill == null) _fill = new CrowdFill(startingCapacity, startingFollowing);
            _fill.RaiseCapacity(delta);
            Build();
        }

        /// <summary>Build (or rebuild) the member pool: all Capacity members, active ones at full
        /// scale, the rest hidden at scale 0 (no flicker on rebuild).</summary>
        public void Build()
        {
            ClearPreview();
            if (_fill == null) _fill = new CrowdFill(startingCapacity, startingFollowing);

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            int n = _fill.Capacity;
            int active = _fill.ActiveCount;
            _members = new Transform[n];
            _fullScale = new float[n];
            _curScale = new float[n];
            _animators = new Animator[n];
            _popJitter = new float[n];

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
        }

        /// <summary>Show ghost members in the slots a capacity expansion of <paramref name="delta"/>
        /// would add (rows past the current Capacity). Does not touch the fill. Idempotent.</summary>
        public void PreviewCapacity(int delta)
        {
            if (_fill == null || delta <= 0) return;
            ClearPreview();

            int from = _fill.Capacity;
            int to = from + delta;

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
        }

        /// <summary>Destroy any ghost-preview members.</summary>
        public void ClearPreview()
        {
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts[i] != null) Destroy(_ghosts[i]);
            _ghosts.Clear();
        }

        /// <summary>Layout parameters for CrowdLayout. Shared by Build and PreviewCapacity so a
        /// ghost lands exactly where its real member will.</summary>
        private CrowdLayoutSettings LayoutSettings()
        {
            int startRows = Mathf.CeilToInt((float)startingCapacity / Mathf.Max(1, columns));
            return new CrowdLayoutSettings(columns, spacing, startRows,
                positionJitter, rowSpacingFalloff, rotationJitter, scaleJitter);
        }

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

        private void Update()
        {
            if (_analyzer == null || _members == null || _fill == null) return;

            _pop = Mathf.MoveTowards(_pop, 0f, popDecayPerSecond * Time.deltaTime);

            if (_live && _hype != null)                         // fill only advances during a set
                _fill.Tick(_hype.HypeFraction);

            int active = _fill.ActiveCount;
            // Dance energy tracks HYPE, not raw FFT intensity: the spectrum's energy sum has too
            // little dynamic range to sweep 0..1 (measured ~0.7 floor), while hype builds 0 -> 1
            // across a set — sway to groove to jumping as the set takes off. Beat pops keep the
            // direct music reactivity.
            float energy = _hype != null ? _hype.HypeFraction : _analyzer.Intensity01;

            for (int i = 0; i < _members.Length; i++)
            {
                Transform tr = _members[i];
                if (tr == null) continue;

                float target = (i < active) ? 1f : 0f;
                _curScale[i] = Mathf.MoveTowards(_curScale[i], target, scaleInPerSecond * Time.deltaTime);
                float s = _fullScale[i] * _curScale[i];
                tr.localScale = Vector3.one * s;

                bool visible = _curScale[i] > 0.05f;
                Vector3 p = tr.localPosition;
                float jitter = _popJitter != null && i < _popJitter.Length ? _popJitter[i] : 1f;
                p.y = visible ? _pop * jitter : 0f;     // clips own body motion; beat pops lift the root
                tr.localPosition = p;

                var anim = _animators != null && i < _animators.Length ? _animators[i] : null;
                if (anim != null && visible) anim.SetFloat(EnergyParam, energy);
            }
        }
    }
}
