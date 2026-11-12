# Changelog

## 0.2.0 — 2026-11-12

- Added the optional Integration Windows sample: an idempotent prefab/WindowDefinition builder, interaction list/radial selection, building catalog list/radial selection and combined controls, language and audio settings.
- Interaction windows bind the current presenter and its input presentation; building windows borrow the placer's canonical session and follow rebinding without owning its lifetime.
- Added InputSettings text/extras hooks and localized resource labels without competing text writers. Audio children are prepared inactive, preserve explicit context/preset choices, inherit only missing choices and own their own input requests.
- Hardened reentrant Select/CloseAll/reopen and held-action cleanup. Radial updates retain stable selection IDs; WindowScrollIntoView keeps navigation visible; obsolete localization requests cancel quietly.
- Added nested-context, builder, standalone-core and visual regressions. The base runtime still requires only uGUI and Input System.

## 0.1.0

- Added per-player window stack, reusable definitions, instance handles, modal raycast shields, nested focus and Back routing.
- Added input-release fencing, cancellable unscaled fades and lifecycle-safe request ownership.
- Added standalone input provider, confirmation results, radial menus and press/hold triggers.
- Added separate EasyInput, EasyLocalization, EasyFeedbacks, EasyBuilding, EasyResources and EasyInteraction integrations.
- Added Getting Started sample, PlayMode regressions and a standalone profiling harness.
