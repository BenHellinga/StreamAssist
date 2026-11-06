using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.Background;



// one connection Windows lists for a keyboard or mouse (a device can have several, e.g. one for normal keys and one for
// media keys or N-key rollover, each with its own device path)
public sealed record DeviceInterface(string Path, int VendorId, int ProductId, string Manufacturer, string Product, Guid? ContainerId,
                                     DeviceKind Kind = DeviceKind.Keyboard);



// one physical device: its model (what slots pick and save, by its main path) and every path its input can come from
public sealed record DeviceGroup(InputDeviceModel Device, IReadOnlyList<string> Paths);



// finding input devices and telling them and their buttons apart. Windows' raw input gives each key press / mouse
// click the device path of the connection it came through; a physical keyboard (or mouse) is every connection of that
// kind with the same container id (Windows' id for one physical device: a receiver for a keyboard and a mouse has one
// id, so the kind keeps them apart), so identical devices stay separate and a device's input counts whichever
// connection it uses. Everything is a key code: keyboards Windows' keys, mice Windows' mouse button codes (and four of
// its unused codes for the wheel), controllers Windows' gamepad codes
public static class InputDevices
{
    // key codes the raw keys are turned into (Windows' own left / right codes, and one of its unused codes for numpad Enter)
    public const ushort LeftShift = 0xA0, RightShift = 0xA1, LeftCtrl = 0xA2, RightCtrl = 0xA3, LeftAlt = 0xA4, RightAlt = 0xA5;
    public const ushort NumpadEnter = 0x0E;

    // mice: windows' button codes, the wheel as four of its unassigned codes (a wheel step is a press and a release)
    public const ushort MouseLeft = 0x01, MouseRight = 0x02, MouseMiddle = 0x04, MouseBack = 0x05, MouseForward = 0x06;
    public const ushort WheelUp = 0x3A, WheelDown = 0x3B, WheelLeft = 0x3C, WheelRight = 0x3D;

    // controllers: windows' gamepad codes
    public const ushort PadA = 0xC3, PadB = 0xC4, PadX = 0xC5, PadY = 0xC6, PadRB = 0xC7, PadLB = 0xC8, PadLT = 0xC9, PadRT = 0xCA;
    public const ushort PadUp = 0xCB, PadDown = 0xCC, PadLeft = 0xCD, PadRight = 0xCE, PadMenu = 0xCF, PadView = 0xD0;
    public const ushort PadLS = 0xD1, PadRS = 0xD2;
    public const ushort LeftStickUp = 0xD3, LeftStickDown = 0xD4, LeftStickRight = 0xD5, LeftStickLeft = 0xD6;
    public const ushort RightStickUp = 0xD7, RightStickDown = 0xD8, RightStickRight = 0xD9, RightStickLeft = 0xDA;

    // the names the Mouse / Controller Button Pressed blocks offer (held buttons: the wheel isn't, it only steps)
    public static readonly IReadOnlyList<(string Name, ushort Code)> MouseButtons =
    [
        ("Left", MouseLeft), ("Right", MouseRight), ("Middle", MouseMiddle), ("Back", MouseBack), ("Forward", MouseForward),
    ];

    public static readonly IReadOnlyList<(string Name, ushort Code)> ControllerButtons =
    [
        ("A", PadA), ("B", PadB), ("X", PadX), ("Y", PadY), ("LB", PadLB), ("RB", PadRB), ("LT", PadLT), ("RT", PadRT),
        ("View", PadView), ("Menu", PadMenu), ("Left stick click", PadLS), ("Right stick click", PadRS),
        ("D-pad up", PadUp), ("D-pad down", PadDown), ("D-pad left", PadLeft), ("D-pad right", PadRight),
        ("Left stick up", LeftStickUp), ("Left stick down", LeftStickDown), ("Left stick left", LeftStickLeft), ("Left stick right", LeftStickRight),
        ("Right stick up", RightStickUp), ("Right stick down", RightStickDown), ("Right stick left", RightStickLeft), ("Right stick right", RightStickRight),
    ];

    // a microphone box's one button, pressed while talking (not a code any device sends)
    public const ushort Talking = 0xFF;

    // how far a trigger or stick goes before it counts as pressed (a trigger 0-255, a stick -32768 to 32767)
    public const byte TriggerPressed = 30; // XInput's own threshold
    public const short StickPressed = 16000; // about halfway

    // what a container id is when Windows doesn't know which physical device it belongs to (each path is its own keyboard)
    private static readonly Guid NoContainer = new("00000000-0000-0000-ffff-ffffffffffff");



    // public api



