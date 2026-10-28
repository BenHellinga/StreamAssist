using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



// bottom panel for the keyboard tab: the selected key's block editor, plus the block list
public partial class KeybindViewModel : ViewModelBase
{
    // private



    private KeyViewModel? _key;
    private KeybindConfigModel? _config;

    // the bindable being edited, either the key's real one or a draft that isn't saved yet
    private BindableModel? _bindable;
    private bool _isDraft;

    private readonly BlockPaletteViewModel _palette;

    [ObservableProperty] private BlockSlotViewModel? _rootSlot;
    [ObservableProperty] private bool _isKeySelected;
    [ObservableProperty] private string _label;



    // public



    public BlockPaletteViewModel Palette => _palette;



    // constructor



    public KeybindViewModel()
    {
        _key = null;
        _config = null;
        _bindable = null;
        _isDraft = false;

        _palette = new();

        _rootSlot = null;
        _isKeySelected = false;
        _label = "null";
    }



    // model events



    private void OnKeyBound(ushort vk, BindableModel bindable)
    {
        if (vk != _key?.VK) return;

        // our own draft was just committed, keep editing it
        if (bindable == _bindable) return;

        // something else was bound to the selected key (e.g. a key dragged onto it)
        ShowBindable(bindable, isDraft: false);
    }



    private void OnKeyUnbound(ushort vk, BindableModel _)
    {
        if (vk != _key?.VK) return;

        // the selected key was cleared, go back to an unsaved default
        ShowBindable(BindableModel.CreateDefault(), isDraft: true);
    }



    // the first change to a draft saves it into the config
    private void OnDraftChanged()
    {
        if (!_isDraft || _bindable == null || _key == null || _config == null) return;

        _bindable.Changed -= OnDraftChanged;
        _isDraft = false;

        _config.TryBindKey(_key.VK, _bindable);
    }



    // public api



    public void SetSelectedKey(KeyViewModel key, BindableModel? bindable, KeybindConfigModel config)
    {
        // a new key was selected
        UnsubscribeFromKeybindConfigModel(_config);

        _key?.IsSelected = false;

        _key = key;
        _config = config;

        SubscribeToKeybindConfigModel(_config);

        if (bindable == null) ShowBindable(BindableModel.CreateDefault(), isDraft: true);
        else ShowBindable(bindable, isDraft: false);

        Label = $"Key: {key.Label.Replace('\n', '-')}     Config: {_config.Name}";
        IsKeySelected = true;
    }



    public void DeselectKeys(IReadOnlyList<KeyViewModel>? keys)
    {
        if (keys != null && !keys.Contains(_key)) return;

        // the selected key was just deselected
        UnsubscribeFromKeybindConfigModel(_config);
        ClearBindable();

        IsKeySelected = false;

        _key?.IsSelected = false;
        _key = null;
        _config = null;
    }



    // helpers



    private void ShowBindable(BindableModel bindable, bool isDraft)
    {
        ClearBindable();

        _bindable = bindable;
        _isDraft = isDraft;

        if (_isDraft)
            _bindable.Changed += OnDraftChanged;

        RootSlot = new BlockSlotViewModel(_bindable.Root);
    }



    private void ClearBindable()
    {
        var oldSlot = RootSlot;
        RootSlot = null;
        oldSlot?.Dispose();

        // a draft that was never changed is thrown away, a real bindable belongs to the config
        if (_bindable != null && _isDraft)
        {
            _bindable.Changed -= OnDraftChanged;
            _bindable.Dispose();
        }

        _bindable = null;
        _isDraft = false;
    }



    private void SubscribeToKeybindConfigModel(KeybindConfigModel? config)
    {
        config?.KeyBound += OnKeyBound;
        config?.KeyUnbound += OnKeyUnbound;
    }



    private void UnsubscribeFromKeybindConfigModel(KeybindConfigModel? config)
    {
        config?.KeyBound -= OnKeyBound;
        config?.KeyUnbound -= OnKeyUnbound;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeybindConfigModel(_config);
        ClearBindable();

        _palette.Dispose();
    }
}
