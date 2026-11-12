using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyUI.Tests
{
    public sealed class StandaloneProviderTests
    {
        private GameObject go;
        private StandaloneInputProvider provider;
        private InputAction jump, dormant;
        private InputActionAsset asset;
        private InputActionReference jumpReference, dormantReference;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior background;
        #if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        #endif
        private bool visible;
        private CursorLockMode cursorLock;
        [SetUp] public void Setup()
        {
            background = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            #if UNITY_EDITOR
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode; InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            #endif
            visible = Cursor.visible; cursorLock = Cursor.lockState;
            keyboard = InputSystem.AddDevice<Keyboard>();
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Gameplay");
            jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            dormant = map.AddAction("Dormant", InputActionType.Button, "<Keyboard>/q");
            jump.Enable();
            jumpReference = InputActionReference.Create(jump); dormantReference = InputActionReference.Create(dormant);
            go = new GameObject("Provider", typeof(StandaloneInputProvider)); provider = go.GetComponent<StandaloneInputProvider>(); provider.GameplayActions = new[] { jumpReference, dormantReference };
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(go); Object.DestroyImmediate(jumpReference); Object.DestroyImmediate(dormantReference);
            if (asset) { asset.Disable(); Object.DestroyImmediate(asset); } InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background;
            #if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            #endif
            Cursor.lockState = cursorLock; Cursor.visible = visible;
        }
        [UnityTest] public IEnumerator OwnRequestsReleaseOutOfOrderAndRestoreOnlyPreviouslyEnabledActionsAfterNeutral()
        {
            var a = provider.Acquire(null, new WindowRequirements(true, WindowCursor.Visible));
            var b = provider.Acquire(null, new WindowRequirements(true, WindowCursor.Hidden));
            Assert.IsFalse(jump.enabled); Assert.IsFalse(dormant.enabled); Assert.IsFalse(Cursor.visible);
            a.Dispose(); a.Dispose(); Assert.AreEqual(1, provider.ActiveRequestCount); Assert.IsFalse(jump.enabled); Assert.IsFalse(Cursor.visible);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
            b.Dispose(); yield return null; yield return null; Assert.IsFalse(jump.enabled); Assert.IsTrue(provider.GameplayBlocked);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            yield return null; yield return null; yield return null; yield return null;
            Assert.IsTrue(jump.enabled); Assert.IsFalse(dormant.enabled); Assert.IsFalse(provider.GameplayBlocked);
            Assert.AreEqual(cursorLock, Cursor.lockState); Assert.AreEqual(visible, Cursor.visible);
        }
        [Test] public void MissingGameplayConfigurationIsActionable()
        {
            provider.GameplayActions = Array.Empty<InputActionReference>();
            Assert.IsFalse(provider.CanAcquire(new WindowRequirements(true, WindowCursor.Inherit), out string reason));
            Assert.That(reason, Does.Contain("Gameplay Actions"));
        }
        [Test] public void DisablingProviderReleasesAllLeasesAndRestoresActionState()
        {
            var a = provider.Acquire(null, new WindowRequirements(true, WindowCursor.Visible));
            var b = provider.Acquire(null, new WindowRequirements(true, WindowCursor.Visible));
            provider.enabled = false; a.Dispose(); b.Dispose();
            Assert.AreEqual(0, provider.ActiveRequestCount); Assert.IsTrue(jump.enabled); Assert.IsFalse(dormant.enabled);
        }
    }
}