    // every keyboard and mouse connection Windows has right now
    public static List<DeviceInterface> Enumerate() =>
        RawInputDevice.GetDevices()
            .Where(d => d?.DevicePath != null && d.DeviceType is RawInputDeviceType.Keyboard or RawInputDeviceType.Mouse)
            .Select(d => new DeviceInterface(d.DevicePath!, d.VendorId, d.ProductId, d.ManufacturerName ?? "", d.ProductName ?? "",
                ContainerIdOf(d.DevicePath!), d.DeviceType == RawInputDeviceType.Mouse ? DeviceKind.Mouse : DeviceKind.Keyboard))
            .ToList();



    // the connections grouped into physical keyboards, in the order Windows listed them. A keyboard's main path (what's
    // saved) is its first connection that isn't a sub-collection ("&Col"), the same one older versions picked, so
    // saved slots still find their keyboard. Keyboards with the same name are numbered (#2, #3) so they can be told
    // apart. existing: models to keep (by main path), so open dropdowns and slots keep the same object
    public static List<DeviceGroup> Group(IEnumerable<DeviceInterface> interfaces, IReadOnlyList<InputDeviceModel>? existing = null)
    {
        var groups = new List<List<DeviceInterface>>();
        foreach (var item in interfaces)
        {
            var group = item.ContainerId is { } id && id != NoContainer && id != Guid.Empty
                ? groups.FirstOrDefault(g => g[0].ContainerId == id && g[0].Kind == item.Kind)
                : null;

            if (group != null) group.Add(item);
            else groups.Add([item]);
        }

        var result = new List<DeviceGroup>();
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var main = group.FirstOrDefault(i => !i.Path.Contains("&Col", StringComparison.OrdinalIgnoreCase)) ?? group[0];
            var key = $"{main.Kind}|{main.Product}|{main.Manufacturer}|{main.VendorId}|{main.ProductId}";
            names[key] = names.TryGetValue(key, out var count) ? count + 1 : 1;

            var device = existing?.FirstOrDefault(d => string.Equals(d.DevicePath, main.Path, StringComparison.OrdinalIgnoreCase))
                ?? new InputDeviceModel(main.Path, Name(main.Product), Name(main.Manufacturer), main.ProductId, main.VendorId, main.Kind);
            device.SetNumber(names[key]);

            result.Add(new DeviceGroup(device, group.Select(i => i.Path).ToList()));
        }

