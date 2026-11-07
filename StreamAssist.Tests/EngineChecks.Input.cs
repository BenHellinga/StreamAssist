using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Actions;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Spotify.Values;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.Data.Background;
using StreamAssist.Components.Data.Actions;
using StreamAssist.Components.Data.Containers;
using StreamAssist.Components.Data.Values;
using System.Net;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Conditions;
using StreamAssist.Components.OBS.Values;
using StreamAssist.Components.OBS.Actions;
using StreamAssist.Components.OutputConsole.Actions;
using StreamAssist.Components.Input.Conditions;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Core.Connections;
using StreamAssist.Components.Chatbot.Values;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Blocks.Values;
using StreamAssist.Components.Blocks.Actions;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.ChatBlocks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Blocks.Conditions;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Input.UI;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.UI;

namespace StreamAssist.Tests;



// engine checks: key names, key state, keybinds and raw input devices
internal static partial class EngineChecks
{
    // ---- key names ----
    private static void KeyNameParsing()
    {
        ushort V(string n) => StreamAssist.Components.Input.Background.KeyNames.TryParse(n, out var v) ? v : (ushort)0xFFFF;
        Check("key names", V("shift") == 0x10 && V("Ctrl") == 0x11 && V("alt") == 0x12 && V("t") == 0x54 && V("F5") == 0x74
            && V(";") == 0xBA && V("[") == 0xDB && V("/") == 0xBF && V("numpad1") == 0x61 && V("esc") == 0x1B && V(" Space ") == 0x20 && V("5") == 0x35);
        foreach (var n in new[]{"/","-","+","*",".","divide"}) Log($"  {n} -> {V(n):X}");
        Check("bad key name rejected", V("nonsense") == 0xFFFF && V("") == 0xFFFF);
    }



    // ---- key state + keybind manager + key pressed ----
    private static async Task KeyStateAndKeybinds()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var input = new DeviceInputManager();
        var held = (HashSet<(string, ushort)>)typeof(DeviceInputManager).GetField("_keysDown", flags)!.GetValue(input)!;
        void Raise(string name, string dev, ushort vk)
        {
            var d = (Action<string, ushort>?)typeof(DeviceInputManager).GetField(name, flags)!.GetValue(input);
            d?.Invoke(dev, vk);
        }
        void Down(string dev, ushort vk) { if (held.Add((dev, vk))) Raise("KeyDown", dev, vk); }
        void Up(string dev, ushort vk) { held.Remove((dev, vk)); Raise("KeyUp", dev, vk); }
        StreamAssist.Shell.GlobalContext.DeviceInputManager = input;

        var list = Keyboards();
        var dev1 = new InputDeviceModel("dev1", "p", "m", 1, 2);
        var dev2 = new InputDeviceModel("dev2", "p", "m", 3, 4);
        list.AttachDevices(new List<InputDeviceModel> { dev1, dev2 });
        list.CreateSlot(out var slot);
        var cfg = slot.SelectedConfig!; // the slot's keys in the Default config
        slot.TrySetSelectedDevice(dev1);
        var km = new KeybindManager(list, input);

        // T: if shift held -> "S" else "T"; on release -> "R"
        var tBind = new BindableModel();
        var tList = new ListBlock(); tBind.TryAddBlock(tList);
        var press = new OnPressedBlock(); var ifb = new IfBlock();
        var kp = new KeyPressedCondition { Key = "shift" };
        ((BlockBranchList)ifb.Slots[0]).TryInsertBranch(kp, 0);
        ((BlockBranchList)ifb.Slots[0]).Branches[0].Then.TrySetBlock(new RecordBlock { Tag = "S" });
        ((BlockSlot)ifb.Slots[1]).TrySetBlock(new RecordBlock { Tag = "T" });
        ((BlockSlot)press.Slots[0]).TrySetBlock(ifb);
        var rel = new OnReleasedBlock(); ((BlockSlot)rel.Slots[0]).TrySetBlock(new RecordBlock { Tag = "R" });
        ((BlockSlotList)tList.Slots[0]).TryInsertBlock(press, 0); ((BlockSlotList)tList.Slots[0]).TryInsertBlock(rel, 1);
        cfg.TryBindKey(0x54, tBind);

