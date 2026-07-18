using System;
using System.Collections.Generic;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Config;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSaveManager : IDisposable
{
    private readonly KeyboardComponentModel _model;
    private readonly KeyboardListModel _listModel;



    // constructor



    public KeyboardSaveManager(KeyboardComponentModel model)
    {
        _model = model;
        _listModel = _model.ListModel;

        SubscribeToKeyboardListModel(_listModel);
    }



    // subscription helpers



    private void SubscribeToKeyboardListModel(KeyboardListModel list)
    {
        list.SlotAdded += OnSlotAdded;
        list.SlotRemoved += OnSlotRemoved;
        list.ConfigCreated += OnConfigCreated;
        list.ConfigDeleted += OnConfigDeleted;

        foreach (var slot in list.Slots)
            SubscribeToKeyboardSlotModel(slot);

        foreach (var config in list.Configs)
            SubscribeToKeybindConfigModel(config);
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel list)
    {
        list.SlotAdded -= OnSlotAdded;
        list.SlotRemoved -= OnSlotRemoved;
        list.ConfigCreated -= OnConfigCreated;
        list.ConfigDeleted -= OnConfigDeleted;

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

        keybind.IsEnabledChanged += OnIsEnabledChanged;

        foreach (var group in groups)
        {
            group.ActionAdded += OnActionAdded;
            group.ActionRemoved += OnActionRemoved;

            foreach (var action in group.Actions)
                SubscribeToActionModel(action);
        }
    }



    private void UnsubscribeFromKeybindModel(KeybindModel keybind)
    {
        var groups = new List<ActionGroupModel>() { keybind.OnPressed, keybind.OnReleased, keybind.OnRepeated };

        keybind.IsEnabledChanged -= OnIsEnabledChanged;

        foreach (var group in groups)
        {
            group.ActionAdded -= OnActionAdded;
            group.ActionRemoved -= OnActionRemoved;

            foreach (var action in group.Actions)
                UnsubscribeFromActionModel(action);
        }
    }



    private void SubscribeToActionModel(ActionModel action)
    {
        action.ConfigurationChanged += OnConfigurationChanged;
    }



    private void UnsubscribeFromActionModel(ActionModel action)
    {
        action.ConfigurationChanged -= OnConfigurationChanged;
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



    private void OnConfigCreated(KeybindConfigModel config)
    {
        SubscribeToKeybindConfigModel(config);
        Save();
    }



    private void OnConfigDeleted(KeybindConfigModel config)
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



    private void OnIsEnabledChanged(bool _) => Save();



    private void OnActionAdded(ActionModel action, int _)
    {
        SubscribeToActionModel(action);
        Save();
    }



    private void OnActionRemoved(ActionModel action)
    {
        UnsubscribeFromActionModel(action);
        Save();
    }



    private void OnConfigurationChanged() => Save();



    // persistence



    public void Save() => JsonFileStore.Save("keyboard", _model);



    // disposal



    public void Dispose()
    {
        UnsubscribeFromKeyboardListModel(_listModel);
    }
}