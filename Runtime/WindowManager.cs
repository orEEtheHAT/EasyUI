using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EasyUI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)), AddComponentMenu("EasyUI/Window Manager")]
    public sealed class WindowManager : MonoBehaviour
    {
        [Tooltip("Optional: a component implementing IWindowInputProvider. Required for gameplay or cursor policies.")]
        public MonoBehaviour InputProvider;
        [Tooltip("Optional per-player EventSystem. A dedicated EventSystem is created if none exists.")]
        public EventSystem EventSystem;
        public int BaseSortingOrder = 100;
        public Color ModalShade = new Color(0, 0, 0, .45f);
        public WindowHandle ActiveWindow { get; private set; }
        public int Count => windows.Count;
        public bool InputReady => !fenced && isActiveAndEnabled;
        public EasyUIInputModule InputModule { get; private set; }
        public event Action<WindowHandle> WindowOpened;
        public event Action<WindowHandle> WindowClosed;
        public event Action<WindowHandle> ActiveWindowChanged;
        private readonly List<WindowHandle> windows = new List<WindowHandle>(8);
        private readonly List<InputAction> releaseActions = new List<InputAction>(2);
        private bool initialized, ownsEventSystem, shuttingDown, reconciling, dirty;
        private bool fenced;
        private int fenceFrame, neutralFrame = -1, backFrame = -1;
        private int lifecycleDepth;
        private BaseInputModule previousModule;
        private bool previousModuleEnabled;
        private GameObject originalSelection;
        private GameObject inputShield;
        private float nextMove;
        private Vector2 lastMove;
        private static readonly ProfilerMarker OpenMarker = new ProfilerMarker("EasyUI.Open");
        private static readonly ProfilerMarker CloseMarker = new ProfilerMarker("EasyUI.Close");
        private static readonly ProfilerMarker NavigationMarker = new ProfilerMarker("EasyUI.Navigation");

        public WindowHandle GetWindow(int index) => windows[index];
        public WindowHandle Open(WindowDefinition definition, WindowHandle parent = null, InputAction openingAction = null)
        {
            if (!definition) throw new ArgumentNullException(nameof(definition));
            if (!definition.Prefab) throw new InvalidOperationException("Assign a Prefab in the EasyUI Window Definition.");
            return OpenCore(definition.Prefab, definition, definition.Policy, parent, true, openingAction);
        }
        public WindowHandle Open(UIWindow window, WindowHandle parent = null, InputAction openingAction = null)
        {
            if (!window) throw new ArgumentNullException(nameof(window));
            bool prefab = !window.gameObject.scene.IsValid();
            return OpenCore(window, window, window.Policy, parent, prefab, openingAction);
        }
        public bool TryOpen(WindowDefinition definition, out WindowHandle handle, out string reason, WindowHandle parent = null)
        {
            handle = null;
            try { handle = Open(definition, parent); reason = null; return true; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { reason = e.Message; return false; }
        }

        private WindowHandle OpenCore(UIWindow source, UnityEngine.Object key, WindowPolicy policy, WindowHandle parent, bool instantiate, InputAction opener)
        {
            using var marker = OpenMarker.Auto();
            if (shuttingDown || !isActiveAndEnabled) throw new InvalidOperationException("Enable the Window Manager before opening a window.");
            if (!source.enabled) throw new InvalidOperationException("Enable the UIWindow component before opening this window.");
            Initialize();
            if (parent != null && (parent.Manager != this || !parent.IsOpen)) throw new InvalidOperationException("The parent must be an open window of this manager.");
            for (int i = 0; i < windows.Count; i++)
            {
                var existing = windows[i];
                if (existing.Key != key) continue;
                if (existing.Parent != parent) throw new InvalidOperationException("This window is already open with a different parent. Close it before changing its parent.");
                if (existing.State == WindowState.Closing)
                {
                    if (!existing.Window || !existing.Window.isActiveAndEnabled)
                        throw new InvalidOperationException("Cannot reopen a window while its object is being disabled or destroyed.");
                    existing.State = WindowState.Opening;
                    FenceInput(opener);
                    Reconcile();
                }
                return existing; // Reuse without changing order or duplicating leases/events.
            }
            policy = (policy ?? new WindowPolicy()).Copy();
            var provider = InputProvider as IWindowInputProvider;
            bool requestInput = policy.Requirements.NeedsProvider || (provider != null && policy.AcceptsInput);
            if (requestInput)
            {
                if (provider == null || !InputProvider.isActiveAndEnabled)
                    throw new InvalidOperationException("This window requests gameplay/cursor control. Assign an enabled IWindowInputProvider to Window Manager, or turn Block Gameplay off, clear Blocked Actions and choose Cursor = Inherit.");
                if (!provider.CanAcquire(policy.Requirements, out string reason)) throw new InvalidOperationException(reason);
            }
            if (!instantiate && source.Handle != null) throw new InvalidOperationException("This scene window already belongs to another manager.");
            var window = instantiate ? Instantiate(source, transform, false) : source;
            var handle = new WindowHandle { Window = window, Manager = this, Parent = parent, Key = key, Policy = policy,
                State = WindowState.Opening, OwnsInstance = instantiate, OriginalParent = window.transform.parent, OriginalSibling = window.transform.GetSiblingIndex() };
            try
            {
                RememberSelection();
                window.gameObject.SetActive(false);
                window.Prepare();
                window.transform.SetParent(transform, false);
                window.Handle = handle;
                window.Group.alpha = handle.Alpha = policy.FadeIn > 0 ? 0 : 1;
                if (policy.FadeIn <= 0) handle.State = WindowState.Open;
                windows.Add(handle);
                // Publish the pending instance before provider events. A reentrant Open reuses it,
                // and Close can finish it while Acquire is still notifying context subscribers.
                if (requestInput)
                {
                    var lease = provider.Acquire(handle, policy.Requirements) ?? throw new InvalidOperationException("The input provider returned a null request handle.");
                    if (handle.Completed || !window || !isActiveAndEnabled) { lease.Dispose(); return handle; }
                    handle.InputLease = lease;
                }
                if (policy.BlocksLowerWindows) handle.Blocker = CreateBlocker();
                window.gameObject.SetActive(true);
                FenceInput(opener);
                Reconcile();
            }
            catch
            {
                windows.Remove(handle);
                handle.InputLease?.Dispose();
                handle.InputLease = null;
                if (window && window.Handle == handle) window.Handle = null;
                if (handle.Blocker) Destroy(handle.Blocker);
                if (instantiate && window) Destroy(window.gameObject);
                handle.Manager = null;
                handle.State = WindowState.Closed;
                Reconcile();
                throw;
            }
            // Reentrant selection/active-window callbacks can already have closed this handle.
            if (handle.IsOpen && window)
            {
                window.NotifyOpened();
                if (handle.IsOpen) Signals.Invoke(WindowOpened, handle);
            }
            return handle;
        }

        public void Close(WindowHandle handle, bool immediate = false) => CloseCore(handle, WindowCloseReason.Closed, immediate);
        private void CloseCore(WindowHandle handle, WindowCloseReason reason, bool immediate)
        {
            using var marker = CloseMarker.Auto();
            if (handle == null || handle.Manager != this || handle.State == WindowState.Closed) return;
            if (handle.State == WindowState.Closing && !immediate) return;
            RememberSelection();
            // Mark the whole subtree before dispatching any event. Reentrant Open cannot attach to a closing parent.
            for (int i = 0; i < windows.Count; i++)
            {
                var item = windows[i];
                if (item == handle || DescendsFrom(item, handle))
                { item.State = WindowState.Closing; item.CloseReason = item == handle ? reason : WindowCloseReason.ParentClosed; }
            }
            FenceInput();
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (i >= windows.Count) continue;
                var item = windows[i];
                if (item.State == WindowState.Closing && (item == handle || DescendsFrom(item, handle)) && (immediate || item.Policy.FadeOut <= 0)) FinishClose(item);
            }
            Reconcile();
        }
        public void CloseAll()
        {
            bool previous = shuttingDown;
            shuttingDown = true;
            try { while (windows.Count > 0) CloseCore(windows[windows.Count - 1], WindowCloseReason.ManagerDisabled, true); }
            finally { shuttingDown = previous; }
        }
        private static bool DescendsFrom(WindowHandle item, WindowHandle parent)
        { for (var p = item.Parent; p != null; p = p.Parent) if (p == parent) return true; return false; }

        public bool Back()
        {
            if (backFrame == Time.frameCount || (!InputReady && !(InputProvider is IWindowInputGuard guard && guard.SuppressUIInput))) return false;
            var recipient = ActiveWindow;
            if (recipient == null || !recipient.IsOpen || !recipient.Window) return false;
            backFrame = Time.frameCount;
            FenceInput();
            if (recipient.Window.ConsumeBack()) return true;
            if (recipient.Policy.CloseOnBack) CloseCore(recipient, WindowCloseReason.Back, false);
            return true;
        }
        internal void WindowUnavailable(WindowHandle handle, WindowCloseReason reason)
        {
            handle.InLifecycleCallback = true;
            lifecycleDepth++;
            try { CloseCore(handle, reason, true); }
            finally { lifecycleDepth--; handle.InLifecycleCallback = false; }
        }

        private void FinishClose(WindowHandle handle)
        {
            if (handle.Completed || handle.Manager != this) return;
            // Complete descendants first even when their fade duration is longer.
            for (int i = windows.Count - 1; i >= 0; i--)
                if (i < windows.Count && DescendsFrom(windows[i], handle)) FinishClose(windows[i]);
            // A descendant's Closed listener may explicitly reopen this still-live parent.
            if (handle.State != WindowState.Closing) return;
            handle.Completed = true;
            windows.Remove(handle);
            handle.State = WindowState.Closed;
            var lease = handle.InputLease;
            handle.InputLease = null;
            try { lease?.Dispose(); } catch (Exception e) { Debug.LogException(e); }
            if (handle.Blocker) { handle.Blocker.SetActive(false); Destroy(handle.Blocker); }
            var window = handle.Window;
            if (window)
            {
                window.Handle = null;
                if (window.Group) { window.Group.alpha = 0; window.Group.interactable = false; window.Group.blocksRaycasts = false; }
                if (!handle.InLifecycleCallback || !window.enabled) window.gameObject.SetActive(false);
                if (!handle.OwnsInstance)
                {
                    // Unity forbids reparenting during its own SetActive/Destroy callbacks.
                    if (lifecycleDepth == 0 && !handle.InLifecycleCallback && window.transform.parent != handle.OriginalParent)
                    { window.transform.SetParent(handle.OriginalParent, false); window.transform.SetSiblingIndex(handle.OriginalSibling); }
                }
                else Destroy(window.gameObject);
            }
            Reconcile();
            if (window) window.NotifyClosed(handle.CloseReason);
            handle.NotifyClosed();
            Signals.Invoke(WindowClosed, handle);
            handle.Manager = null;
        }

        public void Initialize()
        {
            if (initialized) return;
            var canvas = GetComponent<Canvas>();
            if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = BaseSortingOrder;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            if (!EventSystem) EventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (!EventSystem)
            {
                EventSystem = new GameObject("EasyUI EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                EventSystem.transform.SetParent(transform, false);
                ownsEventSystem = true;
            }
            var existing = EventSystem.GetComponent<EasyUIInputModule>();
            if (existing && existing.Owner && existing.Owner != this) throw new InvalidOperationException("Each Window Manager needs its own EventSystem. Assign a separate per-player EventSystem.");
            originalSelection = EventSystem.currentSelectedGameObject;
            previousModule = EventSystem.currentInputModule;
            if (!previousModule) previousModule = EventSystem.GetComponent<BaseInputModule>();
            previousModuleEnabled = previousModule && previousModule.enabled;
            if (previousModule && previousModule != existing) previousModule.enabled = false;
            InputModule = existing ? existing : EventSystem.gameObject.AddComponent<EasyUIInputModule>();
            InputModule.Owner = this;
            InputModule.enabled = true;
            if (!InputModule.actionsAsset) InputModule.AssignDefaultActions();
            if (InputProvider is IWindowUIActions actions) actions.ConfigureUI(InputModule);
            inputShield = CreateBlocker();
            inputShield.name = "Input release shield";
            inputShield.GetComponent<Image>().color = Color.clear;
            inputShield.SetActive(false);
            initialized = true;
        }
        private GameObject CreateBlocker()
        {
            var go = new GameObject("Modal input shield", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = ModalShade;
            go.GetComponent<Canvas>().overrideSorting = true;
            return go;
        }
        private void RememberSelection()
        {
            if (!EventSystem) return;
            var selected = EventSystem.currentSelectedGameObject;
            if (ActiveWindow != null && ActiveWindow.Window && ActiveWindow.Window.Available(selected)) ActiveWindow.Selection = selected;
        }
        private void Reconcile()
        {
            dirty = true;
            if (reconciling) return;
            reconciling = true;
            try
            {
                for (int pass = 0; dirty && pass < 32; pass++)
                {
                    dirty = false;
                    int modal = -1;
                    for (int i = windows.Count - 1; i >= 0; i--) if (windows[i].Policy.BlocksLowerWindows) { modal = i; break; }
                    WindowHandle active = null;
                    for (int i = 0; i < windows.Count; i++)
                    {
                        var item = windows[i];
                        if (!item.Window) continue;
                        // Unity can reset this when an inactive Canvas is reparented/enabled.
                        // Reassert it after activation so the modal shield remains BELOW its window.
                        item.Window.Canvas.overrideSorting = true;
                        item.Window.Canvas.sortingOrder = BaseSortingOrder + i * 2 + 2;
                        if (item.Blocker) item.Blocker.GetComponent<Canvas>().sortingOrder = BaseSortingOrder + i * 2 + 1;
                        bool interactive = item.Policy.AcceptsInput && i >= modal && item.IsOpen;
                        item.Window.Group.interactable = interactive;
                        item.Window.Group.blocksRaycasts = interactive;
                        if (interactive) active = item;
                    }
                    var previous = ActiveWindow;
                    if (previous != active) RememberSelection();
                    ActiveWindow = active;
                    RestoreActiveSelection();
                    if (previous != active) Signals.Invoke(ActiveWindowChanged, active);
                }
            }
            finally { reconciling = false; }
        }
        internal void RestoreActiveSelection()
        {
            if (!EventSystem) return;
            var active = ActiveWindow;
            if (active == null || !active.Window)
            { if (EventSystem.currentSelectedGameObject) EventSystem.SetSelectedGameObject(null); return; }
            var window = active.Window;
            var current = EventSystem.currentSelectedGameObject;
            if (window.Available(current)) { active.Selection = current; return; }
            var next = window.Available(active.Selection) ? active.Selection : window.DefaultSelection();
            if (current != next) EventSystem.SetSelectedGameObject(next);
            active.Selection = next;
        }

        public void FenceInput(InputAction openingAction = null)
        {
            fenced = true; fenceFrame = Time.frameCount; neutralFrame = -1; lastMove = Vector2.zero;
            if (inputShield) { inputShield.GetComponent<Canvas>().sortingOrder = BaseSortingOrder + windows.Count * 2 + 3; inputShield.SetActive(true); }
            if (openingAction != null && !releaseActions.Contains(openingAction)) releaseActions.Add(openingAction);
        }
        private bool ControlsHeld(EasyUIInputModule module)
        {
            if (InputReleaseGate.Held(module.submit?.action) || InputReleaseGate.Held(module.cancel?.action) ||
                InputReleaseGate.Held(module.leftClick?.action) || InputReleaseGate.Held(module.rightClick?.action) ||
                InputReleaseGate.Held(module.middleClick?.action) || InputReleaseGate.Held(module.move?.action)) return true;
            for (int i = 0; i < releaseActions.Count; i++) if (InputReleaseGate.Held(releaseActions[i])) return true;
            return false;
        }
        private void PollFence()
        {
            if (!fenced || !InputModule) return;
            if (InputProvider is IWindowInputGuard guard && guard.SuppressUIInput) { neutralFrame = -1; return; }
            if (Time.frameCount <= fenceFrame || ControlsHeld(InputModule)) { neutralFrame = -1; return; }
            if (neutralFrame < 0) { neutralFrame = Time.frameCount; return; }
            if (Time.frameCount <= neutralFrame) return;
            fenced = false; releaseActions.Clear();
            if (inputShield) inputShield.SetActive(false);
        }
        internal bool ProcessInput(EasyUIInputModule module)
        {
            using var marker = NavigationMarker.Auto();
            PollFence();
            if (!InputReady) return false;
            RestoreActiveSelection();
            if (ActiveWindow == null) return true;
            if (module.cancel && module.cancel.action.WasPerformedThisFrame()) { Back(); return false; }
            var selected = EventSystem.currentSelectedGameObject;
            Vector2 move = module.move ? module.move.action.ReadValue<Vector2>() : Vector2.zero;
            if (move.sqrMagnitude < .25f) { lastMove = Vector2.zero; nextMove = 0; }
            else if (selected && (lastMove == Vector2.zero || Vector2.Dot(lastMove, move) <= 0 || Time.unscaledTime >= nextMove))
            {
                bool initial = lastMove == Vector2.zero || Vector2.Dot(lastMove, move) <= 0;
                var axis = new AxisEventData(EventSystem) { moveVector = move, moveDir = Mathf.Abs(move.x) > Mathf.Abs(move.y) ? (move.x > 0 ? MoveDirection.Right : MoveDirection.Left) : (move.y > 0 ? MoveDirection.Up : MoveDirection.Down) };
                ExecuteEvents.Execute(selected, axis, ExecuteEvents.moveHandler);
                lastMove = move; nextMove = Time.unscaledTime + (initial ? module.moveRepeatDelay : module.moveRepeatRate);
                RestoreActiveSelection(); // Explicit navigation may point outside the active window.
            }
            if (!InputReady) return false;
            selected = EventSystem.currentSelectedGameObject;
            if (selected && module.submit && module.submit.action.WasPerformedThisFrame())
                ExecuteEvents.Execute(selected, new BaseEventData(EventSystem), ExecuteEvents.submitHandler);
            return InputReady;
        }
        private void Update()
        {
            PollFence();
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (i >= windows.Count) continue;
                var item = windows[i];
                if (!item.Window) { CloseCore(item, WindowCloseReason.Destroyed, true); continue; }
                if (item.State == WindowState.Opening)
                {
                    item.Alpha = Mathf.MoveTowards(item.Alpha, 1, Time.unscaledDeltaTime / Mathf.Max(.0001f, item.Policy.FadeIn));
                    item.Window.Group.alpha = item.Alpha;
                    if (item.Alpha >= 1) item.State = WindowState.Open;
                }
                else if (item.State == WindowState.Closing)
                {
                    item.Alpha = Mathf.MoveTowards(item.Alpha, 0, Time.unscaledDeltaTime / Mathf.Max(.0001f, item.Policy.FadeOut));
                    item.Window.Group.alpha = item.Alpha;
                    if (item.Alpha <= 0) FinishClose(item);
                }
            }
            if (dirty) Reconcile();
        }
        private void OnEnable()
        {
            if (!initialized) return;
            if (previousModule && previousModule != InputModule) previousModule.enabled = false;
            if (InputModule) InputModule.enabled = true;
        }
        private void OnDisable()
        {
            lifecycleDepth++;
            try { CloseAll(); } finally { lifecycleDepth--; }
            if (inputShield) inputShield.SetActive(false);
            if (InputModule) InputModule.enabled = false;
            if (!ownsEventSystem && previousModule && previousModule != InputModule) previousModule.enabled = previousModuleEnabled;
        }
        private void OnDestroy()
        {
            CloseAll();
            if (InputModule && InputModule.Owner == this) { InputModule.Owner = null; if (!ownsEventSystem) Destroy(InputModule); }
            if (!ownsEventSystem && previousModule) previousModule.enabled = previousModuleEnabled;
            if (!ownsEventSystem && EventSystem && originalSelection && originalSelection.activeInHierarchy) EventSystem.SetSelectedGameObject(originalSelection);
        }
    }
}
