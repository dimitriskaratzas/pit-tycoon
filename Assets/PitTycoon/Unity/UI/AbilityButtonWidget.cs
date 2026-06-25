using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Plain ref-holder for one ability button instance. The editor builder fills these
    /// public fields on a disabled template; LiveHudView clones the template per owned ability.
    /// </summary>
    public sealed class AbilityButtonWidget : MonoBehaviour
    {
        public Button Button;
        public Image Background;
        public TMP_Text Label;
        public TMP_Text Hotkey;
        public Image CooldownOverlay;   // Image.type = Filled; fillAmount: 1 = just fired, 0 = ready
    }
}
