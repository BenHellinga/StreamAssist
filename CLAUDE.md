# StreamAssist — context for Claude

@README.md

## Purpose (in my words)

An "everything app" for streamers: playing music and sounds, notifications, chat commands, keybinds, etc. It has its own logic for configuring and triggering binds, and it also connects to existing software (Spotify, Voicemeeter, OBS, Twitch) for problems those already solve. It glues those tools together rather than replacing them.

Current state: the keybind component is partially done. Voicemeeter integration is basic: it mutes my mic going to OBS, or going to games/Discord, so I can talk to chat without bothering anyone else, or talk to Discord without chat hearing.

## Working with me

- This is my first desktop/frontend app, so explain the reasoning behind UI/MVVM/Avalonia suggestions rather than just giving code.
- Don't fix unrelated bugs unless asked. Point them out instead (see "Known issues" below).
- **The output console is for failures only** ("no news is good news"). Only write to it when something failed or couldn't complete, usually a configuration problem (missing sound file, invalid pattern, can't connect on startup), using `AppLog.WriteError/WriteWarning("Could not <do thing> - <reason>")`. Never log successes, routine actions (logging out, connecting) or mirrored events (chat). Ask before adding any other kind of console message.

## Folder layout

- `Program.cs`, `App.axaml`, `ViewLocator.cs`: app entry point. `App.axaml` lists every shared resource and style file, in order.
- `Shell/`: the app window itself, meaning the main window, the resizable side/bottom/console panels, and `GlobalContext`. `MainWindowViewModel` is where models and managers get created. `WindowLayoutModel` (saved to `Config/window.json` by `WindowLayoutSaveManager`) remembers window position/size, maximized/fullscreen, the selected tab, and every splitter, stored by name under `panelSizes`. A new resizable panel calls `GetPanelSize`/`SetPanelSize` with its own key. The window and panels also listen to the model's events, so code (e.g. a future action through `GlobalContext.WindowLayoutModel`) can change the layout.
- `Components/<Name>/`: one folder per feature (Keyboard, Blocks, Audio, OutputConsole, Chatbot, OBS, …), split into Model/Background/UI as described below. Code that belongs to a single feature lives in its component. `Blocks/` holds the block system: `Model/` (base classes, slots, `BindableModel`), `Containers/`, `Conditions/` and `Actions/` (the block classes), and `UI/` (editor, block list, and `Configurator/` for auto-generated block settings).
- `Core/`: reusable building blocks that know nothing about any feature. `Mvvm/` has the base classes and `ViewModelTabItem`, `Dragging/` the drag-and-drop helpers, `Persistence/` `JsonFileStore` and `CredentialStore` (every secret in one git-ignored `Config/auth.json`, one section per service: `twitchApps`, `twitchLogins`, later e.g. `spotify`; new integrations store credentials/tokens there, never in their own config file), and `Utilities/` small helpers. If something references a component, it doesn't belong in Core.
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

