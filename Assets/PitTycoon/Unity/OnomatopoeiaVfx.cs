using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Self-spawning comic onomatopoeia pop (e.g. "DON!"). World-space TextMesh with a
    /// black offset copy for an inked drop-shadow; punches in, rises, fades, billboards to
    /// the camera, then self-destructs. Runtime-only, no scene wiring. Font is the builtin
    /// LegacyRuntime font so there's no asset import.
    /// </summary>
    public sealed class OnomatopoeiaVfx : MonoBehaviour
    {
        private const float MaxLife = 0.85f;
        private float _life;
        private TextMesh[] _texts;

        public static void Spawn(string word, Color color, Vector3 pos)
        {
            var root = new GameObject("Onomatopoeia");
            root.transform.position = pos;
            var fx = root.AddComponent<OnomatopoeiaVfx>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            TextMesh MakeText(string name, Color c, Vector3 localOffset)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = localOffset;
                var tm = go.AddComponent<TextMesh>();
                tm.text = word; tm.font = font; tm.fontSize = 96; tm.fontStyle = FontStyle.Bold;
                tm.characterSize = 0.12f; tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center; tm.color = c;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                return tm;
            }

            // shadow sits slightly behind (local +z = away from camera after billboard) and offset.
            var shadow = MakeText("ink", Color.black, new Vector3(0.06f, -0.06f, 0.02f));
            var front = MakeText("front", color, Vector3.zero);
            fx._texts = new[] { shadow, front };
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = _life / MaxLife;
            if (t >= 1f) { Destroy(gameObject); return; }

            if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;

            float punch = t < 0.25f
                ? Mathf.Lerp(0.3f, 1.15f, t / 0.25f)
                : Mathf.Lerp(1.15f, 1f, (t - 0.25f) / 0.75f);
            transform.localScale = Vector3.one * punch;
            transform.position += Vector3.up * (0.6f * Time.deltaTime);

            float a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            foreach (var tm in _texts) { var c = tm.color; c.a = a; tm.color = c; }
        }
    }
}
