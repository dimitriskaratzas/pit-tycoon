using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using PitTycoon.Unity;
using PitTycoon.Unity.UI;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds the UGUI HUD (canvas, live HUD, intermission shop) from scratch and wires every
    /// serialized reference, including GameBootstrap.hud. Idempotent: re-running destroys the
    /// previous "GameHUD" root and rebuilds. New-Input-System aware (InputSystemUIInputModule).
    /// </summary>
    public static class HudSetup
    {
        private static readonly Color Panel = new Color(0.14f, 0.12f, 0.21f, 0.96f);
        private static readonly Color Bar = new Color(0.17f, 0.15f, 0.25f, 1f);
        private static readonly Color Ink = new Color(0.07f, 0.07f, 0.07f, 1f);
        private static readonly Color HypeOrange = new Color(0.85f, 0.35f, 0.19f, 1f);
        private static readonly Color Amber = new Color(0.98f, 0.78f, 0.46f, 1f);

        [MenuItem("Pit Tycoon/Build HUD")]
        public static void Build()
        {
            var existing = GameObject.Find("GameHUD");
            if (existing != null) Object.DestroyImmediate(existing);

            EnsureEventSystem();

            var root = new GameObject("GameHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            HudController controller = root.AddComponent<HudController>();

            LiveHudView live = BuildLiveHud(root.transform, out var liveRefs);
            ShopView shop = BuildShop(root.transform, out var shopRefs);

            WireLive(live, liveRefs);
            WireShop(shop, shopRefs);
            WireController(controller, live, shop);
            WireBootstrap(controller);

            live.gameObject.SetActive(true);
            shop.gameObject.SetActive(true);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Pit Tycoon: HUD built. Import TMP Essentials if text is invisible " +
                      "(Window > TextMeshPro > Import TMP Essential Resources).");
        }

        // ---- live HUD ------------------------------------------------------

        private struct LiveRefs
        {
            public Image hypeFill; public RectTransform peakMarker; public TMP_Text hypeText;
            public TMP_Text cashText; public TMP_Text setText; public RectTransform abilityBar;
            public AbilityButtonWidget template; public TMP_Text hitQuality; public Graphic beatPulse;
        }

        private static LiveHudView BuildLiveHud(Transform parent, out LiveRefs r)
        {
            var go = NewUI("LiveHud", parent);
            Stretch(go.GetComponent<RectTransform>());
            LiveHudView view = go.AddComponent<LiveHudView>();
            r = new LiveRefs();

            // hype bar area (top-center)
            var barArea = NewUI("HypeBar", go.transform);
            var barRT = barArea.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 1f); barRT.anchorMax = new Vector2(0.5f, 1f);
            barRT.pivot = new Vector2(0.5f, 1f);
            barRT.anchoredPosition = new Vector2(0f, -24f);
            barRT.sizeDelta = new Vector2(680f, 30f);
            AddImage(barArea, Bar);
            AddOutline(barArea);

            var fill = NewUI("Fill", barArea.transform);
            Stretch(fill.GetComponent<RectTransform>());
            r.hypeFill = AddImage(fill, HypeOrange);
            r.hypeFill.type = Image.Type.Filled;
            r.hypeFill.fillMethod = Image.FillMethod.Horizontal;
            r.hypeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            r.hypeFill.fillAmount = 0.3f;

            var marker = NewUI("PeakMarker", barArea.transform);
            r.peakMarker = marker.GetComponent<RectTransform>();
            r.peakMarker.anchorMin = new Vector2(0.5f, 0f); r.peakMarker.anchorMax = new Vector2(0.5f, 1f);
            r.peakMarker.pivot = new Vector2(0.5f, 0.5f);
            r.peakMarker.sizeDelta = new Vector2(3f, 0f);
            AddImage(marker, Amber);

            var hypeTextGO = NewUI("HypeText", barArea.transform);
            Stretch(hypeTextGO.GetComponent<RectTransform>());
            r.hypeText = AddText(hypeTextGO, "0 / 0", 16, TextAlignmentOptions.Center);

            // cash (top-right)
            var cash = NewUI("Cash", go.transform);
            var cashRT = cash.GetComponent<RectTransform>();
            cashRT.anchorMin = new Vector2(1f, 1f); cashRT.anchorMax = new Vector2(1f, 1f);
            cashRT.pivot = new Vector2(1f, 1f); cashRT.anchoredPosition = new Vector2(-16f, -16f);
            cashRT.sizeDelta = new Vector2(160f, 36f);
            AddImage(cash, Bar); AddOutline(cash);
            var cashTextGO = NewUI("Text", cash.transform);
            Stretch(cashTextGO.GetComponent<RectTransform>());
            r.cashText = AddText(cashTextGO, "$0", 18, TextAlignmentOptions.Center);
            r.cashText.color = Amber;

            // set (top-left)
            var set = NewUI("Set", go.transform);
            var setRT = set.GetComponent<RectTransform>();
            setRT.anchorMin = new Vector2(0f, 1f); setRT.anchorMax = new Vector2(0f, 1f);
            setRT.pivot = new Vector2(0f, 1f); setRT.anchoredPosition = new Vector2(16f, -16f);
            setRT.sizeDelta = new Vector2(160f, 36f);
            AddImage(set, Bar); AddOutline(set);
            var setTextGO = NewUI("Text", set.transform);
            Stretch(setTextGO.GetComponent<RectTransform>());
            r.setText = AddText(setTextGO, "SET 1", 16, TextAlignmentOptions.Center);

            // ability bar (bottom-center) with a horizontal layout
            var abilityBar = NewUI("AbilityBar", go.transform);
            r.abilityBar = abilityBar.GetComponent<RectTransform>();
            r.abilityBar.anchorMin = new Vector2(0.5f, 0f); r.abilityBar.anchorMax = new Vector2(0.5f, 0f);
            r.abilityBar.pivot = new Vector2(0.5f, 0f); r.abilityBar.anchoredPosition = new Vector2(0f, 24f);
            r.abilityBar.sizeDelta = new Vector2(400f, 72f);
            var layout = abilityBar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f; layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            r.template = BuildAbilityButtonTemplate(abilityBar.transform);

            // hit-quality popup (above the ability bar)
            var hit = NewUI("HitQuality", go.transform);
            var hitRT = hit.GetComponent<RectTransform>();
            hitRT.anchorMin = new Vector2(0.5f, 0f); hitRT.anchorMax = new Vector2(0.5f, 0f);
            hitRT.pivot = new Vector2(0.5f, 0f); hitRT.anchoredPosition = new Vector2(0f, 104f);
            hitRT.sizeDelta = new Vector2(300f, 40f);
            r.hitQuality = AddText(hit, "PERFECT!", 24, TextAlignmentOptions.Center);

            // beat pulse (thin bottom-edge accent strip — alpha pulses on each beat,
            // NOT a full-screen flash: a stretched solid image strobes the whole scene)
            var pulse = NewUI("BeatPulse", go.transform);
            var pulseRT = pulse.GetComponent<RectTransform>();
            pulseRT.anchorMin = new Vector2(0f, 0f); pulseRT.anchorMax = new Vector2(1f, 0f);
            pulseRT.pivot = new Vector2(0.5f, 0f);
            pulseRT.sizeDelta = new Vector2(0f, 6f);
            pulseRT.anchoredPosition = Vector2.zero;
            var pulseImg = AddImage(pulse, new Color(Amber.r, Amber.g, Amber.b, 0f));
            pulseImg.raycastTarget = false;
            r.beatPulse = pulseImg;

            return view;
        }

        private static AbilityButtonWidget BuildAbilityButtonTemplate(Transform parent)
        {
            var go = NewUI("AbilityButtonTemplate", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(58f, 58f);
            var bg = AddImage(go, new Color(0.33f, 0.29f, 0.72f, 1f));
            AddOutline(go);
            var button = go.AddComponent<Button>();
            button.targetGraphic = bg;
            var widget = go.AddComponent<AbilityButtonWidget>();
            widget.Button = button; widget.Background = bg;

            var label = NewUI("Label", go.transform);
            Stretch(label.GetComponent<RectTransform>());
            widget.Label = AddText(label, "A", 12, TextAlignmentOptions.Center);

            var cd = NewUI("Cooldown", go.transform);
            Stretch(cd.GetComponent<RectTransform>());
            var cdImg = AddImage(cd, new Color(0f, 0f, 0f, 0.45f));
            cdImg.type = Image.Type.Filled; cdImg.fillMethod = Image.FillMethod.Vertical;
            cdImg.fillOrigin = (int)Image.OriginVertical.Bottom; cdImg.fillAmount = 0f;
            cdImg.raycastTarget = false;
            widget.CooldownOverlay = cdImg;

            var hk = NewUI("Hotkey", go.transform);
            var hkRT = hk.GetComponent<RectTransform>();
            hkRT.anchorMin = new Vector2(0.5f, 0f); hkRT.anchorMax = new Vector2(0.5f, 0f);
            hkRT.pivot = new Vector2(0.5f, 1f); hkRT.anchoredPosition = new Vector2(0f, -2f);
            hkRT.sizeDelta = new Vector2(58f, 16f);
            widget.Hotkey = AddText(hk, "1", 11, TextAlignmentOptions.Center);

            return widget;
        }

        // ---- shop ----------------------------------------------------------

        private struct ShopRefs
        {
            public TMP_Text cash; public TMP_Text banked; public RectTransform upgrades;
            public RectTransform abilities; public RectTransform builds; public ShopRowWidget template; public Button start;
            public Button returnHome;
        }

        private static ShopView BuildShop(Transform parent, out ShopRefs r)
        {
            var go = NewUI("Shop", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(300f, 0f);
            rt.anchoredPosition = new Vector2(-12f, 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, 12f);
            rt.offsetMax = new Vector2(rt.offsetMax.x, -12f);
            AddImage(go, Panel); AddOutline(go);
            ShopView view = go.AddComponent<ShopView>();
            r = new ShopRefs();

            var vlayout = go.AddComponent<VerticalLayoutGroup>();
            vlayout.padding = new RectOffset(12, 12, 12, 12);
            vlayout.spacing = 8f; vlayout.childControlWidth = true; vlayout.childControlHeight = false;
            vlayout.childForceExpandWidth = true; vlayout.childForceExpandHeight = false;

            var header = NewUI("Cash", go.transform);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
            r.cash = AddText(header, "$0", 18, TextAlignmentOptions.Right);
            r.cash.color = Amber;

            var banked = NewUI("Banked", go.transform);
            banked.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 22f);
            r.banked = AddText(banked, "", 14, TextAlignmentOptions.Center);
            r.banked.color = new Color(0.62f, 0.88f, 0.8f);

            var home = NewUI("ReturnHome", go.transform);
            home.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
            var homeImg = AddImage(home, Bar); AddOutline(home);
            r.returnHome = home.AddComponent<Button>(); r.returnHome.targetGraphic = homeImg;
            var homeText = NewUI("Text", home.transform); Stretch(homeText.GetComponent<RectTransform>());
            AddText(homeText, "⌂ Overview", 12, TextAlignmentOptions.Center);

            var upHead = NewUI("UpgradesLabel", go.transform);
            upHead.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            AddText(upHead, "UPGRADES", 12, TextAlignmentOptions.Left).color = new Color(0.6f, 0.6f, 0.6f);

            var upgrades = NewUI("Upgrades", go.transform);
            r.upgrades = upgrades.GetComponent<RectTransform>();
            var ul = upgrades.AddComponent<VerticalLayoutGroup>();
            ul.spacing = 6f; ul.childControlWidth = true; ul.childControlHeight = false;
            ul.childForceExpandWidth = true; ul.childForceExpandHeight = false;
            upgrades.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var abHead = NewUI("AbilitiesLabel", go.transform);
            abHead.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            AddText(abHead, "ABILITIES", 12, TextAlignmentOptions.Left).color = new Color(0.6f, 0.6f, 0.6f);

            var abilities = NewUI("Abilities", go.transform);
            r.abilities = abilities.GetComponent<RectTransform>();
            var al = abilities.AddComponent<VerticalLayoutGroup>();
            al.spacing = 6f; al.childControlWidth = true; al.childControlHeight = false;
            al.childForceExpandWidth = true; al.childForceExpandHeight = false;
            abilities.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bdHead = NewUI("BuildLabel", go.transform);
            bdHead.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            AddText(bdHead, "BUILD", 12, TextAlignmentOptions.Left).color = new Color(0.6f, 0.6f, 0.6f);

            var builds = NewUI("Build", go.transform);
            r.builds = builds.GetComponent<RectTransform>();
            var bl = builds.AddComponent<VerticalLayoutGroup>();
            bl.spacing = 6f; bl.childControlWidth = true; bl.childControlHeight = false;
            bl.childForceExpandWidth = true; bl.childForceExpandHeight = false;
            builds.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            r.template = BuildShopRowTemplate(go.transform);

            var start = NewUI("StartNextSet", go.transform);
            start.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
            var startImg = AddImage(start, HypeOrange); AddOutline(start);
            r.start = start.AddComponent<Button>(); r.start.targetGraphic = startImg;
            var startText = NewUI("Text", start.transform);
            Stretch(startText.GetComponent<RectTransform>());
            AddText(startText, "START NEXT SET", 14, TextAlignmentOptions.Center);

            return view;
        }

        private static ShopRowWidget BuildShopRowTemplate(Transform parent)
        {
            var go = NewUI("ShopRowTemplate", parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);
            var bg = AddImage(go, Bar); AddOutline(go);
            var button = go.AddComponent<Button>(); button.targetGraphic = bg;
            var group = go.AddComponent<CanvasGroup>();
            var widget = go.AddComponent<ShopRowWidget>();
            widget.Button = button; widget.Icon = bg; widget.Group = group;

            var label = NewUI("Label", go.transform);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(10f, 0f); lrt.offsetMax = new Vector2(-64f, 0f);
            widget.Label = AddText(label, "Item", 13, TextAlignmentOptions.Left);

            var cost = NewUI("Cost", go.transform);
            var crt = cost.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 0.5f); crt.sizeDelta = new Vector2(60f, 0f);
            crt.anchoredPosition = new Vector2(-8f, 0f);
            widget.Cost = AddText(cost, "$0", 13, TextAlignmentOptions.Right);

            // Actions sub-panel (Buy / Cancel) — hidden until the row is selected.
            var actions = NewUI("Actions", go.transform);
            var art = actions.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;
            var alay = actions.AddComponent<HorizontalLayoutGroup>();
            alay.spacing = 6f; alay.childAlignment = TextAnchor.MiddleRight;
            alay.padding = new RectOffset(0, 6, 4, 4);
            alay.childControlWidth = false; alay.childControlHeight = false;
            alay.childForceExpandWidth = false; alay.childForceExpandHeight = false;

            var buy = NewUI("Buy", actions.transform);
            buy.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 24f);
            var buyImg = AddImage(buy, HypeOrange); AddOutline(buy);
            widget.BuyButton = buy.AddComponent<Button>(); widget.BuyButton.targetGraphic = buyImg;
            var buyText = NewUI("Text", buy.transform); Stretch(buyText.GetComponent<RectTransform>());
            AddText(buyText, "BUY", 12, TextAlignmentOptions.Center);

            var cancel = NewUI("Cancel", actions.transform);
            cancel.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 24f);
            var cancelImg = AddImage(cancel, Bar); AddOutline(cancel);
            widget.CancelButton = cancel.AddComponent<Button>(); widget.CancelButton.targetGraphic = cancelImg;
            var cancelText = NewUI("Text", cancel.transform); Stretch(cancelText.GetComponent<RectTransform>());
            AddText(cancelText, "CANCEL", 12, TextAlignmentOptions.Center);

            widget.Actions = actions;
            actions.SetActive(false);

            return widget;
        }

        // ---- wiring (SerializedObject for private [SerializeField]s) --------

        private static void WireLive(LiveHudView view, LiveRefs r)
        {
            var so = new SerializedObject(view);
            so.FindProperty("hypeFill").objectReferenceValue = r.hypeFill;
            so.FindProperty("peakMarker").objectReferenceValue = r.peakMarker;
            so.FindProperty("hypeText").objectReferenceValue = r.hypeText;
            so.FindProperty("cashText").objectReferenceValue = r.cashText;
            so.FindProperty("setText").objectReferenceValue = r.setText;
            so.FindProperty("abilityBar").objectReferenceValue = r.abilityBar;
            so.FindProperty("abilityButtonTemplate").objectReferenceValue = r.template;
            so.FindProperty("hitQualityText").objectReferenceValue = r.hitQuality;
            so.FindProperty("beatPulse").objectReferenceValue = r.beatPulse;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireShop(ShopView view, ShopRefs r)
        {
            var so = new SerializedObject(view);
            so.FindProperty("cashText").objectReferenceValue = r.cash;
            so.FindProperty("bankedText").objectReferenceValue = r.banked;
            so.FindProperty("upgradeContainer").objectReferenceValue = r.upgrades;
            so.FindProperty("abilityContainer").objectReferenceValue = r.abilities;
            so.FindProperty("buildContainer").objectReferenceValue = r.builds;
            so.FindProperty("rowTemplate").objectReferenceValue = r.template;
            so.FindProperty("startNextSetButton").objectReferenceValue = r.start;
            so.FindProperty("returnHomeButton").objectReferenceValue = r.returnHome;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireController(HudController c, LiveHudView live, ShopView shop)
        {
            var so = new SerializedObject(c);
            so.FindProperty("liveView").objectReferenceValue = live;
            so.FindProperty("shopView").objectReferenceValue = shop;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBootstrap(HudController c)
        {
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot == null) { Debug.LogWarning("HudSetup: no GameBootstrap in scene; skipped hud wiring."); return; }
            var so = new SerializedObject(boot);
            so.FindProperty("hud").objectReferenceValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- primitives ----------------------------------------------------

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }
            // New-Input-System: replace any legacy StandaloneInputModule.
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.DestroyImmediate(legacy);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Image AddImage(GameObject go, Color c)
        {
            var img = go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        private static void AddOutline(GameObject go)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(2f, -2f);
        }

        private static TMP_Text AddText(GameObject go, string text, float size, TextAlignmentOptions align)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }
    }
}
