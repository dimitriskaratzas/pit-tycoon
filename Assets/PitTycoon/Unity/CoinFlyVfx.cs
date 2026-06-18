using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Greybox "coins fly up" at set end: spawns small spheres that arc upward under
    /// gravity and self-destruct. Count scales with cash earned. Runtime-only, no wiring.
    /// </summary>
    public sealed class CoinFlyVfx : MonoBehaviour
    {
        /// <summary>Optional comic material applied to spawned coins (set by BeatVfxController).</summary>
        public static Material OverrideMaterial;

        private Vector3 _velocity;
        private float _life;
        private const float MaxLife = 1.3f;

        public static void Burst(int cashEarned, Vector3 origin)
        {
            int count = Mathf.Clamp(cashEarned / 5, 6, 40);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Coin";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                if (OverrideMaterial != null)
                {
                    var rend = go.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = OverrideMaterial;
                }
                go.transform.position = origin + Random.insideUnitSphere * 0.8f;
                go.transform.localScale = Vector3.one * 0.35f;
                var coin = go.AddComponent<CoinFlyVfx>();
                coin._velocity = new Vector3(
                    Random.Range(-2f, 2f), Random.Range(5f, 8f), Random.Range(-2f, 2f));
            }
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (_life >= MaxLife) { Destroy(gameObject); return; }
            _velocity += Physics.gravity * Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(180f * Time.deltaTime, 220f * Time.deltaTime, 0f);
        }
    }
}
