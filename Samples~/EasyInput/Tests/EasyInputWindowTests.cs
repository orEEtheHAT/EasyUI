using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using EasyInput;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyUI.Integrations.Input.Tests
{
    public sealed class TestSettingsText : MonoBehaviour, IInputSettingsTextProvider
    {
        public string Prefix = "";
        public event System.Action Changed;
        public string GetText(string key, string fallback) => Prefix + fallback;
        public void Change(string prefix) { Prefix = prefix; Changed?.Invoke(); }
    }
    public sealed class EasyInputWindowTests
    {
        private GameObject root, ui, eventObject;
        private PlayerInputSettings player;
        private WindowManager manager;
        private EasyInputWindowProvider provider;
        private readonly List<Object> assets = new List<Object>();
        private Keyboard keyboard;
        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior background;
        #if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        #endif
        [SetUp] public void Setup()
        {
            background = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            #if UNITY_EDITOR
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode; InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            #endif
            keyboard = InputSystem.AddDevice<Keyboard>(); gamepad = InputSystem.AddDevice<Gamepad>();
            root = new GameObject("Player"); root.SetActive(false); player = root.AddComponent<PlayerInputSettings>(); player.ControlsCursor = false; root.SetActive(true); player.PairDevices(keyboard, gamepad);
            ui = new GameObject("UI", typeof(RectTransform), typeof(WindowManager)); manager = ui.GetComponent<WindowManager>();
            provider = ui.AddComponent<EasyInputWindowProvider>(); provider.Player = player; manager.InputProvider = provider;
            eventObject = new GameObject("Events", typeof(EventSystem)); manager.EventSystem = eventObject.GetComponent<EventSystem>(); manager.Initialize();
        }
        [TearDown] public void Cleanup()
        {
            try
            {
                if (manager) manager.CloseAll(); if (ui) Object.DestroyImmediate(ui); if (root) Object.DestroyImmediate(root); if (eventObject) Object.DestroyImmediate(eventObject);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
                foreach (var asset in assets) if (asset) Object.DestroyImmediate(asset); assets.Clear();
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = background;
                #if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                #endif
            }
        }
        private UIWindow Window(string name, bool settings = false)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.SetActive(false); go.transform.SetParent(ui.transform, false);
            var window = go.AddComponent<UIWindow>(); window.Policy = new WindowPolicy { BlockGameplay = true, Cursor = WindowCursor.Visible, FadeIn = 0, FadeOut = 0 };
            if (settings) go.AddComponent<InputSettingsWindow>(); return window;
        }
        private IEnumerator Ready() { yield return null; yield return null; yield return null; }
        [UnityTest] public IEnumerator RealContextsStayOwnedAcrossNestedWindowsAndExternalRequest()
        {
            using var external = player.AcquireContext(root, new InputContextRequest { BlockedActions = new[] { "Gameplay/Look" } });
            var a = manager.Open(Window("A")); var b = manager.Open(Window("B"), a); yield return Ready();
            Assert.AreEqual(3, player.ContextRequestCount); Assert.IsFalse(player.Action("Gameplay/Jump").enabled); Assert.IsTrue(player.Action("UI/Submit").enabled);
            b.Close(); Assert.AreEqual(2, player.ContextRequestCount); Assert.IsFalse(player.Action("Gameplay/Jump").enabled);
            a.Close(); yield return Ready(); Assert.AreEqual(1, player.ContextRequestCount); Assert.IsTrue(player.Action("Gameplay/Jump").enabled); Assert.IsFalse(player.Action("Gameplay/Look").enabled);
        }
        [UnityTest] public IEnumerator CancelFirstEndsRebindThenNextPressClosesWindow()
        {
            var window = Window("Controls", true); var handle = manager.Open(window); var settings = window.GetComponent<InputSettingsWindow>(); yield return Ready();
            settings.BeginRebind("Gameplay/Jump", 0); Assert.IsTrue(player.Rebind.IsActive);
            Assert.IsTrue(manager.Back()); Assert.IsFalse(player.Rebind.IsActive); Assert.IsTrue(handle.IsOpen);
            yield return Ready(); Assert.IsTrue(manager.Back()); Assert.IsFalse(handle.IsOpen);
        }
        [UnityTest] public IEnumerator GamepadCancelIsNotAssignedAsNewBinding()
        {
            var window = Window("Controls", true); var h = manager.Open(window); yield return Ready();
            var settings = window.GetComponent<InputSettingsWindow>(); string previous = player.Action("Gameplay/Jump").bindings[0].effectivePath;
            settings.BeginRebind("Gameplay/Jump", 0); yield return new WaitForSecondsRealtime(.18f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East)); InputSystem.Update();
            Assert.IsFalse(player.Rebind.IsActive); Assert.AreEqual(RebindStatus.Cancelled, player.Rebind.Status); Assert.IsTrue(h.IsOpen);
            Assert.AreEqual(previous, player.Action("Gameplay/Jump").bindings[0].effectivePath);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update(); yield return Ready();
        }
        [UnityTest] public IEnumerator RebindCancelCannotLeakIntoGameplayResumedByAUiOnlyWindow()
        {
            var window = Window("UI-only controls", true); window.Policy.BlockGameplay = false;
            var h = manager.Open(window); yield return Ready();
            int gameplayCancels = 0; player.Action("Gameplay/CancelInteraction").performed += _ => gameplayCancels++;
            window.GetComponent<InputSettingsWindow>().BeginRebind("Gameplay/Jump", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
            Assert.IsFalse(player.Rebind.IsActive); Assert.IsTrue(h.IsOpen); Assert.AreEqual(0, gameplayCancels);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); yield return Ready();
            Assert.IsTrue(player.Action("Gameplay/CancelInteraction").enabled);
        }
        [UnityTest] public IEnumerator SettingsExtensionUsesOneViewAndUnsubscribesAfterDisable()
        {
            var window = Window("Controls extension", true); var settings = window.GetComponent<InputSettingsWindow>();
            var source = root.AddComponent<TestSettingsText>(); settings.TextProvider = source;
            int built = 0; settings.ViewBuilt += rect => { Assert.IsNotNull(rect); built++; };
            var h = manager.Open(window); yield return Ready(); Assert.AreEqual(1, built); Assert.IsNotNull(settings.ExtrasRoot);
            source.Change("RU "); yield return null; yield return null;
            Assert.IsTrue(System.Array.Exists(window.GetComponentsInChildren<UnityEngine.UI.Text>(), x => x.text == "RU Controls"));
            h.Close(); source.Change("DE "); manager.Open(window); yield return Ready(); Assert.AreEqual(2, built);
            Assert.AreEqual(1, window.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Length);
            Assert.IsTrue(System.Array.Exists(window.GetComponentsInChildren<UnityEngine.UI.Text>(), x => x.text == "DE Controls"));
        }
        [UnityTest] public IEnumerator SettingsWindowUsesRuntimeCaptureRestoreAndPresentation()
        {
            var h = manager.Open(Window("Controls", true)); var settings = h.Window.GetComponent<InputSettingsWindow>(); yield return Ready();
            settings.Capture(); player.MouseSensitivity = 2.3f;
            Assert.IsTrue(player.TrySetBinding(player.Action("Gameplay/Jump"), 0, "<Keyboard>/k", out var reason), reason);
            yield return null; Assert.That(player.GetPresentation("Gameplay/Jump", 0).Text, Does.Contain("K").IgnoreCase);
            settings.Restore(); Assert.AreEqual(1, player.MouseSensitivity, .001f); Assert.That(player.GetPresentation("Gameplay/Jump", 0).Text, Does.Not.Contain("K"));
            h.Close(); Assert.IsFalse(player.Rebind?.IsActive == true);
        }
        [UnityTest] public IEnumerator BindingRowsAreVisibleInsideViewportAndSliderHandlesFitTracks()
        {
            var handle = manager.Open(Window("Controls", true)); yield return Ready(); Canvas.ForceUpdateCanvases();
            var scroll = handle.Window.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            Assert.Greater(scroll.content.childCount, 0);
            var first = (RectTransform)scroll.content.GetChild(0);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, first);
            Assert.IsTrue(scroll.viewport.rect.Contains(bounds.center), "The first binding row must start inside the viewport, not below the content midpoint.");
            foreach (var slider in handle.Window.GetComponentsInChildren<UnityEngine.UI.Slider>())
                Assert.LessOrEqual(slider.handleRect.rect.height, ((RectTransform)slider.transform).rect.height + .1f);
        }
        [UnityTest] public IEnumerator PartialRestrictionsAndOutOfOrderClosureKeepMoveEnabled()
        {
            var look = Window("Look only"); look.Policy.BlockGameplay = false; look.Policy.BlockedActions = new[] { "Gameplay/Look" };
            var fire = Window("Fire only"); fire.Policy.BlockGameplay = false; fire.Policy.BlockedActions = new[] { "Gameplay/Fire" };
            var a = manager.Open(look); var b = manager.Open(fire); yield return Ready();
            Assert.IsFalse(player.Action("Gameplay/Look").enabled); Assert.IsFalse(player.Action("Gameplay/Fire").enabled); Assert.IsTrue(player.Action("Gameplay/Move").enabled);
            a.Close(); Assert.IsTrue(player.Action("Gameplay/Look").enabled); Assert.IsFalse(player.Action("Gameplay/Fire").enabled); Assert.AreEqual(1, player.ContextRequestCount);
            fire.gameObject.SetActive(false); Assert.AreEqual(0, player.ContextRequestCount); Assert.IsTrue(player.Action("Gameplay/Fire").enabled); Assert.AreEqual(WindowState.Closed, b.State);
        }
        [UnityTest] public IEnumerator ModuleEnableDisableCannotOverrideContextsAndCancelWorksWhileModuleDisabled()
        {
            var window = Window("Controls", true); var h = manager.Open(window); yield return Ready();
            manager.InputModule.enabled = false; manager.InputModule.enabled = true;
            Assert.IsFalse(player.Action("Gameplay/Jump").enabled); Assert.IsTrue(player.Action("UI/Cancel").enabled);
            window.GetComponent<InputSettingsWindow>().BeginRebind("Gameplay/Jump", 0);
            manager.InputModule.enabled = false; manager.InputModule.enabled = true;
            Assert.IsFalse(player.Action("UI/Submit").enabled); Assert.IsFalse(player.Action("Gameplay/Jump").enabled);
            manager.InputModule.enabled = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
            Assert.IsFalse(player.Rebind.IsActive); Assert.IsTrue(h.IsOpen);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            manager.InputModule.enabled = true; yield return Ready();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update(); manager.InputModule.Process();
            Assert.IsFalse(h.IsOpen); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        [UnityTest] public IEnumerator CustomPlayerMapAndHiddenInheritCursorAreRespected()
        {
            bool visible = Cursor.visible; var cursorLock = Cursor.lockState;
            manager.InputModule.enabled = false; Object.DestroyImmediate(root);
            var source = InputActionAsset.FromJson(Resources.Load<InputActionAsset>("EasyInputDefaults").ToJson().Replace("\"Gameplay\"", "\"Player\"")); assets.Add(source);
            var profile = ScriptableObject.CreateInstance<InputProfile>(); assets.Add(profile); profile.DefaultMaps = new[] { "Player" }; profile.DefaultCursor = InputCursorPolicy.Inherit;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            root = new GameObject("Custom player"); root.SetActive(false); player = root.AddComponent<PlayerInputSettings>(); player.Configure(source, profile); root.SetActive(true); player.PairDevices(keyboard, gamepad);
            provider.Player = player; provider.GameplayMaps = new[] { "Player" }; provider.ConfigureUI(manager.InputModule); manager.InputModule.enabled = true;
            try
            {
                var window = Window("Partial custom"); window.Policy.BlockGameplay = false; window.Policy.BlockedActions = new[] { "Player/Look" }; window.Policy.Cursor = WindowCursor.Hidden;
                var h = manager.Open(window); yield return Ready(); Assert.IsFalse(player.Action("Player/Look").enabled); Assert.IsTrue(player.Action("Player/Move").enabled);
                Assert.AreEqual(CursorLockMode.None, Cursor.lockState); Assert.IsFalse(Cursor.visible);
                h.Close(); yield return Ready(); Assert.IsTrue(Cursor.visible); Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
                var full = manager.Open(Window("Full custom")); Assert.IsFalse(player.Action("Player/Move").enabled); full.Close();
                Assert.IsTrue(player.Action("Player/Move").enabled);
            }
            finally { player.ControlsCursor = false; Cursor.lockState = cursorLock; Cursor.visible = visible; }
        }
        [UnityTest] public IEnumerator ExceptionAndReentrantListenerDoNotLeakContexts()
        {
            var a = Window("A"); var b = Window("B");
            manager.WindowOpened += handle => { if (handle.Window == a) throw new System.InvalidOperationException("Expected subscriber failure"); };
            manager.WindowOpened += handle => { if (handle.Window == a) { manager.Open(b); handle.Close(); } };
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Expected subscriber failure"));
            manager.Open(a); yield return Ready(); Assert.AreEqual(1, manager.Count); Assert.AreEqual(1, player.ContextRequestCount);
            manager.CloseAll(); Assert.AreEqual(0, player.ContextRequestCount);
        }
        [Test] public void ContextSubscriberCanCloseWindowWhileItsLeaseIsBeingAcquired()
        {
            bool handled = false;
            player.ContextChanged += () => { if (!handled && manager.Count == 1) { handled = true; manager.Close(manager.GetWindow(0), true); } };
            var handle = manager.Open(Window("Pending window"));
            Assert.IsTrue(handled); Assert.AreEqual(WindowState.Closed, handle.State); Assert.AreEqual(0, manager.Count); Assert.AreEqual(0, player.ContextRequestCount);
        }
        [Test] public void ContextSubscriberCanOpenAnotherWindowDuringAcquisition()
        {
            bool handled = false; var other = Window("Other");
            player.ContextChanged += () => { if (!handled && manager.Count == 1) { handled = true; manager.Open(other); } };
            var first = manager.Open(Window("First"));
            Assert.IsTrue(handled); Assert.AreEqual(2, manager.Count); Assert.AreEqual(2, player.ContextRequestCount);
            first.Close(); Assert.AreEqual(1, player.ContextRequestCount); manager.CloseAll(); Assert.AreEqual(0, player.ContextRequestCount);
        }
    }
}
