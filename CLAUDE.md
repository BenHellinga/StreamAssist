# StreamAssist — context for Claude

@README.md

## Purpose (in my words)

An "everything app" for streamers: playing music and sounds, notifications, chat commands, keybinds, etc. It has its own logic for configuring and triggering binds, and it also connects to existing software (Spotify, Voicemeeter, OBS, Twitch) for problems those already solve. It glues those tools together rather than replacing them.

Current state: the keybind component is partially done. Voicemeeter integration is basic: it mutes my mic going to OBS, or going to games/Discord, so I can talk to chat without bothering anyone else, or talk to Discord without chat hearing.

## Working with me

- This is my first desktop/frontend app, so explain the reasoning behind UI/MVVM/Avalonia suggestions rather than just giving code.
- Don't fix unrelated bugs unless asked. Point them out instead (see "Known issues" below).

## Folder layout

- `Program.cs`, `App.axaml`, `ViewLocator.cs`: app entry point. `App.axaml` lists every shared resource and style file, in order.
- `Shell/`: the app window itself, meaning the main window, the resizable side/bottom/console panels, and `GlobalContext`. `MainWindowViewModel` is where models and managers get created.
- `Components/<Name>/`: one folder per feature (Keyboard, AppAction, Audio, OutputConsole, Chatbot, OBS, …), split into Model/Background/UI as described below. Code that belongs to a single feature lives in its component. `AppAction/Actions/` holds the actual action classes, and `AppAction/UI/Configurator/` holds the auto-generated settings UI for actions.
- `Core/`: reusable building blocks that know nothing about any feature. `Mvvm/` has the base classes and `ViewModelTabItem`, `Dragging/` the drag-and-drop helpers, `Persistence/` `JsonFileStore`, and `Utilities/` small helpers. If something references a component, it doesn't belong in Core.
- `Styles/Resources/`: named values (`Brushes`: palette + meaning-based brushes, `Typography`: font + sizes, `Spacing`: shared margins like `HeaderMargin` and `PanelGapRight`). Views use them with `{StaticResource Name}`, and style files use `{DynamicResource Name}`.
- `Styles/Controls/`: one file per control type with app-wide styles and style classes (e.g. `Border.Draggable`, `Button.Square`, `ListBox.TopTabStrip`).

## XAML conventions

- ViewModel → View mapping goes through `ViewLocator`, so don't add a `DataTemplate` that only wraps the matching view. Only write an `ItemTemplate` when it adds something (extra elements, a different view).
- Repeated spacing goes in `Styles/Resources/Spacing.axaml` and colors in `Brushes.axaml`. Don't hard-code hex colors in views.
- Styles needed by only one view go in that view's `<UserControl.Styles>` (see `ActionUIButtons`, `ActionGroupView`). App-wide ones go in `Styles/Controls/`.
- Style class names are case-sensitive (`Label`, not `label`).
- `Border` has no global style. Add `Classes="Panel"` for the rounded panel look (keys, action cards, drop zones). `Divider` is a separate class.
- Button sizes: `Small` (corner buttons on keys/actions), `Medium` (next to inputs), `Large` (standalone). Combine with `Square` for icon buttons, e.g. `Classes="Square Medium"`. Plus/minus icons use the full-width `＋` / `－`.

## Architecture rules

Each component (`Components/<Name>/`) is split into three folders:

