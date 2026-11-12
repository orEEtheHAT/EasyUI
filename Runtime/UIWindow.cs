using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EasyUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform), typeof(CanvasGroup)), AddComponentMenu("EasyUI/UI Window")]
    public class UIWindow : MonoBehaviour
    {
        public WindowPolicy Policy = new WindowPolicy();
        [Tooltip("Optional. The first available Selectable is used otherwise.")]
        public Selectable FirstSelected;
        [SerializeField] private UnityEvent onOpened = new UnityEvent();
        [SerializeField] private UnityEvent onClosed = new UnityEvent();
        public UnityEvent OnOpened => onOpened;
        public UnityEvent OnClosed => onClosed;
        public WindowHandle Handle { get; internal set; }
        public WindowState State => Handle?.State ?? WindowState.Closed;
        public bool IsActive => Handle != null && Handle.IsActive;
        public event Action Opened;
        public event Action<WindowCloseReason> Closed;
        internal CanvasGroup Group;
        internal Canvas Canvas;
        private readonly List<Selectable> selectables = new List<Selectable>();
        private readonly List<MonoBehaviour> backHandlers = new List<MonoBehaviour>();

        public void Close() => Handle?.Close();
        public void RefreshNavigation() { GetComponentsInChildren(true, selectables); }
        internal void Prepare()
        {
            Group = GetComponent<CanvasGroup>();
            Canvas = GetComponent<Canvas>();
            if (!Canvas) Canvas = gameObject.AddComponent<Canvas>();
            Canvas.overrideSorting = true;
            if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            RefreshNavigation();
        }
        internal bool Contains(GameObject target) => target && (target.transform == transform || target.transform.IsChildOf(transform));
        internal bool Available(GameObject target)
        {
            if (!Contains(target) || !target.activeInHierarchy) return false;
            var selectable = target.GetComponent<Selectable>();
            return selectable && selectable.IsActive() && selectable.IsInteractable();
        }
        internal GameObject DefaultSelection()
        {
            if (FirstSelected && Available(FirstSelected.gameObject)) return FirstSelected.gameObject;
            for (int i = 0; i < selectables.Count; i++)
                if (selectables[i] && Available(selectables[i].gameObject)) return selectables[i].gameObject;
            // Dynamic content may have changed since opening. No scan on idle frames.
            RefreshNavigation();
            for (int i = 0; i < selectables.Count; i++)
                if (selectables[i] && Available(selectables[i].gameObject)) return selectables[i].gameObject;
            return null;
        }
        internal bool ConsumeBack()
        {
            GetComponents(backHandlers);
            for (int i = 0; i < backHandlers.Count; i++)
                if (backHandlers[i].isActiveAndEnabled && backHandlers[i] is IWindowBackHandler handler && handler.HandleBack()) return true;
            return false;
        }
        internal void NotifyOpened() { Signals.Invoke(Opened); Signals.Unity(onOpened); }
        internal void NotifyClosed(WindowCloseReason reason) { Signals.Invoke(Closed, reason); Signals.Unity(onClosed); }
        protected virtual void OnDisable() { if (Handle != null && Handle.Manager) Handle.Manager.WindowUnavailable(Handle, WindowCloseReason.Disabled); }
        protected virtual void OnDestroy() { if (Handle != null && Handle.Manager) Handle.Manager.WindowUnavailable(Handle, WindowCloseReason.Destroyed); }
    }
}
