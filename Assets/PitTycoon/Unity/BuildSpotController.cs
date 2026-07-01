using System.Collections.Generic;
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Owns the build-spot geometry on the festival ground. Reads a VenueLayout, raises the real
    /// structure at a spot on Build, and shows a translucent ghost clone on PreviewSpot (reusing
    /// the shared ghost material). Mirrors VenueController's ghost discipline: clear before
    /// re-preview, clear on destroy. Owns no economy — BuildSystem drives purchases.
    /// </summary>
    public sealed class BuildSpotController : MonoBehaviour
    {
        [SerializeField] private VenueLayout layout;
        [Tooltip("Translucent material for ghost-preview clones (wired by Build Festival Ground).")]
        [SerializeField] private Material ghostMaterial;

        private readonly HashSet<string> _built = new HashSet<string>();
        private readonly Dictionary<string, GameObject> _raised = new Dictionary<string, GameObject>();
        private readonly List<GameObject> _ghosts = new List<GameObject>();

        public VenueLayout Layout => layout;
        public bool IsBuilt(string id) => _built.Contains(id);

        /// <summary>Raise the real structure at a spot. Idempotent (no-op if already built).</summary>
        public void Build(string id)
        {
            if (layout == null || _built.Contains(id)) return;
            if (!TryGet(id, out BuildSpot spot) || spot.structurePrefab == null) return;
            ClearPreview();
            GameObject go = Instantiate(spot.structurePrefab, transform);
            go.name = string.IsNullOrEmpty(spot.id) ? "Structure" : spot.id;
            go.transform.SetPositionAndRotation(spot.position, Quaternion.Euler(spot.euler));
            _raised[id] = go;
            _built.Add(id);
        }

        /// <summary>Show a translucent ghost of the structure at an un-built spot. Idempotent:
        /// clears any prior preview first.</summary>
        public void PreviewSpot(string id)
        {
            if (layout == null || _built.Contains(id)) return;
            if (!TryGet(id, out BuildSpot spot) || spot.structurePrefab == null) return;
            ClearPreview();
            GameObject ghost = Instantiate(spot.structurePrefab, transform);
            ghost.name = spot.id + " (ghost)";
            ghost.transform.SetPositionAndRotation(spot.position, Quaternion.Euler(spot.euler));
            foreach (var c in ghost.GetComponentsInChildren<Collider>()) Destroy(c);
            if (ghostMaterial != null)
                foreach (var r in ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;
            _ghosts.Add(ghost);
        }

        /// <summary>Destroy any active ghost clones.</summary>
        public void ClearPreview()
        {
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts[i] != null) Destroy(_ghosts[i]);
            _ghosts.Clear();
        }

        private void OnDestroy() => ClearPreview();

        private bool TryGet(string id, out BuildSpot spot)
        {
            if (layout != null && layout.spots != null)
                foreach (var s in layout.spots)
                    if (s.id == id) { spot = s; return true; }
            spot = default;
            return false;
        }
    }
}
