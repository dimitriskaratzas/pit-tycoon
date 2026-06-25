using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Top-level HUD root. Initializes the two views, then toggles Live ↔ Shop on the set
    /// lifecycle: SetStarted shows the live HUD, SetEnded shows the intermission shop with the
    /// banked amount. Owns no widget logic itself.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private LiveHudView liveView;
        [SerializeField] private ShopView shopView;

        private EventBus _bus;

        public void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy,
                               SetController set, AbilitySystem abilities, UpgradeSystem upgrades)
        {
            _bus = bus;

            if (liveView != null) liveView.Initialize(bus, hype, economy, set, abilities);
            if (shopView != null) shopView.Initialize(bus, economy, set, abilities, upgrades);

            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);

            // Hidden until the first SetStarted (SetController.Start fires after all Awakes).
            if (liveView != null) liveView.Hide();
            if (shopView != null) shopView.Hide();
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
        }

        private void OnSetStarted(SetStarted e)
        {
            if (shopView != null) shopView.Hide();
            if (liveView != null) liveView.Show();
        }

        private void OnSetEnded(SetEnded e)
        {
            if (liveView != null) liveView.Hide();
            if (shopView != null) shopView.Show(e.CashEarned);
        }
    }
}
