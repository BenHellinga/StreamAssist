using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardListViewModel : ViewModelBase
{
    // public static constants
    public static readonly KeyboardInfo NoneDevice = new("None", "None", "", 0, 0);
    public static readonly string NoneConfig = "None";

    // mangement objects
    private readonly InputManager _inputManager;
    private readonly KeyboardListData _settings;
    private readonly ActionPanelViewModel _actionPanel;
    private readonly SuppressionCounter _suppressCallbacks;

    // data lists
    public ObservableCollection<KeyboardInfo> AllDevices { get; }
    public ObservableCollection<string> AllConfigs { get; }
    public ObservableCollection<KeyboardSlotViewModel> Slots { get; }



    // constructor



    public KeyboardListViewModel(InputManager inputManager, KeyboardListData settings, ActionPanelViewModel actionPanel)
    {
        _inputManager = inputManager;
        _settings = settings;
        _actionPanel = actionPanel;
        _suppressCallbacks = new();

        _suppressCallbacks.Increment();

        AllDevices = [];
        AllConfigs = [];
        Slots = [];

        RefreshDevices();
        RefreshConfigs();

        if (_settings.Slots.Count == 0)
            AddSlot();
        else
            foreach (var slotData in _settings.Slots)
                AddSlot(slotData);

        _suppressCallbacks.Decrement();
    }



    // button callbacks



    [RelayCommand]
    private void OnAddKeyboard()
    {
        AddSlot();
        Save();
    }



    [RelayCommand]
    private void OnRemoveKeyboard()
    {
        RemoveSlot();
        Save();
    }



    // slot methods



    private void AddSlot(KeyboardSlotData? slotData = null)
    {
        var data = slotData ?? new KeyboardSlotData();

        if (slotData is null)
            _settings.Slots.Add(data);

        var slot = new KeyboardSlotViewModel(_inputManager, data, _settings.Configs, AllDevices, AllConfigs, _suppressCallbacks);
        Slots.Add(slot);

        slot.KeySelected += _actionPanel.ShowKey;
        slot.CreateConfigRequested += OnCreateConfigRequested;
        slot.DeleteConfigRequested += OnDeleteConfigRequested;
        slot.ConfigBindingsUpdated += OnConfigBindingsUpdated;
        slot.SaveRequested += Save;
    }



    private void RemoveSlot()
    {
        if (Slots.Count <= 1) return;

        var slot = Slots.Last();
        Slots.RemoveAt(Slots.Count - 1);
        _settings.Slots.RemoveAt(_settings.Slots.Count - 1);

        slot.KeySelected -= _actionPanel.ShowKey;
        slot.CreateConfigRequested -= OnCreateConfigRequested;
        slot.DeleteConfigRequested -= OnDeleteConfigRequested;
        slot.ConfigBindingsUpdated -= OnConfigBindingsUpdated;
        slot.SaveRequested -= Save;

        slot.Dispose();
    }



    // refresh methods



    private void RefreshDevices()
    {
        AllDevices.Clear();
        AllDevices.Add(NoneDevice);
        foreach (var d in InputManager.GetAvailableKeyboards())
            AllDevices.Add(d);
    }



    private void RefreshConfigs()
    {
        AllConfigs.Clear();
        AllConfigs.Add(NoneConfig);
        foreach (var name in _settings.Configs.Select(c => c.Name).OrderBy(n => n))
            AllConfigs.Add(name);
    }



    // refresh callbacks



    private void OnCreateConfigRequested(string name, ObservableCollection<KeyInfo> keys)
    {
        var config = new KeyboardConfigData { Name = name };

        foreach (var key in keys)
            if (key.BoundAction is not null)
                config.KeyBindings[key.VK] = ActionResolver.GetKey(key.BoundAction);

        _settings.Configs.Add(config);
        RefreshConfigs();

        foreach (var slot in Slots)
            slot.RefreshConfig();
    }



    private void OnDeleteConfigRequested(string name)
    {
        var config = _settings.Configs.First(c => c.Name == name);

        _settings.Configs.Remove(config);
        RefreshConfigs();

        foreach (var slot in Slots)
            slot.RefreshConfig();
    }



    private void OnConfigBindingsUpdated(string configName, KeyInfo key)
    {
        var config = _settings.Configs.First(c => c.Name == configName);

        if (key.BoundAction is null)
            config.KeyBindings.Remove(key.VK);
        else
            config.KeyBindings[key.VK] = ActionResolver.GetKey(key.BoundAction);

        foreach (var slot in Slots)
            if (slot.MenuConfig == configName)
                slot.RefreshBindings();
    }



    // persistence



    private void Save() => JsonFileStore.Save("keyboard", _settings);
}



public sealed class SuppressionCounter
{
    public int Count { get; private set; }
    public bool IsSuppressed => Count > 0;

    public void Increment() => ++Count;
    public void Decrement() => --Count;
}