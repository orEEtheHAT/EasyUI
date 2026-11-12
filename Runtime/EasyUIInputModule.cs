using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace EasyUI
{
    /// <summary>Standard InputSystemUIInputModule pointer processing with window-scoped navigation.
    /// Created automatically by WindowManager; assign custom UI actions normally.</summary>
    [AddComponentMenu("EasyUI/EasyUI Input Module")]
    public sealed class EasyUIInputModule : InputSystemUIInputModule
    {
        public WindowManager Owner { get; internal set; }
        public override void Process()
        {
            if (!Owner) { base.Process(); return; }
            bool ready = Owner.ProcessInput(this);
            // Drain pointer press/release state against the transparent input shield while fenced.
            // Skipping base.Process would replay a held opening click after the fence was removed.
            if (!ready) eventSystem.SetSelectedGameObject(null);
            bool navigation = eventSystem.sendNavigationEvents;
            eventSystem.sendNavigationEvents = false;
            try { base.Process(); }
            finally { eventSystem.sendNavigationEvents = navigation; }
            if (Owner && ready && Owner.InputReady) Owner.RestoreActiveSelection();
        }
    }
}
