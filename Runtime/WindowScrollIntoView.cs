using UnityEngine;
using UnityEngine.UI;

namespace EasyUI
{
    /// <summary>Keeps keyboard/gamepad selection visible without fighting manual scrolling.</summary>
    [RequireComponent(typeof(ScrollRect)), AddComponentMenu("EasyUI/Window Scroll Into View")]
    public sealed class WindowScrollIntoView : MonoBehaviour
    {
        private ScrollRect scroll;
        private UIWindow window;
        private GameObject previous;
        private void OnEnable() { scroll = GetComponent<ScrollRect>(); window = GetComponentInParent<UIWindow>(); previous = null; }
        private void LateUpdate()
        {
            var selected = window?.Handle?.Manager.EventSystem.currentSelectedGameObject;
            if (selected == previous) return; previous = selected;
            if (!selected || !scroll.content || !scroll.viewport || !selected.transform.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            var rect = scroll.viewport.rect; var position = scroll.content.anchoredPosition;
            if (bounds.min.y < rect.yMin) position.y += rect.yMin - bounds.min.y;
            else if (bounds.max.y > rect.yMax) position.y += rect.yMax - bounds.max.y;
            position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, scroll.content.rect.height - rect.height));
            scroll.content.anchoredPosition = position;
        }
    }
}
