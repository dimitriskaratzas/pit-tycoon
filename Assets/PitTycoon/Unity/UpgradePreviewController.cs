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
        [SerializeField] private BuildSpotController buildSpots;

        [Header("Camera waypoints (filled by Build Upgrade Preview, tune in play)")]
        [SerializeField] private PreviewWaypoint[] waypoints;
        [SerializeField] private Vector3 abilityCamPosition = new Vector3(0f, 9f, -11f);
        [SerializeField] private Vector3 abilityCamEuler = new Vector3(30f, 0f, 0f);
        [SerializeField] private float flyDuration = 0.6f;

        [Header("Resting camera poses (filled by Build Festival Ground, tune in play)")]
        [Tooltip("Wide overview of the whole ground — the intermission resting pose.")]
        [SerializeField] private Vector3 surveyCamPosition = new Vector3(0f, 45f, -55f);
        [SerializeField] private Vector3 surveyCamEuler = new Vector3(40f, 0f, 0f);
        [Tooltip("Close to the stage/crowd — the live-set resting pose (= the camera's start pose).")]
        [SerializeField] private Vector3 liveCamPosition = new Vector3(0f, 9f, -11f);
        [SerializeField] private Vector3 liveCamEuler = new Vector3(20f, 0f, 0f);

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

        public void BeginBuildPreview(BuildSpot spot)
        {
            ClearGhost();
            if (buildSpots != null) buildSpots.PreviewSpot(spot.id);
            if (rig != null) rig.FlyTo(spot.cameraPosition, Quaternion.Euler(spot.cameraEuler), flyDuration);
        }

        /// <summary>Back out of the current preview without moving the camera.</summary>
        public void Cancel() => ClearGhost();

        /// <summary>Clear the preview and fly to the wide survey pose (⌂ Overview, intermission start).</summary>
        public void ReturnHome() => GoToSurvey();

        /// <summary>Fly to the wide survey pose (intermission). Clears any active ghost.</summary>
        public void GoToSurvey()
        {
            ClearGhost();
            if (rig != null) rig.FlyTo(surveyCamPosition, Quaternion.Euler(surveyCamEuler), flyDuration);
        }

        /// <summary>Fly to the close live pose (set start). Clears any active ghost so no set begins mid-ghost.</summary>
        public void GoToLive()
        {
            ClearGhost();
            if (rig != null) rig.FlyTo(liveCamPosition, Quaternion.Euler(liveCamEuler), flyDuration);
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
            if (buildSpots != null) buildSpots.ClearPreview();
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
