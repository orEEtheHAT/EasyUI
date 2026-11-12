using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EasyUI.Editor
{
    [CustomEditor(typeof(UIWindow), true)]
    public sealed class WindowInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var window = (UIWindow)target;
            if (window.Policy.Requirements.NeedsProvider)
                EditorGUILayout.HelpBox("This window requires an enabled Input Provider on its Window Manager. Each instance owns one request; closing a child keeps its parent's request.", MessageType.Info);
            if (!window.Policy.AcceptsInput && window.Policy.CloseOnBack)
                EditorGUILayout.HelpBox("A passive window does not receive Back. Disable Close On Back, or enable Accepts Input.", MessageType.Warning);
            if (Application.isPlaying) EditorGUILayout.LabelField("State", window.State + (window.IsActive ? " / Active" : ""));
        }
    }
    [CustomEditor(typeof(WindowManager))]
    public sealed class ManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var manager = (WindowManager)target;
            if (manager.InputProvider && !(manager.InputProvider is IWindowInputProvider))
                EditorGUILayout.HelpBox("Input Provider must implement IWindowInputProvider. Use Standalone Input Provider or the EasyInput adapter.", MessageType.Error);
            if (!manager.InputProvider) EditorGUILayout.HelpBox("UI-only windows work immediately. For gameplay or cursor control, add and assign an Input Provider.", MessageType.Info);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Visible windows", manager.Count.ToString());
                EditorGUILayout.LabelField("Input", manager.InputReady ? "Ready" : "Waiting for input release");
                if (GUILayout.Button("Back")) manager.Back();
            }
        }
    }
    public static class WindowSetup
    {
        [MenuItem("GameObject/EasyUI/Window Manager", false, 10)]
        private static void CreateManager()
        {
            var go = new GameObject("Player UI", typeof(RectTransform), typeof(WindowManager));
            Undo.RegisterCreatedObjectUndo(go, "Create EasyUI manager");
            Selection.activeGameObject = go;
        }
        [MenuItem("GameObject/EasyUI/Window", false, 11)]
        private static void CreateWindow()
        {
            var go = new GameObject("My Window", typeof(RectTransform), typeof(Image), typeof(UIWindow));
            Undo.RegisterCreatedObjectUndo(go, "Create EasyUI window");
            if (Selection.activeTransform) go.transform.SetParent(Selection.activeTransform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(540, 380);
            go.GetComponent<Image>().color = new Color(.07f, .1f, .16f, 1);
            Selection.activeGameObject = go;
        }
    }
}
