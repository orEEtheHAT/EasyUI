# Verification tools

Import **Getting Started**, then copy `Verification` into an Assets folder. Its runtime assembly depends on `EasyUI.Sample` and `EasyUI.Runtime`.

Create a benchmark scene with `EasyUI.Verification.Editor.VerificationBuilder.Create("Assets/EasyUI Benchmark", "Assets/Samples/EasyUI/0.1.0/Getting Started/Content/Inventory.asset")`. Build that scene as a Windows Development Player. Run with `-easyUIReport <absolute-path.json>`; the player writes measurements and exits.

For the automatic first-run check, put GettingStarted first and Benchmark second in the build. `-easyUISmoke <absolute-path.json>` exercises I, Enter, Escape and hold-R plus gamepad direction. `-easyUIBenchmark 1 -easyUIReport <absolute-path.json>` switches to Benchmark. Use a graphical player for rendering measurements; batchmode is reported explicitly.

Input settings changed by the normal test path are restored. Devices created by the tests are removed. Per-thread allocation reporting is calibrated; unsupported counters are marked -1. Whole-frame GC includes engine and harness allocations. UICapture is an offscreen helper for the Built-in-renderer clean test project; it is not part of the public runtime assembly.
