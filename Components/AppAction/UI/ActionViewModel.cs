using System.Collections.ObjectModel;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Configurator;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionViewModel : ViewModelBase
{
    // private storage fields
    private readonly IAppAction _action;
    private readonly ObservableCollection<ConfigurableParameterViewModel> _configurators;

    // public getter properties
    public string Name => _action.Name;
    public string Description => _action.Description;
    public ObservableCollection<ConfigurableParameterViewModel> Configurators => _configurators;



    // constructor



    public ActionViewModel(IAppAction action)
    {
        _action = action;
        _configurators = ConfiguratorBuilder.Build(action);
    }
}