using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Input.Model;



// contains all data for the entire keyboard component
public class InputComponentModel : ModelBase
{
    // private



    private InputListModel _listModel;



    // public



    [JsonIgnore] public InputListModel ListModel => _listModel;



    // json



    [JsonInclude] [JsonPropertyName("inputList")]
    private InputListModel JsonListModel
    {
        get => _listModel;
        set => _listModel = value;
    }



    // constructor



    public InputComponentModel()
    {
        _listModel = new();
    }



    // disposal



    public override void Dispose()
    {
        _listModel.Dispose();
    }
}