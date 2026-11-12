using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyUI.Samples.Editor
{
    public static class EasyUISampleBuilder
    {
        [MenuItem("Tools/EasyUI/Create Getting Started Scene")]
        public static void Create()
        {
            var current = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(current.path) && current.isDirty)
            { EditorUtility.DisplayDialog("Save your scene", "Save the current untitled scene first, then create the EasyUI example. Your scene has not been changed.", "OK"); return; }
            CreateAt("Assets/EasyUI Example");
        }
        public static string CreateAt(string folder)
        {
            Directory.CreateDirectory(folder + "/Content"); AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            bool replaceUntitled = string.IsNullOrEmpty(previous.path);
            if (replaceUntitled && previous.isDirty) throw new System.InvalidOperationException("Save the current untitled scene before creating the EasyUI sample.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var camera = new GameObject("UI camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UIFactory.Ink;
                var go = new GameObject("Player UI", typeof(RectTransform), typeof(WindowManager));
                var manager = go.GetComponent<WindowManager>();
                var gameplay = go.AddComponent<DemoGameplayGate>(); var provider = go.AddComponent<StandaloneInputProvider>();
                provider.GameplayGates = new MonoBehaviour[] { gameplay }; manager.InputProvider = provider;
                var demo = go.AddComponent<EasyUIDemo>(); demo.Manager = manager; demo.Provider = provider; demo.Gameplay = gameplay;
                demo.Inventory = MakeWindow(folder, DemoWindowKind.Inventory); demo.Settings = MakeWindow(folder, DemoWindowKind.Settings);
                demo.Confirmation = MakeConfirmation(folder); demo.Radial = MakeRadial(folder);
                var hud = UIFactory.Panel("HUD", go.transform, Vector2.zero, new Vector2(1280, 720), UIFactory.Ink);
                var r = (RectTransform)hud.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                demo.HUD = hud.gameObject.AddComponent<UIWindow>(); demo.HUD.Policy = WindowPolicy.HUD;
                UIFactory.Panel("Side accent", hud.transform, new Vector2(-580, 0), new Vector2(3, 560), UIFactory.Teal);
                UIFactory.Label("Brand", hud.transform, "EASY UI  /  GETTING STARTED", new Vector2(0, 285), new Vector2(1080, 40), 20).color = UIFactory.Teal;
                UIFactory.Label("Title", hud.transform, "Every window. One clear path.", new Vector2(0, 205), new Vector2(1080, 100), 46);
                UIFactory.Label("Intro", hud.transform, "A persistent HUD, nested menus and a radial selector.\nOpen a menu and follow the focus as you move between windows.", new Vector2(0, 115), new Vector2(1080, 80), 22);
                string[] titles = { "01  INVENTORY", "02  NESTED WINDOWS", "03  RADIAL MENU" };
                string[] text = { "I / Start\nOpen inventory\nSpace pulses gameplay", "Arrows / D-pad / Stick\nEnter / A confirms\nEsc / B goes back", "Tab / RB toggles\nHold R / LB, release to pick\nMouse or stick selects" };
                for (int i = 0; i < 3; i++)
                {
                    float x = -365 + i * 365;
                    var card = UIFactory.Panel(titles[i], hud.transform, new Vector2(x, -60), new Vector2(340, 185), UIFactory.Card);
                    UIFactory.Label("Heading", card.transform, titles[i], new Vector2(0, 55), new Vector2(302, 42), 20).color = UIFactory.Teal;
                    UIFactory.Label("Instructions", card.transform, text[i], new Vector2(0, -17), new Vector2(302, 110), 19);
                }
                demo.Status = UIFactory.Label("Status", hud.transform, "Ready", new Vector2(0, -205), new Vector2(1080, 50), 23);
                demo.InputStatus = UIFactory.Label("Input status", hud.transform, "", new Vector2(0, -260), new Vector2(1080, 35), 17); demo.InputStatus.color = new Color(.58f, .7f, .8f);
                demo.PulseStatus = UIFactory.Label("Pulse", hud.transform, "GAMEPLAY PULSES  0", new Vector2(0, -300), new Vector2(1080, 35), 17); demo.PulseStatus.color = UIFactory.Teal;
                string path = folder + "/GettingStarted.unity"; EditorSceneManager.SaveScene(scene, path); AssetDatabase.SaveAssets(); return path;
            }
            finally { if (!replaceUntitled) { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); } }
        }
        private static WindowDefinition Save(UIWindow window, string folder, string name)
        {
            window.gameObject.SetActive(false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(window.gameObject, folder + "/Content/" + name + ".prefab");
            var definition = AssetDatabase.LoadAssetAtPath<WindowDefinition>(folder + "/Content/" + name + ".asset");
            if (!definition) { definition = ScriptableObject.CreateInstance<WindowDefinition>(); AssetDatabase.CreateAsset(definition, folder + "/Content/" + name + ".asset"); }
            definition.Prefab = prefab.GetComponent<UIWindow>(); definition.Policy = window.Policy.Copy(); EditorUtility.SetDirty(definition);
            Object.DestroyImmediate(window.gameObject); return definition;
        }
        private static WindowDefinition MakeWindow(string folder, DemoWindowKind kind)
        {
            var window = UIFactory.Window(kind.ToString());
            UIFactory.Label("Eyebrow", window.transform, kind == DemoWindowKind.Inventory ? "YOUR LOADOUT" : "PREFERENCES", new Vector2(0, 185), new Vector2(440, 35), 16).color = UIFactory.Teal;
            UIFactory.Label("Title", window.transform, kind.ToString(), new Vector2(0, 135), new Vector2(440, 60), 36);
            UIFactory.Label("Description", window.transform, kind == DemoWindowKind.Inventory ? "Your HUD remains visible. Gameplay is paused.\nOpen settings to add another input request." : "The inventory keeps its own input request.\nClosing this window returns to its selected button.", new Vector2(0, 63), new Vector2(440, 76), 18);
            var view = window.gameObject.AddComponent<DemoWindowView>(); view.Kind = kind;
            view.Primary = UIFactory.Button(window.transform, kind == DemoWindowKind.Inventory ? "Settings  >" : "Apply settings  >", -25);
            view.Secondary = UIFactory.Button(window.transform, kind == DemoWindowKind.Inventory ? "Choose with radial menu" : "Preview a change", -90);
            view.Back = UIFactory.Button(window.transform, "Back", -165);
            window.FirstSelected = view.Primary;
            return Save(window, folder, kind.ToString());
        }
        private static WindowDefinition MakeConfirmation(string folder)
        {
            var window = UIFactory.Window("Confirmation", 500, 320); var dialog = window.gameObject.AddComponent<ConfirmationDialog>();
            UIFactory.Label("Title", window.transform, "One last check", new Vector2(0, 105), new Vector2(400, 60), 32);
            dialog.Message = UIFactory.Label("Message", window.transform, "Apply these settings?", new Vector2(0, 35), new Vector2(400, 50), 21);
            dialog.ConfirmButton = UIFactory.Button(window.transform, "Confirm", -35, 360); dialog.CancelButton = UIFactory.Button(window.transform, "Cancel", -100, 360);
            window.FirstSelected = dialog.CancelButton;
            return Save(window, folder, "Confirmation");
        }
        private static WindowDefinition MakeRadial(string folder)
        {
            var window = UIFactory.Window("Radial", 660, 580); var radial = window.gameObject.AddComponent<RadialMenu>();
            radial.Items = new[] { new RadialItem("build", "Build"), new RadialItem("inspect", "Inspect"), new RadialItem("repair", "Repair"), new RadialItem("locked", "Locked", false), new RadialItem("craft", "Craft"), new RadialItem("equip", "Equip") };
            UIFactory.Label("Hint", window.transform, "CHOOSE\nCenter cancels", Vector2.zero, new Vector2(180, 100), 18, TextAnchor.MiddleCenter).color = UIFactory.Teal;
            UIFactory.Label("Footer", window.transform, "Mouse / stick  ·  Click / Enter / A  ·  Esc / B cancels", new Vector2(0, -255), new Vector2(600, 38), 17, TextAnchor.MiddleCenter);
            return Save(window, folder, "Radial");
        }
    }
}
