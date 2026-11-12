using System.Collections.Generic;
using EasyInteraction;
using UnityEngine;
using UnityEngine.UI;

namespace EasyUI.Integrations.Interaction
{
    /// <summary>A list, or a radial when RadialMenu is present. Selection uses the actor; execution stays with its presenter.</summary>
    [RequireComponent(typeof(UIWindow)), AddComponentMenu("EasyUI/Integrations/Interaction Menu Window")]
    public sealed class InteractionMenuWindow : MonoBehaviour, IWindowBackHandler
    {
        public InteractionPresenter Presenter;
        public bool CloseOnSelection = true;
        public string TitleKey = "ui.interactions", TitleFallback = "Actions";
        public string ToggleKey = "ui.toggle", ToggleFallback = "Toggle";
        public string HoldKey = "ui.hold", HoldFallback = "Hold";
        private InteractionPresenter subscribed;
        private InteractionAction pressed;
        private InteractionPresenter pressedPresenter;
        private UIWindow window;
        private RadialMenu radial;
        private RectTransform panel, rowsRoot;
        private Text title, hint, reason;
        private Image glyph;
        private readonly List<InteractionAction> actions = new List<InteractionAction>();
        private readonly List<InteractionAction> next = new List<InteractionAction>();
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<Image> icons = new List<Image>();
        private bool dirty;
        private float nextCheck;
        private int activateFrame = -1;
        public InteractionPrompt Prompt => Presenter ? Presenter.Prompt : default;

