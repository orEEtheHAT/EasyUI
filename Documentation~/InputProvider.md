# Input provider contract — EasyUI 0.1

`IWindowInputProvider` belongs to `EasyUI.Runtime`. The base package only depends on uGUI and Input System. Adapters belong to EasyUI and are imported separately.

```csharp
bool CanAcquire(WindowRequirements requirements, out string reason);
IDisposable Acquire(WindowHandle window, WindowRequirements requirements);
```

`WindowRequirements` contains `bool BlockGameplay`, `WindowCursor Cursor` (`Inherit`, `Visible`, `Locked`, `Hidden`), `bool AcceptsInput`, and a read-only `BlockedActions` list of individual action paths/IDs. `NeedsProvider` is true when gameplay, cursor or named-action policies require intervention. `CanAcquire` reports an actionable configuration error before any window is instantiated. `Acquire` runs on the Unity main thread and returns a distinct, idempotent lease for this window; failure must leave no side effects. A configured provider also receives a lease for an input-taking UI-only window, so it can enable that player's UI action map without blocking gameplay.

The provider owns enabled gameplay actions, player contexts and cursor state. It aggregates all live leases, permits out-of-order disposal, and restores the state captured before its first applicable request only after the last one ends. A child never replaces its parent's request. The provider must keep gameplay suspended until the closing input is released and a neutral frame has passed. Scene changes, window destruction and manager shutdown dispose all leases owned by those windows. Other systems' requests must survive.

Optional `IWindowUIActions.ConfigureUI(EasyUIInputModule module)` assigns the player's current Point, Move, Submit, Cancel, Click and Scroll references. The module derives from `InputSystemUIInputModule` and delegates pointer events to it. EasyUI routes Back once to its active window, with `IWindowBackHandler` first refusal for an operation such as rebinding. The next distinct Cancel may close that window.

```csharp
var inventory = ui.Open(inventoryDefinition, openingAction: inventoryAction);
var settings = ui.Open(settingsDefinition, inventory);
settings.Close(); // Only the settings lease ends.
inventory.Close(); // Ends inventory and any remaining descendants.
inventory.Close(); // Safe no-op.
```

For standalone use, assign `StandaloneInputProvider`, then configure `GameplayActions` and/or `IWindowGameplayGate` components. It snapshots enabled action states and the cursor, and restores them after the last request. All systems affecting those same actions should use this provider or a shared higher-level arbiter. Two unrelated components that independently toggle the same action cannot infer each other's desired state.

EasyInput integration should map each lease to its own player-context request, preserving EasyInput ownership of active maps, restrictions, cursor and saved user settings. EasyUI must not call a global “set gameplay enabled” method on closing a child. The original provider implementation is validated with a counting test provider before the actual EasyInput API is connected.
