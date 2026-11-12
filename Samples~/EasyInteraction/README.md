# EasyInteraction integration

**Compatibility:** EasyInteraction 0.3.0+, EasyUI 0.2.0+. No prerequisite sample imports.

Requires **EasyInteraction 0.3.0+** and EasyUI.

Add **Open Window Action** to an interaction object, assign Window Definition and Window Manager. If Manager is empty, it resolves below `InteractionActor.PlayerRoot`. EasyInteraction handles availability and execution; the adapter calls EasyUI's public API. `LastWindow` exposes the opened handle.
