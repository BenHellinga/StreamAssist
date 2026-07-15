using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.AppAction.UI;



public class ActionGroupViewModel : ViewModelBase
{
    // private



    private ActionGroupModel _groupModel;



    // constructor



    public ActionGroupViewModel(ActionGroupModel groupModel)
    {
        _groupModel = groupModel;
    }
}