        private void OnEnable()
        {
            window = GetComponent<UIWindow>(); radial = GetComponent<RadialMenu>();
            if (radial) { radial.CloseOnConfirm = false; radial.Confirmed += Confirmed; }
            BuildPanel(); Bind(Presenter);
        }
        private void OnDisable()
        {
            ReleaseSelected(); Unsubscribe();
            if (radial) radial.Confirmed -= Confirmed;
        }
        public void Bind(InteractionPresenter presenter)
        {
            ReleaseSelected(); Unsubscribe(); Presenter = presenter;
            if (isActiveAndEnabled && Presenter)
            {
                subscribed = Presenter; subscribed.Changed += Changed;
                subscribed.Actor.TargetChanged += TargetChanged; subscribed.Actor.ActionStateChanged += ActionChanged;
            }
            Refresh();
        }
        private void Unsubscribe()
        {
            if (subscribed)
            {
                subscribed.Changed -= Changed; subscribed.Actor.TargetChanged -= TargetChanged;
                subscribed.Actor.ActionStateChanged -= ActionChanged;
            }
            subscribed = null;
        }
        private void Changed(InteractionPrompt _) => dirty = true;
        private void TargetChanged(InteractionTarget _) => dirty = true;
        private void ActionChanged(InteractionActionEvent _) => dirty = true;
        private void LateUpdate()
        {
            // Unselected conditions expose no aggregate event. Check them at the presenter's cadence;
            // unchanged views are retained and unchanged Text values are never assigned again.
            if (dirty || Time.unscaledTime >= nextCheck) Refresh();
        }
        public void Refresh()
        {
            dirty = false; nextCheck = Time.unscaledTime + .1f;
            if (!isActiveAndEnabled || !panel) return;
            if (Presenter) Presenter.Refresh();
            next.Clear();
            if (Presenter && Presenter.Actor.isActiveAndEnabled)
                foreach (var action in Presenter.Actor.Actions)
                    if (action && action.isActiveAndEnabled && (!Presenter.Actor.CurrentPoint || Presenter.Actor.CurrentPoint.Allows(action))
                        && (!action.HideWhenUnavailable || Presenter.Check(action).IsAvailable)) next.Add(action);
            bool rebuild = actions.Count != next.Count;
            for (int i = 0; !rebuild && i < next.Count; i++) rebuild = actions[i] != next[i];
            if (rebuild) { actions.Clear(); actions.AddRange(next); RebuildItems(); }
            SetText(title, Presenter ? Presenter.Translate(TitleKey, TitleFallback) : TitleFallback);
            var prompt = Prompt;
            string mode = !Presenter || !prompt.Action ? "" : Presenter.ToggleInsteadOfHold
                ? Presenter.Translate(ToggleKey, ToggleFallback)
                : prompt.Action.InputGesture == InteractionInputGesture.HoldWhileActive || prompt.Action.InputGesture == InteractionInputGesture.HoldToConfirm
                    ? Presenter.Translate(HoldKey, HoldFallback) : "";
            SetText(hint, (prompt.Binding ?? "") + (mode.Length > 0 ? " · " + mode : ""));
            SetText(reason, Presenter ? prompt.Reason : "Assign an Interaction Presenter or call Bind(presenter).");
            glyph.sprite = prompt.BindingIcon; glyph.enabled = prompt.BindingIcon;
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i]; var check = Presenter.Check(action);
                string label = Presenter.Translate(action.LabelKey, action.GetLabel(Presenter.Actor));
                if (radial) radial.UpdateItem(action.GetInstanceID().ToString(), label, check.IsAvailable, action.Icon);
                else
                {
                    buttons[i].interactable = check.IsAvailable; SetText(labels[i], label);
                    icons[i].sprite = action.Icon; icons[i].enabled = action.Icon;
                }
            }
        }
        public bool Select(InteractionAction action)
        {
            if (!isActiveAndEnabled || !window || !window.IsActive || !Presenter || !Presenter.Actor.isActiveAndEnabled) return false;
            var handle = window.Handle; var presenter = Presenter;
            if (!presenter.Check(action).IsAvailable || !Owns(handle, presenter) || !presenter.Actor || !presenter.Actor.SelectAction(action)) return false;
            presenter.Refresh();
            if (!Owns(handle, presenter)) return true;
            if (CloseOnSelection) handle.Close(); else Refresh();
            return true;
        }
        private bool Owns(WindowHandle handle, InteractionPresenter presenter) => this && isActiveAndEnabled && window && window.IsActive
            && window.Handle == handle && handle?.IsOpen == true && presenter && Presenter == presenter;
        /// <summary>Optional explicit UI press. Pair with ReleaseSelected for hold/hold-to-confirm gestures.</summary>
        public bool ActivateSelected()
        {
            if (!Presenter || !window.IsActive || !window.Handle.Manager.InputReady || activateFrame == Time.frameCount) return false;
            activateFrame = Time.frameCount;
            var handle = window.Handle; var presenter = Presenter;
            ReleaseSelected(); if (!Owns(handle, presenter)) return false;
            var action = presenter.Actor.SelectedAction;
            // Publish ownership before callbacks so OnDisable can release an action started during Activate.
            pressed = action; pressedPresenter = presenter;
            bool result = presenter.Activate(action);
            if (!result || !Owns(handle, presenter)) ReleaseSelected();
            if (Owns(handle, presenter)) Refresh(); return result;
        }
        public void ReleaseSelected()
        { var action = pressed; var presenter = pressedPresenter; pressed = null; pressedPresenter = null; if (presenter && action) presenter.Release(action); }
        public bool SetValue(float value) => Presenter && Presenter.SetValue(pressed ? pressed : Presenter.Actor.SelectedAction, value);
        public bool HandleBack()
        {
            if (!Presenter) return false;
            var actor = Presenter.Actor;
            if (actor.CommandAdapter && (Prompt.Running || actor.ActiveCount > 0))
            { actor.CommandAdapter.Cancel(); Refresh(); return true; }
            var action = actor.ActiveAction;
            if (!action) return false;
            bool result = Presenter.Activate(action); Refresh(); return result;
        }
        private void Confirmed(RadialItem item)
        {
            foreach (var action in actions)
                if (action && action.GetInstanceID().ToString() == item.Id) { Select(action); return; }
        }
        private void BuildPanel()
        {
            if (panel) return;
            panel = Rect("Interaction view", transform, Vector2.zero, new Vector2(radial ? 220 : 620, radial ? 140 : 580));
            var background = panel.gameObject.AddComponent<Image>(); background.color = new Color(.055f, .08f, .13f, .96f);
            title = Label(panel, "Actions", new Vector2(0, radial ? 38 : 240), new Vector2(radial ? 200 : 540, 48), 28);
            hint = Label(panel, "Binding", new Vector2(20, radial ? 0 : -225), new Vector2(radial ? 150 : 460, 32), 18);
            reason = Label(panel, "Reason", new Vector2(0, radial ? -40 : -260), new Vector2(radial ? 210 : 560, 36), 15);
            glyph = Rect("Binding glyph", panel, new Vector2(radial ? -85 : -240, radial ? 0 : -225), new Vector2(28, 28)).gameObject.AddComponent<Image>(); glyph.raycastTarget = false;
            if (!radial)
            {
                var sr = Rect("Actions", panel, Vector2.zero, new Vector2(560, 400));
                var scroll = sr.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
                var viewport = Rect("Viewport", sr, Vector2.zero, new Vector2(560, 400)); viewport.gameObject.AddComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
                rowsRoot = Rect("Rows", viewport, Vector2.zero, new Vector2(550, 0)); rowsRoot.anchorMin = rowsRoot.anchorMax = rowsRoot.pivot = new Vector2(.5f, 1);
                scroll.viewport = viewport; scroll.content = rowsRoot;
                sr.gameObject.AddComponent<WindowScrollIntoView>();
            }
        }
        private void RebuildItems()
        {
            if (radial)
            {
                var items = new List<RadialItem>(actions.Count);
                foreach (var action in actions) items.Add(new RadialItem(action.GetInstanceID().ToString(), action.GetLabel(Presenter.Actor)));
                radial.SetItems(items); return;
            }
            foreach (var button in buttons) if (button) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
            buttons.Clear(); labels.Clear(); icons.Clear();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i]; var r = Rect("Action " + action.ActionId, rowsRoot, new Vector2(0, -26 - i * 54), new Vector2(540, 48));
                r.anchorMin = r.anchorMax = new Vector2(.5f, 1);
                var bg = r.gameObject.AddComponent<Image>(); bg.color = new Color(.12f, .22f, .3f);
                var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = bg; button.onClick.AddListener(() => Select(action)); buttons.Add(button);
                labels.Add(Label(r, "Label", new Vector2(20, 0), new Vector2(460, 44), 20));
                var icon = Rect("Icon", r, new Vector2(-240, 0), new Vector2(28, 28)).gameObject.AddComponent<Image>(); icon.raycastTarget = false; icons.Add(icon);
            }
            rowsRoot.sizeDelta = new Vector2(550, Mathf.Max(400, actions.Count * 54)); window.RefreshNavigation();
        }
        private static void SetText(Text label, string value) { if (label.text != value) label.text = value; }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        { var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; r.SetParent(parent, false); r.anchoredPosition = position; r.sizeDelta = size; return r; }
        private static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int font)
        { var t = Rect(name, parent, position, size).gameObject.AddComponent<Text>(); t.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = font; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false; return t; }
    }
}
