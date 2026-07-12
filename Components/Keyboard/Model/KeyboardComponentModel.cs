using System.Text.Json.Serialization;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardComponentModel
{
    // private storage fields
    private KeyboardListModel _listModel;

    // public getter properties
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
}