# EasyInput integration

**Compatibility:** EasyInput 0.2.0+, EasyUI 0.2.0+. No prerequisite sample imports.

Requires **EasyInput 0.2.0+**, EasyUI and Input System 1.20. Import this sample after installing EasyInput.

1. Add `PlayerInputSettings` to the player, using its default actions/profile or your configured source.
2. Add **EasyInput Window Provider** to the Window Manager and assign Player. Assign this provider to the manager's Input Provider field. It replaces Standalone Input Provider.
3. Add **Input Settings Window** to a UIWindow prefab. Set its definition to Block Gameplay and Cursor = Visible. The Player reference resolves from the manager's provider when opened.
4. Open it with `manager.Open(definition)`. The generated uGUI view includes binding rows/glyphs, sensitivity, deadzone, inversion, device preference, capture/restore and reset.

UI action names are Point, Click, Scroll, Navigate, Submit and Cancel inside UI Map. Gameplay Maps default to Gameplay and Building. Custom Player + UI assets must configure these map names explicitly. For partial restrictions, leave Block Gameplay off and set the window's Blocked Actions, for example Player/Look.

Every window owns a separate EasyInput context. The provider passes the UIWindow component as its Unity owner. Escape and the current UI/Cancel controls cancel rebinding independently of the UI module. The cancelling event is consumed before resumed gameplay actions can receive it; the next separate Cancel navigates back. Do not attach the separate EasyInput InputUIActions component to this same module.

Capture/Restore keeps settings in memory; persist the EasyInput snapshot through the game's save system. Localized action prompts belong to EasyInput's Localization sample. The test assembly in this sample requires Unity Test Framework.
