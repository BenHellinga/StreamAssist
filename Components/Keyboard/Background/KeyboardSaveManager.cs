using System;
using System.Collections.Generic;
using Avalonia.Threading;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSaveManager : IDisposable
{
    // private



    private readonly KeyboardComponentModel _model;
    private IDisposable? _pendingSave;



    // constructor



    public KeyboardSaveManager(KeyboardComponentModel model)
    {
        _model = model;
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



    private void OnKeyBound(ushort _, BindableModel bindable)
    {
        SubscribeToBindableModel(bindable);
        Save();
    }



    private void OnKeyUnbound(ushort _, BindableModel bindable)
    {
        UnsubscribeFromBindableModel(bindable);
        Save();
    }



    private void OnBindableChanged() => Save();



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
            SubscribeToBindableModel(bindingModel);
    }



    private void UnsubscribeFromKeybindConfigModel(KeybindConfigModel config)
    {
        config.KeyBound -= OnKeyBound;
        config.KeyUnbound -= OnKeyUnbound;

        foreach (var bindingModel in config.Keybinds.Values)
            UnsubscribeFromBindableModel(bindingModel);
    }



    // a bindable raises Changed for anything inside it, so one subscription covers the whole block tree
    private void SubscribeToBindableModel(BindableModel bindable) => bindable.Changed += OnBindableChanged;



    private void UnsubscribeFromBindableModel(BindableModel bindable) => bindable.Changed -= OnBindableChanged;



    // disposal



    public void Dispose()
    {
        UnsubscribeFromKeyboardComponentModel(_model);

        _pendingSave?.Dispose();
    }
}