**Everything the UI can do must be a public model method** (bind/unbind a key, remove a block, disable something, switch config, add a slot, …), with the rules enforced inside the model, not in view models. The goal is that a block, chat command or any other code can do anything the user can, just by calling the same model methods, and the UI updates because it listens to the model.

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
- Block kinds (base class → block list heading): `ContainerBlock` (has slots, e.g. List, If, On Pressed, On Released), `ConditionBlock` (`CheckAsync` returns bool, a disabled or empty condition counts as false), `ActionBlock` (leaf, e.g. Play Sound).
- Slots are typed: `new BlockSlot(label, typeof(ExecutableBlock))` for blocks that run, `typeof(ConditionBlock)` for conditions. `BlockSlotList` is the expanding version. `BlockBranchList` (used by `IfBlock`) is a list of `BlockBranch` (condition slot + then slot). Dropping a condition into a gap creates a branch, and a branch removes itself once both its slots are empty, so a filled "then" is never lost. Number/text value blocks later = a new base class and slot type.
- Drop targets: every slot's empty `+` zone is the shared `Blocks/UI/DropZone` control (`IsCondition` picks the block or condition look). Change the drop zone shape/text there only, never inline in slot views.
- Each slot kind has a matching view model/view in `Blocks/UI` (`BlockSlot*`, `BlockSlotList*`, `BlockBranchList*`), picked in `BlockViewModel`'s constructor. A new slot kind needs one there too. Slots that hold other slots (like branches) expose them through `InnerSlots`, so the "can't drop a block into itself" check sees them.
- Change tracking bubbles up: any parameter, disabled state, or slot change raises `Changed` on the block, then its parent, up to `BindableModel.Changed`. Saving only listens there.
- Empty bindables remove themselves: when a bindable's root block is removed (from the UI, `Clear()`, or any other code), it calls the unbind callback its owner attached (`AttachUnbindSelf`, set by `KeybindConfigModel`), so no `"root": null` bindings are saved. This only happens when the root block is removed, not on save. Empty bindables in an old save file are dropped once on load.
- Key input: `KeyboardInputManager` keeps the set of held keys per keyboard (`IsKeyDown(vk)` / `IsKeyDown(device, vk)`, reachable through `GlobalContext.KeyboardInputManager`). It updates the set before raising `KeyDown`/`KeyUp`, and `KeyDown` only fires on the first press (no OS repeat). Left/right Shift/Ctrl/Alt share a code. Keys can get stuck as held if Windows swallows a key-up (e.g. Win+L), not handled yet.
- `KeybindManager` caches nothing: on each key event it walks the slots, matches the device, and looks the key up in the slot's selected config, so model changes apply immediately. Start/Stop aren't paired (a release goes to whatever is bound at release time), which is intentional.
- Typing key names: `DeviceDisplayHelper.TryParseKeyName` accepts aliases (`ctrl`, `esc`), windows names (`SHIFT`, `F5`, `NUMPAD1`, `DIVIDE`) and characters on the keys (`;`, `/`, which mean the main-row key, not the numpad one).
- **Shared bottom panel editor:** `Blocks/UI/BindableEditorViewModel` + `BindableEditorView` (bindable's block tree + Containers/Conditions/Actions list + splitter). Any tab that edits bindables puts one in the bottom panel and calls `ShowBindable(bindable, label)` / `ClearBindable()`. `KeybindViewModel` derives from it and adds key selection/drafts.
- **Collapsing:** every block has `IsCollapsed` (saved with it, `SetIsCollapsed`). The `▴` corner button collapses a block to a drop-zone-sized box that keeps its outline and shows `▾`, and clicking that box expands it. Keys don't collapse (`IEditButtons.CanCollapse`).
- Keys: a key is green when its root slot has a block. Selecting an unbound key shows an unsaved draft with an empty On Pressed, which gets bound to the config on its first change. 
- Binding slots vs block slots: a **binding slot** (each key, later chat commands, …) holds a whole `BindableModel`, and **block slots** live inside a bindable's tree. Blocks are only placed in the editor, never dropped on a key. Whole bindings move between binding slots with `BindableDragPayload` (drag a selected key's handle onto another key). A binding slot only accepts a drop when it's empty, and the source is removed only after the copy is bound.

## Adding a block

Create a class in `Components/Blocks/Actions/`, `Containers/` or `Conditions/` that inherits `ActionBlock`, `ContainerBlock` or `ConditionBlock`, and implement `Name`, `Description` and `ExecuteAsync` (or `CheckAsync`). `BlockResolver` finds it automatically, and it appears in the block list under its category.
- Configurable fields are properties marked `[ConfigurableParameter("Label", ConfiguratorType.X)]` whose setter calls `SetParameter(ref _field, value)`.
- A `Menu` parameter gets its options from a `public static List<string> Get{PropertyName}MenuItems()` method. A `Slider` gets its range from `public static (double, double) Get{PropertyName}Bounds()`.
- Containers: create slots in the constructor, return them from `Slots`, call `TrackSlots()` at the end of the constructor, and save each slot through a private `[JsonInclude]` property using `slot.GetData()` / `slot.LoadData(value)` (see `OnPressedBlock`, `ListBlock`). The editor UI is generated from `Slots`, so no new view is needed.
- Mark `Name`, `Description` and `Slots` overrides `[JsonIgnore]`.
- **The class name is the JSON key.** Renaming a block class drops saved blocks of that type.

## Chatbot