- **Model/**: data only, always loaded, serialized to JSON. Models are the "shouters": they raise C# events when they change. They never execute behavior.
- **Background/**: managers created once at startup that live for the whole app (`KeyboardComponentManager`, `KeybindManager`, `KeyboardSaveManager`, `VoicemeeterManager`). They listen to models and do the actual work (dispatching key presses, saving).
- **UI/**: view models + views. A view model is created when its tab or panel opens and disposed when it closes. View models listen to models, and change things only by calling model methods, never by calling managers. Model events then update the background and any other UI.

So data flows: UI → model method → model event → (background + UI listeners). Something with no UI (chat command, timer) can drive the model the same way.

`MainWindowViewModel` is the composition root: it loads models, creates managers, and sets `GlobalContext` (a static service locator that actions use to reach managers like `VoicemeeterManager`).

## Code conventions

- Public `TryXxx(...)` methods validate, log through `AppLog.WriteError`, and return `bool`. They call a private `Xxx(...)` that does the work and raises the event.
- Every event subscription goes through paired `SubscribeToXxx` / `UnsubscribeFromXxx` helpers. `Dispose()` must unsubscribe everything.
- JSON: public getters are `[JsonIgnore]`. Serialization goes through private `[JsonInclude] [JsonPropertyName("camelCase")]` properties. Links that need the whole tree (e.g. slot → config by name) are resolved in `IJsonOnDeserialized.OnDeserialized` or `AttachXxx` methods.
- A child gets a callback to remove itself from its parent through `AttachUnbindSelf(...)` (e.g. an empty action group unbinds its trigger, and an empty keybind unbinds its key).
- `CallbackBlocker` stops UI → model → UI feedback loops when a view model sets its own bound properties.
- Views are found by naming convention (`ViewLocator`): `FooViewModel` → `FooView` in the same namespace.
- Style: `_camelCase` private fields initialized explicitly in the constructor, `// section` comment headers, three blank lines between members.

## Adding an action

Create a class in `Components/AppAction/Actions/` that inherits `ActionModel` and implements `Name`, `Description` and `Execute()`. `ActionResolver` finds it automatically through reflection.
- Configurable fields are properties marked `[ConfigurableParameter("Label", ConfiguratorType.X)]` whose setter calls `SetParameter(ref _field, value)`.
- A `Menu` parameter gets its options from a `public static List<string> Get{PropertyName}MenuItems()` method. A `Slider` gets its range from `public static (double, double) Get{PropertyName}Bounds()`.
- **The class name is the JSON key.** Renaming an action class drops existing saved bindings of that type.

## Threading

Right now everything runs on the UI thread, because raw key input arrives through a hooked window procedure (`KeyboardInputManager.WndProc`). Future integrations (Twitch, OBS websocket, Spotify) will call back on other threads. Switch back to the UI thread with `Dispatcher.UIThread.Post` before touching models.

## Environment and setup notes

- Windows only, .NET 10, Avalonia 12, CommunityToolkit.Mvvm. Build with `dotnet build`.
- The config is saved to `Config/keyboard.json`. The path is hard-coded in `JsonFileStore.RootDir`; the AppData version is commented out.
- Voicemeeter setup is specific to my setup: mic = `Strip[0]`, "Misc" (games/Discord) = `Bus[3]`, "Obs" = `Bus[4]`. The mute/unmute sound paths in `MuteVoicemeeterAction.cs` are hard-coded.
- `ObsService` and `ChatbotService` are empty placeholders.

## Known issues (found in review, not fixed yet)

- `JsonFileStore.Load` ignores parse errors and returns defaults, and `KeyboardComponentManager` saves right away. A bad config file gets overwritten with an empty one.
- `KeyboardSaveManager.Dispose` cancels the pending save instead of saving immediately, so changes made in the last second before closing are lost.
- Remove slot with zero slots crashes (`Slots.Last()`).
- `KeybindViewModel.SetSelectedKey` subscribes `keybind` instead of `_keybind`, so actions added to an unbound key from the bottom panel are never bound.
- When one trigger's group is emptied while the key has other triggers, the panel keeps showing the detached group.
- `KeyViewModel.OnKeybindDroppedOn` ignores a failed `TryBindKey` and still removes the source keybind.
- `KeyboardSlotModel.Dispose` disposes the selected config and device, which it doesn't own.
- `KeybindModel.UnbindTrigger` and `ActionGroupModel.RemoveAction` call unbind-self before raising their own removed event, so `KeyboardSaveManager` leaks an `ActionGroupChangeManager`.
- Voicemeeter toggle uses a remembered mute state (it doesn't read the real one back), connecting resets mutes and gains, and `TrySetGainDb` stores the unclamped value.
- Device list skips devices with the same vendor/product ID, so identical keyboards collapse into one.
- Left/right modifier keys and Enter/numpad Enter share a key code, so they can't be bound separately.
- `OutputConsoleView` never unsubscribes from the static `AppLog.Lines`.
