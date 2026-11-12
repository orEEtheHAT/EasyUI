using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EasyUI
{
    [Serializable]
    public sealed class RadialItem
    {
        public string Id;
        public string Label;
        public Sprite Icon;
        public bool Available = true;
        public UnityEvent OnSelected = new UnityEvent();
        public RadialItem() { }
        public RadialItem(string id, string label, bool available = true, Sprite icon = null)
        { Id = id; Label = label; Available = available; Icon = icon; }
    }

    [RequireComponent(typeof(UIWindow)), AddComponentMenu("EasyUI/Radial Menu")]
    public sealed class RadialMenu : MonoBehaviour
    {
        public RadialItem[] Items = Array.Empty<RadialItem>();
        [Min(0)] public float MouseDeadZone = 40;
        [Range(0, .95f)] public float StickDeadZone = .25f;
        [Min(60)] public float Radius = 180;
        [Tooltip("Optional. Defaults to the manager's UI Move action.")]
        public InputActionReference StickAction;
        public bool CloseOnConfirm = true;
        public int SelectedIndex { get; private set; } = -1;
        public RadialItem SelectedItem => SelectedIndex >= 0 && SelectedIndex < Items.Length ? Items[SelectedIndex] : null;
        public event Action<int> SelectionChanged;
        public event Action<RadialItem> Confirmed;
        private UIWindow window;
        private RectTransform rect;
        private readonly List<Image> views = new List<Image>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<Image> icons = new List<Image>();
        private Vector2 lastPointer, lastStick;
        private bool useStick;
        private int lastConfirmFrame = -1;
        private static readonly Color SelectedColor = new Color(.12f, .7f, .65f, 1);
        private static readonly Color NormalColor = new Color(.1f, .16f, .25f, .97f);

        private void Awake() { window = GetComponent<UIWindow>(); rect = (RectTransform)transform; }
        private void OnEnable()
        {
            if (!window) window = GetComponent<UIWindow>();
            rect = (RectTransform)transform;
            SetItems(Items);
            lastPointer = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            lastStick = Vector2.zero; useStick = false;
        }
        public void SetItems(IEnumerable<RadialItem> items)
        {
            string selectedId = SelectedItem?.Id;
            Items = items == null ? Array.Empty<RadialItem>() : new List<RadialItem>(items).ToArray();
            for (int i = 0; i < views.Count; i++) if (views[i]) Destroy(views[i].gameObject);
            views.Clear(); labels.Clear(); icons.Clear(); SelectedIndex = -1;
            for (int i = 0; i < Items.Length; i++)
            {
                var item = Items[i] ?? new RadialItem { Available = false };
                Items[i] = item;
                var go = new GameObject("Item " + item.Id, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform;
                float angle = i * Mathf.PI * 2 / Items.Length;
                r.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * Radius;
                r.sizeDelta = new Vector2(140, 72);
                var image = go.GetComponent<Image>(); image.raycastTarget = false;
                views.Add(image);
                var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                var tr = (RectTransform)textGo.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(4, 4); tr.offsetMax = new Vector2(-4, -4);
                var label = textGo.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 18; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; label.text = item.Label;
                labels.Add(label);
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(go.transform, false);
                var ir = (RectTransform)icon.transform; ir.sizeDelta = new Vector2(28, 28); ir.anchoredPosition = new Vector2(0, 42);
                var iconImage = icon.GetComponent<Image>(); iconImage.raycastTarget = false; icons.Add(iconImage);
                if (!string.IsNullOrEmpty(selectedId) && item.Id == selectedId && item.Available) SelectedIndex = i;
                RefreshItem(i);
            }
            window?.RefreshNavigation();
            // Repaint after resolving selection, including when the selected ID moved to another index.
            for (int i = 0; i < Items.Length; i++) Paint(i);
            Signals.Invoke(SelectionChanged, SelectedIndex);
        }
        /// <summary>Refresh presentation in place, retaining callbacks and the selected item. IDs must be unique.</summary>
        public bool UpdateItem(string id, string label, bool available = true, Sprite icon = null)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                var item = Items[i];
                if (item == null || item.Id != id) continue;
                item.Label = label; item.Available = available; item.Icon = icon;
                RefreshItem(i);
                if (!available && SelectedIndex == i)
                { SelectedIndex = -1; Paint(i); Signals.Invoke(SelectionChanged, -1); }
                return true;
            }
            return false;
        }
        private void RefreshItem(int index)
        {
            if (index >= labels.Count) return;
            if (labels[index].text != Items[index].Label) labels[index].text = Items[index].Label;
            icons[index].sprite = Items[index].Icon; icons[index].enabled = Items[index].Icon;
            Paint(index);
        }
        /// <summary>Zero/dead-zone and disabled sectors select nothing. Sector zero points up, order is clockwise.</summary>
        public void SelectDirection(Vector2 direction, float deadZone)
        {
            int index = -1;
            if (Items.Length > 0 && direction.magnitude > deadZone)
            {
                float angle = Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg + 180f / Items.Length, 360);
                index = Mathf.FloorToInt(angle / (360f / Items.Length)) % Items.Length;
                if (!Items[index].Available) index = -1;
            }
            if (index == SelectedIndex) return;
            int previous = SelectedIndex; SelectedIndex = index;
            Paint(previous); Paint(index);
            Signals.Invoke(SelectionChanged, index);
        }
        public bool ConfirmSelection()
        {
            if (!window || !window.IsActive || !window.Handle.Manager.InputReady || lastConfirmFrame == Time.frameCount) return false;
            var item = SelectedItem;
            if (item == null || !item.Available) return false;
            lastConfirmFrame = Time.frameCount;
            var handle = window.Handle;
            handle.Manager.FenceInput();
            // Snapshot listeners: an item callback can synchronously close/disable this window.
            var confirmed = Confirmed;
            bool close = CloseOnConfirm;
            Signals.Unity(item.OnSelected);
            Signals.Invoke(confirmed, item);
            if (close) handle.Close();
            return true;
        }
        private void Paint(int index)
        {
            if (index < 0 || index >= views.Count || !views[index]) return;
            views[index].color = Items[index].Available ? (index == SelectedIndex ? SelectedColor : NormalColor) : new Color(.15f, .15f, .15f, .6f);
            labels[index].color = Items[index].Available ? Color.white : Color.gray;
        }
        private void Update()
        {
            if (!window || !window.IsActive) return;
            var manager = window.Handle.Manager;
            if (!manager.InputReady || !manager.InputModule) return;
            var module = manager.InputModule;
            var stick = StickAction ? StickAction.action : module.move?.action;
            Vector2 stickValue = stick != null ? stick.ReadValue<Vector2>() : Vector2.zero;
            Vector2 pointer = module.point ? module.point.action.ReadValue<Vector2>() : lastPointer;
            if ((pointer - lastPointer).sqrMagnitude > 1) useStick = false;
            if ((stickValue - lastStick).sqrMagnitude > .001f && stickValue.sqrMagnitude > .001f) useStick = true;
            lastPointer = pointer; lastStick = stickValue;
            if (useStick) SelectDirection(stickValue, StickDeadZone);
            else { RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pointer, null, out var direction); SelectDirection(direction, MouseDeadZone); }
            if ((module.submit && module.submit.action.WasPerformedThisFrame()) || (module.leftClick && module.leftClick.action.WasPressedThisFrame())) ConfirmSelection();
        }
    }
}
