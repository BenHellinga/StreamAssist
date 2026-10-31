using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI.Configurator;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// view model for one block card in the editor, including the slots nested inside it
public partial class BlockViewModel : ViewModelBase, IEditButtons
{
    // private



    // which view model shows each kind of slot, other components add their own with RegisterSlotViewModel
    private static readonly Dictionary<Type, Func<BlockSlotBase, ViewModelBase>> SlotViewModelFactories = new()
    {
        [typeof(BlockSlot)] = slot => new BlockSlotViewModel((BlockSlot)slot),
        [typeof(BlockSlotList)] = slot => new BlockSlotListViewModel((BlockSlotList)slot),
        [typeof(BlockBranchList)] = slot => new BlockBranchListViewModel((BlockBranchList)slot),
        [typeof(ValueSlot)] = slot => new ValueSlotViewModel((ValueSlot)slot), // every ValueSlot<T> and AnyValueSlot
        [typeof(ValueSlotList)] = slot => new ValueSlotListViewModel((ValueSlotList)slot),
        [typeof(ValueSlotPairList)] = slot => new ValueSlotPairListViewModel((ValueSlotPairList)slot),
    };

    private readonly BlockModel _block;
    private readonly Action? _removeFromSource;
    private readonly ObservableCollection<ConfigurableParameterViewModel> _configurators;
    private readonly ObservableCollection<ViewModelBase> _slots;
    private readonly Flash _flash; // lights up IsTriggered when it runs

    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isCollapsed;
    [ObservableProperty] private bool _isTriggered;



    // public



    public string Name => _block.Name;
    public string Description => _block.Description;
    public BlockModel Block => _block;
    public ObservableCollection<ConfigurableParameterViewModel> Configurators => _configurators;
    public ObservableCollection<ViewModelBase> Slots => _slots; // BlockSlotViewModel or BlockSlotListViewModel

    // where the slots go in the card: under the settings, or beside them (e.g. Parse Parameter)
    public int SlotsRow => _block.SlotsBesideSettings ? 0 : 1;
    public int SlotsColumn => _block.SlotsBesideSettings ? 1 : 0;



    // constructor



    public BlockViewModel(BlockModel block, Action? removeFromSource)
    {
        _block = block;
        _removeFromSource = removeFromSource;
        _configurators = ConfiguratorListBuilder.Build(block);
        _slots = [];
        _flash = new(isOn => IsTriggered = isOn);

        _isDisabled = _block.IsDisabled;
        _isCollapsed = _block.IsCollapsed;
        _isTriggered = false;

        foreach (var slot in _block.Slots)
        {
            var createViewModel = FindSlotViewModelFactory(slot.GetType());
            if (createViewModel != null) _slots.Add(createViewModel(slot));
        }

        SubscribeToBlockModel(_block);
    }



    // lets a component show its own kind of slot (e.g. the chatbot's Match Text rows)
    public static void RegisterSlotViewModel<TSlot>(Func<TSlot, ViewModelBase> create) where TSlot : BlockSlotBase =>
        SlotViewModelFactories[typeof(TSlot)] = slot => create((TSlot)slot);



    // the slot's own type, or the nearest base type registered (so ValueSlot<int> uses ValueSlot's)
    private static Func<BlockSlotBase, ViewModelBase>? FindSlotViewModelFactory(Type? type)
    {
        for (; type != null; type = type.BaseType)
            if (SlotViewModelFactories.TryGetValue(type, out var create)) return create;

        return null;
    }



    // ui events



    [RelayCommand] private void OnToggleButtonPressed() => _block.SetIsDisabled(!_block.IsDisabled);



    [RelayCommand] private void OnRemoveButtonPressed() => _removeFromSource?.Invoke();



    public bool CanCollapse => true;
    [RelayCommand] private void OnCollapseButtonPressed() => _block.SetIsCollapsed(true);
    [RelayCommand] private void OnExpandPressed() => _block.SetIsCollapsed(false);



    // drag drop



    public object? GetDragPayload() => new BlockDragPayload(_block, _removeFromSource);



    // model events



    private void OnBlockIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    private void OnBlockIsCollapsedChanged(bool isCollapsed) => IsCollapsed = isCollapsed;



    private void OnTriggered() => _flash.Trigger();



    // public api



    public bool RepresentsModel(BlockModel block) => block == _block;



    // helpers



    private void SubscribeToBlockModel(BlockModel block)
    {
        block.IsDisabledChanged += OnBlockIsDisabledChanged;
        block.IsCollapsedChanged += OnBlockIsCollapsedChanged;
        block.Triggered += OnTriggered;
    }



    private void UnsubscribeFromBlockModel(BlockModel block)
    {
        block.IsDisabledChanged -= OnBlockIsDisabledChanged;
        block.IsCollapsedChanged -= OnBlockIsCollapsedChanged;
        block.Triggered -= OnTriggered;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromBlockModel(_block);
        _flash.Dispose();

        foreach (var slot in _slots)
            slot.Dispose();
    }
}
