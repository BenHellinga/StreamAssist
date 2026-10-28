using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
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



    private readonly BlockModel _block;
    private readonly Action? _removeFromSource;
    private readonly ObservableCollection<ConfigurableParameterViewModel> _configurators;
    private readonly ObservableCollection<ViewModelBase> _slots;
    private IDisposable? _triggerTimer;

    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isTriggered;



    // public



    public string Name => _block.Name;
    public string Description => _block.Description;
    public BlockModel Block => _block;
    public ObservableCollection<ConfigurableParameterViewModel> Configurators => _configurators;
    public ObservableCollection<ViewModelBase> Slots => _slots; // BlockSlotViewModel or BlockSlotListViewModel



    // constructor



    public BlockViewModel(BlockModel block, Action? removeFromSource)
    {
        _block = block;
        _removeFromSource = removeFromSource;
        _configurators = ConfiguratorListBuilder.Build(block);
        _slots = [];
        _triggerTimer = null;

        _isDisabled = _block.IsDisabled;
        _isTriggered = false;

        foreach (var slot in _block.Slots)
        {
            ViewModelBase? slotViewModel = slot switch
            {
                BlockSlot single => new BlockSlotViewModel(single),
                BlockSlotList list => new BlockSlotListViewModel(list),
                BlockBranchList branches => new BlockBranchListViewModel(branches),
                _ => null
            };

            if (slotViewModel != null) _slots.Add(slotViewModel);
        }

        SubscribeToBlockModel(_block);
    }



    // ui events



    [RelayCommand] private void OnToggleButtonPressed() => _block.SetIsDisabled(!_block.IsDisabled);



    [RelayCommand] private void OnRemoveButtonPressed() => _removeFromSource?.Invoke();



    // drag drop



    public object? GetDragPayload() => new BlockDragPayload(_block, _removeFromSource);



    // model events



    private void OnBlockIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    private void OnTriggered() => Dispatcher.UIThread.Post(FlashTriggered);



    // public api



    public bool RepresentsModel(BlockModel block) => block == _block;



    // helpers



    private void SubscribeToBlockModel(BlockModel block)
    {
        block.IsDisabledChanged += OnBlockIsDisabledChanged;
        block.Triggered += OnTriggered;
    }



    private void UnsubscribeFromBlockModel(BlockModel block)
    {
        block.IsDisabledChanged -= OnBlockIsDisabledChanged;
        block.Triggered -= OnTriggered;
    }



    private void FlashTriggered()
    {
        IsTriggered = true;

        _triggerTimer?.Dispose();
        _triggerTimer = DispatcherTimer.RunOnce(() =>
        {
            _triggerTimer = null;
            IsTriggered = false;
        }, TimeSpan.FromMilliseconds(50));
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromBlockModel(_block);
        _triggerTimer?.Dispose();

        foreach (var slot in _slots)
            slot.Dispose();
    }
}