        return result;
    }



    // the key code a raw key press is bound by, null to ignore it. Raw input gives the same code to both Shifts, both
    // Ctrls, both Alts and both Enters; the scan code and the extended (E0) flag tell them apart. Numpad keys with Num
    // Lock off arrive as Home / arrows / ... without E0, they're turned back into the numpad keys they are
    public static ushort? KeyCode(int vk, int scanCode, bool isE0)
    {
        switch (vk)
        {
            case 0x10: // Shift (with E0 it's a fake shift Windows adds around numpad keys, not a key press)
                if (isE0) return null;
                return scanCode == 0x36 ? RightShift : LeftShift;
            case 0x11: return isE0 ? RightCtrl : LeftCtrl;
            case 0x12: return isE0 ? RightAlt : LeftAlt;
            case 0x0D: return isE0 ? NumpadEnter : (ushort)0x0D;
            case 0xFF: return null; // no key
        }

        if (!isE0)
        {
            ushort? numpad = vk switch
            {
                0x2D => 0x60, // Insert → Numpad 0
                0x23 => 0x61, // End → 1
                0x28 => 0x62, // Down → 2
                0x22 => 0x63, // Page Down → 3
                0x25 => 0x64, // Left → 4
                0x0C => 0x65, // Clear → 5
                0x27 => 0x66, // Right → 6
                0x24 => 0x67, // Home → 7
                0x26 => 0x68, // Up → 8
                0x21 => 0x69, // Page Up → 9
                0x2E => 0x6E, // Delete → Numpad .
                _ => null,
            };
            if (numpad != null) return numpad;
        }

        return (ushort)vk;
    }



    // a mouse report's buttons as key codes, in order: (code, true) pressed, (code, false) let go. A wheel step is its
    // code pressed and let go at once (up / right for a positive delta, down / left for a negative one)
    public static List<(ushort Code, bool IsDown)> MouseCodes(RawMouseButtonFlags buttons, int wheelDelta)
    {
        var codes = new List<(ushort, bool)>();
        void Button(RawMouseButtonFlags down, RawMouseButtonFlags up, ushort code)
        {
            if (buttons.HasFlag(down)) codes.Add((code, true));
            if (buttons.HasFlag(up)) codes.Add((code, false));
        }

        Button(RawMouseButtonFlags.LeftButtonDown, RawMouseButtonFlags.LeftButtonUp, MouseLeft);
        Button(RawMouseButtonFlags.RightButtonDown, RawMouseButtonFlags.RightButtonUp, MouseRight);
        Button(RawMouseButtonFlags.MiddleButtonDown, RawMouseButtonFlags.MiddleButtonUp, MouseMiddle);
        Button(RawMouseButtonFlags.Button4Down, RawMouseButtonFlags.Button4Up, MouseBack);
        Button(RawMouseButtonFlags.Button5Down, RawMouseButtonFlags.Button5Up, MouseForward);

        if (wheelDelta != 0 && buttons.HasFlag(RawMouseButtonFlags.MouseWheel))
        {
            var step = wheelDelta > 0 ? WheelUp : WheelDown;
            codes.Add((step, true));
            codes.Add((step, false));
        }

        if (wheelDelta != 0 && buttons.HasFlag(RawMouseButtonFlags.MouseHorizontalWheel))
        {
            var step = wheelDelta > 0 ? WheelRight : WheelLeft;
            codes.Add((step, true));
            codes.Add((step, false));
        }

        return codes;
    }



    // the codes a controller is holding right now: its buttons, triggers past TriggerPressed, sticks past StickPressed
    // (each direction its own button)
    public static HashSet<ushort> ControllerCodes(ushort buttons, byte leftTrigger, byte rightTrigger, short leftX, short leftY, short rightX, short rightY)
    {
        var held = new HashSet<ushort>();
        void Bit(ushort mask, ushort code)
        {
            if ((buttons & mask) != 0) held.Add(code);
        }

        Bit(0x0001, PadUp);
        Bit(0x0002, PadDown);
        Bit(0x0004, PadLeft);
        Bit(0x0008, PadRight);
        Bit(0x0010, PadMenu);
        Bit(0x0020, PadView);
        Bit(0x0040, PadLS);
        Bit(0x0080, PadRS);
        Bit(0x0100, PadLB);
        Bit(0x0200, PadRB);
        Bit(0x1000, PadA);
        Bit(0x2000, PadB);
        Bit(0x4000, PadX);
        Bit(0x8000, PadY);

        if (leftTrigger > TriggerPressed) held.Add(PadLT);
        if (rightTrigger > TriggerPressed) held.Add(PadRT);

        if (leftY > StickPressed) held.Add(LeftStickUp);
        if (leftY < -StickPressed) held.Add(LeftStickDown);
        if (leftX > StickPressed) held.Add(LeftStickRight);
        if (leftX < -StickPressed) held.Add(LeftStickLeft);
        if (rightY > StickPressed) held.Add(RightStickUp);
        if (rightY < -StickPressed) held.Add(RightStickDown);
        if (rightX > StickPressed) held.Add(RightStickRight);
        if (rightX < -StickPressed) held.Add(RightStickLeft);

        return held;
    }



    // a code that stands for either side (Shift, Ctrl, Alt from a typed name) and the codes it covers
    public static IReadOnlyList<ushort> CodesFor(ushort vk) => vk switch
    {
        0x10 => [LeftShift, RightShift],
        0x11 => [LeftCtrl, RightCtrl],
        0x12 => [LeftAlt, RightAlt],
        _ => [vk],
    };



    // helpers



    private static string Name(string text) => string.IsNullOrWhiteSpace(text) ? "Unknown" : text;



    // the physical device a connection belongs to (its container id), null if Windows can't say
    private static Guid? ContainerIdOf(string path)
    {
        try
        {
            // raw input sometimes writes the path as \??\..., the device functions want \\?\...
            var interfacePath = path.StartsWith(@"\??\") ? @"\\?\" + path[4..] : path;

            var instanceKey = InstanceIdKey;
            if (GetInterfaceProperty(interfacePath, ref instanceKey) is not { } instanceBytes) return null;
            var instanceId = System.Text.Encoding.Unicode.GetString(instanceBytes).TrimEnd('\0');

            if (CM_Locate_DevNodeW(out var node, instanceId, 0) != 0) return null;

            var containerKey = ContainerIdKey;
            uint size = 16;
            var buffer = new byte[16];
            return CM_Get_DevNode_PropertyW(node, ref containerKey, out _, buffer, ref size, 0) == 0 ? new Guid(buffer) : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }



    private static byte[]? GetInterfaceProperty(string path, ref DevPropKey key)
    {
        uint size = 0;
        CM_Get_Device_Interface_PropertyW(path, ref key, out _, null, ref size, 0);
        if (size == 0) return null;

        var buffer = new byte[size];
        return CM_Get_Device_Interface_PropertyW(path, ref key, out _, buffer, ref size, 0) == 0 ? buffer : null;
    }



    // windows' device property functions (cfgmgr32)



    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey(Guid category, uint id)
    {
        public Guid Category = category;
        public uint Id = id;
    }

    private static DevPropKey InstanceIdKey => new(new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), 256); // DEVPKEY_Device_InstanceId
    private static DevPropKey ContainerIdKey => new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2); // DEVPKEY_Device_ContainerId

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_PropertyW(string deviceInterface, ref DevPropKey key, out uint type, byte[]? buffer, ref uint size, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_PropertyW(uint devInst, ref DevPropKey key, out uint type, byte[] buffer, ref uint size, uint flags);
}
