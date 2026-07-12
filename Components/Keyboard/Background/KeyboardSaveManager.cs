using System;
using StreamAssist.Components.AppAction.Model;
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
        _listModel = _model.ListModel;

        SubscribeToList(_listModel);
    }



    // subscription helpers



    private void SubscribeToList(KeyboardListModel list)
    {
        list.SlotAdded += OnSlotAdded;
        list.SlotRemoved += OnSlotRemoved;
        list.ConfigCreated += OnConfigCreated;
        list.ConfigDeleted += OnConfigDeleted;

        foreach (var slot in list.Slots)
            SubscribeToSlot(slot);

        foreach (var config in list.Configs)
            SubscribeToConfig(config);
    }



    private void UnsubscribeFromList(KeyboardListModel list)
    {
        list.SlotAdded -= OnSlotAdded;
        list.SlotRemoved -= OnSlotRemoved;
        list.ConfigCreated -= OnConfigCreated;
        list.ConfigDeleted -= OnConfigDeleted;

        foreach (var slot in list.Slots)
            UnsubscribeFromSlot(slot);

        foreach (var config in list.Configs)
            UnsubscribeFromConfig(config);
    }



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



    private void SubscribeToConfig(KeyboardConfigModel config)
    {
        config.KeyBound += OnKeyBound;
        config.KeyUnbound += OnKeyUnbound;

        foreach (var bindingModel in config.Keybinds.Values)
            SubscribeToBindingModel(bindingModel);
    }



    private void UnsubscribeFromConfig(KeyboardConfigModel config)
    {
        config.KeyBound -= OnKeyBound;
        config.KeyUnbound -= OnKeyUnbound;

        foreach (var bindingModel in config.Keybinds.Values)
            UnsubscribeFromBindingModel(bindingModel);
    }



    private void SubscribeToBindingModel(KeyboardBindingModel bindingModel)
    {
        bindingModel.OnPressed.ActionAdded += OnActionAdded;
        bindingModel.OnPressed.ActionRemoved += OnActionRemoved;
        bindingModel.OnReleased.ActionAdded += OnActionAdded;
        bindingModel.OnReleased.ActionRemoved += OnActionRemoved;
    }



    private void UnsubscribeFromBindingModel(KeyboardBindingModel bindingModel)
    {
        bindingModel.OnPressed.ActionAdded -= OnActionAdded;
        bindingModel.OnPressed.ActionRemoved -= OnActionRemoved;
        bindingModel.OnReleased.ActionAdded -= OnActionAdded;
        bindingModel.OnReleased.ActionRemoved -= OnActionRemoved;
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



    private void OnConfigCreated(KeyboardConfigModel config)
    {
        SubscribeToConfig(config);
        Save();
    }



    private void OnConfigDeleted(KeyboardConfigModel config)
    {
        UnsubscribeFromConfig(config);
        Save();
    }



    private void OnKeyBound(ushort _, KeyboardBindingModel bindingModel)
    {
        SubscribeToBindingModel(bindingModel);
        Save();
    }



    private void OnKeyUnbound(ushort _, KeyboardBindingModel bindingModel)
    {
        UnsubscribeFromBindingModel(bindingModel);
        Save();
    }



    private void OnSelectedDeviceChanged(KeyboardDeviceModel? _, KeyboardDeviceModel? __) => Save();



    private void OnSelectedConfigChanged(KeyboardConfigModel? _, KeyboardConfigModel? __) => Save();



    private void OnActionAdded(IAppAction _) => Save();



    private void OnActionRemoved(IAppAction _) => Save();



    // persistence



    public void Save() => JsonFileStore.Save("keyboard", _model);



    // disposal



    public void Dispose()
    {
        UnsubscribeFromList(_listModel);
    }
}