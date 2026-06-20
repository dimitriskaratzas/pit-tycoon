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
        [SerializeField] private AbilitySystem abilities;
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

            if (abilities != null && setController != null
                && setController.Current == SetController.Phase.Live)
            {
                float ay = 234f;
                foreach (var a in abilities.Abilities)
                {
                    if (!a.Owned) continue;
                    var def = abilities.DefinitionOf(a);
                    bool ready = a.CanFire;
                    string label = ready ? def.DisplayName : $"{def.DisplayName} {a.CooldownRemaining:0.0}s";
                    GUI.enabled = ready;
                    if (GUI.Button(new Rect(12, ay, 200, 28), label)) abilities.TryFire(a);
                    GUI.enabled = true;
                    ay += 32f;
                }
                if (abilities.LastQuality != HitQuality.None)
                {
                    Color prevQ = GUI.color;
                    GUI.color = abilities.LastQuality switch
                    {
                        HitQuality.Perfect => new Color(0.3f, 1f, 0.4f),
                        HitQuality.Good => new Color(1f, 0.9f, 0.3f),
                        _ => new Color(0.7f, 0.7f, 0.7f),
                    };
                    GUI.Label(new Rect(220, 238, 180, 22), abilities.LastQuality.ToString().ToUpperInvariant());
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

            if (setController != null && setController.Current == SetController.Phase.Intermission)
            {
                const float px = 12, pw = 320;
                float py = 290;

                int upgradeRows = upgrades != null ? upgrades.Upgrades.Count : 0;
                int abilityRows = 0;
                if (abilities != null)
                    foreach (var a in abilities.Abilities) if (!a.Owned) abilityRows++;
                float boxH = 44f + (upgradeRows + abilityRows + 1) * 30f;
                GUI.Box(new Rect(px, py, pw, boxH), "INTERMISSION");

                float y = py + 26f;
                if (upgrades != null)
                {
                    foreach (var u in upgrades.Upgrades)
                    {
                        int cost = upgrades.CurrentCost(u);
                        int lvl = upgrades.LevelOf(u);
                        bool afford = upgrades.CanAfford(u);
                        GUI.enabled = afford;
                        if (GUI.Button(new Rect(px + 10, y, pw - 20, 26), $"{u.DisplayName}  Lv{lvl} (${cost})"))
                            upgrades.TryPurchase(u);
                        GUI.enabled = true;
                        y += 30f;
                    }
                }
                if (abilities != null)
                {
                    foreach (var a in abilities.Abilities)
                    {
                        if (a.Owned) continue;
                        var def = abilities.DefinitionOf(a);
                        bool afford = abilities.CanAfford(def);
                        GUI.enabled = afford;
                        if (GUI.Button(new Rect(px + 10, y, pw - 20, 26), $"Buy {def.DisplayName} (${def.Cost})"))
                            abilities.TryUnlock(def);
                        GUI.enabled = true;
                        y += 30f;
                    }
                }
                if (GUI.Button(new Rect(px + 10, y, pw - 20, 28), "Start Next Set ▶"))
                    setController.StartNextSet();
            }
        }
    }
}
