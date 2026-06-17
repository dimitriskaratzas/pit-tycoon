using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Greybox whirlpool: a flattened cylinder that scales up and spins, then
    /// self-destructs. Spawned at runtime (no scene wiring). Bigger when fired
    /// closer to the beat, so the on-beat reward is visible.
    /// </summary>
    public sealed class WhirlpoolVfx : MonoBehaviour
    {
        private float _life;
        private float _maxLife = 0.7f;
        private float _maxScale = 4f;
        private const float SpinDegPerSec = 720f;

        /// <param name="onBeat01">0 = off-beat, 1 = perfect on-beat (sizes the VFX).</param>
        public static void Spawn(Vector3 pos, float onBeat01)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "WhirlpoolVFX";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = pos + Vector3.up * 0.1f;
            go.transform.localScale = new Vector3(0.1f, 0.05f, 0.1f);
            var fx = go.AddComponent<WhirlpoolVfx>();
            fx._maxScale = Mathf.Lerp(2.5f, 6f, Mathf.Clamp01(onBeat01));
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = _life / _maxLife;
            if (t >= 1f) { Destroy(gameObject); return; }
            float s = Mathf.Lerp(0.1f, _maxScale, t);
            transform.localScale = new Vector3(s, 0.05f, s);
            transform.Rotate(0f, SpinDegPerSec * Time.deltaTime, 0f, Space.World);
        }
    }
}
