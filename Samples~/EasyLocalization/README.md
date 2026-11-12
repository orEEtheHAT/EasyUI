# EasyLocalization integration

**Compatibility:** EasyLocalization 0.2.0+, EasyUI 0.2.0+, Unity Localization 1.5.13+. No prerequisite sample imports.

Requires **EasyLocalization 0.2.0+**, **Unity Localization 1.5.13**, EasyUI and uGUI. Configure languages and standard string tables in EasyLocalization first.

Add **Localized Window Text** to a uGUI Text, assign its standard LocalizedString and a fallback. It refreshes through `EasyLocalization.Localization.TextAsync`, uses language/string events, and unsubscribes on disable. Use `SetArguments` for dynamic values.

The standalone localized action prompt belongs to EasyInput's Localization adapter; bind that component's TextChanged event to a label inside an EasyUI window. This integration does not duplicate it.
