using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EasyUI.Tests
{
    public sealed class TestProvider : MonoBehaviour, IWindowInputProvider
    {
        public readonly HashSet<object> Owners = new HashSet<object>();
        public int Acquired, Released;
        public bool CanAcquire(WindowRequirements requirements, out string reason) { reason = null; return true; }
        public IDisposable Acquire(WindowHandle handle, WindowRequirements requirements)
        { var lease = new Lease(this); Owners.Add(lease); Acquired++; return lease; }
        private sealed class Lease : IDisposable
        {
            private TestProvider owner;
            public Lease(TestProvider owner) { this.owner = owner; }
            public void Dispose() { if (!owner) return; owner.Owners.Remove(this); owner.Released++; owner = null; }
        }
    }
    public sealed class DisableSensitiveRadialListener : MonoBehaviour
    {
        public int Count;
        private void OnEnable() => GetComponent<RadialMenu>().Confirmed += Confirmed;
        private void OnDisable() => GetComponent<RadialMenu>().Confirmed -= Confirmed;
        private void Confirmed(RadialItem _) => Count++;
    }
    public sealed class WindowTests
    {
        private readonly List<Object> created = new List<Object>();
        private WindowManager manager;
        private TestProvider provider;
        private EventSystem events;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior background;
        #if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        #endif
        private bool runInBackground;
        private GameObject New(string name, params Type[] types) { var go = new GameObject(name, types); created.Add(go); return go; }
        [SetUp] public void Setup()
        {
            runInBackground = Application.runInBackground; Application.runInBackground = true;
            background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            #if UNITY_EDITOR
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            #endif
            keyboard = InputSystem.AddDevice<Keyboard>(); gamepad = InputSystem.AddDevice<Gamepad>(); mouse = InputSystem.AddDevice<Mouse>();
            events = New("Test EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            manager = New("Test UI", typeof(RectTransform), typeof(WindowManager)).GetComponent<WindowManager>();
            manager.EventSystem = events;
            provider = manager.gameObject.AddComponent<TestProvider>(); manager.InputProvider = provider; manager.Initialize();
        }
        [TearDown] public void Cleanup()
        {
            if (manager) manager.CloseAll();
            for (int i = created.Count - 1; i >= 0; i--) if (created[i]) Object.DestroyImmediate(created[i]);
            created.Clear();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            #if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            #endif
            Application.runInBackground = runInBackground;
        }
        private UIWindow Window(string name, bool gameplay = true)
        {
            var go = New(name, typeof(RectTransform), typeof(Image), typeof(UIWindow));
            var window = go.GetComponent<UIWindow>();
            window.Policy = new WindowPolicy { BlockGameplay = gameplay, FadeIn = 0, FadeOut = 0 };
            Button(window, "First"); Button(window, "Second");
            go.SetActive(false); return window;
        }
        private Button Button(UIWindow parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent.transform, false);
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); return button;
        }
        private IEnumerator Ready() { yield return null; yield return null; yield return null; }

        [UnityTest] public IEnumerator NestedBackRestoresParentSelectionAndItsLease()
        {
            var parent = Window("Inventory"); var p = manager.Open(parent); yield return Ready();
            var second = parent.transform.GetChild(1).gameObject; events.SetSelectedGameObject(second);
            var c = manager.Open(Window("Settings"), p); yield return Ready();
            Assert.AreEqual(2, provider.Owners.Count);
            Assert.IsTrue(manager.Back()); Assert.IsFalse(manager.Back());
            Assert.AreEqual(WindowState.Closed, c.State); Assert.AreEqual(1, provider.Owners.Count);
            Assert.AreEqual(second, events.currentSelectedGameObject);
            yield return Ready(); manager.Back(); Assert.AreEqual(0, provider.Owners.Count);
        }
        [UnityTest] public IEnumerator MissingSelectionFallsBackAndModalRemovesLowerRaycasts()
        {
            var parent = Window("Parent"); var p = manager.Open(parent); yield return Ready();
            var second = parent.transform.GetChild(1).gameObject; events.SetSelectedGameObject(second);
            var c = manager.Open(Window("Child"), p); second.SetActive(false);
            Assert.IsFalse(parent.GetComponent<CanvasGroup>().blocksRaycasts);
            c.Close(); Assert.AreEqual(parent.transform.GetChild(0).gameObject, events.currentSelectedGameObject);
            Assert.IsTrue(parent.GetComponent<CanvasGroup>().blocksRaycasts);
        }
        [Test] public void RepeatedOpenAndOutOfOrderCloseDoNotDuplicateRequests()
        {
            var a = Window("A"); var first = manager.Open(a); var second = manager.Open(Window("B"));
            Assert.AreSame(first, manager.Open(a)); Assert.AreEqual(2, provider.Acquired);
            first.Close(); first.Close(); Assert.AreEqual(1, provider.Owners.Count); Assert.AreSame(second, manager.ActiveWindow);
            second.Close(); Assert.AreEqual(provider.Acquired, provider.Released);
        }
        [Test] public void ClosingParentClosesDescendantsAndAllowsReentrantUnrelatedOpen()
        {
            var p = manager.Open(Window("Parent")); var c = manager.Open(Window("Child"), p); var g = manager.Open(Window("Grandchild"), c);
            var other = Window("Other"); c.Closed += _ => manager.Open(other);
            p.Close(); Assert.AreEqual(WindowState.Closed, g.State); Assert.AreEqual(1, manager.Count); Assert.AreEqual(1, provider.Owners.Count);
        }
        [Test] public void ReopeningParentFromChildClosedListenerCancelsItsPendingClose()
        {
            var window = Window("Parent"); var parent = manager.Open(window); var child = manager.Open(Window("Child"), parent);
            child.Closed += _ => Assert.AreSame(parent, manager.Open(window));
            parent.Close();
            Assert.IsTrue(parent.IsOpen); Assert.AreEqual(1, manager.Count); Assert.AreEqual(1, provider.Owners.Count);
            Assert.AreEqual(2, provider.Acquired); manager.Close(parent, true); Assert.AreEqual(0, provider.Owners.Count);
        }
        [Test] public void DisableDestroyAndManagerShutdownReleaseEverything()
        {
            var a = Window("A"); var first = manager.Open(a); a.gameObject.SetActive(false);
            Assert.AreEqual(WindowState.Closed, first.State);
            var b = Window("B"); manager.Open(b); Object.DestroyImmediate(b.gameObject);
            Assert.AreEqual(0, provider.Owners.Count);
            manager.Open(Window("C")); manager.enabled = false; Assert.AreEqual(0, provider.Owners.Count);
        }
        [Test] public void MissingProviderRejectsBeforeCreatingAWindow()
        {
            manager.InputProvider = null;
            Assert.Throws<InvalidOperationException>(() => manager.Open(Window("Needs provider")));
            Assert.AreEqual(0, manager.Count);
            Assert.DoesNotThrow(() => manager.Open(Window("UI only", false)));
        }
        [UnityTest] public IEnumerator OpeningHeldSubmitDoesNotActivateChildAndCancelClosesOnce()
        {
            var parent = manager.Open(Window("Parent")); yield return Ready();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); InputSystem.Update();
            var child = Window("Child"); int clicks = 0; child.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(() => clicks++);
            var c = manager.Open(child, parent); yield return null; yield return null; Assert.AreEqual(0, clicks);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); yield return Ready();
            Assert.IsTrue(manager.InputReady);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
            manager.InputModule.Process(); manager.InputModule.Process();
            Assert.AreEqual(WindowState.Closed, c.State); Assert.IsTrue(parent.IsOpen);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        [UnityTest] public IEnumerator CancelCloseAnimationCanReopenWithoutAcquiringTwice()
        {
            var window = Window("Animated"); window.Policy.FadeOut = 1; var h = manager.Open(window);
            h.Close(); Assert.AreEqual(WindowState.Closing, h.State);
            Assert.AreSame(h, manager.Open(window)); Assert.IsTrue(h.IsOpen); Assert.AreEqual(1, provider.Acquired);
            manager.Close(h, true); yield return null; Assert.AreEqual(0, provider.Owners.Count);
        }
        [UnityTest] public IEnumerator GamepadNavigationCannotSubmitOutsideWindow()
        {
            var lower = Window("Lower"); lower.Policy.BlocksLowerWindows = false; manager.Open(lower);
            var top = Window("Top"); top.Policy.BlocksLowerWindows = false; manager.Open(top); yield return Ready();
            var a = top.transform.GetChild(0).GetComponent<Button>(); var b = lower.transform.GetChild(0).GetComponent<Button>();
            a.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = b };
            int lowerClicks = 0; b.onClick.AddListener(() => lowerClicks++);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.down }.WithButton(GamepadButton.South)); InputSystem.Update();
            manager.InputModule.Process();
            Assert.AreEqual(0, lowerClicks); Assert.IsTrue(events.currentSelectedGameObject.transform.IsChildOf(top.transform));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
        }
        [UnityTest] public IEnumerator DialogCompletesCancelledWhenItsParentIsDestroyed()
        {
            var pWindow = Window("Parent"); var p = manager.Open(pWindow);
            var dialogWindow = Window("Dialog"); var dialog = dialogWindow.gameObject.AddComponent<ConfirmationDialog>();
            manager.Open(dialogWindow, p); var task = dialog.Ask("Continue?"); Object.DestroyImmediate(pWindow.gameObject);
            yield return null; Assert.IsTrue(task.IsCompleted); Assert.AreEqual(ConfirmationResult.Cancelled, task.Result);
        }
        [UnityTest] public IEnumerator RadialCenterDisabledSectorAndConfirmationAreDeterministic()
        {
            var window = Window("Radial"); var radial = window.gameObject.AddComponent<RadialMenu>();
            manager.Open(window); radial.SetItems(new[] { new RadialItem("up", "Up"), new RadialItem("right", "Right", false), new RadialItem("down", "Down"), new RadialItem("left", "Left") });
            yield return Ready();
            radial.SelectDirection(Vector2.zero, .2f); Assert.AreEqual(-1, radial.SelectedIndex);
            radial.SelectDirection(Vector2.right, .2f); Assert.AreEqual(-1, radial.SelectedIndex);
            radial.SelectDirection(Vector2.up, .2f); Assert.AreEqual(0, radial.SelectedIndex);
            int called = 0; radial.Confirmed += _ => called++;
            Assert.IsTrue(radial.ConfirmSelection()); Assert.IsFalse(radial.ConfirmSelection()); Assert.AreEqual(1, called);
        }
        [UnityTest] public IEnumerator RadialRefreshRetainsViewSelectionAndDeliversBeforeImmediateDisable()
        {
            var window = Window("Radial refresh"); var radial = window.gameObject.AddComponent<RadialMenu>();
            var listener = window.gameObject.AddComponent<DisableSensitiveRadialListener>();
            manager.Open(window); radial.SetItems(new[] { new RadialItem("one", "One"), new RadialItem("two", "Two") });
            yield return Ready(); radial.SelectDirection(Vector2.up, .2f);
            var view = window.transform.Find("Item one");
            Assert.IsTrue(radial.UpdateItem("one", "Translated"));
            Assert.AreSame(view, window.transform.Find("Item one")); Assert.AreEqual("one", radial.SelectedItem.Id);
            Assert.AreEqual("Translated", view.Find("Label").GetComponent<Text>().text);
            radial.SetItems(new[] { new RadialItem("two", "Two"), new RadialItem("one", "One") });
            Assert.AreEqual(1, radial.SelectedIndex);
            Assert.IsTrue(radial.ConfirmSelection()); Assert.AreEqual(1, listener.Count); Assert.IsFalse(window.gameObject.activeSelf);
        }
        [Test] public void DisablingSelectedRadialItemClearsSelectionWithoutRebuilding()
        {
            var window = Window("Radial disabled"); var radial = window.gameObject.AddComponent<RadialMenu>(); manager.Open(window);
            radial.SetItems(new[] { new RadialItem("one", "One") }); radial.SelectDirection(Vector2.up, .2f);
            var view = window.transform.Find("Item one"); radial.UpdateItem("one", "Unavailable", false);
            Assert.IsNull(radial.SelectedItem); Assert.AreSame(view, window.transform.Find("Item one"));
        }
        [UnityTest] public IEnumerator OpeningPointerPressCannotReplayOnChildAfterRelease()
        {
            var parent = manager.Open(Window("Parent")); yield return Ready();
            var position = new Vector2(Screen.width / 2f, Screen.height / 2f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); InputSystem.Update();
            var child = Window("Child"); int clicks = 0;
            foreach (var button in child.GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => clicks++);
            manager.Open(child, parent); Canvas.ForceUpdateCanvases(); manager.InputModule.Process();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); InputSystem.Update(); manager.InputModule.Process();
            yield return Ready(); manager.InputModule.Process(); Assert.AreEqual(0, clicks);
        }
        [UnityTest] public IEnumerator ModalShieldWinsActualRaycastOutsideWindow()
        {
            var parent = Window("Parent"); var p = manager.Open(parent); var child = manager.Open(Window("Child"), p); yield return Ready();
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = new Vector2(5, 5) }, hits);
            Assert.IsNotEmpty(hits); Assert.AreEqual("Modal input shield", hits[0].gameObject.name);
            Assert.IsFalse(hits[0].gameObject.transform.IsChildOf(parent.transform)); child.Close();
        }
        [UnityTest] public IEnumerator TopModalReceivesRealPointerClickAboveItsShield()
        {
            var p = manager.Open(Window("Parent")); var child = Window("Top modal"); manager.Open(child, p); yield return Ready();
            Canvas.ForceUpdateCanvases();
            var canvas = child.GetComponent<Canvas>(); Assert.IsTrue(canvas.overrideSorting);
            Assert.Greater(canvas.sortingOrder, p.Window.GetComponent<Canvas>().sortingOrder);
            var position = RectTransformUtility.WorldToScreenPoint(null, child.transform.position);
            var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = position }, hits);
            Assert.IsNotEmpty(hits); Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(child.transform), hits[0].gameObject.name);
            int clicks = 0; foreach (var button in child.GetComponentsInChildren<Button>()) button.onClick.AddListener(() => clicks++);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); InputSystem.Update(); manager.InputModule.Process();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); InputSystem.Update(); manager.InputModule.Process();
            Assert.AreEqual(1, clicks);
        }
        [UnityTest] public IEnumerator FirstModalBlocksLauncherButtonsOnTheManagersRootCanvas()
        {
            var launcher = new GameObject("Launcher", typeof(RectTransform), typeof(Image), typeof(Button));
            launcher.transform.SetParent(manager.transform, false); ((RectTransform)launcher.transform).anchoredPosition = new Vector2(250, 0);
            manager.Open(Window("Modal")); yield return Ready(); Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, launcher.transform.position);
            var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = point }, hits);
            Assert.IsNotEmpty(hits); Assert.AreEqual("Modal input shield", hits[0].gameObject.name);
        }
        [Test] public void DisablingOnlyTheWindowComponentHidesAndUnblocksItsView()
        {
            var window = Window("Disabled component"); var h = manager.Open(window); window.enabled = false;
            Assert.AreEqual(WindowState.Closed, h.State); Assert.AreEqual(0, provider.Owners.Count);
            Assert.AreEqual(0, window.GetComponent<CanvasGroup>().alpha); Assert.IsFalse(window.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.Throws<InvalidOperationException>(() => manager.Open(window));
        }
        [UnityTest] public IEnumerator UnloadingManagersSceneReleasesAllOwnedRequests()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("EasyUI unload regression");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(manager.gameObject, scene);
            var p = manager.Open(Window("Parent")); manager.Open(Window("Child"), p);
            var leaseTracker = provider;
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            Assert.AreEqual(0, leaseTracker.Owners.Count); Assert.AreEqual(leaseTracker.Acquired, leaseTracker.Released);
        }
    }
}
