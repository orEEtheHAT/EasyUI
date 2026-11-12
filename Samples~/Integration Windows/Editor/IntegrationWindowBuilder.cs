using System;
using System.IO;
using EasyUI.Integrations.Building;
using EasyUI.Integrations.Feedbacks;
using EasyUI.Integrations.Input;
using EasyUI.Integrations.Interaction;
using UnityEditor;
using UnityEngine;

namespace EasyUI.Integrations.Editor
{
    public static class IntegrationWindowBuilder
    {
        [MenuItem("Tools/EasyUI/Create Integration Windows")]
        public static void Create() => Selection.activeObject = CreateAt("Assets/EasyUI/IntegrationWindows");

        /// <summary>Creates only prefabs/definitions; no scene or project settings. Existing sets are returned without overwriting authoring.</summary>
        public static IntegrationWindowSet CreateAt(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Choose a folder below Assets.", nameof(folder));
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains("..")) throw new ArgumentException("Choose a folder below Assets.", nameof(folder));
            string setPath = folder + "/EasyUI Windows.asset";
            var existing = AssetDatabase.LoadAssetAtPath<IntegrationWindowSet>(setPath); if (existing) return existing;
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var set = ScriptableObject.CreateInstance<IntegrationWindowSet>();
            set.Audio = Save(folder, "Audio", go => go.AddComponent<FeedbackSettingsWindow>());
            set.Settings = Save(folder, "Settings", go => { go.AddComponent<InputSettingsWindow>(); var view = go.AddComponent<IntegratedSettingsWindow>(); view.AudioWindow = set.Audio; });
            set.Interaction = Save(folder, "Interaction", go => go.AddComponent<InteractionMenuWindow>());
            set.InteractionRadial = Save(folder, "Interaction Radial", go => { go.AddComponent<RadialMenu>(); go.AddComponent<InteractionMenuWindow>(); });
            set.Building = Save(folder, "Building", AddBuilding);
            set.BuildingRadial = Save(folder, "Building Radial", go => { go.AddComponent<RadialMenu>().Radius = 245; AddBuilding(go); });
            AssetDatabase.CreateAsset(set, setPath); AssetDatabase.SaveAssets(); return set;
        }
        private static void AddBuilding(GameObject go)
        { var presentation = go.AddComponent<BuildingCatalogPresentation>(); go.AddComponent<BuildingCatalogWindow>().Presentation = presentation; }
        private static WindowDefinition Save(string folder, string name, Action<GameObject> configure)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.SetActive(false);
            try
            {
                ((RectTransform)go.transform).sizeDelta = new Vector2(760, 700);
                var window = go.AddComponent<UIWindow>(); window.Policy = new WindowPolicy { BlockGameplay = true, Cursor = WindowCursor.Visible };
                configure(go); go.AddComponent<WindowFeedbacks>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".prefab"));
                var definition = ScriptableObject.CreateInstance<WindowDefinition>(); definition.Prefab = prefab.GetComponent<UIWindow>(); definition.Policy = window.Policy.Copy();
                AssetDatabase.CreateAsset(definition, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset")); return definition;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
