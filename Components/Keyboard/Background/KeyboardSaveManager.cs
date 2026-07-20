using System;
using System.Collections.Generic;
using StreamAssist.Components.AppAction.Background;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Config;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSaveManager : IDisposable
{
    // private



    private readonly KeyboardComponentModel _model;
    private readonly Dictionary<ActionGroupModel, ActionGroupChangeManager> _changeManagers;



    // constructor



    public KeyboardSaveManager(KeyboardComponentModel model)
    {
        _model = model;
        _changeManagers = [];

        SubscribeToKeyboardComponentModel(_model);
    }



    // subscription helpers



    private void SubscribeToKeyboardComponentModel(KeyboardComponentModel component)
    {
        SubscribeToKeyboardListModel(component.ListModel);
    }



    private void UnsubscribeFromKeyboardComponentModel(KeyboardComponentModel component)
    {
        UnsubscribeFromKeyboardListModel(component.ListModel);
    }



    private void SubscribeToKeyboardListModel(KeyboardListModel list)
    {
        list.SlotAdded += OnSlotAdded;
        list.SlotRemoved += OnSlotRemoved;
        list.ConfigAdded += OnConfigAdded;
        list.ConfigRemoved += OnConfigRemoved;

        foreach (var slot in list.Slots)
            SubscribeToKeyboardSlotModel(slot);

        foreach (var config in list.Configs)
            SubscribeToKeybindConfigModel(config);
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel list)
    {
        list.SlotAdded -= OnSlotAdded;
        list.SlotRemoved -= OnSlotRemoved;
        list.ConfigAdded -= OnConfigAdded;
        list.ConfigRemoved -= OnConfigRemoved;

        foreach (var slot in list.Slots)
            UnsubscribeFromKeyboardSlotModel(slot);

        foreach (var config in list.Configs)
            UnsubscribeFromKeybindConfigModel(config);
    }



    private void SubscribeToKeyboardSlotModel(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
        slot.SelectedConfigChanged += OnSelectedConfigChanged;
    }



    private void UnsubscribeFromKeyboardSlotModel(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.SelectedConfigChanged -= OnSelectedConfigChanged;
    }



    private void SubscribeToKeybindConfigModel(KeybindConfigModel config)
    {
        config.KeyBound += OnKeyBound;
        config.KeyUnbound += OnKeyUnbound;

        foreach (var bindingModel in config.Keybinds.Values)
            SubscribeToKeybindModel(bindingModel);
    }



    private void UnsubscribeFromKeybindConfigModel(KeybindConfigModel config)
    {
        config.KeyBound -= OnKeyBound;
        config.KeyUnbound -= OnKeyUnbound;

        foreach (var bindingModel in config.Keybinds.Values)
            UnsubscribeFromKeybindModel(bindingModel);
    }



    private void SubscribeToKeybindModel(KeybindModel keybind)
    {
        var groups = new List<ActionGroupModel>() { keybind.OnPressed, keybind.OnReleased, keybind.OnRepeated };

        foreach (var group in groups)
        {
            var changeManager = new ActionGroupChangeManager(group);
            _changeManagers.Add(group, changeManager);
            SubscribeToActionGroupChangeManager(changeManager);
        }
    }



    private void UnsubscribeFromKeybindModel(KeybindModel keybind)
    {
        var groups = new List<ActionGroupModel>() { keybind.OnPressed, keybind.OnReleased, keybind.OnRepeated };

        foreach (var group in groups)
        {
            var changeManager = _changeManagers[group];
            UnsubscribeFromActionGroupChangeManager(changeManager);
            changeManager.Dispose();
        }
    }



    private void SubscribeToActionGroupChangeManager(ActionGroupChangeManager group)
    {
        group.GroupChanged += OnGroupChanged;
    }



    private void UnsubscribeFromActionGroupChangeManager(ActionGroupChangeManager group)
    {
        group.GroupChanged -= OnGroupChanged;
    }



    // model callbacks



    private void OnSlotAdded(KeyboardSlotModel slot)
    {
        SubscribeToKeyboardSlotModel(slot);
        Save();
    }



    private void OnSlotRemoved(KeyboardSlotModel slot)
    {
        UnsubscribeFromKeyboardSlotModel(slot);
        Save();
    }



    private void OnConfigAdded(KeybindConfigModel config)
    {
        SubscribeToKeybindConfigModel(config);
        Save();
    }



    private void OnConfigRemoved(KeybindConfigModel config)
    {
        UnsubscribeFromKeybindConfigModel(config);
        Save();
    }



    private void OnKeyBound(ushort _, KeybindModel bindingModel)
    {
        SubscribeToKeybindModel(bindingModel);
        Save();
    }



    private void OnKeyUnbound(ushort _, KeybindModel bindingModel)
    {
        UnsubscribeFromKeybindModel(bindingModel);
        Save();
    }



    private void OnSelectedDeviceChanged(KeyboardDeviceModel? _, KeyboardDeviceModel? __) => Save();



    private void OnSelectedConfigChanged(KeybindConfigModel? _, KeybindConfigModel? __) => Save();



    private void OnGroupChanged() => Save();



    // persistence



    public void Save() => JsonFileStore.Save("keyboard", _model);



    // disposal



    public void Dispose()
    {
        UnsubscribeFromKeyboardComponentModel(_model);
    }
}