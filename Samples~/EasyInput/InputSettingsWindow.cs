using System;
using System.Collections.Generic;
using EasyInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EasyUI.Integrations.Input
{
    [RequireComponent(typeof(UIWindow)), AddComponentMenu("EasyUI/Integrations/Input Settings Window")]
    public sealed class InputSettingsWindow : MonoBehaviour, IWindowBackHandler
    {
        [Tooltip("Optional when the Window Manager uses EasyInput Window Provider.")]
        public PlayerInputSettings Player;
        public string[] Actions = { "Gameplay/Interact", "Gameplay/Jump", "Gameplay/Move", "Gameplay/Fire", "Gameplay/Look" };
        public float RebindTimeout = 10;
        [SerializeField, Tooltip("Optional IInputSettingsTextProvider. Binding glyphs still come from EasyInput.")]
        private MonoBehaviour textProvider;
        public MonoBehaviour TextProvider
        {
            get => textProvider;
            set { textProvider = value; BindTextProvider(); Refresh(); }
        }
        public RectTransform ExtrasRoot { get; private set; }
        public event Action<RectTransform> ViewBuilt;
        private IInputSettingsTextProvider subscribedText;
        private string statusKey;
        private readonly List<Caption> captions = new List<Caption>();
        private sealed class Caption { internal Text Label; internal string Key, Fallback; }
        private void BindTextProvider()
        {
            if (subscribedText != null) subscribedText.Changed -= Refresh;
            subscribedText = isActiveAndEnabled ? textProvider as IInputSettingsTextProvider : null;
            if (subscribedText != null) subscribedText.Changed += Refresh;
        }
        private string T(string key, string fallback) => subscribedText?.GetText(key, fallback) ?? fallback;
        private string Format(string key, string fallback, params object[] values)
        {
            try { return string.Format(T(key, fallback), values); }
            catch (FormatException) { return string.Format(fallback, values); }
        }
        private void CaptionFor(Text label, string key, string fallback)
        { captions.Add(new Caption { Label = label, Key = key, Fallback = fallback }); }
        private void SetStatus(string key, string fallback) { statusKey = key; Status = fallback; dirty = true; }
        public InputSettingsSnapshot CapturedSettings { get; private set; }
        public string Status { get; private set; }
        private PlayerInputSettings subscribed;
        private UIWindow window;
        private RectTransform content, viewport;
        private GameObject generated;
        private ScrollRect scroll;
        private Text status, mouseLabel, gamepadLabel;
        private Button invert, device;
        private Slider mouse, gamepad, deadzone;
        private bool refreshing, dirty;
        private GameObject lastSelected;
        private readonly List<Row> rows = new List<Row>();
        private readonly List<Selectable> editControls = new List<Selectable>();
        private readonly List<InputRebind> ownedRebinds = new List<InputRebind>();
        private sealed class Row { internal InputAction Action; internal int Binding; internal Text Label; internal Image Icon; }

        private void OnEnable()
        {
            window = GetComponent<UIWindow>();
            if (!Player && window.Handle?.Manager.InputProvider is EasyInputWindowProvider provider) Player = provider.Player;
            if (!Player) { Debug.LogError("Input Settings Window: assign Player Input Settings or open it with EasyInput Window Provider.", this); return; }
            subscribed = Player; subscribed.SettingsChanged += Refresh; subscribed.PresentationChanged += Refresh; subscribed.RebindChanged += RebindChanged;
            BindTextProvider(); Build(); Refresh();
        }
        private void OnDisable()
        {
            foreach (var rebind in ownedRebinds) rebind.Dispose(); ownedRebinds.Clear();
            if (subscribed) { subscribed.SettingsChanged -= Refresh; subscribed.PresentationChanged -= Refresh; subscribed.RebindChanged -= RebindChanged; }
            subscribed = null;
            if (subscribedText != null) subscribedText.Changed -= Refresh;
            subscribedText = null;
        }
        public void BeginRebind(string actionName, int bindingIndex)
        {
            if (!Player) throw new InvalidOperationException("Assign Player Input Settings.");
            ownedRebinds.RemoveAll(session => !session.IsActive);
            ownedRebinds.Add(Player.BeginRebind(Player.Action(actionName), bindingIndex, this, RebindTimeout));
            window.Handle?.Manager.FenceInput();
        }
        public bool HandleBack()
        {
            if (!Player || Player.Rebind?.IsActive != true) return false;
            Player.CancelRebind(); window.Handle?.Manager.FenceInput(); return true;
        }
        public void Capture() { CapturedSettings = Player.CaptureSettings(); SetStatus("status.captured", "Settings captured in memory."); }
        public void Restore()
        {
            if (CapturedSettings == null) SetStatus("status.captureFirst", "Capture settings first.");
            else if (Player.RestoreSettings(CapturedSettings, out var error)) SetStatus("status.restored", "Settings restored.");
            else SetStatus(null, error);
        }
        private void Refresh() => dirty = true;
        private void RebindChanged() { SetStatus(null, Player.Rebind?.Message); }
        private void LateUpdate()
        {
            if (!Player || !generated) return;
            if (dirty)
            {
                dirty = false; refreshing = true;
                try
                {
                    var settings = Player.Settings;
                    mouse.SetValueWithoutNotify(settings.MouseSensitivity); gamepad.SetValueWithoutNotify(settings.GamepadSensitivity); deadzone.SetValueWithoutNotify(settings.StickDeadzoneMin);
                    foreach (var caption in captions) if (caption.Label) caption.Label.text = T(caption.Key, caption.Fallback);
                    mouseLabel.text = Format("mouseSensitivity", "Mouse sensitivity  {0:0.00}", settings.MouseSensitivity);
                    gamepadLabel.text = Format("gamepadSensitivity", "Gamepad sensitivity  {0:0.00}", settings.GamepadSensitivity);
                    invert.GetComponentInChildren<Text>().text = Format("invertY", "Invert Y: {0}", settings.InvertY ? T("on", "On") : T("off", "Off"));
                    device.GetComponentInChildren<Text>().text = Format("devices", "Devices: {0}", T("device." + settings.DevicePreference, settings.DevicePreference.ToString()));
                    foreach (var row in rows)
                    {
                        var presentation = Player.GetPresentation(row.Action, row.Binding); var binding = row.Action.bindings[row.Binding];
                        row.Label.text = T("action." + row.Action.actionMap.name + "/" + row.Action.name, row.Action.name)
                            + (binding.isPartOfComposite ? " / " + T("part." + binding.name, binding.name) : "") + "   [" + presentation.Text + "]";
                        row.Icon.sprite = presentation.Sprite; row.Icon.enabled = presentation.Sprite != null;
                    }
                    bool rebinding = Player.Rebind?.IsActive == true;
                    foreach (var control in editControls) control.interactable = !rebinding;
                    status.text = rebinding ? T("rebind." + Player.Rebind.Status, Player.Rebind.Message)
                        : Status != null ? (statusKey != null ? T(statusKey, Status) : T("rebind." + Player.Rebind?.Status, Status))
                        : T("status.help", "Select a binding. Esc / B cancels rebinding before closing this window.");
                }
                finally { refreshing = false; }
            }
            // Keep gamepad-selected bindings inside the scroll viewport.
            var selected = window.Handle?.Manager.EventSystem.currentSelectedGameObject;
            bool selectionChanged = selected != lastSelected; lastSelected = selected;
            if (selectionChanged && selected && selected.transform.IsChildOf(content) && selected.transform is RectTransform item)
            {
                float top = -item.anchoredPosition.y - 18;
                float offset = content.anchoredPosition.y;
                if (top < offset) content.anchoredPosition = new Vector2(0, Mathf.Max(0, top));
                else if (top + 40 > offset + viewport.rect.height) content.anchoredPosition = new Vector2(0, Mathf.Min(content.rect.height - viewport.rect.height, top + 40 - viewport.rect.height));
            }
        }
        private void Build()
        {
            if (generated) { generated.SetActive(false); Destroy(generated); }
            rows.Clear(); editControls.Clear(); captions.Clear();
            lastSelected = null;
            var root = Rect("Controls", transform, Vector2.zero, new Vector2(680, 650)); generated = root.gameObject;
            var background = generated.AddComponent<Image>(); background.color = new Color(.055f, .08f, .13f);
            CaptionFor(Label(root, "Controls", new Vector2(0, 285), new Vector2(600, 50), 32), "title", "Controls");
            mouseLabel = Label(root, "Mouse sensitivity", new Vector2(-145, 230), new Vector2(300, 30));
            mouse = Slider(root, new Vector2(160, 230), .05f, 5, value => { if (!refreshing) Player.MouseSensitivity = value; });
            gamepadLabel = Label(root, "Gamepad sensitivity", new Vector2(-145, 190), new Vector2(300, 30));
            gamepad = Slider(root, new Vector2(160, 190), .05f, 5, value => { if (!refreshing) Player.GamepadSensitivity = value; });
            CaptionFor(Label(root, "Stick deadzone", new Vector2(-145, 150), new Vector2(300, 30)), "deadzone", "Stick deadzone");
            deadzone = Slider(root, new Vector2(160, 150), 0, .5f, value => { if (!refreshing) { var s = Player.Settings; s.StickDeadzoneMin = Mathf.Min(value, s.StickDeadzoneMax - .01f); Player.SetSettings(s); } });
            invert = Button(root, "Invert Y", new Vector2(-155, 105), new Vector2(290, 36), () => { var s = Player.Settings; s.InvertY = !s.InvertY; Player.SetSettings(s); });
            device = Button(root, "Devices", new Vector2(155, 105), new Vector2(290, 36), () =>
            {
                var s = Player.Settings; s.DevicePreference = (InputDevicePreference)(((int)s.DevicePreference + 1) % 3);
                if (s.DevicePreference == InputDevicePreference.Gamepad && Gamepad.all.Count == 0)
                { SetStatus("status.connectGamepad", "Connect a gamepad before selecting Gamepad mode."); return; }
                Player.SetSettings(s); window.Handle?.Manager.FenceInput();
            });
            var scrollRect = Rect("Bindings", root, new Vector2(0, -55), new Vector2(610, 260)); scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            viewport = Rect("Viewport", scrollRect, Vector2.zero, new Vector2(610, 260)); viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            content = Rect("Rows", viewport, Vector2.zero, new Vector2(600, 0)); content.anchorMin = content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1);
            scroll.viewport = viewport; scroll.content = content; scroll.movementType = ScrollRect.MovementType.Clamped;
            int rowIndex = 0;
            foreach (string name in Actions)
            {
                var action = Player.Actions.FindAction(name, false); if (action == null) continue;
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    if (action.bindings[index].isComposite) continue;
                    int binding = index;
                    var button = Button(content, name, new Vector2(-42, -22 - rowIndex * 44), new Vector2(490, 38), () => BeginRebind(name, binding));
                    ((RectTransform)button.transform).anchorMin = ((RectTransform)button.transform).anchorMax = new Vector2(.5f, 1);
                    var icon = Rect("Glyph", button.transform, new Vector2(-220, 0), new Vector2(25, 25)).gameObject.AddComponent<Image>(); icon.raycastTarget = false;
                    var bindingLabel = button.GetComponentInChildren<Text>(); bindingLabel.rectTransform.anchoredPosition = new Vector2(14, 0); bindingLabel.rectTransform.sizeDelta -= new Vector2(32, 0);
                    rows.Add(new Row { Action = action, Binding = binding, Label = button.GetComponentInChildren<Text>(), Icon = icon });
                    var reset = Button(content, "Reset", new Vector2(255, -22 - rowIndex * 44), new Vector2(80, 38), () => Player.ResetBinding(action, binding));
                    ((RectTransform)reset.transform).anchorMin = ((RectTransform)reset.transform).anchorMax = new Vector2(.5f, 1);
                    rowIndex++;
                }
            }
            content.sizeDelta = new Vector2(600, Mathf.Max(260, rowIndex * 44));
            status = Label(root, "", new Vector2(0, -210), new Vector2(610, 48), 17);
            Button(root, "Capture", new Vector2(-225, -260), new Vector2(140, 38), Capture);
            Button(root, "Restore", new Vector2(-75, -260), new Vector2(140, 38), Restore);
            Button(root, "Defaults", new Vector2(75, -260), new Vector2(140, 38), () => Player.ResetSettings());
            var close = Button(root, "Back", new Vector2(225, -260), new Vector2(140, 38), () => { if (!HandleBack()) window.Close(); });
            ExtrasRoot = Rect("Extensions", root, new Vector2(0, -302), new Vector2(610, 32));
            if (ViewBuilt != null)
                foreach (Action<RectTransform> listener in ViewBuilt.GetInvocationList())
                    try { listener(ExtrasRoot); } catch (Exception e) { Debug.LogException(e, this); }
            editControls.Remove(close); window.FirstSelected = mouse; window.RefreshNavigation();
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        { var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; r.SetParent(parent, false); r.anchoredPosition = position; r.sizeDelta = size; return r; }
        private static Text Label(Transform parent, string text, Vector2 position, Vector2 size, int font = 18)
        { var label = Rect(text, parent, position, size).gameObject.AddComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = font; label.color = Color.white; label.text = text; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; return label; }
        private Button Button(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
        {
            var r = Rect(label, parent, position, size); var image = r.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .22f, .3f);
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(click);
            var colors = button.colors; colors.selectedColor = new Color(.2f, 1, .8f); colors.highlightedColor = colors.selectedColor; button.colors = colors;
            var text = Label(r, label, Vector2.zero, size - new Vector2(16, 0)); text.alignment = TextAnchor.MiddleCenter;
            if (label == "Reset" || label == "Capture" || label == "Restore" || label == "Defaults" || label == "Back")
                CaptionFor(text, label.ToLowerInvariant(), label);
            editControls.Add(button); return button;
        }
        private Slider Slider(Transform parent, Vector2 position, float min, float max, UnityEngine.Events.UnityAction<float> changed)
        {
            var r = Rect("Slider", parent, position, new Vector2(270, 25)); var track = r.gameObject.AddComponent<Image>(); track.color = new Color(.18f, .26f, .34f);
            var slider = r.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
            var handle = Rect("Handle", r, Vector2.zero, new Vector2(18, 0)); var image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.15f, .85f, .7f);
            slider.handleRect = handle; slider.targetGraphic = image; slider.onValueChanged.AddListener(changed); editControls.Add(slider); return slider;
        }
    }
}
