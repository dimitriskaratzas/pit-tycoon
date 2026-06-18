using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Self-spawning screen flash for the Light-burst ability. Code-only (IMGUI overlay),
    /// brief and capped below full white for comfort; player-triggered + cooldown-gated, so
    /// it avoids the every-beat concern that parked the M2c full-frame impact frames.
    /// </summary>
    public sealed class LightBurstVfx : MonoBehaviour
    {
        private const float MaxLife = 0.35f;
        private const float PeakAlpha = 0.55f;
        private float _life;
        private Color _color = new Color(1f, 0.95f, 0.7f);

        public static void Flash() => Flash(new Color(1f, 0.95f, 0.7f));

        public static void Flash(Color color)
        {
            var go = new GameObject("LightBurst");
            go.AddComponent<LightBurstVfx>()._color = color;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (_life >= MaxLife) Destroy(gameObject);
        }

        private void OnGUI()
        {
            float a = Mathf.Clamp01(1f - _life / MaxLife) * PeakAlpha;
            Color prev = GUI.color;
            GUI.color = new Color(_color.r, _color.g, _color.b, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
