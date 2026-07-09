using System;
using System.Collections.Generic;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardListManager : IDisposable
{
    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardListModel _listModel;
    private readonly Dictionary<KeyboardSlotModel, KeyboardSlotManager> _slotManagers = [];



    // constructor



    public KeyboardListManager(KeyboardInputManager inputManager, KeyboardListModel listModel)
    {
        _inputManager = inputManager;
        _listModel = listModel;

        foreach (var slot in _listModel.Slots)
            _slotManagers[slot] = new KeyboardSlotManager(_inputManager, _listModel, slot);

        _listModel.SlotAdded += OnSlotAdded;
        _listModel.SlotRemoved += OnSlotRemoved;
    }



    // slot management



    private void OnSlotAdded(KeyboardSlotModel slot) =>
        _slotManagers[slot] = new KeyboardSlotManager(_inputManager, _listModel, slot);



    private void OnSlotRemoved(KeyboardSlotModel slot)
    {
        if (!_slotManagers.Remove(slot, out var manager)) return;
        manager.Dispose();
    }



    // disposal



    public void Dispose()
    {
        _listModel.SlotAdded -= OnSlotAdded;
        _listModel.SlotRemoved -= OnSlotRemoved;

        foreach (var manager in _slotManagers.Values)
            manager.Dispose();

        _slotManagers.Clear();
    }
}