`Components/Chatbot/` follows the same Model / Background / UI split.
- **Model:** `ChatbotComponentModel` holds the one Client ID/Secret both accounts log in through (`SetCredentials`), `IsEnabled` (whether the streamer should be connected), and `Root`, a single chat block slot every message starts at (a fresh chatbot gets an empty Match Text there; saved as `rootBlock`). The tree and enabled state are saved to `Config/chatbot.json`, the credentials to the `twitchApp` section of `Config/auth.json` (both git-ignored). Connecting/disconnecting is just `SetIsEnabled`, and anything can call it.
- **Two accounts:** the **streamer** login listens (EventSub only sends redeems/subs/bits/follows to the broadcaster, and one websocket carries one account's subscriptions). The optional **bot** login is only used to send chat. Without a bot, chat is sent as the streamer. Each account's tokens remember which app made them. In the tab, the streamer's Log in = connect (asks for a code the first time) and Log out = disconnect + forget. The bot logs in with its own button: the code is entered in a private browser window logged in as the bot. Tokens for both are in the `twitchLogins` section of `Config/auth.json`. The app is a Confidential Twitch app, so the device code flow sends the client secret.
- **Chat blocks** (`Chatbot/Blocks/`) are `BlockModel`s in the `Chat` category. **Match Text** is a list of rows (`MatchTextRowList` of `MatchTextRow`): each row is a pattern (plain text or regex, always case-insensitive unless the pattern says `(?-i)`; named groups `(?<name>...)` become parameters) plus one chat block slot to its right. There's always exactly one empty row at the end (typing in it adds the next). A row disappears when both its text and slot are empty, and an empty row with a block is skipped when matching. **Parameter** has a name and a pattern (default `\S+` = one word, `.*` = rest) and a child list of chat blocks. **Bindable** holds a `BindableModel` edited in the bottom panel, and reaching it runs the bindable with `rest` = the unmatched remainder. Spaces are never implied. Slot kinds from other components are shown through `BlockViewModel.RegisterSlotViewModel` (the chatbot registers `MatchTextRowListViewModel`).
- **Matching** (`ChatBlock.TryMatchSlot(model.Root, ...)`, run by `ChatCommandManager` for every chat message): blocks/rows are tried top to bottom, each pattern anchored where the previous one stopped (`\G`). Something that matches but doesn't lead to a Bindable falls through to the next row/sibling. The **first** Bindable reached runs and the search stops. Disabled blocks are skipped. An invalid pattern is logged once and never matches, and patterns time out after 100ms. Messages from the bot account are ignored. Chat blocks flash purple (raise `Triggered`) when they **match**, even if nothing after them does: a Match Text when any of its rows' text matched, a Parameter when its pattern matched, a Bindable when reached. So `!bruh` lights up the block with the `!` row and nothing after it.
- **Parameters** travel with each run in `TriggerContext.Parameters`, never in `GlobalContext`. Every chat trigger gets `user`, `userLogin`, `userId`, `message`, `messageId`, `channel`, `channelLogin`, `channelId`, `timestamp`, `color`, `badges`, `isBroadcaster`, `isModerator`, `isVip`, `isSubscriber` (`TwitchEvent.ChatParameters`), plus `rest` and anything the chat blocks capture. Text settings use them as `{name}` via `context.Format(text)` (Console Message, Send Chat Message).
- **Background:** `ChatbotManager` watches the model and keeps a connection while enabled, retrying with backoff (5s doubling to 60s). It uses `TwitchAuthManager` for logins and `TwitchEventSubClient` (EventSub over websocket, handling reconnect messages and keepalive timeouts), and subscribes to everything in its `Subscriptions` table. Adding an event type is one line there plus a constant/description in `TwitchEvent`. Subscriptions the account isn't allowed are logged and skipped. `EventReceived` / `StatusChanged` are raised on the UI thread. The status text beside each Log in button is a short state only, and failures go to the console (a repeated connection error is logged once, not on every retry). `TrySendChatMessageAsync` sends as the bot (or streamer). The manager and model are in `GlobalContext`.
- `TwitchApi` is the only place that talks HTTP to Twitch. TwitchLib is still a package reference but unused (its PubSub service was shut down by Twitch).
- **Chatbot tab layout:** the tree with its own Chat Blocks list (chat blocks only) beside it, above the bottom panel splitter. Clicking a Bindable chat block opens its bindable in the bottom panel's `BindableEditorViewModel`, the same editor the Keyboard tab uses (its header shows the command path, e.g. `! › lurk`). The window side panel is unused on this tab. The Client ID/Secret boxes are masked (`PasswordChar` + Fluent's `revealPasswordButton` eye) so the window is safe to show on stream.

## Threading

Right now everything runs on the UI thread, because raw key input arrives through a hooked window procedure (`KeyboardInputManager.WndProc`). Future integrations (Twitch, OBS websocket, Spotify) will call back on other threads. Switch back to the UI thread with `Dispatcher.UIThread.Post` before touching models.

## Environment and setup notes

- Windows only, .NET 10, Avalonia 12, CommunityToolkit.Mvvm. Build with `dotnet build`.
- The config is saved to `Config/keyboard.json`. The path is hard-coded in `JsonFileStore.RootDir`; the AppData version is commented out.
- Voicemeeter setup is specific to my setup: mic = `Strip[0]`, "Misc" (games/Discord) = `Bus[3]`, "Obs" = `Bus[4]`. The mute/unmute sound paths in `MuteVoicemeeterAction.cs` are hard-coded.
- `ObsService` is an empty placeholder.

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
