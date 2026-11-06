using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.UI;



// the boxes' device dropdowns, shared by every box: one list per kind of device, and one with every device (keyboards,
// then mice, then controllers; not microphones, a Generic grid has no talking) for a Generic box. Each starts with None. Kept up to date in place, so a box's choice
// isn't lost while a list changes
public sealed class DeviceMenus
{
    // private



    private readonly Dictionary<DeviceKind, ObservableCollection<DeviceMenuItem>> _byKind;
    private readonly ObservableCollection<DeviceMenuItem> _all;



    // constructor



    public DeviceMenus(IReadOnlyList<InputDeviceModel> devices)
    {
        _byKind = Enum.GetValues<DeviceKind>().ToDictionary(kind => kind, _ => new ObservableCollection<DeviceMenuItem>());
        _all = [];

        foreach (var menu in _byKind.Values.Append(_all)) menu.Add(new(InputListModel.NoneDevice, null));
        Update(devices);
    }



    // public api



    // the list a box shows: its kind's, or every device (null: a Generic box takes any)
    public ObservableCollection<DeviceMenuItem> For(DeviceKind? kind) => kind is { } k ? _byKind[k] : _all;



    // a device was plugged in or removed (or renamed: its number among identical ones changed)
    public void Update(IReadOnlyList<InputDeviceModel> devices)
    {
        foreach (var (kind, menu) in _byKind) Sync(menu, devices.Where(d => d.Kind == kind).ToList());
        Sync(_all, devices.Where(d => d.Kind != DeviceKind.Microphone).OrderBy(d => d.Kind).ToList());
    }



    // helpers



    private static void Sync(ObservableCollection<DeviceMenuItem> menu, List<InputDeviceModel> devices)
    {
        foreach (var item in menu.Where(i => i.Model != null && (!devices.Contains(i.Model) || i.Label != i.Model.GetDisplayLabel())).ToList())
            menu.Remove(item);

        foreach (var device in devices.Where(d => menu.All(i => i.Model != d)))
            menu.Add(new(device.GetDisplayLabel(), device));
    }
}
