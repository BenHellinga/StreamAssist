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



    private void OnLayoutChanged(string _) => Save();



    // a config's keys or bindings changed, or another config was picked, added or deleted
    private void OnConfigsChanged() => Save();



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
        list.Changed += OnConfigsChanged;

        foreach (var slot in list.Slots)
            SubscribeToKeyboardSlotModel(slot);
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel list)
    {
        list.SlotCreated -= OnSlotAdded;
        list.SlotDeleted -= OnSlotRemoved;
        list.Changed -= OnConfigsChanged;

        foreach (var slot in list.Slots)
            UnsubscribeFromKeyboardSlotModel(slot);
    }



    private void SubscribeToKeyboardSlotModel(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
        slot.LayoutChanged += OnLayoutChanged;
    }



    private void UnsubscribeFromKeyboardSlotModel(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.LayoutChanged -= OnLayoutChanged;
    }



    // disposal



    public void Dispose()
    {
        UnsubscribeFromKeyboardComponentModel(_model);

        _pendingSave?.Dispose();
    }
}