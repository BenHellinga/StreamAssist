using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Keyboard.UI;



// bottom panel for the keyboard tab: the shared bindable editor, showing the selected key's bindable
// (an unbound key gets the editor's draft, bound to the key on its first change)
public class KeybindViewModel : BindableEditorViewModel
{
    // private



    private KeyViewModel? _key;
    private KeybindConfigModel? _config;



    // constructor



    public KeybindViewModel(WindowLayoutModel layout) : base(layout, "No key selected")
    {
        _key = null;
        _config = null;
    }



    // model events



    private void OnKeyBound(ushort vk, BindableModel bindable)
    {
        if (vk != _key?.VK) return;

        // our own draft was just saved, keep editing it
        if (bindable == Bindable) return;

        // something else was bound to the selected key (e.g. a key dragged onto it)
        ShowKeybind(bindable);
    }



    // the selected key was cleared (e.g. its last block was removed): it stays selected with an empty draft, the
    // default On Pressed only comes with selecting an unbound key, never from editing
    private void OnKeyUnbound(ushort vk, BindableModel _)
    {
        if (vk != _key?.VK) return;
        ShowKeybind(null, startWithOnPressed: false);
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
        ShowKeybind(bindable);
    }



    public void DeselectKeys(IReadOnlyList<KeyViewModel>? keys)
    {
        if (keys != null && !keys.Contains(_key)) return;

        // the selected key was just deselected
        UnsubscribeFromKeybindConfigModel(_config);
        ClearBindable();

        _key?.IsSelected = false;
        _key = null;
        _config = null;
    }



    // helpers



    // the key's binding, or an unsaved draft that's bound to the key on its first change (starting with an empty
    // On Pressed when the key was just selected)
    private void ShowKeybind(BindableModel? bindable, bool startWithOnPressed = true)
    {
        if (_key == null || _config == null) return;

        var label = $"Key: {_key.Label.Replace('\n', '-')}     Config: {_config.Name}";
        var config = _config;
        var vk = _key.VK;

        if (bindable != null) ShowBindable(bindable, label);
        else ShowDraft(label, draft => config.TryBindKey(vk, draft), startWithOnPressed: startWithOnPressed);
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
        base.Dispose();
    }
}
