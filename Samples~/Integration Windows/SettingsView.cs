using UnityEngine;
using UnityEngine.UI;

namespace EasyUI.Integrations
{
    internal static class SettingsView
    {
        internal static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        { var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; r.SetParent(parent, false); r.anchoredPosition = position; r.sizeDelta = size; return r; }
        internal static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int font = 18)
        { var t = Rect(name, parent, position, size).gameObject.AddComponent<Text>(); t.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = font; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false; return t; }
        internal static Button Button(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
        {
            var r = Rect(label, parent, position, size); var image = r.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .22f, .3f);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.onClick.AddListener(click);
            Label(r, label, Vector2.zero, size - new Vector2(12, 0)).text = label; return b;
        }
        internal static Slider Slider(Transform parent, string name, float y, UnityEngine.Events.UnityAction<float> changed)
        {
            var r = Rect(name, parent, new Vector2(140, y), new Vector2(260, 25)); r.gameObject.AddComponent<Image>().color = new Color(.18f, .26f, .34f);
            var s = r.gameObject.AddComponent<Slider>(); s.minValue = 0; s.maxValue = 1;
            var handle = Rect("Handle", r, Vector2.zero, new Vector2(18, 0)); var image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.15f, .85f, .7f);
            s.handleRect = handle; s.targetGraphic = image; s.onValueChanged.AddListener(changed); return s;
        }
    }
}
