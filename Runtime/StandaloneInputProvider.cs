using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyUI
{
    public interface IWindowGameplayGate { IDisposable BlockGameplay(object owner); }

    [DisallowMultipleComponent, AddComponentMenu("EasyUI/Standalone Input Provider")]
    public sealed class StandaloneInputProvider : MonoBehaviour, IWindowInputProvider
    {
        [Tooltip("Gameplay actions to suspend. Do not assign UI actions. Their enabled state is restored after the last request and a neutral input frame.")]
        public InputActionReference[] GameplayActions = Array.Empty<InputActionReference>();
        [Tooltip("Optional components implementing IWindowGameplayGate, e.g. the EasyBuilding adapter.")]
        public MonoBehaviour[] GameplayGates = Array.Empty<MonoBehaviour>();
        public int ActiveRequestCount => requests.Count;
        public bool GameplayBlocked { get; private set; }
        public event Action<bool> GameplayBlockChanged;
        private readonly List<Request> requests = new List<Request>();
        private readonly List<ActionState> actionStates = new List<ActionState>();
        private readonly List<IDisposable> pendingGates = new List<IDisposable>();
        private bool cursorCaptured, savedVisible, pendingRelease;
        private CursorLockMode savedLock;
        private int releaseFrame, neutralFrame = -1;
        private struct ActionState { public InputAction Action; public bool Enabled; }
        private sealed class Request : IDisposable
        {
            internal StandaloneInputProvider Provider;
            internal WindowRequirements Requirements;
            internal readonly List<IDisposable> Gates = new List<IDisposable>();
            public void Dispose() { var provider = Provider; Provider = null; if (provider) provider.Release(this); }
        }

        public bool CanAcquire(WindowRequirements requirements, out string reason)
        {
            if (requirements.BlockedActions != null && requirements.BlockedActions.Count > 0)
            { reason = "Named Blocked Actions require EasyInput Window Provider or a custom provider supporting action paths. Standalone Input Provider suspends its configured Gameplay Actions as a group."; return false; }
            if (!isActiveAndEnabled) { reason = "Enable the Standalone Input Provider."; return false; }
            if (requirements.BlockGameplay)
            {
                bool configured = false;
                for (int i = 0; i < GameplayActions.Length; i++) if (GameplayActions[i] && GameplayActions[i].action != null) configured = true;
                for (int i = 0; i < GameplayGates.Length; i++)
                {
                    if (!(GameplayGates[i] is IWindowGameplayGate) || !GameplayGates[i].isActiveAndEnabled)
                    { reason = "Each Gameplay Gate must be an enabled component implementing IWindowGameplayGate."; return false; }
                    configured = true;
                }
                if (!configured) { reason = "Assign Gameplay Actions or a Gameplay Gate to Standalone Input Provider, or disable Block Gameplay on the window."; return false; }
            }
            reason = null; return true;
        }
        public IDisposable Acquire(WindowHandle window, WindowRequirements requirements)
        {
            if (!CanAcquire(requirements, out string reason)) throw new InvalidOperationException(reason);
            var request = new Request { Provider = this, Requirements = requirements };
            try
            {
                if (requirements.BlockGameplay)
                {
                    for (int i = 0; i < GameplayGates.Length; i++)
                        request.Gates.Add(((IWindowGameplayGate)GameplayGates[i]).BlockGameplay(request) ?? throw new InvalidOperationException("Gameplay Gate returned a null lease."));
                    if (!GameplayBlocked)
                    {
                        for (int i = 0; i < GameplayActions.Length; i++)
                        {
                            var action = GameplayActions[i] ? GameplayActions[i].action : null;
                            if (action == null || actionStates.Exists(s => s.Action == action)) continue;
                            actionStates.Add(new ActionState { Action = action, Enabled = action.enabled });
                            action.Disable();
                        }
                        GameplayBlocked = true;
                    }
                    pendingRelease = false;
                    DisposePending();
                }
                requests.Add(request);
                ApplyCursor();
            }
            catch
            {
                foreach (var gate in request.Gates) gate.Dispose();
                if (!HasGameplayRequest()) RestoreGameplay();
                request.Provider = null;
                throw;
            }
            if (requirements.BlockGameplay) Signals.Invoke(GameplayBlockChanged, true);
            return request;
        }
        private bool HasGameplayRequest()
        { for (int i = 0; i < requests.Count; i++) if (requests[i].Requirements.BlockGameplay) return true; return false; }
        private void Release(Request request)
        {
            if (!requests.Remove(request)) return;
            bool lastGameplay = request.Requirements.BlockGameplay && !HasGameplayRequest();
            foreach (var gate in request.Gates)
            {
                if (lastGameplay) pendingGates.Add(gate);
                else try { gate.Dispose(); } catch (Exception e) { Debug.LogException(e); }
            }
            request.Gates.Clear();
            if (lastGameplay) { pendingRelease = true; releaseFrame = Time.frameCount; neutralFrame = -1; }
            ApplyCursor();
        }
        private void ApplyCursor()
        {
            var cursor = WindowCursor.Inherit;
            for (int i = requests.Count - 1; i >= 0; i--)
                if (requests[i].Requirements.Cursor != WindowCursor.Inherit) { cursor = requests[i].Requirements.Cursor; break; }
            if (cursor == WindowCursor.Inherit)
            {
                if (cursorCaptured) { Cursor.lockState = savedLock; Cursor.visible = savedVisible; cursorCaptured = false; }
                return;
            }
            if (!cursorCaptured) { savedLock = Cursor.lockState; savedVisible = Cursor.visible; cursorCaptured = true; }
            Cursor.lockState = cursor == WindowCursor.Locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = cursor == WindowCursor.Visible;
        }
        private void LateUpdate()
        {
            if (!pendingRelease) return;
            bool held = InputReleaseGate.AnyButtonHeld();
            for (int i = 0; !held && i < actionStates.Count; i++) held = InputReleaseGate.Held(actionStates[i].Action);
            if (held || Time.frameCount <= releaseFrame) { neutralFrame = -1; return; }
            if (neutralFrame < 0) { neutralFrame = Time.frameCount; return; }
            if (Time.frameCount > neutralFrame) RestoreGameplay();
        }
        private void DisposePending()
        {
            foreach (var gate in pendingGates) try { gate.Dispose(); } catch (Exception e) { Debug.LogException(e); }
            pendingGates.Clear();
        }
        private void RestoreGameplay()
        {
            pendingRelease = false;
            DisposePending();
            foreach (var state in actionStates) if (state.Enabled) state.Action.Enable();
            actionStates.Clear();
            bool changed = GameplayBlocked;
            GameplayBlocked = false;
            if (changed) Signals.Invoke(GameplayBlockChanged, false);
        }
        private void OnDisable()
        {
            for (int i = requests.Count - 1; i >= 0; i--) requests[i].Dispose();
            RestoreGameplay();
            ApplyCursor();
        }
    }
}
