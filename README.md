# StreamAssist

**This project is on hiatus.**

## About

StreamAssist is a Windows desktop app built with Avalonia (MVVM). It's a streaming assistant app — the goal is to have one place to trigger stream-related actions (playing a sound, changing an OBS scene, muting a mic, configuring a chat bot) from various inputs, instead of using separate tools for each.

The UI updates live: keys light up when pressed, actions can be dragged onto keybinds, groups can be reordered, and config changes take effect immediately without saving or restarting.

## Goal

The app is built around one pattern: something happens (a trigger), and one or more actions run in response. A trigger could be a key press, a chat command, a timer, or a manual trigger. An action is a user-configured effect — playing audio, switching an OBS scene, muting a source, posting to chat, etc.

## Current Features

Right now keyboard input is the only trigger source, and there are only a few actions implemented. Triggers and actions are kept decoupled from each other so that new trigger sources can use existing actions, and new actions can be used by existing (and future) triggers, without either side needing to know about the other.

### Keybind Component
- Supports multiple physical keyboards at once, distinguished by device path via [RawInput.Sharp](https://github.com/mfakane/rawinput-sharp), so a bind on one keyboard doesn't fire from another.
- Actions are bound to keys by dragging them onto a visual keyboard layout.
- Each key can be configured to trigger independently on **press**, **release**, and **repeat** (holding the key down), each with its own group of actions.
- Multiple actions can be stacked on a single trigger and reordered by dragging.
- Bound keybinds can be dragged between keys, or removed.
- Keys reflect current state: bound, disabled, selected, or pressed.
- Keyboard "slots" can be added or removed, each independently assigned a device and a config.
- Configs are named and reusable, and can be swapped between slots — e.g. a "Gaming" config and a "Just Chatting" config on the same keyboard, without re-binding anything.
- Configuration is saved to disk automatically.

### Actions
Actions are the "do something" half of the app. There are three so far:
- **Play Sound** — plays a configured audio file.
- **Print Console Message** — writes a configured message to the in-app console. Mainly used for testing trigger flow.
- **Mute Voicemeeter Output** — integrates with Voicemeeter (Banana) to toggle or force-set the mute state of either (or both) of the app's virtual mic outputs. Useful when a game or app has poor or no mute-toggle support — the mic output feeding that app/game can be muted independently while a separate output still feeds chat/OBS.

Actions are discovered and instantiated via reflection, so adding a new one is a matter of writing a class — no manual registration step.

## Planned Work

- **OBS integration** — scene switching, source visibility, source transforms/positioning, volume control, and other source-level control. (`obs-websocket-dotnet` is already a dependency; this isn't wired up yet.)
- **More trigger sources** — chat commands, timers/scheduled events, and other stream events, feeding into the same action system keybinds already use. (`TwitchLib` is already a dependency for this.)
- **Chat bot tab** — command handling and chat interaction.
- **Timer tab** — countdowns, intervals, and scheduled triggers.
- **Dashboard** — a consolidated view across components.
- **More actions** — the current three are a starting point.

## Architecture

StreamAssist is split into three layers per component:

- **Model (data)** — Plain data classes holding configuration state (`KeyboardListModel`, `KeybindConfigModel`, `KeybindModel`, `ActionModel`, etc.). These serialize directly to/from JSON and expose events when they change. Models don't run anything — setting a keybind's trigger doesn't play a sound, it just records that a sound should play when that trigger fires.
- **UI (view models)** — Classes like `KeyboardListViewModel`, `KeybindViewModel`, and `ActionViewModel` wrap the models for the Avalonia front end. View models are created and destroyed as the user navigates the app (e.g. opening/closing a tab). They configure and observe the model, but don't execute behavior themselves.
- **Background (runtime)** — Classes like `KeybindManager` and `KeyboardSaveManager` subscribe to model events and do the actual work: dispatching key presses to action groups, executing those groups, and persisting changes to disk. This layer is instantiated once and runs for the app's lifetime, independent of what's currently open in the UI — closing the Keybinds tab doesn't stop keybinds from working.

The point of this split is that a model can be driven by the UI, restored from a saved file, or eventually driven by something with no UI at all (a chat command handler, for example), without any of those needing to know about each other. Auto-save works the same way — `KeyboardSaveManager` listens for changes anywhere in the model tree and debounces a write to disk, rather than each UI action needing to trigger a save itself.

Actions work similarly: `ActionModel` is a small abstract base with one `Execute()` method to implement, and new actions are picked up automatically via reflection (`ActionResolver`) rather than needing to be registered manually.

## Usage

> **Note:** StreamAssist is Windows-only, due to its reliance on raw input APIs and Voicemeeter.

1. Clone the repository and open the solution.
2. Build and run the project (requires .NET and the Avalonia workload).
3. On first launch, add a keyboard slot and select one of your connected keyboards from the device list.
4. Create a keybind config, then drag actions from the action list onto any key to bind them — choose whether each action should fire on press, release, or repeat.
5. Configuration is saved automatically; no manual export/import step is needed to persist your setup between sessions.

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| [Avalonia](https://avaloniaui.net/) | 12.0.5 | Cross-platform UI framework |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.4.1 | MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| [NAudio](https://github.com/naudio/NAudio) | 2.3.0 | Audio playback for the Play Sound action |
| [obs-websocket-dotnet](https://github.com/BarRaider/obs-websocket-dotnet) | 5.0.1 | OBS Studio integration (in progress) |
| [RawInput.Sharp](https://github.com/mfakane/rawinput-sharp) | 0.1.3 | Raw keyboard input, per-device identification |
| [TwitchLib](https://github.com/TwitchLib/TwitchLib) | 3.5.3 | Twitch chat/API integration (in progress) |

Voicemeeter (Banana) integration is done via direct API calls rather than a NuGet package, and is required at runtime for the Mute Voicemeeter action to function.