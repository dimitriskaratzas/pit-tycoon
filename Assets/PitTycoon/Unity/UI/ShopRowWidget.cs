using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Plain ref-holder for one shop row (an upgrade or an ability unlock). The editor builder
    /// fills these public fields on a disabled template; ShopView clones it per row.
    /// </summary>
    public sealed class ShopRowWidget : MonoBehaviour
    {
        public Button Button;
        public Image Icon;
        public TMP_Text Label;
        public TMP_Text Cost;
        public CanvasGroup Group;       // alpha 1 = affordable, 0.5 = greyed
    }
}
