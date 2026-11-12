# Integrated windows

**Compatibility:** EasyBuilding 0.3.0+, EasyFeedbacks 1.8.0+, EasyInput 0.2.0+, EasyInteraction 0.3.0+, EasyLocalization 0.2.0+, EasyResources 0.3.0+, EasyUI 0.2.0+. Import first: EasyBuilding / EasyResources; EasyUI / EasyBuilding; EasyUI / EasyFeedbacks; EasyUI / EasyInput; EasyUI / EasyInteraction; EasyUI / EasyResources.

Optional sample for combining existing Easy packages. It creates reusable window prefabs, not a game scene.

Install EasyUI, EasyInput, EasyLocalization, EasyInteraction, EasyBuilding, EasyResources and EasyFeedbacks. Import EasyUI's EasyInput, EasyInteraction, EasyBuilding, EasyResources and EasyFeedbacks samples, and EasyBuilding's EasyResources sample, before importing this sample. Minimum versions are listed above; older package snapshots may lack the public contracts documented below. The base EasyUI sample still requires only uGUI and Input System.

## First run

1. Use **Tools → EasyUI → Create Integration Windows**. The result is `Assets/EasyUI/IntegrationWindows/EasyUI Windows.asset`, containing six WindowDefinitions and their inactive prefabs. Repeating the command returns the existing set and preserves your edits.
2. Use one WindowManager and one EventSystem for the player. Assign **EasyInput Window Provider → Player**, and assign that provider to the manager. Do not add a second UI-input adapter or a Standalone provider to the same stack.
3. Prepare a window once, bind its scene references while inactive, then open it as often as needed:

```csharp
using EasyUI;
using EasyUI.Integrations;
using EasyUI.Integrations.Building;
using EasyUI.Integrations.Interaction;

var catalogWindow = IntegrationWindowSet.InstantiateWindow(windows.Building, uiRoot);
catalogWindow.GetComponent<BuildingCatalogWindow>().Bind(placer, catalog);
catalogWindow.GetComponent<BuildingCatalogPresentation>().Bind(buildingResources, translations);
var actionsWindow = IntegrationWindowSet.InstantiateWindow(windows.InteractionRadial, uiRoot);
actionsWindow.GetComponent<InteractionMenuWindow>().Bind(interactionPresenter);
var settingsWindow = IntegrationWindowSet.InstantiateWindow(windows.Settings, uiRoot);
settingsWindow.GetComponent<IntegratedSettingsWindow>().Bind(translations);

WindowHandle parent = manager.Open(settingsWindow);
WindowHandle child = manager.Open(catalogWindow, parent);
child.Close(); // The settings window's input request remains.
```

`translations` is EasyLocalization's `LocalizedCatalogCache`. Configure its standard LocalizedString entries through EasyLocalization. Null uses readable fallback labels. The application owns creation/destruction of these reusable scene instances. You can also use `manager.Open(definition)` and bind the returned instance; configure scene references before Open when an opening feedback/event needs them immediately.

4. **Arrows / D-pad / left stick** navigate the list; **Enter / A / left click** chooses. In a circle, mouse/stick direction chooses a sector, the centre chooses nothing, and **Enter / A / left click** confirms once. **Esc / B** cancels a running interaction or selected construction first; the next separate Back closes the window. Choosing an action selects it on the actor; it does not execute it. The player's normal interaction input subsequently runs the chosen action. Set `CloseOnSelection=false` to retain a list after selection.
5. In Settings, change a binding/sensitivity using the existing InputSettingsWindow. **Esc / B** first cancels rebinding. The bottom **Language** button cycles available locales; **Audio & vibration** opens a child window. Audio sliders apply `FeedbackSettings` immediately; **Restore** restores the snapshot from opening that child, **Defaults** applies runtime defaults, and **Back** keeps the edited values. Persist snapshots in your game's settings system.

Expected: choosing a building changes the *same* `BuildingPlacer.Session` used by interaction and placement. UI closure never disposes it. A nested window has its own EasyInput context; closing it preserves every other context. One Text component owns each displayed value, so resource changes and translation updates cannot race two different writers.

## Connection contracts