        async Task<string> Do(Action a) { RecordBlock.Log.Clear(); a(); await Task.Delay(50); return string.Join("", RecordBlock.Log); }
        Check("T alone -> else", await Do(() => Down("dev1", 0x54)) == "T");
        Check("release -> on released", await Do(() => Up("dev1", 0x54)) == "R");
        slot.SetIsDisabled(true);
        Check("a switched-off input device box runs nothing", await Do(() => Down("dev1", 0x54)) == "");
        Up("dev1", 0x54);
        slot.SetIsDisabled(false);

        // input device boxes: made where they're dropped (Keyboard or Generic), moved by dragging
        list.TryCreateSlot(InputSlotModel.AllKeysLayout, 0, out var generic);
        Check("input devices: a Generic box goes where it's dropped", list.Slots[0] == generic && generic.Layout == InputSlotModel.AllKeysLayout && list.Slots[1] == slot);
        Check("input devices: moving one puts it before the one it's dropped on", list.TryMoveSlot(generic, 2) && list.Slots[1] == generic && list.Slots[0] == slot);
        list.TryDeleteSlot(generic);
        Check("shift+T -> then", await Do(() => { Down("dev1", InputDevices.LeftShift); Down("dev1", 0x54); }) == "S");
        Check("repeat key down ignored", await Do(() => Down("dev1", 0x54)) == "");
        await Do(() => { Up("dev1", 0x54); Up("dev1", InputDevices.LeftShift); });
        Check("other keyboard doesn't trigger", await Do(() => Down("dev2", 0x54)) == "");
        Up("dev2", 0x54);
        Check("right shift on other keyboard counts for Any keyboard", await Do(() => { Down("dev2", InputDevices.RightShift); Down("dev1", 0x54); }) == "S");
        Up("dev1", 0x54); Up("dev2", InputDevices.RightShift);

        Down("dev2", 0x41);
        var heldOk = input.IsKeyDown(0x41) && input.IsKeyDown("dev2", 0x41) && !input.IsKeyDown("dev1", 0x41);
        Up("dev2", 0x41);
        Check("IsKeyDown any/specific", heldOk && !input.IsKeyDown(0x41));

        // switching config/device takes effect immediately (no cached lookup)
        list.Configs.TryCreate("c2"); // a copy of Default, switched to
        var cfg2 = slot.SelectedConfig!;
        cfg2.UnbindKey(0x54);
        var b2 = BindableModel.CreateDefault(); ((BlockSlot)b2.Root.Blocks.FirstOrDefault()!.Slots[0]).TrySetBlock(new RecordBlock { Tag = "2" }); cfg2.TryBindKey(0x54, b2);
        Check("config switch used right away", await Do(() => Down("dev1", 0x54)) == "2");
        Up("dev1", 0x54);
        slot.TrySetSelectedDevice(dev2);
        Check("device switch used right away", await Do(() => Down("dev2", 0x54)) == "2");
        Up("dev2", 0x54);

        // a bind that changes config while the trigger loop runs doesn't crash
        var swap = new BindableModel(); swap.TryAddBlock(new SwapConfigBlock { List = list, To = "Default" });
        cfg2.TryBindKey(0x55, swap);
        Check("bind can switch config mid-trigger", await Do(() => Down("dev2", 0x55)) == "" && slot.SelectedConfig == cfg);
        Up("dev2", 0x55);
        km.Dispose();

