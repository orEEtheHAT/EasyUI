using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyUI.Verification.Editor
{
    public static class VerificationBuilder
    {
        public static string Create(string folder, string templatePath)
        {
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                new GameObject("Camera", typeof(Camera));
                var manager = new GameObject("UI", typeof(RectTransform), typeof(WindowManager)).GetComponent<WindowManager>();
                var runner = new GameObject("Benchmark", typeof(UIBenchmark)).GetComponent<UIBenchmark>(); runner.Manager = manager; runner.Template = AssetDatabase.LoadAssetAtPath<WindowDefinition>(templatePath);
                string path = folder + "/Benchmark.unity"; EditorSceneManager.SaveScene(scene, path); return path;
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }
    }
}
