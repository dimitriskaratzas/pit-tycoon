using UnityEngine;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Dev-only IMGUI overlay (toggle with F1, default off): analyzer intensity bar, a flash on
    /// each detected beat, and the last beat's dsp time. The player-facing UI is now UGUI
    /// (HudController/LiveHudView/ShopView); this exists purely for tuning the analyzer.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;

        private double _lastBeat = -1.0;
        private float _flash;
        private bool _show;

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
            if (kb != null && kb.f1Key.wasPressedThisFrame) _show = !_show;
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
        }
    }
}
