# EasyResources integration

**Compatibility:** EasyResources 0.3.0+, EasyUI 0.2.0+. No prerequisite sample imports.

Requires **EasyResources 0.3.0+**, EasyUI and uGUI.

Add **Resource Balance Label** to a Text, then assign Container and Resource. `Bind(container, resource)` changes the source with proper unsubscription. Resource Changed events mark the label dirty; multiple changes in one frame produce one text refresh in LateUpdate. The displayed amount is the available balance after reservations.
