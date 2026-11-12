using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyUI
{
    public enum WindowState { Opening, Open, Closing, Closed }
    public enum WindowCursor { Inherit, Visible, Locked, Hidden }
    public enum WindowCloseReason { Closed, Back, ParentClosed, Disabled, Destroyed, ManagerDisabled }

    [Serializable]
    public sealed class WindowPolicy
    {
        [Tooltip("Receives keyboard/gamepad navigation. Turn off for a passive HUD.")]
        public bool AcceptsInput = true;
        [Tooltip("Stops clicks and navigation reaching lower windows.")]
        public bool BlocksLowerWindows = true;
        [Tooltip("Requires an input provider. Each window owns its own request.")]
        public bool BlockGameplay;
        [Tooltip("Optional individual action paths/IDs, e.g. Gameplay/Look. Requires a provider supporting named actions (EasyInput). Leave Block Gameplay off to keep other actions enabled.")]
        public string[] BlockedActions = Array.Empty<string>();
        [Tooltip("Requires an input provider unless Inherit is selected.")]
        public WindowCursor Cursor = WindowCursor.Inherit;
        public bool CloseOnBack = true;
        [Min(0)] public float FadeIn = .12f;
        [Min(0)] public float FadeOut = .1f;

        public WindowPolicy Copy()
        { var copy = (WindowPolicy)MemberwiseClone(); copy.BlockedActions = (string[])(BlockedActions ?? Array.Empty<string>()).Clone(); return copy; }
        public WindowRequirements Requirements => new WindowRequirements(BlockGameplay, Cursor, AcceptsInput, BlockedActions);
        public static WindowPolicy HUD => new WindowPolicy { AcceptsInput = false, BlocksLowerWindows = false, CloseOnBack = false, FadeIn = 0, FadeOut = 0 };
    }

    public readonly struct WindowRequirements
    {
        public readonly bool BlockGameplay;
        public readonly WindowCursor Cursor;
        public readonly bool AcceptsInput;
        public readonly IReadOnlyList<string> BlockedActions;
        public bool NeedsProvider => BlockGameplay || Cursor != WindowCursor.Inherit || (BlockedActions != null && BlockedActions.Count > 0);
        public WindowRequirements(bool blockGameplay, WindowCursor cursor, bool acceptsInput = true, string[] blockedActions = null)
        {
            BlockGameplay = blockGameplay; Cursor = cursor; AcceptsInput = acceptsInput;
            BlockedActions = blockedActions == null || blockedActions.Length == 0 ? Array.Empty<string>() : Array.AsReadOnly((string[])blockedActions.Clone());
        }
    }

    /// <summary>Main-thread contract. Acquire must either return a unique lease or leave no side effects.
    /// Dispose is idempotent, may occur out of order, and only releases that lease.
    /// The provider owns aggregation, cursor restoration and neutral-input handoff to gameplay.</summary>
    public interface IWindowInputProvider
    {
        bool CanAcquire(WindowRequirements requirements, out string reason);
        IDisposable Acquire(WindowHandle window, WindowRequirements requirements);
    }

    /// <summary>Optional provider hook for assigning the player's current UI actions.</summary>
    public interface IWindowUIActions { void ConfigureUI(EasyUIInputModule module); }
    public interface IWindowInputGuard { bool SuppressUIInput { get; } }

    /// <summary>Return true to consume this Back before the window can close (e.g. cancel a rebind).</summary>
    public interface IWindowBackHandler { bool HandleBack(); }
}
