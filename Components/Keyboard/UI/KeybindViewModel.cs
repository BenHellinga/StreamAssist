using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Keyboard.UI;



// bottom panel for the keyboard tab: the shared bindable editor, showing the selected key's bindable
public class KeybindViewModel : BindableEditorViewModel
{
    // private



    private KeyViewModel? _key;
    private KeybindConfigModel? _config;

    // the bindable being edited, either the key's real one or a draft that isn't saved yet
    private BindableModel? _bindable;
    private bool _isDraft;



    // constructor



    public KeybindViewModel(WindowLayoutModel layout) : base(layout, "No key selected")
    {
        _key = null;
        _config = null;
        _bindable = null;
        _isDraft = false;
    }



    // model events



    private void OnKeyBound(ushort vk, BindableModel bindable)
    {
        if (vk != _key?.VK) return;

        // our own draft was just committed, keep editing it
        if (bindable == _bindable) return;

        // something else was bound to the selected key (e.g. a key dragged onto it)
        ShowKeybind(bindable, isDraft: false);
    }



    private void OnKeyUnbound(ushort vk, BindableModel _)
    {
        if (vk != _key?.VK) return;

        // the selected key was cleared, go back to an unsaved default
        ShowKeybind(BindableModel.CreateDefault(), isDraft: true);
    }



    // the first change to a draft saves it into the config
    private void OnDraftChanged()
    {
        if (!_isDraft || _bindable == null || _key == null || _config == null) return;

        // e.g. the default On Pressed was deleted, there's still nothing worth saving
        if (!_bindable.HasBlock) return;

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

        if (bindable == null) ShowKeybind(BindableModel.CreateDefault(), isDraft: true);
        else ShowKeybind(bindable, isDraft: false);
    }



    public void DeselectKeys(IReadOnlyList<KeyViewModel>? keys)
    {
        if (keys != null && !keys.Contains(_key)) return;

        // the selected key was just deselected
        UnsubscribeFromKeybindConfigModel(_config);
        ClearKeybind();

        _key?.IsSelected = false;
        _key = null;
        _config = null;
    }



    // helpers



    private void ShowKeybind(BindableModel bindable, bool isDraft)
    {
        ClearKeybind();

        _bindable = bindable;
        _isDraft = isDraft;

        if (_isDraft)
            _bindable.Changed += OnDraftChanged;

        ShowBindable(_bindable, $"Key: {_key?.Label.Replace('\n', '-')}     Config: {_config?.Name}");
    }



    private void ClearKeybind()
    {
        ClearBindable();

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
        ClearKeybind();

        base.Dispose();
    }
}
