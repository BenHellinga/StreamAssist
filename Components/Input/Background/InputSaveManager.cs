using System;
using System.Collections.Generic;
using Avalonia.Threading;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.Background;



public class InputSaveManager : IDisposable
{
    public const string FileName = "input"; // Config/input.json

    // private



    private readonly InputComponentModel _model;
    private IDisposable? _pendingSave;



    // constructor



    public InputSaveManager(InputComponentModel model)
    {
        _model = model;
        _pendingSave = null;

        SubscribeToKeyboardComponentModel(_model);
    }



    // model callbacks



    private void OnSlotMoved(InputSlotModel _, int __, int ___) => Save();



    private void OnSlotSettingChanged(bool _) => Save();
    private void OnMicSettingsChanged() => Save();



    private void OnSlotAdded(InputSlotModel slot, int _)
    {
        SubscribeToKeyboardSlotModel(slot);
        Save();
    }



    private void OnSlotRemoved(InputSlotModel slot)
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
            JsonFileStore.Save(FileName, _model);
        }, TimeSpan.FromSeconds(1));
    }



    // helpers



    private void SubscribeToKeyboardComponentModel(InputComponentModel component)
    {
        SubscribeToKeyboardListModel(component.ListModel);
    }



    private void UnsubscribeFromKeyboardComponentModel(InputComponentModel component)
    {
        UnsubscribeFromKeyboardListModel(component.ListModel);
    }



    private void SubscribeToKeyboardListModel(InputListModel list)
    {
        list.SlotCreated += OnSlotAdded;
        list.SlotDeleted += OnSlotRemoved;
        list.SlotMoved += OnSlotMoved;
        list.Changed += OnConfigsChanged;

        foreach (var slot in list.Slots)
            SubscribeToKeyboardSlotModel(slot);
    }



    private void UnsubscribeFromKeyboardListModel(InputListModel list)
    {
        list.SlotCreated -= OnSlotAdded;
        list.SlotDeleted -= OnSlotRemoved;
        list.SlotMoved -= OnSlotMoved;
        list.Changed -= OnConfigsChanged;

        foreach (var slot in list.Slots)
            UnsubscribeFromKeyboardSlotModel(slot);
    }



    private void SubscribeToKeyboardSlotModel(InputSlotModel slot)
    {
        slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
        slot.LayoutChanged += OnLayoutChanged;
        slot.IsDisabledChanged += OnSlotSettingChanged;
        slot.IsCollapsedChanged += OnSlotSettingChanged;
        slot.MicSettingsChanged += OnMicSettingsChanged;
    }



    private void UnsubscribeFromKeyboardSlotModel(InputSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.LayoutChanged -= OnLayoutChanged;
        slot.IsDisabledChanged -= OnSlotSettingChanged;
        slot.IsCollapsedChanged -= OnSlotSettingChanged;
        slot.MicSettingsChanged -= OnMicSettingsChanged;
    }



    // disposal



    public void Dispose()
    {
        UnsubscribeFromKeyboardComponentModel(_model);

        _pendingSave?.Dispose();
    }
}