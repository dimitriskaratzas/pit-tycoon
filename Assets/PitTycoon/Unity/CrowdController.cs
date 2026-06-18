using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Greybox crowd: a grid of capsules that bob with intensity and pop on beats.
    /// Reacts only through the IAudioAnalyzer interface (never touches AudioSource).
    /// Grid size will later be driven by the capacity upgrade (Plan 3).
    /// </summary>
    public sealed class CrowdController : MonoBehaviour
    {
        [SerializeField] private int columns = 12;
        [SerializeField] private int rows = 7;
        [SerializeField] private float spacing = 1.2f;
        [SerializeField] private float baseHeight = 1f;
        [SerializeField] private float bounceHeight = 0.6f;
        [SerializeField] private float beatPop = 0.7f;
        [SerializeField] private float popDecayPerSecond = 2.5f;
        [SerializeField] private float bobSpeed = 7f;
        [SerializeField] private Material memberMaterial;
        [SerializeField] private GameObject memberPrefab;
        [SerializeField] private float rotationJitter = 18f;
        [SerializeField] private float scaleJitter = 0.12f;

        private IAudioAnalyzer _analyzer;
        private Transform[] _members;
        private float _pop;

        /// <summary>Wire the analyzer (called by GameBootstrap before Build).</summary>
        public void Initialize(IAudioAnalyzer analyzer)
        {
            if (_analyzer != null) _analyzer.BeatDetected -= OnBeat;
            _analyzer = analyzer;
            if (_analyzer != null) _analyzer.BeatDetected += OnBeat;
        }

        private void OnDestroy()
        {
            if (_analyzer != null) _analyzer.BeatDetected -= OnBeat;
        }

        /// <summary>Increase grid size (capacity upgrade). Visible after the next Build().</summary>
        public void Grow(int addColumns, int addRows)
        {
            columns = Mathf.Clamp(columns + addColumns, 1, 40);
            rows = Mathf.Clamp(rows + addRows, 1, 30);
        }

        public int MemberCount => columns * rows;

        private void OnBeat(BeatInfo beat)
        {
            _pop = Mathf.Max(_pop, beatPop * Mathf.Clamp01(0.4f + beat.Strength));
        }

        /// <summary>One-shot crowd jolt (an ability fired). Visible on the next Update.</summary>
        public void Pop(float strength)
        {
            _pop = Mathf.Max(_pop, strength);
        }

        /// <summary>Build (or rebuild) the greybox crowd grid as child capsules.</summary>
        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _members = new Transform[columns * rows];
            float offsetX = (columns - 1) * spacing * 0.5f;
            float offsetZ = (rows - 1) * spacing * 0.5f;
            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
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
                    go.name = $"Crowd_{r}_{c}";
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(
                        c * spacing - offsetX, 0f, r * spacing - offsetZ);
                    go.transform.localRotation = Quaternion.Euler(0f, Random.Range(-rotationJitter, rotationJitter), 0f);
                    go.transform.localScale = Vector3.one * (1f + Random.Range(-scaleJitter, scaleJitter));
                    _members[idx++] = go.transform;
                }
            }
        }

        private void Update()
        {
            if (_analyzer == null || _members == null) return;

            _pop = Mathf.MoveTowards(_pop, 0f, popDecayPerSecond * Time.deltaTime);
            float intensity = _analyzer.Intensity01;
            float t = Time.time;

            for (int i = 0; i < _members.Length; i++)
            {
                Transform tr = _members[i];
                if (tr == null) continue;
                float phase = i * 0.6f;
                float bob = Mathf.Abs(Mathf.Sin(t * bobSpeed + phase)) * bounceHeight * intensity;
                Vector3 p = tr.localPosition; p.y = bob + _pop; tr.localPosition = p;
            }
        }
    }
}
