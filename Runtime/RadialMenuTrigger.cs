using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyUI
{
    public enum RadialOpenMode { Press, Hold }
    [AddComponentMenu("EasyUI/Radial Menu Trigger")]
    public sealed class RadialMenuTrigger : MonoBehaviour
    {
        public WindowManager Manager;
        public WindowDefinition Menu;
        public InputActionReference OpenAction;
        public RadialOpenMode Mode = RadialOpenMode.Hold;
        private InputAction action;
        private WindowHandle handle;
        private void OnEnable()
        {
            if (!OpenAction || OpenAction.action == null) return;
            action = OpenAction.action.Clone(); action.Enable();
        }
        private void Update()
        {
            if (action == null || !Manager || !Menu) return;
            if (action.WasPressedThisFrame())
            {
                if (handle != null && handle.IsOpen && Mode == RadialOpenMode.Press) { handle.Close(); return; }
                handle = Manager.Open(Menu, openingAction: Mode == RadialOpenMode.Press ? action : null);
            }
            if (Mode == RadialOpenMode.Hold && action.WasReleasedThisFrame() && handle != null && handle.IsOpen)
            {
                var radial = handle.Window.GetComponent<RadialMenu>();
                if (radial) radial.ConfirmSelection();
                handle.Close();
            }
        }
        private void OnDisable() { handle?.Close(); handle = null; action?.Dispose(); action = null; }
    }
}
