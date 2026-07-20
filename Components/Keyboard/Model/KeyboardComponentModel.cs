using System.Text.Json.Serialization;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.Keyboard.Model;



// contains all data for the entire keyboard component
public class KeyboardComponentModel : ModelBase
{
    // private



    private KeyboardListModel _listModel;



    // public



    [JsonIgnore] public KeyboardListModel ListModel => _listModel;



    // json



    [JsonInclude] [JsonPropertyName("keyboardList")]
    private KeyboardListModel JsonListModel
    {
        get => _listModel;
        set => _listModel = value;
    }



    // constructor



    public KeyboardComponentModel()
    {
        _listModel = new();
    }



    // disposal



    public override void Dispose()
    {
        _listModel.Dispose();
    }
}