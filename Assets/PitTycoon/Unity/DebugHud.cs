using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Greybox on-screen readout: intensity bar + a flash on each detected beat.
    /// IMGUI (OnGUI) so it needs no Canvas wiring during M1.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private WhirlpoolAbility ability;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private SetController setController;
        [SerializeField] private UpgradeSystem upgrades;

        private double _lastBeat = -1.0;
        private float _flash;

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
        }

        private void OnGUI()
        {
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

            if (hype != null)
            {
                float frac = hype.Ceiling > 0f ? Mathf.Clamp01(hype.Current / hype.Ceiling) : 0f;
                GUI.Label(new Rect(12, 180, 320, 20), $"Hype: {hype.Current:0} / {hype.Ceiling:0}");
                GUI.Box(new Rect(12, 202, 260, 22), GUIContent.none);
                Color prevHype = GUI.color;
                GUI.color = new Color(0.3f, 0.9f, 0.5f, 1f);
                GUI.Box(new Rect(12, 202, 260f * frac, 22), GUIContent.none);
                GUI.color = prevHype;
            }

            if (ability != null)
            {
                bool ready = ability.Ready;
                float cd = ability.CooldownRemaining;
                string label = ready ? "WHIRLPOOL (Space)" : $"Whirlpool {cd:0.0}s";
                GUI.enabled = ready;
                if (GUI.Button(new Rect(12, 234, 200, 30), label)) ability.Fire();
                GUI.enabled = true;
                if (ability.LastQuality != WhirlpoolAbility.HitQuality.None)
                {
                    Color prevQ = GUI.color;
                    GUI.color = ability.LastQuality switch
                    {
                        WhirlpoolAbility.HitQuality.Perfect => new Color(0.3f, 1f, 0.4f),
                        WhirlpoolAbility.HitQuality.Good => new Color(1f, 0.9f, 0.3f),
                        _ => new Color(0.7f, 0.7f, 0.7f),
                    };
                    string q = ability.LastQuality.ToString().ToUpperInvariant();
                    GUI.Label(new Rect(220, 238, 170, 22), $"{q}  x{ability.LastMultiplier:0.0}");
                    GUI.color = prevQ;
                }
            }

            float rx = Screen.width - 220;
            if (economy != null)
                GUI.Label(new Rect(rx, 12, 200, 22), $"Cash: ${economy.Cash}");
            if (setController != null)
            {
                GUI.Label(new Rect(rx, 34, 200, 22), $"Set {setController.SetNumber} — {setController.Current}");
                if (setController.Current == SetController.Phase.Intermission && setController.LastCashEarned > 0)
                    GUI.Label(new Rect(rx, 56, 200, 22), $"Banked: +${setController.LastCashEarned}");
            }

            if (setController != null && setController.Current == SetController.Phase.Intermission
                && upgrades != null)
            {
                const float px = 12, py = 290, pw = 320;
                GUI.Box(new Rect(px, py, pw, 110), "INTERMISSION");
                var upg = upgrades.CapacityUpgrade;
                if (upg != null)
                {
                    bool afford = upgrades.CanAfford(upg);
                    GUI.enabled = afford;
                    if (GUI.Button(new Rect(px + 10, py + 26, pw - 20, 30),
                            $"Buy {upg.DisplayName} (${upg.Cost})  +{upg.AddColumns}x{upg.AddRows} crowd"))
                        upgrades.TryPurchase(upg);
                    GUI.enabled = true;
                    if (!afford)
                        GUI.Label(new Rect(px + 10, py + 58, pw - 20, 20), "Not enough cash.");
                }
                if (GUI.Button(new Rect(px + 10, py + 76, pw - 20, 28), "Start Next Set ▶"))
                    setController.StartNextSet();
            }
        }
    }
}
