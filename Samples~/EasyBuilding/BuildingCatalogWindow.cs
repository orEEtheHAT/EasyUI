using System.Collections.Generic;
using EasyBuilding;
using UnityEngine;
using UnityEngine.UI;

namespace EasyUI.Integrations.Building
{
    [RequireComponent(typeof(UIWindow)), AddComponentMenu("EasyUI/Integrations/Building Catalog Window")]
    public sealed class BuildingCatalogWindow : MonoBehaviour, IWindowBackHandler
    {
        public BuildingPlacer Placer;
        public BuildingCatalog Catalog;
        public bool CloseOnSelection = true;
        [SerializeField, Tooltip("Optional IBuildingCatalogPresentation. This window remains the sole writer of its labels.")]
        private MonoBehaviour presentation;
        public MonoBehaviour Presentation { get => presentation; set { presentation = value; RebindPresentation(); Refresh(); } }
        public BuildingSession Session => Placer ? Placer.Session : null;
        private BuildingPlacer subscribed;
        private BuildingSession session;
        private IBuildingCatalogPresentation text;
        private UIWindow window;
        private RadialMenu radial;
        private RectTransform panel, rowsRoot;
        private Text title, status;
        private readonly List<BuildingDefinition> definitions = new List<BuildingDefinition>();
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<Image> icons = new List<Image>();
        private bool dirty;
        private int selectFrame = -1;

