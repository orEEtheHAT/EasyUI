using System;
using UnityEngine;

namespace EasyUI
{
    public sealed class WindowHandle : IDisposable
    {
        public UIWindow Window { get; internal set; }
        public WindowManager Manager { get; internal set; }
        public WindowHandle Parent { get; internal set; }
        public WindowState State { get; internal set; } = WindowState.Closed;
        public WindowCloseReason CloseReason { get; internal set; }
        public bool IsOpen => State == WindowState.Open || State == WindowState.Opening;
        public bool IsVisible => State != WindowState.Closed;
        public bool IsActive => Manager && Manager.ActiveWindow == this;
        public WindowRequirements Requirements => Policy?.Requirements ?? default;
        public bool AcceptsInput => Policy != null && Policy.AcceptsInput;
        public bool BlocksLowerWindows => Policy != null && Policy.BlocksLowerWindows;
        public bool ClosesOnBack => Policy != null && Policy.CloseOnBack;
        public event Action<WindowHandle> Closed;
        internal UnityEngine.Object Key;
        internal WindowPolicy Policy;
        internal IDisposable InputLease;
        internal GameObject Blocker;
        internal GameObject Selection;
        internal bool OwnsInstance;
        internal Transform OriginalParent;
        internal int OriginalSibling;
        internal float Alpha;
        internal bool Completed;
        internal bool InLifecycleCallback;

        public void Close() { if (Manager) Manager.Close(this); }
        public void Dispose() => Close();
        internal void NotifyClosed()
        {
            var listeners = Closed;
            Closed = null;
            Signals.Invoke(listeners, this);
        }
    }

    internal static class Signals
    {
        internal static void Invoke(Action action)
        {
            if (action == null) return;
            foreach (Action listener in action.GetInvocationList())
                try { listener(); } catch (Exception e) { Debug.LogException(e); }
        }
        internal static void Invoke<T>(Action<T> action, T value)
        {
            if (action == null) return;
            foreach (Action<T> listener in action.GetInvocationList())
                try { listener(value); } catch (Exception e) { Debug.LogException(e); }
        }
        internal static void Unity(UnityEngine.Events.UnityEvent action)
        { try { action?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }
    }
}
