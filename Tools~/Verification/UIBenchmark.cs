using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EasyUI.Verification
{
    public sealed class UIBenchmark : MonoBehaviour
    {
        public WindowManager Manager;
        public WindowDefinition Template;
        [Serializable] public sealed class Phase { public string name; public int operations; public double milliseconds; public long allocatedBytes; public long managedMemory; public long totalMemory; public double navigationNanoseconds; public double frameGcBytes; public double canvasNanoseconds; }
        [Serializable] public sealed class Report { public string unity, cpu; public bool development, batchMode, threadAllocationCounterSupported; public string allocationNote, canvasMarker; public Phase[] phases; public int remainingWindows, liveSceneWindows; }
        private readonly List<Phase> phases = new List<Phase>();
        private readonly List<UIWindow> hidden = new List<UIWindow>();
        private readonly Stopwatch watch = new Stopwatch();
        private long allocationStart;
        private string canvasMarker;
        private bool allocationSupported;
        private static string Argument(string name)
        { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
        private IEnumerator Start()
        {
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
            var originalBackground = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            long check = GC.GetAllocatedBytesForCurrentThread(); var allocationProbe = new byte[8192];
            allocationSupported = GC.GetAllocatedBytesForCurrentThread() - check >= allocationProbe.Length; GC.KeepAlive(allocationProbe);
            using var frameAllocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            Manager.Initialize(); yield return null; yield return null;
            var window = CreateWindow("Reusable");
            for (int i = 0; i < 32; i++) Manager.Open(window).Close();
            yield return null;
            StartMeasure();
            for (int i = 0; i < 500; i++) Manager.Open(window).Close();
            FinishMeasure("500 reusable modal open/close", 500);
            yield return null; phases[phases.Count - 1].frameGcBytes = frameAllocations.LastValue; yield return null;
            var definition = Instantiate(Template); definition.Policy = new WindowPolicy { FadeIn = 0, FadeOut = 0 };
            StartMeasure();
            for (int i = 0; i < 200; i++) Manager.Open(definition).Close();
            FinishMeasure("200 prefab instantiate/destroy", 200);
            yield return null; phases[phases.Count - 1].frameGcBytes = frameAllocations.LastValue;
            Destroy(definition); Destroy(window.gameObject); yield return null; yield return null;
            GC.Collect(); yield return null;
            phases.Add(new Phase { name = "Memory before hidden windows", managedMemory = Profiler.GetMonoUsedSizeLong(), totalMemory = Profiler.GetTotalAllocatedMemoryLong() });
            for (int i = 0; i < 1000; i++) hidden.Add(CreateWindow("Hidden " + i));
            yield return null; yield return null;
            Manager.enabled = false;
            for (int i = 0; i < 10; i++) yield return null;
            yield return Idle("Baseline: manager disabled / 120 idle frames", 120);
            Manager.enabled = true;
            for (int i = 0; i < 10; i++) yield return null;
            yield return Idle("1000 unopened inactive windows / 120 idle frames", 120);
            foreach (var item in hidden) Manager.Open(item).Close();
            for (int i = 0; i < 10; i++) yield return null;
            yield return Idle("1000 previously opened inactive windows / 120 idle frames", 120);
            window = CreateWindow("Navigation"); Manager.Open(window); yield return null; yield return null; yield return null; yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>(); var gamepad = InputSystem.AddDevice<Gamepad>();
            StartMeasure();
            for (int i = 0; i < 200; i++)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = i % 2 == 0 ? Vector2.down : Vector2.up }); InputSystem.Update();
                Manager.InputModule.Process();
            }
            FinishMeasure("200 gamepad navigation steps", 200);
            yield return null; phases[phases.Count - 1].frameGcBytes = frameAllocations.LastValue;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update(); Manager.InputModule.Process();
            StartMeasure();
            for (int i = 0; i < 200; i++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(i % 2 == 0 ? Key.DownArrow : Key.None));
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = i % 2 == 0 ? Vector2.zero : Vector2.up }); InputSystem.Update(); Manager.InputModule.Process();
            }
            FinishMeasure("200 keyboard/gamepad switches", 200);
            yield return null; phases[phases.Count - 1].frameGcBytes = frameAllocations.LastValue;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad);
            yield return Idle("Visible menu and 1000 inactive windows / 120 idle frames", 120);
            Manager.CloseAll(); Destroy(window.gameObject);
            foreach (var item in hidden) Destroy(item.gameObject); hidden.Clear();
            yield return null; yield return null; yield return Resources.UnloadUnusedAssets(); GC.Collect(); yield return null;
            phases.Add(new Phase { name = "Memory after cleanup", managedMemory = Profiler.GetMonoUsedSizeLong(), totalMemory = Profiler.GetTotalAllocatedMemoryLong() });
            var report = new Report { unity = Application.unityVersion, cpu = SystemInfo.processorType, development = UnityEngine.Debug.isDebugBuild,
                batchMode = Application.isBatchMode,
                threadAllocationCounterSupported = allocationSupported, allocationNote = "allocatedBytes = -1 when Mono's per-thread allocation counter is unsupported. frameGcBytes for synchronous phases is the whole execution frame from ProfilerRecorder, including harness/Input System overhead; idle phases are per-frame averages.",
                canvasMarker = canvasMarker, phases = phases.ToArray(), remainingWindows = Manager.Count, liveSceneWindows = Object.FindObjectsByType<UIWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length };
            string path = Argument("-easyUIReport") ?? Path.Combine(Application.persistentDataPath, "easy-ui-benchmark.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log("EasyUI benchmark: " + path);
            InputSystem.settings.backgroundBehavior = originalBackground;
            if (!Application.isEditor) Application.Quit(0);
        }
        private void StartMeasure() { allocationStart = GC.GetAllocatedBytesForCurrentThread(); watch.Restart(); }
        private void FinishMeasure(string name, int count)
        { watch.Stop(); long bytes = allocationSupported ? GC.GetAllocatedBytesForCurrentThread() - allocationStart : -1; phases.Add(new Phase { name = name, operations = count, milliseconds = watch.Elapsed.TotalMilliseconds, allocatedBytes = bytes, managedMemory = Profiler.GetMonoUsedSizeLong(), totalMemory = Profiler.GetTotalAllocatedMemoryLong() }); }
        private IEnumerator Idle(string name, int frames)
        {
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            ProfilerRecorder canvas = default;
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name.Contains("Canvas.BuildBatch")) { canvasMarker = description.Name; canvas = ProfilerRecorder.StartNew(description.Category, description.Name); break; }
            }
            using var navigation = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "EasyUI.Navigation");
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            double navTotal = 0, gcTotal = 0, canvasTotal = 0;
            try
            {
                for (int i = 0; i < frames; i++) { yield return null; if (navigation.Valid) navTotal += navigation.LastValue; if (gc.Valid) gcTotal += gc.LastValue; if (canvas.Valid) canvasTotal += canvas.LastValue; }
                phases.Add(new Phase { name = name, operations = frames, navigationNanoseconds = navTotal / frames, frameGcBytes = gcTotal / frames, canvasNanoseconds = canvasTotal / frames, managedMemory = Profiler.GetMonoUsedSizeLong(), totalMemory = Profiler.GetTotalAllocatedMemoryLong() });
            }
            finally { canvas.Dispose(); }
        }
        private UIWindow CreateWindow(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UIWindow)); go.SetActive(false); go.transform.SetParent(Manager.transform, false);
            var window = go.GetComponent<UIWindow>(); window.Policy = new WindowPolicy { FadeIn = 0, FadeOut = 0 };
            var first = Button(go.transform, "First", 30); var second = Button(go.transform, "Second", -30);
            first.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = second, selectOnUp = second };
            second.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = first, selectOnUp = first }; window.FirstSelected = first; return window;
        }
        private static Button Button(Transform parent, string name, float y)
        { var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); ((RectTransform)go.transform).anchoredPosition = new Vector2(0, y); var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); return button; }
    }
}
