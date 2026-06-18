using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Self-spawning shockwave: a flat ink ring (LineRenderer loop on the XZ plane) that
    /// expands outward from the stage and fades. Runtime-only, no scene wiring.
    /// </summary>
    public sealed class ShockwaveVfx : MonoBehaviour
    {
        private const int Segments = 48;
        private const float MaxLife = 0.6f;
        private static Material _ringMat;

        private LineRenderer _lr;
        private float _life;
        private float _maxRadius = 9f;

        public static void Spawn(Vector3 pos, float maxRadius)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = pos + Vector3.up * 0.05f;
            var fx = go.AddComponent<ShockwaveVfx>();
            fx._maxRadius = maxRadius;

            var lr = go.AddComponent<LineRenderer>();
            if (_ringMat == null) _ringMat = new Material(Shader.Find("Sprites/Default"));
            lr.material = _ringMat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = Segments;
            lr.widthMultiplier = 0.35f;
            lr.numCapVertices = 2;
            lr.startColor = lr.endColor = new Color(0.08f, 0.06f, 0.10f, 1f);
            fx._lr = lr;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = _life / MaxLife;
            if (t >= 1f) { Destroy(gameObject); return; }

            float r = Mathf.Lerp(0.2f, _maxRadius, t);
            for (int i = 0; i < Segments; i++)
            {
                float a = (i / (float)Segments) * Mathf.PI * 2f;
                _lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            _lr.widthMultiplier = Mathf.Lerp(0.35f, 0.02f, t);
            var col = new Color(0.08f, 0.06f, 0.10f, 1f - t);
            _lr.startColor = _lr.endColor = col;
        }
    }
}
