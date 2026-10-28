using System;
using System.Collections.Generic;
using Avalonia.Threading;
using StreamAssist.Components.AppAction.Background;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSaveManager : IDisposable
{
    // private



    private readonly KeyboardComponentModel _model;
    private readonly Dictionary<ActionGroupModel, ActionGroupChangeManager> _changeManagers;
    private IDisposable? _pendingSave;



    // constructor



    public KeyboardSaveManager(KeyboardComponentModel model)
    {
        _model = model;
        _changeManagers = [];
        _pendingSave = null;

        SubscribeToKeyboardComponentModel(_model);
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



    private void OnSelectedDeviceChanged(InputDeviceModel? _, InputDeviceModel? __) => Save();



    private void OnSelectedConfigChanged(KeybindConfigModel? _, KeybindConfigModel? __) => Save();



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



    private void OnTriggerBound(KeybindModel.TriggerType _, ActionGroupModel group)
    {
        SubscribeToActionGroupModel(group);
        Save();
    }



    private void OnTriggerUnbound(KeybindModel.TriggerType _, ActionGroupModel group)
    {
        UnsubscribeFromActionGroupModel(group);
        Save();
    }



    private void OnGroupChanged() => Save();



    // public api



    public void Save()
    {
        if (_pendingSave != null) return;

        _pendingSave = DispatcherTimer.RunOnce(() =>
        {
            _pendingSave = null;
            JsonFileStore.Save("keyboard", _model);
        }, TimeSpan.FromSeconds(1));
    }



    // helpers



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
        list.SlotCreated += OnSlotAdded;
        list.SlotDeleted += OnSlotRemoved;
        list.ConfigCreated += OnConfigAdded;
        list.ConfigDeleted += OnConfigRemoved;

        foreach (var slot in list.Slots)
            SubscribeToKeyboardSlotModel(slot);

        foreach (var config in list.Configs)
            SubscribeToKeybindConfigModel(config);
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel list)
    {
        list.SlotCreated -= OnSlotAdded;
        list.SlotDeleted -= OnSlotRemoved;
        list.ConfigCreated -= OnConfigAdded;
        list.ConfigDeleted -= OnConfigRemoved;

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
        keybind.TriggerBound += OnTriggerBound;
        keybind.TriggerUnbound += OnTriggerUnbound;

        if (keybind.OnPressed != null) SubscribeToActionGroupModel(keybind.OnPressed);
        if (keybind.OnReleased != null) SubscribeToActionGroupModel(keybind.OnReleased);
        if (keybind.OnRepeated != null) SubscribeToActionGroupModel(keybind.OnRepeated);
    }



    private void UnsubscribeFromKeybindModel(KeybindModel keybind)
    {
        keybind.TriggerBound -= OnTriggerBound;
        keybind.TriggerUnbound -= OnTriggerUnbound;

        if (keybind.OnPressed != null) UnsubscribeFromActionGroupModel(keybind.OnPressed);
        if (keybind.OnReleased != null) UnsubscribeFromActionGroupModel(keybind.OnReleased);
        if (keybind.OnRepeated != null) UnsubscribeFromActionGroupModel(keybind.OnRepeated);
    }



    private void SubscribeToActionGroupModel(ActionGroupModel group)
    {
        var changeManager = new ActionGroupChangeManager(group);
        _changeManagers.Add(group, changeManager);
        changeManager.GroupChanged += OnGroupChanged;
    }



    private void UnsubscribeFromActionGroupModel(ActionGroupModel group)
    {
        _changeManagers.Remove(group, out var changeManager);
        changeManager!.GroupChanged -= OnGroupChanged;
        changeManager.Dispose();
    }



    // disposal



    public void Dispose()
    {
        UnsubscribeFromKeyboardComponentModel(_model);

        _pendingSave?.Dispose();
    }
}