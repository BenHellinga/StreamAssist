using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyConfiguratorViewModel : ViewModelBase
{
    // private storage fields
    private KeyboardBindingModel? _bindingModel;

    // public getter properties
    [ObservableProperty] private string _statusText = "Click any key";



    // setter



    public void SetBindingModel(KeyboardBindingModel? bindingModel)
    {
        _bindingModel = bindingModel;
        StatusText = _bindingModel is null ? "Click any key" : "Key selected";
    }
}