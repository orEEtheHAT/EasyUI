using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EasyUI.Samples;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace EasyUI.Verification
{
    public sealed class FirstRunSmoke : MonoBehaviour
    {
        [Serializable] private sealed class Report { public bool passed, batchMode; public string error, unity; public List<string> checks = new List<string>(); }
        private readonly Report report = new Report();
        private Keyboard keyboard;
        private Gamepad gamepad;
        private EasyUIDemo demo;
        private string output;
        private static string Argument(string name)
        { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Argument("-easyUISmoke") != null) new GameObject("EasyUI first-run smoke", typeof(FirstRunSmoke));
            else if (Argument("-easyUIBenchmark") != null && SceneManager.GetActiveScene().name != "Benchmark") SceneManager.LoadScene("Benchmark");
        }
        private IEnumerator Start()
        {
            output = Argument("-easyUISmoke"); report.unity = Application.unityVersion; report.batchMode = Application.isBatchMode; Application.runInBackground = true;
            var originalBackground = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var routine = Run();
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception exception) { report.error = exception.ToString(); break; }
                if (!next) { report.passed = true; break; }
                yield return routine.Current;
            }
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = originalBackground;
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }
        private void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); report.checks.Add(name); }
        private IEnumerator Run()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); gamepad = InputSystem.AddDevice<Gamepad>();
            yield return new WaitForSecondsRealtime(.3f);
            demo = UnityEngine.Object.FindAnyObjectByType<EasyUIDemo>();
            Check(demo && demo.Manager.Count == 1 && demo.Manager.ActiveWindow == null, "Imported scene starts with passive HUD");
            UICapture.Save(Path.Combine(Path.GetDirectoryName(output), "clean-first-run.png"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Manager.ActiveWindow.Window.name.StartsWith("Inventory") && demo.Provider.ActiveRequestCount == 1, "I opens inventory and acquires gameplay request");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Manager.ActiveWindow.Window.name.StartsWith("Settings") && demo.Provider.ActiveRequestCount == 2, "Enter opens nested settings");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Manager.ActiveWindow.Window.GetComponent<ConfirmationDialog>() && demo.Provider.ActiveRequestCount == 3, "Enter opens confirmation with third request");
            UICapture.Save(Path.Combine(Path.GetDirectoryName(output), "clean-nested.png"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Provider.ActiveRequestCount == 2 && demo.Status.text.Contains("Cancelled"), "Escape cancels only dialog");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Provider.ActiveRequestCount == 1 && demo.Manager.EventSystem.currentSelectedGameObject.name.StartsWith("Settings"), "Escape restores inventory's selected Settings button");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Provider.ActiveRequestCount == 0 && !demo.Gameplay.Blocked, "Last close restores gameplay after neutral input");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R)); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Manager.ActiveWindow.Window.GetComponent<RadialMenu>(), "Holding R opens radial");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up }); yield return null; yield return null;
            Check(demo.Manager.ActiveWindow.Window.GetComponent<RadialMenu>().SelectedIndex == 0, "Gamepad stick selects upper sector");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return new WaitForSecondsRealtime(.25f);
            Check(demo.Manager.ActiveWindow == null && demo.Status.text.Contains("Build"), "Releasing R confirms radial item and returns to HUD");
        }
    }
}
