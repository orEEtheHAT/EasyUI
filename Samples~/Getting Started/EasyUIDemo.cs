using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EasyUI.Samples
{
    public sealed class EasyUIDemo : MonoBehaviour
    {
        public WindowManager Manager;
        public StandaloneInputProvider Provider;
        public DemoGameplayGate Gameplay;
        public UIWindow HUD;
        public WindowDefinition Inventory, Settings, Confirmation, Radial;
        public Text Status, InputStatus, PulseStatus;
        private InputAction inventory, radialPress, radialHold, pulse;
        private WindowHandle radialHandle;
        private int pulses;
        private void Awake()
        {
            inventory = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/i"); inventory.AddBinding("<Gamepad>/start");
            radialPress = new InputAction("Radial toggle", InputActionType.Button, "<Keyboard>/tab"); radialPress.AddBinding("<Gamepad>/rightShoulder");
            radialHold = new InputAction("Radial hold", InputActionType.Button, "<Keyboard>/r"); radialHold.AddBinding("<Gamepad>/leftShoulder");
            pulse = new InputAction("Gameplay pulse", InputActionType.Button, "<Keyboard>/space");
        }
        private void OnEnable()
        {
            inventory.Enable(); radialPress.Enable(); radialHold.Enable(); pulse.Enable();
            Manager.WindowOpened += Opened; Manager.ActiveWindowChanged += Changed; Manager.WindowClosed += Changed;
            Provider.GameplayBlockChanged += GameplayChanged;
        }
        private void Start() { Manager.Open(HUD); SetStatus("Ready. Open inventory with I / Start."); Changed(null); }
        private void Update()
        {
            if (inventory.WasPressedThisFrame() && Manager.ActiveWindow == null) Manager.Open(Inventory, openingAction: inventory);
            if (radialPress.WasPressedThisFrame())
            {
                if (radialHandle != null && radialHandle.IsOpen) radialHandle.Close();
                else radialHandle = Manager.Open(Radial, Manager.ActiveWindow, radialPress);
            }
            if (radialHold.WasPressedThisFrame() && (radialHandle == null || !radialHandle.IsOpen)) radialHandle = Manager.Open(Radial, Manager.ActiveWindow);
            if (radialHold.WasReleasedThisFrame() && radialHandle != null && radialHandle.IsOpen)
            { radialHandle.Window.GetComponent<RadialMenu>().ConfirmSelection(); radialHandle.Close(); }
            if (!Gameplay.Blocked && pulse.WasPressedThisFrame()) { pulses++; PulseStatus.text = "GAMEPLAY PULSES  " + pulses; }
        }
        private void Opened(WindowHandle handle)
        {
            var view = handle.Window.GetComponent<DemoWindowView>(); if (view) view.Bind(this, handle);
            var radial = handle.Window.GetComponent<RadialMenu>(); if (radial) { radialHandle = handle; radial.Confirmed += item => SetStatus("Selected: " + item.Label); }
            Changed(handle);
        }
        public void Ask(WindowHandle parent)
        {
            var h = Manager.Open(Confirmation, parent);
            var dialog = h.Window.GetComponent<ConfirmationDialog>();
            dialog.Ask("Apply these settings?");
            dialog.Completed += result => SetStatus("Dialog result: " + result);
        }
        public void SetStatus(string text) { if (Status) Status.text = text; }
        private void GameplayChanged(bool blocked) => Changed(null);
        private void Changed(WindowHandle _)
        {
            if (InputStatus) InputStatus.text = "ACTIVE  " + (Manager.ActiveWindow?.Window ? Manager.ActiveWindow.Window.name.Replace("(Clone)", "") : "Gameplay") + "     /     INPUT REQUESTS  " + Provider.ActiveRequestCount;
        }
        private void OnDisable()
        {
            inventory?.Disable(); radialPress?.Disable(); radialHold?.Disable(); pulse?.Disable();
            if (Manager) { Manager.WindowOpened -= Opened; Manager.ActiveWindowChanged -= Changed; Manager.WindowClosed -= Changed; }
            if (Provider) Provider.GameplayBlockChanged -= GameplayChanged;
        }
        private void OnDestroy() { inventory?.Dispose(); radialPress?.Dispose(); radialHold?.Dispose(); pulse?.Dispose(); }
    }
}
