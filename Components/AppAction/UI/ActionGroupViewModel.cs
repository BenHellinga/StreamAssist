using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.AppAction.UI;



public class ActionGroupViewModel : ViewModelBase
{
    // private storage fields
    private string _title;
    private string _description;
    private ActionGroupModel _groupModel;

    // public getter properties
    public string Title => _title;
    public string Description => _description;



    // constructor



    public ActionGroupViewModel(string title, string description, ActionGroupModel groupModel)
    {
        _title = title;
        _description = description;
        _groupModel = groupModel;
    }
}