        // tab configs: one config for every keyboard, switching swaps every slot's keys at once
        list.CreateSlot(out var slotB);
        Check("configs: a new slot gets its own keys in the config in use", slotB.Id != slot.Id && slotB.SelectedConfig != null && slotB.SelectedConfig != slot.SelectedConfig);
        slotB.SelectedConfig!.TryBindKey(0x41, BindableModel.CreateDefault());
        list.Configs.TrySelect("c2");
        Check("configs: switching swaps every slot's keys", slot.SelectedConfig == cfg2 && !slotB.SelectedConfig!.Keybinds.ContainsKey(0x41));
        list.Configs.TrySelect("Default");
        Check("configs: and back", slot.SelectedConfig == cfg && slotB.SelectedConfig!.Keybinds.ContainsKey(0x41));
        var listJson = JsonSerializer.Serialize(list);
        var listLoaded = JsonSerializer.Deserialize<InputListModel>(listJson)!;
        Check("configs: saved and loaded with the one in use, each slot finds its keys by id", listLoaded.Configs.SelectedName == "Default"
            && listLoaded.Configs.Names.SequenceEqual(new[] { "Default", "c2" }) && listLoaded.Slots[1].SelectedConfig!.Keybinds.ContainsKey(0x41)
            && listLoaded.Slots[0].SelectedConfig!.Keybinds.ContainsKey(0x54) && JsonSerializer.Serialize(listLoaded) == listJson);
        list.TryDeleteSlot(slotB);
        Check("configs: a deleted slot's keys go, in every config", list.Configs.Items.All(c => !c.SlotKeybinds.ContainsKey(slotB.Id)));
        Check("configs: deleting the last config leaves the keyboards with no keys", list.Configs.TryDelete("c2") && list.Configs.TryDelete("Default")
            && list.Configs.Items.Count == 0 && slot.SelectedConfig == null);
        list.CreateSlot(out var slotC);
        Check("configs: with none, a new slot has no keys either, the first config gives them some", slotC.SelectedConfig == null
            && list.Configs.TryCreate("Fresh") && slotC.SelectedConfig != null && slotC.SelectedConfig.Keybinds.Count == 0 && slot.SelectedConfig!.Keybinds.Count == 0);

        // either side: Shift / Ctrl / Alt typed by name mean either, a side's own code only that side
        Down("dev1", InputDevices.RightCtrl);
        Check("held keys: ctrl means either side", input.IsKeyDown(0x11) && input.IsKeyDown("dev1", 0x11) && input.IsKeyDown(InputDevices.RightCtrl)
            && !input.IsKeyDown(InputDevices.LeftCtrl) && !input.IsKeyDown("dev2", 0x11));
        Up("dev1", InputDevices.RightCtrl);

        // keyboards plugged in and removed: slots let go of a removed one (keeping it saved) and find it again
        var plug = Keyboards();
        var kbA = new InputDeviceModel("pathA", "Keys", "Maker", 1, 2);
        var kbB = new InputDeviceModel("pathB", "Keys", "Maker", 3, 4);
        plug.AttachDevices(new List<InputDeviceModel> { kbA, kbB });
        plug.CreateSlot(out var plugSlot);
        plugSlot.TrySetSelectedDevice(kbA);
        int devicesChanged = 0;
        plug.DevicesChanged += () => devicesChanged++;
        plug.AttachDevices(new List<InputDeviceModel> { kbB });
        Check("keyboards: a removed keyboard leaves its slot, still saved", plugSlot.SelectedDevice == null && JsonSerializer.Serialize(plugSlot).Contains("\"selectedDevice\":\"pathA\"") && devicesChanged == 1);
        plug.AttachDevices(new List<InputDeviceModel> { kbB, kbA });
        Check("keyboards: plugged back in, the slot has it again", plugSlot.SelectedDevice == kbA && devicesChanged == 2);
        plug.AttachDevices(new List<InputDeviceModel> { kbB });
        plugSlot.TrySetSelectedDevice(null);
        plug.AttachDevices(new List<InputDeviceModel> { kbA, kbB });
        Check("keyboards: picking none forgets the one that was waiting", plugSlot.SelectedDevice == null && !JsonSerializer.Serialize(plugSlot).Contains("pathA"));

