using UnityEngine;
using UnityEngine.UI;

namespace EasyUI.Samples
{
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(.035f, .05f, .09f);
        public static readonly Color Card = new Color(.07f, .1f, .17f);
        public static readonly Color Teal = new Color(.12f, .77f, .68f);
        public static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        public static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        { var rect = Rect(name, parent, position, size); var image = rect.gameObject.AddComponent<Image>(); image.color = color; return image; }
        public static Text Label(string name, Transform parent, string content, Vector2 position, Vector2 size, int fontSize = 22, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.color = Color.white; text.alignment = anchor; text.text = content; text.raycastTarget = false;
            return text;
        }
        public static Button Button(Transform parent, string label, float y, float width = 400)
        {
            var image = Panel(label, parent, new Vector2(0, y), new Vector2(width, 52), new Color(.13f, .2f, .3f));
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = Teal; colors.selectedColor = Teal; colors.pressedColor = new Color(.08f, .5f, .5f); button.colors = colors;
            Label("Label", image.transform, label, Vector2.zero, new Vector2(width - 28, 48), 21, TextAnchor.MiddleCenter);
            return button;
        }
        public static UIWindow Window(string name, float width = 560, float height = 470)
        {
            var panel = Panel(name, null, Vector2.zero, new Vector2(width, height), Card);
            var window = panel.gameObject.AddComponent<UIWindow>();
            window.Policy = new WindowPolicy { BlockGameplay = true, Cursor = WindowCursor.Visible };
            Panel("Accent", panel.transform, new Vector2(0, height / 2 - 3), new Vector2(width, 6), Teal);
            return window;
        }
    }
}
