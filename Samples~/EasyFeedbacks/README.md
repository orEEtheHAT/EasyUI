# EasyFeedbacks integration

**Compatibility:** EasyFeedbacks 1.8.0+, EasyUI 0.2.0+. No prerequisite sample imports.

Requires **EasyFeedbacks 1.8.0+**, EasyUI and uGUI. Import after installing the feedback package.

Add **Window Feedbacks** to a UIWindow for Open, to a Selectable for Select/Confirm, or to RadialMenu for selection/confirmation. Assign FeedbackPreset assets authored with EasyFeedbacks. The adapter prewarms presets and stops its current handle on disable.

Presets own their audio, Particle System/VFX Graph, camera-shake and gamepad-vibration configuration. The base EasyUI sample has no dependency on these effects. Validate actual output with the game's presets and hardware.