        // layouts: the keyboard picture or every key code, saved per slot; switching remakes the keys, binds stay
        Check("layouts: a slot starts on the keyboard picture", plugSlot.Layout == InputSlotModel.KeyboardLayout && InputSlotModel.Layouts.Count == 5);
        int layoutChanges = 0;
        plugSlot.LayoutChanged += _ => layoutChanges++;
        Check("layouts: an unknown one is refused", !plugSlot.TrySetLayout("Piano") && plugSlot.Layout == InputSlotModel.KeyboardLayout && layoutChanges == 0);
        Check("layouts: switching says so and is saved", plugSlot.TrySetLayout(InputSlotModel.AllKeysLayout) && layoutChanges == 1
            && JsonSerializer.Deserialize<InputSlotModel>(JsonSerializer.Serialize(plugSlot))!.Layout == InputSlotModel.AllKeysLayout
            && JsonSerializer.Deserialize<InputSlotModel>("{\"layout\":\"Piano\"}")!.Layout == InputSlotModel.KeyboardLayout);
        var (allKeys, _, _) = DevicePictures.InitGenericDisplayKeys(25, 3);
        Check("layouts: all keys has F13-F24, media keys, mouse buttons, wheel steps and controller buttons, no either-side codes", allKeys.Any(k => k.VK == 0x7C && k.Label == "F13") && allKeys.Any(k => k.VK == 0x87)
            && allKeys.Any(k => k.Label == "MEDIA\nPLAY\nPAUSE" && k.VK == 0xB3 && k.X == 19 * 28 && k.Y == 5 * 28 && k.W == k.H) && allKeys.All(k => k.VK is < 0x10 or > 0x12)
            && new[] { InputDevices.MouseLeft, InputDevices.MouseBack, InputDevices.WheelUp, InputDevices.WheelRight, InputDevices.PadA, InputDevices.PadUp, InputDevices.RightStickLeft }.All(code => allKeys.Any(k => k.VK == code))
            && allKeys.Count == 0xFE - 3 && allKeys.Any(k => k.VK == 0x07 && k.Label == "0x07")
            && allKeys.Select(k => k.VK).Distinct().Count() == allKeys.Count && allKeys.Any(k => k.VK == InputDevices.RightShift));
        var layoutConfig = plugSlot.SelectedConfig!;
        layoutConfig.TryBindKey(0x7C, BindableModel.CreateDefault());
        var layoutView = new InputSlotViewModel(plugSlot, plug, input, new DeviceMenus([]));
        Check("layouts: the slot shows every key code", layoutView.Keys.Count == allKeys.Count && layoutView.Keys.Single(k => k.VK == 0x7C).IsBound);
        Check("layouts: only bound keys make edit buttons", layoutView.Keys.Single(k => k.VK == 0x7C).EditButtonsContext != null
            && layoutView.Keys.Where(k => k.VK != 0x7C).All(k => k.EditButtonsContext == null));
        plugSlot.TrySetLayout(InputSlotModel.KeyboardLayout);
        Check("layouts: another layout remakes the keys", layoutView.Keys.All(k => k.VK != 0x7C) && layoutView.Keys.Any(k => k.VK == InputDevices.LeftShift));
        layoutView.CollapseButtonPressedCommand.Execute(null);
        layoutView.ToggleButtonPressedCommand.Execute(null);
        Check("input device box: collapse and on / off go to the slot", plugSlot.IsCollapsed && layoutView.IsCollapsed && plugSlot.IsDisabled && layoutView.IsDisabled);
        layoutView.Dispose();

