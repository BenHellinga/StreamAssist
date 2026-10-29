using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Chatbot.Blocks;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.Model;



// everything the chatbot is configured with: the twitch app credentials, the two accounts' connection settings, the two chat block trees,
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
    private readonly BlockSlot _editableRoot;
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
    [JsonIgnore] public BlockSlot Root => _root; // the protected commands, every message is tried here first
    [JsonIgnore] public BlockSlot EditableRoot => _editableRoot; // the commands chat can change (a Match Text, see TryAddCommand)
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

    [JsonInclude] [JsonPropertyName("editableRootBlock")]
    private PendingBlockData? JsonEditableRoot
    {
        get => _editableRoot.GetData();
        set => _editableRoot.LoadData(value);
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
        _editableRoot = new("", typeof(ChatBlock)) { Filter = block => block is MatchTextChatBlock }; // commands are its rows
        _editableRoot.TrySetBlock(new MatchTextChatBlock()); // and an empty one for the commands chat can change
        _redeems = new();
        _events = ChatEvents.All.Select(e => new ChatEventSlot(e.Key, e.Name)).ToList();

        _root.Changed += OnContentsChanged;
        _editableRoot.Changed += OnContentsChanged;
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



    // chat messages



    // runs a chat message through the protected commands, then the editable ones (so chat can't override a protected
    // command), true once a binding has run
    public bool TryMatchMessage(ChatMatchState state) =>
        ChatBlock.TryMatchSlot(_root, state) || ChatBlock.TryMatchSlot(_editableRoot, state);



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



    // editable commands (what chat's !addcom / !editcom / !delcom change)
    // only rows directly in the editable root's Match Text are touched, and only whole commands: a row whose pattern is
    // just that command and that runs a binding (never one that leads into more blocks). logErrors is false when chat
    // asked, so chatters can't fill the console



    // adds a row that replies with the response, e.g. ("!hello", "hi there") adds the row !hello\b (\b = a whole word,
    // so it doesn't run for !helloworld)
    public bool TryAddCommand(string command, string response, bool logErrors = true)
    {
        if (!TryFindCommand(command, "add", logErrors, out var block, out var name)) return false;

        var text = response.Trim();
        if (text == "") return Refuse(logErrors, $"Could not add command '{name}' - no response given");

        var row = FindRow(block, name);
        if (row != null && !row.Target.IsEmpty) return Refuse(logErrors, $"Could not add command '{name}' - it already exists");

        var send = new SendChatMessageAction();
        send.Message.SetText(text);
        var bindable = new BindableModel();
        bindable.Root.TrySetBlock(send);

        (row ?? block.Rows.AddRow(CommandPattern(name))).Target.TryBind(bindable); // a row with just the command typed in gets the binding
        return true;
    }



    // changes what a command replies, only if all it does is send one typed message (anything more is edited in the app)
    public bool TryEditCommand(string command, string response, bool logErrors = true)
    {
        if (!TryFindCommand(command, "edit", logErrors, out var block, out var name)) return false;

        var text = response.Trim();
        if (text == "") return Refuse(logErrors, $"Could not edit command '{name}' - no response given");

        if (!TryFindBoundRow(block, name, "edit", logErrors, out var row)) return false;

        if (row.Target.Bindable!.Root.Block is not SendChatMessageAction send || send.Message.BlockSlot.Block != null)
            return Refuse(logErrors, $"Could not edit command '{name}' - it does more than send a message, edit it in the app");

        send.Message.SetText(text);
        return true;
    }



    public bool TryDeleteCommand(string command, bool logErrors = true)
    {
        if (!TryFindCommand(command, "delete", logErrors, out var block, out var name)) return false;
        if (!TryFindBoundRow(block, name, "delete", logErrors, out var row)) return false;

        block.Rows.RemoveRow(row);
        return true;
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



    // the steps leading to a target, one per Match Text row (its raw pattern) or Parameter ({name}, with a space before it
    // unless the text before already ends in one),
    // e.g. ["!", "pay ", "{user}", " ", "{amount}"], or null if the target isn't in the tree
    public List<string>? FindCommandSteps(ChatTarget target) => FindPath(t => t == target)?.Select(step => step.Text).ToList();



    // the parameters a bind box's binding gets (the editor's Parameters list), null if it's not in the chatbot:
    // a chat command gets the chat ones, rest, and whatever the Parse Parameter blocks and regex groups on its path capture
    public IReadOnlyList<ParameterInfo>? FindParameters(ChatTarget target)
    {
        if (FindPath(t => t == target) is { } path)
        {
            var captured = path.SelectMany(step => step.Captured).ToList();
            var builtIn = ChatEvents.Chat.Append(ChatEvents.Rest).Where(p => captured.All(c => c.Name != p.Name));
            return [.. captured, .. builtIn]; // a capture with a built-in name replaces it, like it does when matching
        }

        if (_redeems.Rows.Any(r => r.Target == target)) return ChatEvents.Redeem;

        var slot = _events.FirstOrDefault(e => e.Target == target);
        return slot == null ? null : ChatEvents.Parameters(slot.Key);
    }



    // the steps run together (spaces are never implied), e.g. "!pay {user} {amount}"
    public static string FormatCommand(IEnumerable<string> steps) => string.Concat(steps);



    // helpers



    private void OnContentsChanged() => Changed?.Invoke();



    private static bool Refuse(bool logErrors, string message)
    {
        if (logErrors) AppLog.WriteError(message);
        return false;
    }



    // the editable root's Match Text, and the command trimmed (one word, the whole command, e.g. "!hello")
    private bool TryFindCommand(string command, string verb, bool logErrors, out MatchTextChatBlock block, out string name)
    {
        name = command.Trim();
        block = null!;

        if (_editableRoot.Block is not MatchTextChatBlock found)
            return Refuse(logErrors, $"Could not {verb} command '{name}' - there's no Match Text in the editable commands");

        block = found;
        if (name == "") return Refuse(logErrors, $"Could not {verb} command - no command name given");
        if (name.Any(char.IsWhiteSpace)) return Refuse(logErrors, $"Could not {verb} command '{name}' - a command is one word");
        return true;
    }



    // the command's row, which has to run a binding (a row that leads into more blocks isn't one command)
    private static bool TryFindBoundRow(MatchTextChatBlock block, string name, string verb, bool logErrors, out MatchTextRow row)
    {
        row = FindRow(block, name)!;
        if (row == null || row.Target.IsEmpty) return Refuse(logErrors, $"Could not {verb} command '{name}' - it doesn't exist");
        if (row.Target.Bindable == null) return Refuse(logErrors, $"Could not {verb} command '{name}' - it leads into more blocks, it isn't one command");
        return true;
    }



    // the row that is this command (case ignored, like matching): its pattern is the command, as typed or as added
    private static MatchTextRow? FindRow(MatchTextChatBlock block, string name) =>
        block.Rows.Rows.FirstOrDefault(row => string.Equals(CommandOf(row.Pattern), name, StringComparison.OrdinalIgnoreCase));



    // the row pattern for a command: the text matched literally, then the end of the word (\b after a letter or digit,
    // otherwise "a space or the end next", since \b only works after one)
    private static string CommandPattern(string name)
    {
        var last = name[^1];
        return Regex.Escape(name) + (char.IsLetterOrDigit(last) || last == '_' ? @"\b" : @"(?!\S)");
    }



    // which command a row pattern is: undoes CommandPattern ("!hello\b" -> "!hello"), plain text is itself ("!lurk"),
    // null for anything that isn't literal text (an empty row, a regex like .* that matches more than one command)
    private static string? CommandOf(string pattern)
    {
        if (pattern.EndsWith(@"\b")) pattern = pattern[..^2];
        else if (pattern.EndsWith(@"(?!\S)")) pattern = pattern[..^6];
        if (pattern == "") return null;

        try
        {
            // literal = escaping the text gives the pattern back (# doesn't need escaping here, people type it plain)
            var text = Regex.Unescape(pattern);
            return Regex.Escape(text).Replace(@"\#", "#") == pattern.Replace(@"\#", "#") ? text : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }



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



    // one step on the way down to a bind box: its text in the command, and the parameters it captures
    private sealed record PathStep(string Text, IReadOnlyList<ParameterInfo> Captured);



    // the steps down to the first target isEnd says yes to, in either root
    private List<PathStep>? FindPath(Func<ChatTarget, bool> isEnd)
    {
        var path = new List<PathStep>();
        return FindPath(_root.Block, isEnd, path) || FindPath(_editableRoot.Block, isEnd, path) ? path : null;
    }



    private static bool FindPath(BlockModel? block, Func<ChatTarget, bool> isEnd, List<PathStep> path)
    {
        switch (block)
        {
            case MatchTextChatBlock match:
                foreach (var row in match.Rows.Rows)
                {
                    var groups = ChatPattern.GroupNames(row.Pattern)
                        .Select(g => new ParameterInfo(g, ValueKind.Text, $"captured by the (?<{g}>...) group in '{row.Pattern}'"))
                        .ToList();
                    if (FindPath(new(row.Pattern, groups), row.Target, isEnd, path)) return true;
                }
                break;

            case HasPermissionChatBlock permission:
                if (FindPath(null, permission.Target, isEnd, path)) return true; // matches no text, so it adds no step
                break;

            case ParameterChatBlock parameter:
                var name = parameter.ParameterName.Trim();
                List<ParameterInfo> captured = name == "" ? []
                    : [new(name, ValueKind.Text, $"captured by the Parse Parameter block ({parameter.Reads.ToLowerInvariant()})")];

                // it skips the spaces before it, so the command reads "!addcom {command} {content}" even with no " " row between
                var before = string.Concat(path.Select(step => step.Text));
                var gap = before != "" && !char.IsWhiteSpace(before[^1]) ? " " : "";

                if (FindPath(new($"{gap}{{{(name == "" ? "?" : name)}}}", captured), parameter.Target, isEnd, path)) return true;
                break;
        }

        return false;
    }



    // a null step (Has Permission) goes through without adding anything to the path
    private static bool FindPath(PathStep? step, ChatTarget current, Func<ChatTarget, bool> isEnd, List<PathStep> path)
    {
        if (step != null) path.Add(step);
        if (isEnd(current) || FindPath(current.Child.Block, isEnd, path)) return true;

        if (step != null) path.RemoveAt(path.Count - 1);
        return false;
    }



    // disposal



    public override void Dispose()
    {
        _root.Changed -= OnContentsChanged;
        _editableRoot.Changed -= OnContentsChanged;
        _redeems.Changed -= OnContentsChanged;
        foreach (var slot in _events)
            slot.Target.Changed -= OnContentsChanged;

        _root.Dispose();
        _editableRoot.Dispose();
        _redeems.Dispose();
        foreach (var slot in _events)
            slot.Dispose();
    }
}
