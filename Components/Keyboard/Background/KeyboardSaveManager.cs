using System;
using StreamAssist.Components.Config;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSaveManager : IDisposable
{
    private readonly KeyboardComponentModel _model;
    private readonly KeyboardListModel _listModel;



    // constructor



    public KeyboardSaveManager(KeyboardComponentModel model)
    {
        _model = model;
        _listModel = _model.KeyboardList;

        foreach (var slot in _listModel.Slots)
            SubscribeToSlot(slot);

        _listModel.SlotAdded += OnSlotAdded;
        _listModel.SlotRemoved += OnSlotRemoved;
        _listModel.ConfigListUpdated += OnConfigListUpdated;
        _listModel.ConfigBindingChanged += OnConfigBindingChanged;
    }



    // model callbacks



    private void OnSlotAdded(KeyboardSlotModel slot)
    {
        SubscribeToSlot(slot);
        Save();
    }



    private void OnSlotRemoved(KeyboardSlotModel slot)
    {
        UnsubscribeFromSlot(slot);
        Save();
    }



    private void OnConfigBindingChanged(string _) => Save();



    private void OnConfigListUpdated() => Save();



    private void OnSelectedDeviceChanged(KeyboardDeviceModel? _) => Save();



    private void OnSelectedConfigChanged(KeyboardConfigModel? _) => Save();



    // subscription helpers



    private void SubscribeToSlot(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
        slot.SelectedConfigChanged += OnSelectedConfigChanged;
    }

    private void UnsubscribeFromSlot(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.SelectedConfigChanged -= OnSelectedConfigChanged;
    }



    // persistence



    public void Save() => JsonFileStore.Save("keyboard", _model);



    // disposal



    public void Dispose()
    {
        _listModel.SlotAdded -= OnSlotAdded;
        _listModel.SlotRemoved -= OnSlotRemoved;
        _listModel.ConfigListUpdated -= OnConfigListUpdated;
        _listModel.ConfigBindingChanged -= OnConfigBindingChanged;

        foreach (var slot in _listModel.Slots)
            UnsubscribeFromSlot(slot);
    }
}