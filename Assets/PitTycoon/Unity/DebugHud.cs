using UnityEngine;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Dev-only IMGUI overlay (toggle with F1, default off): analyzer intensity bar, a flash on
    /// each detected beat, and the last beat's dsp time. Also a skip-to-shop cheat for fast
    /// iteration (F3, or the overlay button): ends the current live set and grants debugCash so
    /// the intermission shop can be tested without playing a whole set. The player-facing UI is
    /// UGUI (HudController/LiveHudView/ShopView); this exists purely for tuning + testing.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;

        [Header("Skip-to-shop cheat (dev only)")]
        [SerializeField] private SetController setController;
        [SerializeField] private EconomySystem economy;
        [SerializeField, Tooltip("Cash the balance is set to when skipping to the shop.")]
        private int debugCash = 99999;

        private double _lastBeat = -1.0;
        private float _flash;
        private bool _show;

        private void Awake()
        {
            // Dev convenience: auto-find the skip-to-shop refs if the scene didn't wire them,
            // so the cheat works on an existing scene without re-running setup.
            if (setController == null) setController = FindFirstObjectByType<SetController>();
            if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        }

        private void OnEnable()
        {
            if (analyzer != null) analyzer.BeatDetected += OnBeat;
        }

        private void OnDisable()
        {
            if (analyzer != null) analyzer.BeatDetected -= OnBeat;
        }

        private void OnBeat(BeatInfo beat)
        {
            _lastBeat = beat.DspTime;
            _flash = Mathf.Clamp01(0.4f + beat.Strength);
        }

        private void Update()
        {
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 4f);
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) _show = !_show;
            if (kb.f3Key.wasPressedThisFrame) SkipToShop();
        }

        /// <summary>Ends the live set and grants debugCash so the shop can be tested immediately.
        /// No-op when not in a live set. Called by the F3 hotkey and the overlay button.</summary>
        private void SkipToShop()
        {
            if (setController == null || setController.Current != SetController.Phase.Live) return;
            setController.DebugSkipSet();          // fires SetEnded -> HUD opens the shop
            if (economy != null) economy.SetCash(debugCash);
        }

        private void OnGUI()
        {
            if (!_show) return;

            if (analyzer == null)
            {
                GUI.Label(new Rect(12, 12, 360, 20), "DebugHud: analyzer not assigned");
                return;
            }

            float intensity = analyzer.Intensity01;
            GUI.Label(new Rect(12, 12, 300, 20), $"Intensity: {intensity:0.00}");
            GUI.Box(new Rect(12, 34, 200, 16), GUIContent.none);
            GUI.Box(new Rect(12, 34, 200f * Mathf.Clamp01(intensity), 16), GUIContent.none);

            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f, _flash);
            GUI.Box(new Rect(12, 56, 90, 90), "BEAT");
            GUI.color = prev;

            GUI.Label(new Rect(12, 152, 320, 20), $"Last beat dsp: {_lastBeat:0.00}");

            if (setController != null && setController.Current == SetController.Phase.Live)
            {
                if (GUI.Button(new Rect(12, 178, 200, 26), $"⏭ Skip to Shop (+${debugCash})"))
                    SkipToShop();
            }
        }
    }
}