        // the keyboards on this PC (read only, logged for a look, how many depends on the PC)
        foreach (var device in DeviceInputManager.Devices) Log($"  keyboard here: {device.GetDisplayLabel()}  [{device.DevicePath}]");
    }



    // ---- raw keys and keyboard connections ----
    private static void RawKeysAndConnections()
    {
        Check("raw keys: the two shifts, ctrls and alts are told apart", InputDevices.KeyCode(0x10, 0x2A, false) == InputDevices.LeftShift
            && InputDevices.KeyCode(0x10, 0x36, false) == InputDevices.RightShift && InputDevices.KeyCode(0x11, 0x1D, false) == InputDevices.LeftCtrl
            && InputDevices.KeyCode(0x11, 0x1D, true) == InputDevices.RightCtrl && InputDevices.KeyCode(0x12, 0x38, false) == InputDevices.LeftAlt
            && InputDevices.KeyCode(0x12, 0x38, true) == InputDevices.RightAlt);
        Check("raw keys: numpad Enter is its own key", InputDevices.KeyCode(0x0D, 0x1C, true) == InputDevices.NumpadEnter && InputDevices.KeyCode(0x0D, 0x1C, false) == 0x0D);
        Check("raw keys: numpad keys with Num Lock off are still numpad keys", InputDevices.KeyCode(0x24, 0x47, false) == 0x67 && InputDevices.KeyCode(0x2E, 0x53, false) == 0x6E
            && InputDevices.KeyCode(0x24, 0x47, true) == 0x24 && InputDevices.KeyCode(0x25, 0x4B, true) == 0x25);
        Check("raw keys: windows' fake shifts and 'no key' are ignored, other keys pass", InputDevices.KeyCode(0x10, 0x2A, true) == null && InputDevices.KeyCode(0xFF, 0, false) == null
            && InputDevices.KeyCode(0x41, 0x1E, false) == 0x41);
        Check("raw keys: the layout and names know the new keys", KeyNames.KeyboardLabel(InputDevices.RightShift) == "Shift" && KeyNames.KeyboardLabel(InputDevices.NumpadEnter) == "Enter"
            && KeyNames.TryParse("shift", out var shiftCode) && InputDevices.CodesFor(shiftCode).SequenceEqual(new[] { InputDevices.LeftShift, InputDevices.RightShift })
            && KeyNames.TryParse("rshift", out var rshift) && rshift == InputDevices.RightShift
            && KeyNames.TryParse("numpadenter", out var npEnter) && npEnter == InputDevices.NumpadEnter);

        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var none = new Guid("00000000-0000-0000-ffff-ffffffffffff");
        var interfaces = new List<DeviceInterface>
        {
            new(@"\\?\HID#VID_1&PID_2&MI_00#a", 1, 2, "Maker", "Keys", g1),
            new(@"\\?\HID#VID_1&PID_2&MI_01&Col01#b", 1, 2, "Maker", "Keys", g1),
            new(@"\\?\HID#VID_1&PID_2&MI_00#c", 1, 2, "Maker", "Keys", g2),
            new(@"\\?\ACPI#laptop", 9, 9, "", "", none),
            new(@"\\?\HID#other", 7, 7, "Other", "Board", null),
        };
        var groups = InputDevices.Group(interfaces);
        Check("keyboards: one per physical keyboard, every connection of it counts", groups.Count == 4 && groups[0].Paths.Count == 2 && groups[0].Device.DevicePath.EndsWith("#a"));
        Check("keyboards: identical keyboards stay separate and are numbered", groups[1].Device.DevicePath.EndsWith("#c") && groups[1].Device.GetDisplayLabel().EndsWith("#2")
            && !groups[0].Device.GetDisplayLabel().Contains('#'));
        Check("keyboards: no known container means a keyboard of its own", groups[2].Paths.Count == 1 && groups[3].Paths.Count == 1 && groups[2].Device.ProductName == "Unknown");
        Check("keyboards: the main path skips sub-collections", InputDevices.Group(new[] { interfaces[1], interfaces[0] })[0].Device.DevicePath.EndsWith("#a"));
        var again = InputDevices.Group(interfaces, groups.Select(g => g.Device).ToList());
        Check("keyboards: reading them again keeps the same models", again.Select(g => g.Device).SequenceEqual(groups.Select(g => g.Device)));

        // mice and controllers
        var receiver = InputDevices.Group(new List<DeviceInterface>
        {
            new(@"\?\HID#VID_5&PID_6&MI_00#k", 5, 6, "Maker", "Combo", g1),
            new(@"\?\HID#VID_5&PID_6&MI_01#m", 5, 6, "Maker", "Combo", g1, StreamAssist.Components.Input.Model.DeviceKind.Mouse),
        });
        Check("input: a receiver's keyboard and mouse stay two devices, each its own kind", receiver.Count == 2
            && receiver[0].Device.Kind == StreamAssist.Components.Input.Model.DeviceKind.Keyboard && receiver[1].Device.Kind == StreamAssist.Components.Input.Model.DeviceKind.Mouse
            && !receiver[1].Device.GetDisplayLabel().Contains('#'));

        var clicks = InputDevices.MouseCodes(Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.LeftButtonDown | Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.Button5Up, 0);
        Check("input: mouse buttons become their codes, pressed and let go",
            clicks.SequenceEqual([(InputDevices.MouseLeft, true), (InputDevices.MouseForward, false)]));
        Check("input: a wheel step is a press and a release, by its direction",
            InputDevices.MouseCodes(Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.MouseWheel, -120).SequenceEqual([(InputDevices.WheelDown, true), (InputDevices.WheelDown, false)])
            && InputDevices.MouseCodes(Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.MouseHorizontalWheel, 120).SequenceEqual([(InputDevices.WheelRight, true), (InputDevices.WheelRight, false)]));

        var pad = InputDevices.ControllerCodes(0x1000 | 0x0001, 200, 10, -20000, 0, 0, 20000);
        Check("input: a controller's buttons, triggers and sticks past their thresholds are held",
            pad.SetEquals([InputDevices.PadA, InputDevices.PadUp, InputDevices.PadLT, InputDevices.LeftStickLeft, InputDevices.RightStickUp]));

        var mouseCodes = DevicePictures.InitMouseDisplayData(25, 3).Item1.Select(k => k.VK).ToHashSet();
        var padCodes = DevicePictures.InitControllerDisplayData(25, 3).Item1.Select(k => k.VK).ToHashSet();
        Check("input: the mouse picture has every button and wheel step, the controller picture every button",
            InputDevices.MouseButtons.All(b => mouseCodes.Contains(b.Code)) && new[] { InputDevices.WheelUp, InputDevices.WheelDown, InputDevices.WheelLeft, InputDevices.WheelRight }.All(mouseCodes.Contains)
            && InputDevices.ControllerButtons.All(b => padCodes.Contains(b.Code)) && padCodes.Count == 24);

        var keyboardDevice = new StreamAssist.Components.Input.Model.InputDeviceModel("kb", "Keys", "Maker", 1, 1);
        var mouseDevice = new StreamAssist.Components.Input.Model.InputDeviceModel("ms", "Mouse", "Maker", 1, 1, StreamAssist.Components.Input.Model.DeviceKind.Mouse);
        var mouseBox = new InputSlotModel([keyboardDevice, mouseDevice], 99, InputSlotModel.MouseLayout);
        Check("input: a mouse box takes a mouse, not a keyboard", !mouseBox.TrySetSelectedDevice(keyboardDevice) && mouseBox.TrySetSelectedDevice(mouseDevice)
            && mouseBox.SelectedDevice == mouseDevice && mouseBox.DeviceKind == StreamAssist.Components.Input.Model.DeviceKind.Mouse);
        // microphones
        var sine = new float[480];
        for (var i = 0; i < sine.Length; i++) sine[i] = (float)(0.5 * Math.Sin(i * 2 * Math.PI / 48));
        var sineBytes = new byte[sine.Length * 4];
        Buffer.BlockCopy(sine, 0, sineBytes, 0, sineBytes.Length);
        var pcm = new byte[960];
        for (var i = 0; i < 480; i++) BitConverter.GetBytes((short)(sine[i] * 32767)).CopyTo(pcm, i * 2);
        var floatLevel = StreamAssist.Components.Input.Background.MicrophoneManager.LevelDb(sineBytes, sineBytes.Length, NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
        var pcmLevel = StreamAssist.Components.Input.Background.MicrophoneManager.LevelDb(pcm, pcm.Length, new NAudio.Wave.WaveFormat(48000, 16, 1));
        Check("mic: the level is how loud it is in dB (a half-volume sine about -9), float or 16 bit, silence -100",
            Math.Abs(floatLevel + 9.03) < 0.1 && Math.Abs(pcmLevel + 9.03) < 0.1
            && StreamAssist.Components.Input.Background.MicrophoneManager.LevelDb(new byte[64], 64, NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)) == -100);
        static (bool, bool) Decide(bool was, double level, double ms) => StreamAssist.Components.Input.Background.MicrophoneManager.Decide(was, level, -35, TimeSpan.FromMilliseconds(ms), 300);
        Check("mic: talking starts at the threshold, keeps on through a pause shorter than the hold, then stops",
            Decide(false, -30, 0) == (true, true) && Decide(false, -50, 0) == (false, false)
            && Decide(true, -50, 200) == (true, false) && Decide(true, -50, 400) == (false, false));
        var micDevice = new StreamAssist.Components.Input.Model.InputDeviceModel("{mic}", "Voicemeeter Out B1", "Audio", 0, 0, StreamAssist.Components.Input.Model.DeviceKind.Microphone);
        var micBox = new InputSlotModel([micDevice, keyboardDevice], 97, InputSlotModel.MicrophoneLayout);
        Check("mic: a microphone box takes a microphone, its threshold and hold are checked",
            micBox.TrySetSelectedDevice(micDevice) && !micBox.TrySetSelectedDevice(keyboardDevice) && micDevice.GetDisplayLabel() == "Voicemeeter Out B1"
            && micBox.TrySetMicThreshold(-42) && !micBox.TrySetMicThreshold(5) && !micBox.TrySetMicHold(-1) && micBox.TrySetMicHold(500)
            && micBox.MicThreshold == -42 && micBox.MicHold == 500);
        var micJson = JsonSerializer.Serialize(micBox);
        Check("mic: only a microphone box saves a threshold and hold", micJson.Contains("\"micThreshold\":-42") && micJson.Contains("\"micHold\":500")
            && !JsonSerializer.Serialize(new InputSlotModel([], 96)).Contains("micThreshold"));

        var genericBox = new InputSlotModel([keyboardDevice, mouseDevice], 98, InputSlotModel.AllKeysLayout);
        var menus = new DeviceMenus([mouseDevice, keyboardDevice]);
        Check("input: a Generic box takes any device, its dropdown lists all of them (keyboards first)",
            genericBox.DeviceKind == null && genericBox.TrySetSelectedDevice(mouseDevice) && genericBox.TrySetSelectedDevice(keyboardDevice)
            && !genericBox.TrySetSelectedDevice(new StreamAssist.Components.Input.Model.InputDeviceModel("m", "Mic", "Audio", 0, 0, StreamAssist.Components.Input.Model.DeviceKind.Microphone))
            && menus.For(null).Select(i => i.Model).SequenceEqual([null, keyboardDevice, mouseDevice])
            && menus.For(StreamAssist.Components.Input.Model.DeviceKind.Mouse).Select(i => i.Model).SequenceEqual([null, mouseDevice]));
    }
}
