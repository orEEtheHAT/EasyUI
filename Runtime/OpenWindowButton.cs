using UnityEngine;
using UnityEngine.UI;

namespace EasyUI
{
    [RequireComponent(typeof(Button)), AddComponentMenu("EasyUI/Open Window Button")]
    public sealed class OpenWindowButton : MonoBehaviour
    {
        [Tooltip("Optional inside a managed window. Outside one, assign the player's Window Manager.")]
        public WindowManager Manager;
        public WindowDefinition Window;
        public bool UseCurrentWindowAsParent = true;
        public WindowHandle LastOpened { get; private set; }
        private void OnEnable() => GetComponent<Button>().onClick.AddListener(Open);
        private void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Open);
        public void Open()
        {
            var parentWindow = GetComponentInParent<UIWindow>();
            var manager = Manager ? Manager : parentWindow?.Handle?.Manager;
            if (!manager) { Debug.LogError("Open Window Button: assign a Window Manager or place the button inside an open UIWindow.", this); return; }
            if (!manager.TryOpen(Window, out var handle, out string reason, UseCurrentWindowAsParent ? parentWindow?.Handle : null)) { Debug.LogError(reason, this); return; }
            LastOpened = handle;
        }
    }
}
