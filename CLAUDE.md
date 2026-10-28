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
- `Components/<Name>/`: one folder per feature (Keyboard, Blocks, Audio, OutputConsole, Chatbot, OBS, …), split into Model/Background/UI as described below. Code that belongs to a single feature lives in its component. `Blocks/` holds the block system: `Model/` (base classes, slots, `BindableModel`), `Containers/`, `Conditions/` and `Actions/` (the block classes), and `UI/` (editor, block list, and `Configurator/` for auto-generated block settings).
- `Core/`: reusable building blocks that know nothing about any feature. `Mvvm/` has the base classes and `ViewModelTabItem`, `Dragging/` the drag-and-drop helpers, `Persistence/` `JsonFileStore`, and `Utilities/` small helpers. If something references a component, it doesn't belong in Core.
- `Styles/Resources/`: named values (`Brushes`: palette + meaning-based brushes, `Typography`: font + sizes, `Spacing`: shared margins like `HeaderMargin` and `PanelGapRight`). Views use them with `{StaticResource Name}`, and style files use `{DynamicResource Name}`.
- `Styles/Controls/`: one file per control type with app-wide styles and style classes (e.g. `Border.Draggable`, `Button.Square`, `ListBox.TopTabStrip`).

## XAML conventions

- ViewModel → View mapping goes through `ViewLocator`, so don't add a `DataTemplate` that only wraps the matching view. Only write an `ItemTemplate` when it adds something (extra elements, a different view).
- Repeated spacing goes in `Styles/Resources/Spacing.axaml` and colors in `Brushes.axaml`. Don't hard-code hex colors in views.
- Styles needed by only one view go in that view's `<UserControl.Styles>` (e.g. a view-specific `<UserControl.Styles>` block). App-wide ones go in `Styles/Controls/`.
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

## Blocks and bindables

Everything that triggers (keys now, chat commands/timers later) owns a `BindableModel` with one root `BlockSlot`. Blocks nest Scratch-style. All of this lives in `Components/Blocks/`.

- Triggers: `TriggerStart()` (key down), `TriggerStop()` (key up), and `TriggerOnce()` (instant things like chat commands). Each one runs the root block once with a `TriggerContext` (`Phase` = Start/Stop/Once, plus a `CancellationToken`). OS key repeat is ignored.
- Running: the root runs through `SafeRunAsync` (catches and logs errors). Blocks call children with `slot.RunAsync(context)` / `list.RunAllAsync(context)`, which don't catch. An error stops the rest of the tree. `RunAsync` checks the block's own `IsDisabled` and returns early, so a disabled block skips everything inside it, but only the disabled block shows red.
- Execution is truly async: `ExecuteAsync` returns a Task, and plain `await` comes back on the UI thread. Pass `context.CancellationToken` to anything that waits. Disposing a bindable (key unbound, app closed) cancels its running trees.
- Block kinds (base class → block list heading): `ContainerBlock` (has slots, e.g. List, On Pressed, On Released), `ConditionBlock` (`CheckAsync` returns bool, a disabled or empty condition counts as false), `ActionBlock` (leaf, e.g. Play Sound).
- Slots are typed: `new BlockSlot(label, typeof(ExecutableBlock))` for blocks that run, `typeof(ConditionBlock)` for conditions. `BlockSlotList` is the expanding version. Number/text value blocks later = a new base class and slot type.
- Change tracking bubbles up: any parameter, disabled state, or slot change raises `Changed` on the block, then its parent, up to `BindableModel.Changed`. Saving only listens there.
- Keys: a key is green when its root slot has a block. Selecting an unbound key shows an unsaved draft with an empty On Pressed, which gets bound to the config on its first change. 
- Binding slots vs block slots: a **binding slot** (each key, later chat commands, …) holds a whole `BindableModel`, and **block slots** live inside a bindable's tree. Blocks are only placed in the editor, never dropped on a key. Whole bindings move between binding slots with `BindableDragPayload` (drag a selected key's handle onto another key). A binding slot only accepts a drop when it's empty, and the source is removed only after the copy is bound.

## Adding a block

Create a class in `Components/Blocks/Actions/`, `Containers/` or `Conditions/` that inherits `ActionBlock`, `ContainerBlock` or `ConditionBlock`, and implement `Name`, `Description` and `ExecuteAsync` (or `CheckAsync`). `BlockResolver` finds it automatically, and it appears in the block list under its category.
- Configurable fields are properties marked `[ConfigurableParameter("Label", ConfiguratorType.X)]` whose setter calls `SetParameter(ref _field, value)`.
- A `Menu` parameter gets its options from a `public static List<string> Get{PropertyName}MenuItems()` method. A `Slider` gets its range from `public static (double, double) Get{PropertyName}Bounds()`.
- Containers: create slots in the constructor, return them from `Slots`, call `TrackSlots()` at the end of the constructor, and save each slot through a private `[JsonInclude]` property using `slot.GetData()` / `slot.LoadData(value)` (see `OnPressedBlock`, `ListBlock`). The editor UI is generated from `Slots`, so no new view is needed.
- Mark `Name`, `Description` and `Slots` overrides `[JsonIgnore]`.
- **The class name is the JSON key.** Renaming a block class drops saved blocks of that type.

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
