using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Blocks;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.Model;



// everything the chatbot is configured with: the twitch app credentials, the two accounts' connection settings, the chat block tree,
// the channel point redeems, and one binding per stream event (sub, raid, ...)
// the credentials are saved to Config/auth.json by ChatbotSaveManager, the rest to chatbot.json
public class ChatbotComponentModel : ModelBase
{
    // private



    private string _clientId;
    private string _clientSecret;
    private bool _connectOnStartup;
    private bool _botConnectOnStartup;
    private bool _isEnabled;
    private bool _isBotEnabled;
    private readonly BlockSlot _root;
    private readonly RedeemList _redeems;
    private readonly List<ChatEventSlot> _events;



    // public



    // the app registered on dev.twitch.tv, both accounts log in through it (treat the secret like a password)
    [JsonIgnore] public string ClientId => _clientId;
    [JsonIgnore] public string ClientSecret => _clientSecret;

    // saved: whether each account connects when the app starts
    [JsonIgnore] public bool ConnectOnStartup => _connectOnStartup;
    [JsonIgnore] public bool BotConnectOnStartup => _botConnectOnStartup;

    // this session only (start as the connect on startup setting): whether each account should be connected right now
    [JsonIgnore] public bool IsEnabled => _isEnabled;
    [JsonIgnore] public bool IsBotEnabled => _isBotEnabled; // without the bot, chat is sent as the streamer
    [JsonIgnore] public BlockSlot Root => _root; // the one chat block every message starts at
    [JsonIgnore] public RedeemList Redeems => _redeems;
    [JsonIgnore] public IReadOnlyList<ChatEventSlot> Events => _events; // in ChatEvents.All order



    // json



    [JsonInclude] [JsonPropertyName("connectOnStartup")]
    private bool JsonConnectOnStartup
    {
        get => _connectOnStartup;
        set => _connectOnStartup = _isEnabled = value;
    }

    [JsonInclude] [JsonPropertyName("botConnectOnStartup")]
    private bool JsonBotConnectOnStartup
    {
        get => _botConnectOnStartup;
        set => _botConnectOnStartup = _isBotEnabled = value;
    }

