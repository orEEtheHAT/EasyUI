using System;
using System.Collections.Generic;
using EasyInput;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace EasyUI.Integrations.Input
{
    [AddComponentMenu("EasyUI/Integrations/EasyInput Window Provider")]
    public sealed class EasyInputWindowProvider : MonoBehaviour, IWindowInputProvider, IWindowUIActions, IWindowInputGuard
    {
        public PlayerInputSettings Player;
        public string UIMap = "UI";
        public string[] GameplayMaps = { "Gameplay", "Building" };
        public int CursorPriority = 100;
        public bool SuppressUIInput => Player && Player.Rebind?.IsActive == true;
        private EasyUIInputModule module;
        private readonly List<InputActionReference> links = new List<InputActionReference>();
        private readonly List<Lease> leases = new List<Lease>();
        private PlayerInputSettings subscribed;
        private sealed class Lease : IDisposable
        {
            internal EasyInputWindowProvider Provider;
            internal InputContextHandle Context;
            public void Dispose()
            {
                var provider = Provider; Provider = null; Context?.Dispose(); Context = null;
                if (provider) provider.leases.Remove(this);
            }
        }
        public bool CanAcquire(WindowRequirements requirements, out string reason)
        {
            if (!Player || !Player.isActiveAndEnabled) { reason = "Assign an enabled EasyInput Player Input Settings to EasyInput Window Provider."; return false; }
            if (Player.Actions.FindActionMap(UIMap, false) == null) { reason = "The EasyInput UI action map is missing: " + UIMap; return false; }
            if (requirements.BlockGameplay)
                foreach (string map in GameplayMaps) if (Player.Actions.FindActionMap(map, false) == null) { reason = "Unknown gameplay map: " + map + ". Match Gameplay Maps to the player's actions."; return false; }
            if (requirements.BlockedActions != null)
                foreach (string action in requirements.BlockedActions) if (Player.Actions.FindAction(action, false) == null) { reason = "Unknown blocked action: " + action; return false; }
            reason = null; return true;
        }
        public IDisposable Acquire(WindowHandle window, WindowRequirements requirements)
        {
            if (!CanAcquire(requirements, out string reason)) throw new InvalidOperationException(reason);
            var request = new InputContextRequest
            {
                EnabledMaps = requirements.AcceptsInput ? new[] { UIMap } : Array.Empty<string>(),
                BlockedMaps = requirements.BlockGameplay ? GameplayMaps : Array.Empty<string>(),
                BlockedActions = requirements.BlockedActions == null ? Array.Empty<string>() : new List<string>(requirements.BlockedActions).ToArray(),
                Cursor = requirements.Cursor == WindowCursor.Visible ? InputCursorPolicy.Free : requirements.Cursor == WindowCursor.Locked ? InputCursorPolicy.Locked : requirements.Cursor == WindowCursor.Hidden ? InputCursorPolicy.Hidden : InputCursorPolicy.Inherit,
                Priority = CursorPriority
            };
            var lease = new Lease { Provider = this, Context = Player.AcquireContext(window.Window, request) };
            leases.Add(lease); return lease;
        }
        public void ConfigureUI(EasyUIInputModule target)
        {
            if (!CanAcquire(new WindowRequirements(false, WindowCursor.Inherit), out string reason)) throw new InvalidOperationException(reason);
            Unsubscribe();
            module = target;
            bool enabled = module.enabled; module.enabled = false;
            module.actionsAsset = Player.Actions;
            module.point = Link("Point"); module.leftClick = Link("Click"); module.scrollWheel = Link("Scroll");
            module.move = Link("Navigate"); module.submit = Link("Submit"); module.cancel = Link("Cancel");
            module.rightClick = null; module.middleClick = null; module.trackedDevicePosition = null; module.trackedDeviceOrientation = null;
            module.enabled = enabled;
            subscribed = Player; subscribed.RebindChanged += RebindChanged;
            InputSystem.onEvent += InputEvent;
        }
        private InputActionReference Link(string action)
        { var link = InputActionReference.Create(Player.Action(UIMap + "/" + action)); links.Add(link); return link; }
        private void RebindChanged() { if (module && module.Owner) module.Owner.FenceInput(); }
        private void InputEvent(InputEventPtr inputEvent, InputDevice device)
        {
            if (!SuppressUIInput || !module || !module.cancel || !inputEvent.IsA<StateEvent>() && !inputEvent.IsA<DeltaStateEvent>()) return;
            // Registered before BeginRebind's operation: Cancel cannot become the new binding.
            foreach (var control in module.cancel.action.controls)
            {
                if (control.device != device || !(control is ButtonControl button)) continue;
                if (button.ReadValueFromEvent(inputEvent, out float value) && value >= button.pressPointOrDefault)
                {
                    // In Input System 1.20 this preserves device state but suppresses action
                    // notifications. Maps resumed by CancelRebind must not receive this same press.
                    inputEvent.handled = true;
                    Player.CancelRebind(); if (module.Owner) module.Owner.FenceInput(); return;
                }
            }
        }
        private void Unsubscribe()
        {
            if (subscribed) subscribed.RebindChanged -= RebindChanged; subscribed = null;
            InputSystem.onEvent -= InputEvent;
            foreach (var link in links) if (link) Destroy(link); links.Clear();
        }
        private void OnDisable()
        {
            if (module && module.Owner) module.Owner.CloseAll();
            for (int i = leases.Count - 1; i >= 0; i--) leases[i].Dispose();
            Unsubscribe();
        }
        private void OnEnable() { if (module && Player) ConfigureUI(module); }
    }
}