        private void OnEnable()
        {
            window = GetComponent<UIWindow>(); radial = GetComponent<RadialMenu>();
            if (radial) { radial.CloseOnConfirm = false; radial.Confirmed += Confirmed; radial.SelectionChanged += RadialSelectionChanged; }
            BuildPanel(); Bind(Placer, Catalog);
        }
        private void OnDisable()
        {
            if (radial) { radial.Confirmed -= Confirmed; radial.SelectionChanged -= RadialSelectionChanged; }
            if (subscribed) subscribed.SessionChanged -= SessionChanged;
            subscribed = null; BindSession(null);
            if (text != null) text.Changed -= MarkDirty; text = null;
        }
        public void Bind(BuildingPlacer placer, BuildingCatalog catalog)
        {
            if (subscribed) subscribed.SessionChanged -= SessionChanged;
            Placer = placer; Catalog = catalog; subscribed = isActiveAndEnabled ? Placer : null;
            if (subscribed) subscribed.SessionChanged += SessionChanged;
            RebindPresentation(); BindSession(isActiveAndEnabled ? Session : null); RebuildItems(); Refresh();
        }
        private void RebindPresentation()
        {
            if (text != null) text.Changed -= MarkDirty;
            text = isActiveAndEnabled ? presentation as IBuildingCatalogPresentation : null;
            if (text != null) { text.Changed += MarkDirty; text.Bind(session); }
        }
        private void SessionChanged(BuildingSession value) { BindSession(value); MarkDirty(); }
        private void BindSession(BuildingSession value)
        {
            if (!this || !isActiveAndEnabled) value = null;
            if (ReferenceEquals(session, value)) return;
            if (session != null) { session.SelectionChanged -= SelectionChanged; session.Disposed -= Disposed; }
            session = value;
            if (session != null) { session.SelectionChanged += SelectionChanged; session.Disposed += Disposed; }
            text?.Bind(session);
        }
        private void SelectionChanged(BuildingDefinition _) => MarkDirty();
        private void RadialSelectionChanged(int _) => MarkDirty();
        private void Disposed(BuildingSession _) { BindSession(null); MarkDirty(); }
        private void MarkDirty() => dirty = true;
        private void LateUpdate() { if (dirty) Refresh(); }
        public void Refresh()
        {
            dirty = false;
            if (!isActiveAndEnabled || !panel) return;
            int count = 0; bool changed = false;
            if (Catalog && Catalog.Buildings != null)
                foreach (var definition in Catalog.Buildings)
                    if (definition) { if (count >= definitions.Count || definitions[count] != definition) changed = true; count++; }
            if (changed || count != definitions.Count) RebuildItems();
            BindSession(Session);
            var focused = session?.Selected;
            if (radial && radial.SelectedItem != null)
                foreach (var definition in definitions) if (definition.Id == radial.SelectedItem.Id) { focused = definition; break; }
            SetText(title, radial && focused ? Name(focused) : text?.Text("ui.buildings", "Buildings") ?? "Buildings");
            string message = !Placer ? "Assign a Building Placer or call Bind(placer, catalog)."
                : !Catalog ? "Assign a Building Catalog."
                : session == null || session.IsDisposed ? Placer.ConfigurationError ?? "The building session is closed."
                : focused ? (radial ? text?.Details(focused) ?? Name(focused) : Name(focused)) : text?.Text("ui.building.choose", "Choose a building") ?? "Choose a building";
            SetText(status, message);
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                bool available = Placer && session != null && !session.IsDisposed && definition && definition.ConfigurationError == null;
                string label = Name(definition); string details = text?.Details(definition) ?? "";
                if (!radial && !string.IsNullOrEmpty(details)) label += "\n" + details;
                if (radial) radial.UpdateItem(definition.Id, label, available, definition.Icon);
                else { buttons[i].interactable = available; SetText(labels[i], label); icons[i].sprite = definition.Icon; icons[i].enabled = definition.Icon; }
            }
        }
        /// <summary>Select through the placer's current borrowed session. Never creates/disposes a session.</summary>
        public bool Select(BuildingDefinition definition)
        {
            if (!isActiveAndEnabled || !window || !window.IsActive || !Placer || !definition || definition.ConfigurationError != null || selectFrame == Time.frameCount) return false;
            var handle = window.Handle; var placer = Placer;
            var current = placer.Session;
            if (!Owns(handle, placer) || current == null || current.IsDisposed) return false;
            // A SelectionChanged listener can close, destroy, rebind or reopen this window.
            selectFrame = Time.frameCount;
            if (!current.Select(definition)) return false;
            if (!Owns(handle, placer)) return true;
            var latest = placer.Session;
            if (!Owns(handle, placer)) return true;
            BindSession(latest);
            if (!Owns(handle, placer)) return true;
            if (CloseOnSelection) handle.Close(); else Refresh();
            return true;
        }
        private bool Owns(WindowHandle handle, BuildingPlacer placer) => this && isActiveAndEnabled && window && window.IsActive
            && window.Handle == handle && handle?.IsOpen == true && placer && Placer == placer;
        public bool HandleBack()
        {
            var current = Session;
            if (current == null || current.IsDisposed || !current.Selected) return false;
            current.Cancel(); Refresh(); return true;
        }
        private string Name(BuildingDefinition definition) => !definition ? "" : text?.Name(definition) ?? definition.Name;
        private void Confirmed(RadialItem item)
        { foreach (var definition in definitions) if (definition && definition.Id == item.Id) { Select(definition); return; } }
        private void BuildPanel()
        {
            if (panel) return;
            panel = Rect("Building catalog", transform, Vector2.zero, new Vector2(radial ? 220 : 680, radial ? 140 : 580));
            panel.gameObject.AddComponent<Image>().color = new Color(.055f, .08f, .13f, .96f);
            title = Label(panel, "Title", new Vector2(0, radial ? 32 : 240), new Vector2(radial ? 200 : 600, 48), 28);
            status = Label(panel, "Status", new Vector2(0, radial ? -25 : -245), new Vector2(radial ? 210 : 620, 65), 17);
            if (!radial)
            {
                var sr = Rect("Catalog", panel, Vector2.zero, new Vector2(620, 400));
                var scroll = sr.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
                var viewport = Rect("Viewport", sr, Vector2.zero, new Vector2(620, 400)); viewport.gameObject.AddComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
                rowsRoot = Rect("Rows", viewport, Vector2.zero, new Vector2(610, 0)); rowsRoot.anchorMin = rowsRoot.anchorMax = rowsRoot.pivot = new Vector2(.5f, 1);
                scroll.viewport = viewport; scroll.content = rowsRoot;
                sr.gameObject.AddComponent<WindowScrollIntoView>();
            }
        }
        private void RebuildItems()
        {
            if (!panel) return;
            definitions.Clear();
            if (Catalog && Catalog.Buildings != null) foreach (var d in Catalog.Buildings) if (d && !definitions.Contains(d)) definitions.Add(d);
            if (radial)
            {
                var items = new List<RadialItem>(definitions.Count);
                foreach (var definition in definitions) items.Add(new RadialItem(definition.Id, Name(definition)));
                radial.SetItems(items); return;
            }
            foreach (var button in buttons) if (button) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
            buttons.Clear(); labels.Clear(); icons.Clear();
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i]; var r = Rect("Building " + definition.Name, rowsRoot, new Vector2(0, -38 - i * 78), new Vector2(600, 72));
                r.anchorMin = r.anchorMax = new Vector2(.5f, 1);
                var bg = r.gameObject.AddComponent<Image>(); bg.color = new Color(.12f, .22f, .3f);
                var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = bg; button.onClick.AddListener(() => Select(definition)); buttons.Add(button);
                labels.Add(Label(r, "Label", new Vector2(20, 0), new Vector2(510, 68), 17));
                var icon = Rect("Icon", r, new Vector2(-265, 0), new Vector2(28, 28)).gameObject.AddComponent<Image>(); icon.raycastTarget = false; icons.Add(icon);
            }
            rowsRoot.sizeDelta = new Vector2(610, Mathf.Max(400, definitions.Count * 78)); window.RefreshNavigation();
        }
        private static void SetText(Text label, string value) { if (label.text != value) label.text = value; }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        { var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; r.SetParent(parent, false); r.anchoredPosition = position; r.sizeDelta = size; return r; }
        private static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int font)
        { var t = Rect(name, parent, position, size).gameObject.AddComponent<Text>(); t.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = font; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false; return t; }
    }
}
