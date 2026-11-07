using System;
using System.Collections.Generic;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.Background;



public class InputSaveManager : IDisposable
{
    public const string FileName = "input"; // Config/input.json

    // private



    private readonly InputComponentModel _model;
    private readonly DelayedSave _save; // a burst of changes (a slider dragged, keys bound) is saved once



    // constructor



    public InputSaveManager(InputComponentModel model)
    {
        _model = model;
        _save = new(() => JsonFileStore.Save(FileName, _model), TimeSpan.FromSeconds(1));

        SubscribeToComponentModel(_model);
    }



    // model callbacks



    private void OnSlotMoved(InputSlotModel _, int __, int ___) => Save();



    private void OnSlotSettingChanged(bool _) => Save();
    private void OnMicSettingsChanged() => Save();



    private void OnSlotAdded(InputSlotModel slot, int _)
    {
        SubscribeToSlotModel(slot);
        Save();
    }



    private void OnSlotRemoved(InputSlotModel slot)
    {
        UnsubscribeFromSlotModel(slot);
        Save();
    }



    private void OnSelectedDeviceChanged(InputDeviceModel? _, InputDeviceModel? __) => Save();



    private void OnLayoutChanged(string _) => Save();



    // a config's keys or bindings changed, or another config was picked, added or deleted
    private void OnConfigsChanged() => Save();



    // public api



    public void Save() => _save.Request();



    // helpers



    private void SubscribeToComponentModel(InputComponentModel component)
    {
        SubscribeToListModel(component.ListModel);
    }



    private void UnsubscribeFromComponentModel(InputComponentModel component)
    {
        UnsubscribeFromListModel(component.ListModel);
    }



    private void SubscribeToListModel(InputListModel list)
    {
        list.SlotCreated += OnSlotAdded;
        list.SlotDeleted += OnSlotRemoved;
        list.SlotMoved += OnSlotMoved;
        list.Changed += OnConfigsChanged;

        foreach (var slot in list.Slots)
            SubscribeToSlotModel(slot);
    }



    private void UnsubscribeFromListModel(InputListModel list)
    {
        list.SlotCreated -= OnSlotAdded;
        list.SlotDeleted -= OnSlotRemoved;
        list.SlotMoved -= OnSlotMoved;
        list.Changed -= OnConfigsChanged;

        foreach (var slot in list.Slots)
            UnsubscribeFromSlotModel(slot);
    }



    private void SubscribeToSlotModel(InputSlotModel slot)
    {
        slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
        slot.LayoutChanged += OnLayoutChanged;
        slot.IsDisabledChanged += OnSlotSettingChanged;
        slot.IsCollapsedChanged += OnSlotSettingChanged;
        slot.MicSettingsChanged += OnMicSettingsChanged;
    }



    private void UnsubscribeFromSlotModel(InputSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.LayoutChanged -= OnLayoutChanged;
        slot.IsDisabledChanged -= OnSlotSettingChanged;
        slot.IsCollapsedChanged -= OnSlotSettingChanged;
        slot.MicSettingsChanged -= OnMicSettingsChanged;
    }



    // disposal



    // a save still waiting (the last second's changes) is written now, not dropped (DelayedSave)
    public void Dispose()
    {
        UnsubscribeFromComponentModel(_model);

        _save.Dispose();
    }
}