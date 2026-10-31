using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Chatbot.Model;



// one config of the Events tab: the alerts (one binding per stream event) and the channel point redeems.
// Switching configs swaps both at once (see ChatbotComponentModel.EventsConfigs)
public sealed class EventsConfigModel : INamedConfig
{
    // private



    private string _name;
    private readonly RedeemList _redeems;
    private readonly List<ChatEventSlot> _events;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public RedeemList Redeems => _redeems;
    [JsonIgnore] public IReadOnlyList<ChatEventSlot> Events => _events; // in ChatEvents.All order



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    [JsonInclude] [JsonPropertyName("redeems")]
    private List<RedeemRowData> JsonRedeems
    {
        get => _redeems.GetData();
        set => _redeems.LoadData(value);
    }

    // only bound events are saved, e.g. { "raid": { ... } }
    [JsonInclude] [JsonPropertyName("events")]
    private Dictionary<string, BindableModel> JsonEvents
    {
        get => _events.Where(e => e.Target.Bindable != null).ToDictionary(e => e.Key, e => e.Target.Bindable!);
        set => LoadEvents(value);
    }



    // events



    public event Action? Changed;



    // constructor



    public EventsConfigModel()
    {
        _name = "";
        _redeems = new();
        _events = ChatEvents.All.Select(e => new ChatEventSlot(e.Key, e.Name)).ToList();

        _redeems.Changed += OnContentsChanged;
        foreach (var slot in _events)
            slot.Target.Changed += OnContentsChanged;
    }



    // public api



    public void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        Changed?.Invoke();
    }



    // helpers



    private void OnContentsChanged() => Changed?.Invoke();



    // only runs when loading: an unknown event name is skipped with a warning
    private void LoadEvents(Dictionary<string, BindableModel> data)
    {
        foreach (var (key, bindable) in data)
        {
            var slot = _events.FirstOrDefault(e => e.Key == key);
            if (slot == null)
            {
                AppLog.WriteWarning($"unknown event '{key}' skipped");
                bindable.Dispose();
                continue;
            }

            slot.Target.LoadData(new() { Bindable = bindable });
        }
    }



    // disposal



    public void Dispose()
    {
        _redeems.Changed -= OnContentsChanged;
        foreach (var slot in _events)
            slot.Target.Changed -= OnContentsChanged;

        _redeems.Dispose();
        foreach (var slot in _events)
            slot.Dispose();
    }
}
