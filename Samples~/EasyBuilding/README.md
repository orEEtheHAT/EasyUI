# EasyBuilding integration

**Compatibility:** EasyBuilding 0.3.0+, EasyUI 0.2.0+. No prerequisite sample imports.

Requires **EasyBuilding 0.3.0+** and EasyUI.

Add **Building Gameplay Gate**, assign Building Placer, then put this component in Standalone Input Provider's Gameplay Gates array. Each lease calls `SetInputBlocked(owner, true)` with a distinct owner and releases only that owner. Another system's lock survives closing the UI.

Use the existing EasyBuilding/EasyResources payment adapter for costs. The combined workshop is a local Games Factory scene, not part of this adapter sample.
