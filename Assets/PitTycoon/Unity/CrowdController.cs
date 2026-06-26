using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Organic pit: a pre-built pool of members that scale in front-to-back (from the
    /// stage outward) as the CrowdFill's Active count rises with hype. Owns one CrowdFill;
    /// the persistent Following ratchets up across sets (banked at set end). Reacts only
    /// through IAudioAnalyzer (never touches AudioSource) and reads hype via IHypeMeter.
    /// Exposes FillFraction (ICrowdMeter) so HypeSystem can scale its rate by how full it is.
    /// </summary>
    public sealed class CrowdController : MonoBehaviour, ICrowdMeter
    {
        [SerializeField] private int columns = 12;
        [SerializeField] private int startingCapacity = 84;
        [SerializeField] private int startingFollowing = 24;
        [SerializeField] private float spacing = 1.2f;
        [SerializeField] private float bounceHeight = 0.6f;
        [SerializeField] private float beatPop = 0.7f;
        [SerializeField] private float popDecayPerSecond = 2.5f;
        [SerializeField] private float bobSpeed = 7f;
        [Tooltip("How fast a member pops in/out as the pit fills (scale units/sec).")]
        [SerializeField] private float scaleInPerSecond = 3f;
        [SerializeField] private Material memberMaterial;
        [SerializeField] private GameObject memberPrefab;
        [SerializeField] private float rotationJitter = 18f;
        [SerializeField] private float scaleJitter = 0.12f;
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

            int startRows = Mathf.CeilToInt((float)startingCapacity / columns);
            float frontZ = (startRows - 1) * spacing * 0.5f;   // front row fixed near the stage
            float offsetX = (columns - 1) * spacing * 0.5f;

            for (int i = 0; i < n; i++)
            {
                int row = i / columns;                          // row 0 = front (near stage)
                int col = i % columns;

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

                go.name = $"Crowd_{row}_{col}";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    col * spacing - offsetX, 0f, frontZ - row * spacing);
                go.transform.localRotation = Quaternion.Euler(
                    0f, Random.Range(-rotationJitter, rotationJitter), 0f);

                float full = 1f + Random.Range(-scaleJitter, scaleJitter);
                float cur = (i < active) ? 1f : 0f;             // active members appear immediately
                _fullScale[i] = full;
                _curScale[i] = cur;
                go.transform.localScale = Vector3.one * (full * cur);
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

            int startRows = Mathf.CeilToInt((float)startingCapacity / columns);
            float frontZ = (startRows - 1) * spacing * 0.5f;
            float offsetX = (columns - 1) * spacing * 0.5f;

            for (int i = from; i < to; i++)
            {
                int row = i / columns;
                int col = i % columns;

                GameObject go = memberPrefab != null
                    ? Instantiate(memberPrefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"GhostCrowd_{row}_{col}";
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                if (ghostMaterial != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;

                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(col * spacing - offsetX, 0f, frontZ - row * spacing);
                go.transform.localScale = Vector3.one;
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

        private void Update()
        {
            if (_analyzer == null || _members == null || _fill == null) return;

            _pop = Mathf.MoveTowards(_pop, 0f, popDecayPerSecond * Time.deltaTime);

            if (_live && _hype != null)                         // fill only advances during a set
                _fill.Tick(_hype.HypeFraction);

            int active = _fill.ActiveCount;
            float intensity = _analyzer.Intensity01;
            float t = Time.time;

            for (int i = 0; i < _members.Length; i++)
            {
                Transform tr = _members[i];
                if (tr == null) continue;

                float target = (i < active) ? 1f : 0f;
                _curScale[i] = Mathf.MoveTowards(_curScale[i], target, scaleInPerSecond * Time.deltaTime);
                float s = _fullScale[i] * _curScale[i];
                tr.localScale = Vector3.one * s;

                bool visible = _curScale[i] > 0.05f;
                float bob = visible ? Mathf.Abs(Mathf.Sin(t * bobSpeed + i * 0.6f)) * bounceHeight * intensity : 0f;
                Vector3 p = tr.localPosition;
                p.y = bob + (visible ? _pop : 0f);
                tr.localPosition = p;
            }
        }
    }
}
