using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Linearstar.Windows.RawInput;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



// one connection Windows lists for a keyboard (a keyboard can have several, e.g. one for normal keys and one for media
// keys or N-key rollover, each with its own device path)
public sealed record KeyboardInterface(string Path, int VendorId, int ProductId, string Manufacturer, string Product, Guid? ContainerId);



// one physical keyboard: its model (what slots pick and save, by its main path) and every path its keys can come from
public sealed record KeyboardGroup(InputDeviceModel Device, IReadOnlyList<string> Paths);



// finding keyboards and telling them and their keys apart. Windows' raw input gives each key press the device path of
// the connection it came through; a physical keyboard is every connection with the same container id (Windows' id for
// one physical device), so identical keyboards stay separate and a keyboard's keys count whichever connection they use
public static class KeyboardDevices
{
    // key codes the raw keys are turned into (Windows' own left / right codes, and one of its unused codes for numpad Enter)
    public const ushort LeftShift = 0xA0, RightShift = 0xA1, LeftCtrl = 0xA2, RightCtrl = 0xA3, LeftAlt = 0xA4, RightAlt = 0xA5;
    public const ushort NumpadEnter = 0x0E;

    // what a container id is when Windows doesn't know which physical device it belongs to (each path is its own keyboard)
    private static readonly Guid NoContainer = new("00000000-0000-0000-ffff-ffffffffffff");



    // public api



    // every keyboard connection Windows has right now
    public static List<KeyboardInterface> Enumerate() =>
        RawInputDevice.GetDevices()
            .Where(d => d?.DevicePath != null && d.DeviceType == RawInputDeviceType.Keyboard)
            .Select(d => new KeyboardInterface(d.DevicePath!, d.VendorId, d.ProductId, d.ManufacturerName ?? "", d.ProductName ?? "", ContainerIdOf(d.DevicePath!)))
            .ToList();



    // the connections grouped into physical keyboards, in the order Windows listed them. A keyboard's main path (what's
    // saved) is its first connection that isn't a sub-collection ("&Col"), the same one older versions picked, so
    // saved slots still find their keyboard. Keyboards with the same name are numbered (#2, #3) so they can be told
    // apart. existing: models to keep (by main path), so open dropdowns and slots keep the same object
    public static List<KeyboardGroup> Group(IEnumerable<KeyboardInterface> interfaces, IReadOnlyList<InputDeviceModel>? existing = null)
    {
        var groups = new List<List<KeyboardInterface>>();
        foreach (var item in interfaces)
        {
            var group = item.ContainerId is { } id && id != NoContainer && id != Guid.Empty
                ? groups.FirstOrDefault(g => g[0].ContainerId == id)
                : null;

            if (group != null) group.Add(item);
            else groups.Add([item]);
        }

        var result = new List<KeyboardGroup>();
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var main = group.FirstOrDefault(i => !i.Path.Contains("&Col", StringComparison.OrdinalIgnoreCase)) ?? group[0];
            var key = $"{main.Product}|{main.Manufacturer}|{main.VendorId}|{main.ProductId}";
            names[key] = names.TryGetValue(key, out var count) ? count + 1 : 1;

            var device = existing?.FirstOrDefault(d => string.Equals(d.DevicePath, main.Path, StringComparison.OrdinalIgnoreCase))
                ?? new InputDeviceModel(main.Path, Name(main.Product), Name(main.Manufacturer), main.ProductId, main.VendorId);
            device.SetNumber(names[key]);

            result.Add(new KeyboardGroup(device, group.Select(i => i.Path).ToList()));
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