- `InteractionMenuWindow.Bind(InteractionPresenter)`, `Select(action)`, `ActivateSelected()`, `ReleaseSelected()`, `SetValue(float)`, `Refresh()`. Binding labels, glyphs and toggle preferences come from `IInteractionInputSource` through the presenter. Explicit UI press/hold buttons must pair Activate and Release; normal selection does not emulate gameplay. Back routes cancellation through the presenter's actor/command adapter. Unselected availability conditions have no aggregate event, so this view checks them at 10 Hz, reusing unchanged rows.
- `BuildingCatalogWindow.Bind(BuildingPlacer, BuildingCatalog)`, `Session`, `Select(definition)`, `Refresh()`. It observes `Placer.SessionChanged`, `Session.SelectionChanged` and `Disposed`. It never creates or disposes a session. Call Refresh after changing the public catalog array in place; normal selection and resource changes refresh automatically. Add RadialMenu to the same object for a circle. Lists automatically scroll to the selected button.
- `BuildingCatalogWindow.Presentation` accepts `IBuildingCatalogPresentation`. The included `BuildingCatalogPresentation.Bind(BuildingResources, LocalizedCatalogCache)` uses the current session actor's accounts. Call `RefreshSources()` after changing the accounts provider or resource mappings. It displays **authored base cost / available balance**. Quantity, length, area, price modifiers and placement conditions remain authoritative in EasyBuilding; the catalogue is not a final-price quote.
- `ResourceBalanceLabel.Presentation` accepts `IResourceLabelProvider`; use `LocalizedResourceLabelProvider.Bind(cache)`. Do not add LocalizedWindowText to that same Text. Bind the container/resource through `ResourceBalanceLabel.Bind` as usual.
- `InputSettingsWindow.TextProvider` accepts `IInputSettingsTextProvider`; `ExtrasRoot` and `ViewBuilt(RectTransform)` allow extra controls without copying its rebinding implementation. The combined settings component uses those hooks. It subscribes/unsubscribes on enable/disable and cancels its pending language request on close.
- `WindowFeedbacks.ContextProvider` accepts an enabled `IFeedbackContextProvider`, such as EasyInput's PlayerFeedbackContext. Assign before Open. UI does not choose devices or write the cursor.
- The Audio child is prepared inactive before its first Open, inherits the parent's context provider and a missing Open preset, and retains explicitly authored child presets/providers. It is reused while the settings window remains open, then closed/destroyed with its owner or manager. The child uses the same manager/EventSystem and owns one separate input request.

Editor callers can use `EasyUI.Integrations.Editor.IntegrationWindowBuilder.CreateAt("Assets/MyUI")` and retain its `IntegrationWindowSet`. It creates no scene, EventSystem, project settings or master setup entry.

## Translation keys

`resource.<ResourceDefinition.Id>` and `building.<BuildingDefinition.Id>` use stable asset IDs. `ui.resource.balance` is an ordinary format template with name `{0}` and formatted available amount `{1}`. `ui.buildings`, `ui.building.choose`, `ui.building.free`, `ui.building.baseCosts` translate catalogue copy. Interaction labels/reasons use the presenter's text provider; `ui.interactions`, `ui.toggle`, `ui.hold` are its UI keys.

Controls use `ui.settings.` plus these suffixes: `title`, `mouseSensitivity` / `gamepadSensitivity` (`{0:0.00}`), `invertY` / `devices` (`{0}`), `deadzone`, `on`, `off`, `device.<enum>`, `action.<map>/<action>`, `part.<binding-name>`, `reset`, `capture`, `restore`, `defaults`, `back`, `language`, `status.help`, `status.captured`, `status.restored`, `status.captureFirst`, `status.connectGamepad`, `rebind.<RebindStatus>`. Binding text/glyphs themselves stay with EasyInput. Audio suffixes are `audio.title`, `audio.master`, `audio.music`, `audio.sfx`, `audio.haptics`, `audio.hapticsIntensity`. `language.<locale-code>` can override locale display names.

Format templates above are ordinary non-Smart strings in LocalizedString tables: the window supplies dynamic values after the cache has translated the template. Invalid formatting falls back safely. No Google/network setup is required for runtime use.
