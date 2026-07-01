using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>What kind of effect a build spot grants when built.</summary>
    public enum BuildEffectKind
    {
        HypeRate,       // -> HypeSystem.RaiseRate(effectMagnitude)
        Capacity,       // -> CrowdController.RaiseCapacity(round(effectMagnitude))
        PassiveCash,    // -> EconomySystem.AddPassiveIncome(round(effectMagnitude)): flat cash per set-end
        CashMultiplier, // -> EconomySystem.AddCashMultiplier(effectMagnitude): +pct on hype->cash
        AbilitySpike    // -> AbilitySystem.AddSpikeBonus(targetAbilityId, effectMagnitude): +pct spike
    }

    /// <summary>One authored build spot on the festival ground: a structure prefab that rises
    /// at a fixed pose, the camera pose to inspect it, its cost, and the effect it grants.
    /// Pure data — no behavior.</summary>
    [System.Serializable]
    public struct BuildSpot
    {
        public string id;                 // stable identity (used for built-state + StructureBuilt)
        public string label;              // shop row label
        public GameObject structurePrefab;
        public Vector3 position;          // WORLD position the structure/ghost is placed at
        public Vector3 euler;             // WORLD rotation (euler degrees)
        public Vector3 cameraPosition;    // camera fly-to pose for preview
        public Vector3 cameraEuler;
        public int cost;
        public BuildEffectKind effect;
        public float effectMagnitude;     // rate delta, or capacity (rounded to int)
        public string targetAbilityId;    // AbilitySpike only: the Ability.Id to boost (e.g. "woofer"); empty otherwise
        public Color color;               // optional row tint
    }

    /// <summary>The data-driven festival-ground layout: the set of build spots. One asset =
    /// one theme (M4a ships an open-air layout). Reskinning later = a new asset + prefabs,
    /// no code changes.</summary>
    [CreateAssetMenu(menuName = "Pit Tycoon/Venue Layout", fileName = "VenueLayout")]
    public sealed class VenueLayout : ScriptableObject
    {
        public BuildSpot[] spots;
    }
}
