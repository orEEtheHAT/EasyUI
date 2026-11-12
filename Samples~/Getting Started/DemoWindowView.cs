using UnityEngine;
using UnityEngine.UI;

namespace EasyUI.Samples
{
    public enum DemoWindowKind { Inventory, Settings }
    public sealed class DemoWindowView : MonoBehaviour
    {
        public DemoWindowKind Kind;
        public Button Primary;
        public Button Secondary;
        public Button Back;
        public void Bind(EasyUIDemo demo, WindowHandle handle)
        {
            Primary.onClick.RemoveAllListeners(); Secondary.onClick.RemoveAllListeners(); Back.onClick.RemoveAllListeners();
            if (Kind == DemoWindowKind.Inventory)
            {
                Primary.onClick.AddListener(() => demo.Manager.Open(demo.Settings, handle));
                Secondary.onClick.AddListener(() => demo.Manager.Open(demo.Radial, handle));
            }
            else
            {
                Primary.onClick.AddListener(() => demo.Ask(handle));
                Secondary.onClick.AddListener(() => demo.SetStatus("Settings preview changed. Confirm to apply."));
            }
            Back.onClick.AddListener(handle.Close);
        }
    }
}
