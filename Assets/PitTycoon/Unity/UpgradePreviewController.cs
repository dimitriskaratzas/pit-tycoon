using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Orchestrates the intermission upgrade ghost-preview: flies the camera to a per-kind
    /// waypoint and asks the owning system to show its ghost. Owns no ghost geometry itself —
    /// VenueController/CrowdController/AbilitySystem each render their own. Driven entirely by
    /// ShopView (no event subscriptions). Camera only moves on a new selection or an explicit
    /// ReturnHome; Cancel leaves the camera where it is.
    /// </summary>
    public sealed class UpgradePreviewController : MonoBehaviour
    {
        [System.Serializable]
        public struct PreviewWaypoint
        {
            public UpgradeKind kind;
            public Vector3 position;
            public Vector3 euler;
        }

        [SerializeField] private CameraRig rig;
        [SerializeField] private VenueController venue;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private AbilitySystem abilities;

        [Header("Camera waypoints (filled by Build Upgrade Preview, tune in play)")]
        [SerializeField] private PreviewWaypoint[] waypoints;
        [SerializeField] private Vector3 abilityCamPosition = new Vector3(0f, 9f, -11f);
        [SerializeField] private Vector3 abilityCamEuler = new Vector3(30f, 0f, 0f);
        [SerializeField] private float flyDuration = 0.6f;

        public void BeginUpgradePreview(UpgradeDefinition def, int nextLevel)
        {
            if (def == null) return;
            ClearGhost();
            ShowGhost(def, nextLevel);
            FlyToKind(def.Kind);
        }

        public void BeginAbilityPreview(AbilityDefinition def)
        {
            if (def == null) return;
            ClearGhost();
            if (abilities != null) abilities.PlayDemo(def);
            if (rig != null) rig.FlyTo(abilityCamPosition, Quaternion.Euler(abilityCamEuler), flyDuration);
        }

        /// <summary>Back out of the current preview without moving the camera.</summary>
        public void Cancel() => ClearGhost();

        /// <summary>Clear the preview and tween the camera back to the overview.</summary>
        public void ReturnHome()
        {
            ClearGhost();
            if (rig != null) rig.ReturnHome();
        }

        /// <summary>Clear the preview and snap the camera home instantly (set start).</summary>
        public void ForceClear()
        {
            ClearGhost();
            if (rig != null) rig.SnapHome();
        }

        private void OnDestroy() => ClearGhost();

        private void ShowGhost(UpgradeDefinition def, int level)
        {
            if (def.Kind == UpgradeKind.Grounds)
            {
                if (crowd != null) crowd.PreviewCapacity(def.AddCapacity);
            }
            else
            {
                if (venue != null) venue.Preview(def.Kind, level);
            }
        }

        private void ClearGhost()
        {
            if (venue != null) venue.ClearPreview();
            if (crowd != null) crowd.ClearPreview();
        }

        private void FlyToKind(UpgradeKind kind)
        {
            if (rig == null || waypoints == null) return;
            for (int i = 0; i < waypoints.Length; i++)
                if (waypoints[i].kind == kind)
                {
                    rig.FlyTo(waypoints[i].position, Quaternion.Euler(waypoints[i].euler), flyDuration);
                    return;
                }
        }
    }
}
