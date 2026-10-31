# StreamAssist — context for Claude

An "everything app" for streamers (Windows, .NET 10, Avalonia 12, CommunityToolkit.Mvvm): keybinds, a Twitch chatbot, alerts / redeems, Voicemeeter, OBS, Spotify and SQLite tables, all driven by Scratch-like blocks. It glues existing tools together rather than replacing them. This file holds the rules and the non-obvious parts; details (block lists, settings, field names) are in the code, search there.

## Working with me

- This is my first desktop/frontend app: explain the reasoning behind UI/MVVM/Avalonia suggestions, not just code.
- Don't fix unrelated bugs unless asked, point them out (see Known issues).
- Don't make changes just for the sake of changes. Commit only when asked.
- Never read or print `Config/auth.json` or any secret/token.
- The user's app may be running: check `tasklist | grep -i streamassist` before building into `bin/` or touching `Config/`. While it runs, build/test into another folder (`dotnet build -o <tmp>`, `dotnet test StreamAssist.Tests -o <tmp>`) and never edit `Config/` (the app would save over it). Back up a config file before editing it.
- No auto-migration code for old save formats: a format change gets a one-off conversion of my `Config/` files (app closed, backed up), then a load check with the real models.
- Don't simulate clicks or typing in the app (they can land in another window).
- **Key input must keep working while the app isn't focused:** raw input is registered with `RIDEV_INPUTSINK` on the main window. Never change that or route keys through anything that needs focus.
- **Console = failures only** ("no news is good news", `AppLog`, `[time] kind - message`): `WriteError` (failed), `WriteWarning` (went wrong but the app carried on, e.g. a dropped connection), `WritePrompt` (the user must act, e.g. a login link); `WriteLine` only for the Console Message block. Short (5-10 words), no full stop, outside details in brackets at the end: `can't send chat message, twitch not connected`, `streamer disconnected from twitch (reason)`. A block's error starts with its name. Asking for what's already the case is silent success, except something the user named that isn't there (`!delcom !lurkk` → not found). Ask before adding any other kind of message.
- Twitch blocks act only on my own channel (no raids, whispers, other channels). The bot account only has chat + moderation scopes; everything else is done as the streamer.
- Built-in chat commands confirm briefly in chat when they work (`Command !lurk added`).
- Not built yet, don't build unless asked: timers, a Cooldown block.

## Layout

