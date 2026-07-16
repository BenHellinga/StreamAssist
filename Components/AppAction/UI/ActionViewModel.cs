using System.Collections.ObjectModel;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Configurator;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionViewModel : ViewModelBase
{
    // private



    private readonly IAppAction _action;
    private readonly ObservableCollection<ConfigurableParameterViewModel> _configurators;



    // public



    public string Name => _action.Name;
    public string Description => _action.Description;
    public IAppAction Action => _action;
    public ObservableCollection<ConfigurableParameterViewModel> Configurators => _configurators;



    // constructor



    public ActionViewModel(IAppAction action)
    {
        _action = action;
        _configurators = ConfiguratorBuilder.Build(action);
    }



    // public api



    public bool RepresentsModel(IAppAction action) => action == _action;



    // diposal



    public override void Dispose() {}
}