    [JsonInclude] [JsonPropertyName("rootBlock")]
    private PendingBlockData? JsonRoot
    {
        get => _root.GetData();
        set => _root.LoadData(value);
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



    public event Action? ConnectionChanged; // credentials or enabled changed, or a reconnect was asked for (streamer)
    public event Action? BotConnectionChanged; // the bot was enabled/disabled, or a reconnect was asked for
    public event Action? CredentialsChanged; // client id or secret changed (saved to auth.json)
    public event Action? Changed; // anything else changed (saved to chatbot.json)



    // constructor



    public ChatbotComponentModel()
    {
        _clientId = "";
        _clientSecret = "";
        _connectOnStartup = false;
        _botConnectOnStartup = false;
        _isEnabled = false;
        _isBotEnabled = false;
        _root = new("", typeof(ChatBlock));
        _root.TrySetBlock(new MatchTextChatBlock()); // a fresh chatbot starts with an empty Match Text
        _redeems = new();
        _events = ChatEvents.All.Select(e => new ChatEventSlot(e.Key, e.Name)).ToList();

        _root.Changed += OnContentsChanged;
        _redeems.Changed += OnContentsChanged;
        foreach (var slot in _events)
            slot.Target.Changed += OnContentsChanged;
    }



    // connection settings



    public void SetCredentials(string clientId, string clientSecret)
    {
        var id = clientId.Trim();
        var secret = clientSecret.Trim();
        if (id == _clientId && secret == _clientSecret) return;

        _clientId = id;
        _clientSecret = secret;

        CredentialsChanged?.Invoke();
        ConnectionChanged?.Invoke();
    }



    // connect / disconnect the streamer for this session (doesn't forget the login or the credentials)
    public void SetIsEnabled(bool isEnabled)
    {
        if (isEnabled == _isEnabled) return;

        _isEnabled = isEnabled;
        ConnectionChanged?.Invoke();
    }



    // connect / disconnect the bot for this session (doesn't forget its login)
    public void SetIsBotEnabled(bool isEnabled)
    {
        if (isEnabled == _isBotEnabled) return;

        _isBotEnabled = isEnabled;
        BotConnectionChanged?.Invoke();
    }



    // try again now (e.g. after a failed attempt), connecting if it wasn't
    public void Reconnect()
    {
        if (!_isEnabled) SetIsEnabled(true);
        else ConnectionChanged?.Invoke();
    }



    public void ReconnectBot()
    {
        if (!_isBotEnabled) SetIsBotEnabled(true);
        else BotConnectionChanged?.Invoke();
    }



    public void SetConnectOnStartup(bool connectOnStartup)
    {
        if (connectOnStartup == _connectOnStartup) return;

        _connectOnStartup = connectOnStartup;
        Changed?.Invoke();
    }



    public void SetBotConnectOnStartup(bool connectOnStartup)
    {
        if (connectOnStartup == _botConnectOnStartup) return;

        _botConnectOnStartup = connectOnStartup;
        Changed?.Invoke();
    }



    // events and redeems



    // runs the binding for a stream event (a ChatEvents key), false if it isn't bound or is disabled
    public bool TryRunEvent(string key, IReadOnlyDictionary<string, string> parameters)
    {
        var slot = _events.FirstOrDefault(e => e.Key == key);
        if (slot == null)
        {
            AppLog.WriteError($"Could not run chatbot event '{key}' - unknown event");
            return false;
        }

        return slot.Target.TryRun(parameters);
    }



    // describing targets



    // what a bind box is for, e.g. ("Chat command", "!pay {user}"), ("Redeem", "Hydrate"), ("Alert", "Raid"), null if it's not in the chatbot
    public (string Kind, string Text)? Describe(ChatTarget target)
    {
        var steps = FindCommandSteps(target);
        if (steps != null) return ("Chat command", FormatCommand(steps));

        var redeem = _redeems.Rows.FirstOrDefault(r => r.Target == target);
        if (redeem != null) return ("Redeem", redeem.Title);

        var slot = _events.FirstOrDefault(e => e.Target == target);
        if (slot != null) return ("Alert", slot.Name);

        return null;
    }



    // the steps leading to a target, one per Match Text row (its raw pattern) or Parameter ({name}),
    // e.g. ["!", "pay ", "{user}", " ", "{amount}"], or null if the target isn't in the tree
    public List<string>? FindCommandSteps(ChatTarget target)
    {
        var steps = new List<string>();
        return FindCommandSteps(_root.Block, target, steps) ? steps : null;
    }



    // the steps run together (spaces are never implied), e.g. "!pay {user} {amount}"
    public static string FormatCommand(IEnumerable<string> steps) => string.Concat(steps);



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
                AppLog.WriteWarning($"Could not load chatbot event '{key}' - unknown event, it was skipped");
                bindable.Dispose();
                continue;
            }

            slot.Target.LoadData(new() { Bindable = bindable });
        }
    }



    private static bool FindCommandSteps(BlockModel? block, ChatTarget target, List<string> steps)
    {
        switch (block)
        {
            case MatchTextChatBlock match:
                foreach (var row in match.Rows.Rows)
                    if (FindCommandSteps(row.Pattern, row.Target, target, steps)) return true;
                break;

            case ParameterChatBlock parameter:
                var name = parameter.ParameterName.Trim();
                if (FindCommandSteps($"{{{(name == "" ? "?" : name)}}}", parameter.Target, target, steps)) return true;
                break;
        }

        return false;
    }



    private static bool FindCommandSteps(string step, ChatTarget current, ChatTarget target, List<string> steps)
    {
        steps.Add(step);
        if (current == target || FindCommandSteps(current.Child.Block, target, steps)) return true;

        steps.RemoveAt(steps.Count - 1);
        return false;
    }



    // disposal



    public override void Dispose()
    {
        _root.Changed -= OnContentsChanged;
        _redeems.Changed -= OnContentsChanged;
        foreach (var slot in _events)
            slot.Target.Changed -= OnContentsChanged;

        _root.Dispose();
        _redeems.Dispose();
        foreach (var slot in _events)
            slot.Dispose();
    }
}