- `Shell/`: main window, panels, `GlobalContext` (static service locator), `MainWindowViewModel` (composition root: loads models, creates managers), `WindowLayoutModel` (`Config/window.json`: window, selected tab, splitter sizes by name via `GetPanelSize`/`SetPanelSize`, collapsed block groups).
- `Components/<Name>/` one folder per feature, each split into **Model/** (data only, JSON, raises events, never does work), **Background/** (managers created once at startup that listen to models and do the work), **UI/** (view models made when a tab opens, disposed when it closes; they change things only through model methods, never managers). Flow: UI → model method → model event → background + UI. Feature blocks live in their component's `Actions/`, `Conditions/`, `Values/`; `Components/Blocks/` holds the block system and the generic logic blocks.
- `Core/`: knows nothing about features (if it references a component it doesn't belong here). `Persistence/`: `JsonFileStore`, `CredentialStore` (every secret in one git-ignored `Config/auth.json`, a section per service), `AppPaths`. `Utilities/`: `GrowingList<T>` (a list that always ends in one empty item), `Formula`. `Mvvm/`: base classes, `CommitOnLeave`, `Flash`.
- `Styles/Resources/` (`Brushes`, `Typography`, `Spacing`) and `Styles/Controls/` (one file per control type, app-wide style classes). `App.axaml` lists them in order.
- **`AppPaths`**: the app folder is the exe's folder, or the project folder when run from `bin/` (found by `StreamAssist.csproj`). `Config/` and `Assets/` live there; both are git-ignored. Paths in blocks (sounds, …) are relative to `Assets/` (`Sounds/boom.mp3`, folders Sounds / Images / Videos made at startup) and can never leave it (`TryResolveAsset`); the file picker only takes files inside it. App files that must ship (e.g. a future logo) don't go in `Assets/`.
- `StreamAssist.Tests/`: see Tests.

## Rules

- **Everything the UI can do is a public model method** with the rules enforced in the model, so blocks, chat commands and code can do anything the user can and the UI follows the model's events.
- Public `TryXxx(...)` validates, logs via `AppLog.WriteError`, returns bool, and calls a private `Xxx(...)` that does the work and raises the event.
- Event subscriptions go through paired `SubscribeToXxx` / `UnsubscribeFromXxx`; `Dispose()` unsubscribes everything. Keep a reference to what you subscribed to (don't re-read a property that may have changed, e.g. a config switch).
- JSON: public getters `[JsonIgnore]`, saved through private `[JsonInclude] [JsonPropertyName("camelCase")]` properties. Cross-links resolve in `IJsonOnDeserialized.OnDeserialized` / `AttachXxx`.
- Style: `_camelCase` fields initialised in the constructor, `// section` headers, three blank lines between members, comments explain why.
- `CallbackBlocker` stops UI → model → UI loops. `AttachUnbindSelf(...)` lets a child remove itself from its parent (an emptied bindable unbinds itself).
- Threading: everything runs on the UI thread (raw input arrives via `KeyboardInputManager.WndProc`). Integrations calling back on other threads switch with `Dispatcher.UIThread.Post` before touching models.

## XAML / look

- `ViewLocator` maps `FooViewModel` → `FooView` (same namespace): no DataTemplate that only wraps the matching view.
- Colours in `Brushes.axaml`, spacing in `Spacing.axaml`, never hard-coded in views. Views use `{StaticResource}`, style files `{DynamicResource}`. View-only styles go in the view's `<UserControl.Styles>`. Class names are case-sensitive.
- Outlines only, calm low-contrast palette (old palette/fills kept in a comment in `Brushes.axaml`). Panel fills are black at alpha 1 (`#01000000`), not Transparent: a fully transparent fill isn't hit-testable, so clicks/drops would only land on text.
- **Everything clickable has a border.** **Ghost fields** (`GhostBorder`, `GhostText`, text drawn at half strength): the "new thing" boxes (`TextBox.New` while empty: the empty row of a growing list, New table/config, …) and empty block slots (`DropZone`). `TextBox.Plain` (no border) only for text that can't be edited.
- `Border` has no global style: `Classes="Panel"` for the rounded look, `Divider` for lines. Buttons: `Small` / `Medium` / `Large`, plus `Square` for icons; plus/minus are full-width `＋` / `－`. Collapsible list headings: title text + a `Square Small` arrow button (only the arrow toggles).
- A box that must show a value the view model resets (cleared "new" boxes, refused values) commits on leave (`CommitOnLeave`, `Commit` class) instead of binding Text directly: a TextBox ignores changes to the property it's writing.
- **Spacing (keep exact, it's very visible):** lines (dividers, splitters, scrollbars) run edge to edge, 5px from the next line or window edge; content sits 5px further in.
  - Splitter column/row = `SplitterLength` (15) with `ColumnSplitterMargin` / `RowSplitterMargin`; panels beside it get no gap.
  - Panel content (not dividers) in `Margin="{StaticResource ContentInset}"`; dividers are siblings of inset sections.
  - A panel's ScrollViewer gets `Classes="Gapped"` and no margin; its content carries the inset (e.g. `5,5,10,0`). Not on scroll viewers inside controls. Bars stay thin (default).
  - Main scroll areas end with `<Border Padding="{StaticResource ScrollEndPadding}">` (room to scroll the last row up).
  - Headers: `HeaderMargin` + `Height="{StaticResource HeaderHeight}"` (`MinHeight="0"` on boxes), divider under it with `DividerMargin`. The tab bar and Output dividers are the reference.

## Blocks

- Every trigger (key, chat row, redeem, alert) owns a `BindableModel` with one root `BlockSlot`; blocks nest Scratch-style. `TriggerStart/Stop/Once` run the root with a `TriggerContext` (phase, cancellation token, parameters). Execution is async; pass `context.CancellationToken` to waits; disposing a bindable cancels its runs.
- Errors are logged where they happen: `throw Fail("reason")` logs `error - <block>: <reason>` (value slots add their label) and throws `BlockFailedException`, which stops the run; `SafeRunAsync` at the root only logs unexpected exceptions. **Stop** throws `StopRunException`, ended quietly. A disabled block skips itself and everything inside.
- Kinds: `ContainerBlock` (slots), `ActionBlock`, value blocks `ValueBlock<T>` (text, int, double, bool, char); `ConditionBlock` is the bool one (disabled/empty = false). `AdaptableValueBlock` ("any type": Convert, Expression, Get Table Value, getters) switches to the kind of the slot it's dropped in; changing its dropdown to a kind its slot can't take is refused.
- **Values:** a setting that can come from logic is a `ValueSlot<T>`: typed text (`{name}` parameters and `{var.name}` variables filled in, then read as the slot's kind) or a value block placed in it (wins over the text; disabled falls back to it). `ValueTypes` decides all conversions. `AnyValueSlot` takes any kind (Equals). Growing slot lists: `ValueSlotList` (one kind), `ValueSlotPairList` (name + value rows: String Builder, Set Parameters, Set Table Row).
- **Expression** (`Core/Utilities/Formula`): typed maths and true/false, C# rules (whole numbers stay whole, `== != < > && || !`, `round floor ceil truncate abs min max`), numbers and booleans only. **String Builder**: a text with `{name}`s plus name/value rows that **only that block sees** (block-scoped). **Set Parameters**: run-scoped. Parameters live in `TriggerContext.Parameters` (each run its own copy), always text; nothing is kept between runs. Persistent values are Data variables/tables.
- **Blocks are getters and setters, the logic is the user's** (If, Expression, Equals around them). Simple options like Toggle next to On/Off are fine.
- **Adding a block:** inherit `ActionBlock` / `ContainerBlock` / `ConditionBlock` / `ValueBlock<T>`, implement `Name`, `Description` (all `[JsonIgnore]`), `ExecuteAsync` / `CheckAsync` / `ComputeAsync`. Logic-capable settings are `ValueSlot<T>`s made in the constructor, listed in `Slots`, `TrackSlots()` at the end of the constructor, saved via `GetData()` / `LoadData()` in a private `[JsonInclude]` property. Fixed settings are `[ConfigurableParameter("Label", ConfiguratorType.X)]` properties whose setter calls `SetParameter(ref _field, value)`; a Menu's options come from `public static List<string> Get{Prop}MenuItems()`, a Slider's range from `Get{Prop}Bounds()`. `BlockResolver` finds blocks by reflection and the block list groups them by component (or `KindGroup`, or a `Group` override). **The class name is the JSON key**: renaming a block class drops saved ones.
- Slots: `BlockSlot` (typed by `ExecutableBlock` / `ConditionBlock`), `BlockSlotList`, `BlockBranchList` (If), value slots. `Filter` narrows what fits, `Lock` refuses blocks for now, `Owner` is the block. Each slot kind has a view model picked in `BlockViewModel` (other components add theirs with `RegisterSlotViewModel`); slots holding slots expose `InnerSlots`. Dropping on a list item inserts before it (`Border.InsertBefore` line). The nearest drop target handles a drag (`DropBehaviorHandler`).
- Changes bubble up to `BindableModel.Changed`; saving listens there. Loops call `ContainerBlock.NextPassAsync` each pass (yields to the dispatcher on the UI thread, stops when cancelled).
- **Configs** (`ConfigSet<T>` + `ConfigBar`): the Keyboard, Chat and Events tabs each have named configs and one in use for the whole tab. A tab starts with **none** (nothing can be bound or runs, the page just says "Create a config"); the first is empty, later ones copy the current one, deleting the last returns to none. Keyboard config = every slot's keys by slot id; Chat config = both chat trees; Events config = alerts + redeems. The model's `Root` / `EditableRoot` / `Redeems` / `Events` are the selected config's (null/empty without one).
- Shared bottom editor: `BindableEditorViewModel` (`ShowBindable`, `ShowDraft` for an unsaved draft that binds on its first change, `ClearBindable`); `KeybindViewModel` extends it for keys.

## Keyboard

- `KeyboardInputManager` (raw input, `RIDEV_INPUTSINK` + `RIDEV_DEVNOTIFY`, session lock releases held keys) keeps held keys per keyboard; `KeyDown` fires once per press. `KeyboardDevices` groups raw input connections into physical keyboards by container id; a slot saves its keyboard's main path and finds it again when plugged back in.
- Key codes are Windows VKs, except left/right Shift/Ctrl/Alt use `0xA0`-`0xA5`, numpad Enter is the app's `0x0E`, and numpad keys stay numpad with Num Lock off. Typed `shift`/`ctrl`/`alt` mean either side.
- `KeybindManager` caches nothing (looks the key up on every event). Slot layouts: Keyboard picture or All keys (32×8 grid by code). Keys stay light (edit buttons only on bound keys).

## Chatbot (Twitch)

- No Twitch library: `TwitchApi` does all HTTP (logins via device code flow, EventSub websocket, Helix). Two accounts: the **streamer** listens (EventSub), the optional **bot** sends chat/moderation (as the streamer if no bot, unless **Chat only as bot**).
- Connections (all integrations): **one attempt** at startup (if connect on startup) or on Connect/Retry; failures are logged once and left Failed, never auto-retried. Logins are refreshed only by the background check (Twitch every 30 min, Spotify every 5), Connect/Retry and startup, never by an action; a refused token marks that account disconnected.
- All Twitch actions share one `TwitchActionQueue` (one per second). Duplicate chat messages get an invisible character so Twitch posts them. Only the app that created a reward can edit it.
- Chat trees: **Built-in** root tried before **Custom** (only a Match Text; `!addcom`/`!editcom`/`!delcom` edit it via `TryAddCommand` etc., walking plain-text rows). Match Text rows are plain text (whole words, case ignored, spacing rules in `ChatText`) or `/regex/` (`//` matches anything). Parse Parameter reads a word or the rest. Has Permission: Anyone < Subscriber < VIP < Moderator < Broadcaster (no Follower: chat doesn't say). A `ChatTarget` is **either** a binding **or** a child block, never both. The first binding reached runs. Bind boxes show the command path.
- Chat parameters come from `TwitchEvent.ChatParameters`; event/redeem parameter lists are in `ChatEvents` (tests check they match what Twitch and the test panels give: add a parameter in both).

## Other integrations

- **Connections tab:** one `ConnectionRowViewModel` per connection; the connected account's name is only in the status tooltip (the tab can be on stream). A new integration = model (saved connect-on-startup, session-only enabled, reconnect) + manager (`Status`, `StatusChanged`) + a row.
- **OBS:** obs-websocket v5 directly (`ObsClient`, `ObsManager`), sources by name, empty scene = live scene. Tests use `FakeObsServer`.
- **Spotify:** Web API with PKCE login on `http://127.0.0.1:8974/callback` (plain socket listener). Playback needs Premium. Tests use `FakeSpotifyServer`.
- **Voicemeeter:** direct DLL calls; channels by the names its window shows (`VoicemeeterChannels` per edition). The app keeps no Voicemeeter state.

## Data and files

- **Config is JSON** (`Config/*.json`, what you build); **data is SQLite** (`Config/data.db`, what accumulates: variables, quotes, per-viewer rows) via `DataModel` (in memory, Try methods) + `DataStore` (writes each event as a transaction). Built-in table `variables` (columns locked); custom tables free. `{var.name}` reads a variable. Table blocks take the table by name (a text slot).
- `JsonFileStore`: **a file that can't be read is never overwritten** (the tab shows `BrokenFileViewModel` with Back up & start fresh); small settings files reset with a backup; saves go through a `.tmp` swap with retries (OneDrive). `CredentialStore` writes nothing while auth.json is broken.

## Tests

- `dotnet test StreamAssist.Tests` (not a bare `dotnet test`). `EngineChecks.RunAsync` is one long ordered list of `Check("...", condition)` in `// ---- section ----` blocks, run as one xUnit test. Add/update checks in the matching section when behaviour changes. Test-only blocks (`RecordBlock`, `FixedCondition`, ...) record what ran; helpers `Chatbot()` / `Keyboards()` make models with a config.
- Checks that count console errors must come before the first `await` (AppLog posts through Avalonia's dispatcher).
- Tests never touch the real `Config/` (they use a temp folder) or real OBS/Spotify/Twitch.

## My setup

- Voicemeeter binds: Numpad `-` = Stereo Input 1 (mic), `*` = B1 (games/Discord), `/` = B2 (OBS), each Set Voicemeeter Mute (Toggle) with my mute/unmute sounds.
- Quotes: `!addquote <message>` (mods) and `!quote` (VIP+) use the custom `quotes` table.
- OBS WebSocket on `localhost:4455` with a password.

## Known issues (not fixed yet)

- `KeyboardSaveManager.Dispose` cancels the pending save instead of saving, so changes in the last second before closing are lost.
- Remove slot with zero slots crashes (`KeyboardListViewModel` calls `Slots.Last()`).
- `KeyboardSlotModel.Dispose` disposes the selected device, which it doesn't own.
- A repeated block error (e.g. a broken chat command) is logged every time it runs, not once.
