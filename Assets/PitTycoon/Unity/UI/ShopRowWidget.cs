using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Plain ref-holder for one shop row (an upgrade or an ability unlock). The editor builder
    /// fills these public fields on a disabled template; ShopView clones it per row. The row's
    /// Button selects/previews; the Actions sub-panel (Buy/Cancel) shows only while selected.
    /// </summary>
    public sealed class ShopRowWidget : MonoBehaviour
    {
        public Button Button;
        public Image Icon;
        public TMP_Text Label;
        public TMP_Text Cost;
        public CanvasGroup Group;       // alpha 1 = affordable, 0.5 = greyed
        public GameObject Actions;      // Buy/Cancel container, hidden until the row is selected
        public Button BuyButton;
        public Button CancelButton;
    }
}
