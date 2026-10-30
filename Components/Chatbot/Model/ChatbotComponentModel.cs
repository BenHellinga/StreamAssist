using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Chatbot.ChatBlocks;
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
    public event Action<NewReward>? CreateRewardRequested; // the background creates it on twitch, then adds its redeem row



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



    // asks for a new channel point reward on twitch (the New reward panel, or anything else), the chatbot manager creates
    // it as the streamer and adds a redeem row for it. Rewards made this way are the only ones the app can change later
    // (Set Reward Setting): twitch only lets the app that created a reward edit it
    public bool TryRequestCreateReward(string title, string cost, string prompt, bool userInputRequired)
    {
        var name = title.Trim();
        if (name == "")
        {
            AppLog.WriteError("reward needs a title");
            return false;
        }

        if (name.Length > 45)
        {
            AppLog.WriteError("reward title over 45 characters");
            return false;
        }

        if (!int.TryParse(cost.Trim(), out var points) || points < 1)
        {
            AppLog.WriteError($"reward cost '{cost.Trim()}' must be 1 or more");
            return false;
        }

        CreateRewardRequested?.Invoke(new NewReward(name, points, prompt.Trim(), userInputRequired));
        return true;
    }



    // runs the binding for a stream event (a ChatEvents key), false if it isn't bound or is disabled
    public bool TryRunEvent(string key, IReadOnlyDictionary<string, string> parameters)
    {
        var slot = _events.FirstOrDefault(e => e.Key == key);
        if (slot == null)
        {
            AppLog.WriteError($"unknown event '{key}'");
            return false;
        }

        return slot.Target.TryRun(parameters);
    }



    // editable commands (what chat's !addcom / !editcom / !delcom change)
    // only the editable tree is touched, found by walking literal rows (see FindCommand), and only whole commands: a row
    // whose text completes the command and that runs a binding (never one that leads into more blocks). logErrors is
    // false when chat asked, so chatters can't fill the console, and failure says why (so chat can be told instead)



    public bool TryAddCommand(string command, string response, bool logErrors = true) => TryAddCommand(command, response, out _, logErrors);
    public bool TryEditCommand(string command, string response, bool logErrors = true) => TryEditCommand(command, response, out _, logErrors);
    public bool TryDeleteCommand(string command, bool logErrors = true) => TryDeleteCommand(command, out _, logErrors);



    // adds a row that replies with the response, e.g. ("!hello", "hi there") adds hello after an existing "!" row, or
    // !hello at the top if there's none (plain text rows match whole words, so it doesn't run for !helloworld)
    public bool TryAddCommand(string command, string response, out CommandFailure failure, bool logErrors = true)
    {
        if (!TryFindCommand(command, "add", logErrors, out failure, out var block, out var name)) return false;

        var text = response.Trim();
        if (text == "") return Refuse(logErrors, out failure, CommandFailure.Invalid, $"can't add {name}, no response");

        var place = FindCommand(block, name);
        if (place.Row != null && !place.Row.Target.IsEmpty)
            return Refuse(logErrors, out failure, CommandFailure.AlreadyExists, $"can't add {name}, already exists");

        var send = new SendChatMessageAction();
        send.Message.SetText(text);
        var bindable = new BindableModel();
        bindable.Root.TrySetBlock(send);

        // a row with just the command typed in gets the binding, otherwise a new row goes where the walk stopped
        (place.Row ?? place.Block.Rows.AddRow(place.Remaining)).Target.TryBind(bindable);
        return true;
    }



    // changes what a command replies, only if all it does is send one typed message (anything more is edited in the app)
    public bool TryEditCommand(string command, string response, out CommandFailure failure, bool logErrors = true)
    {
        if (!TryFindCommand(command, "edit", logErrors, out failure, out var block, out var name)) return false;

        var text = response.Trim();
        if (text == "") return Refuse(logErrors, out failure, CommandFailure.Invalid, $"can't edit {name}, no response");

        if (!TryFindBoundRow(block, name, "edit", logErrors, out failure, out _, out var row)) return false;

        if (row.Target.Bindable!.Root.Block is not SendChatMessageAction send || send.Message.BlockSlot.Block != null)
            return Refuse(logErrors, out failure, CommandFailure.NotSimple, $"can't edit {name}, not just a message");

        send.Message.SetText(text);
        return true;
    }



    public bool TryDeleteCommand(string command, out CommandFailure failure, bool logErrors = true)
    {
        if (!TryFindCommand(command, "delete", logErrors, out failure, out var root, out var name)) return false;
        if (!TryFindBoundRow(root, name, "delete", logErrors, out failure, out var block, out var row)) return false;

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



    // the steps leading to a target, one per Match Text row (plain text, or a /regex/ as written) or Parameter ({name}),
    // with a space in front where two words meet (see ChatText.Join),
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



    private static bool Refuse(bool logErrors, out CommandFailure failure, CommandFailure reason, string message)
    {
        if (logErrors) AppLog.WriteError(message);
        failure = reason;
        return false;
    }



    // the editable root's Match Text, and the command trimmed (one word, the whole command, e.g. "!hello")
    private bool TryFindCommand(string command, string verb, bool logErrors, out CommandFailure failure,
        out MatchTextChatBlock block, out string name)
    {
        name = command.Trim();
        block = null!;

        if (_editableRoot.Block is not MatchTextChatBlock found)
            return Refuse(logErrors, out failure, CommandFailure.Invalid, $"can't {verb} {name}, no editable Match Text");

        block = found;
        if (name == "") return Refuse(logErrors, out failure, CommandFailure.Invalid, $"can't {verb} command, no name");
        if (name.Any(char.IsWhiteSpace)) return Refuse(logErrors, out failure, CommandFailure.Invalid, $"can't {verb} {name}, must be one word");
        if (ChatText.TryGetRegex(name, out _)) return Refuse(logErrors, out failure, CommandFailure.Invalid, $"can't {verb} {name}, slashes make it a regex");

        failure = CommandFailure.None;
        return true;
    }



    // the command's row (and the Match Text it's in), which has to run a binding (a row that leads into more blocks
    // isn't one command, so it counts as not found)
    private static bool TryFindBoundRow(MatchTextChatBlock root, string name, string verb, bool logErrors, out CommandFailure failure,
        out MatchTextChatBlock block, out MatchTextRow row)
    {
        var place = FindCommand(root, name);
        block = place.Block;
        row = place.Row!;
        if (row == null || row.Target.IsEmpty)
            return Refuse(logErrors, out failure, CommandFailure.NotFound, $"can't {verb} {name}, not found");
        if (row.Target.Bindable == null)
            return Refuse(logErrors, out failure, CommandFailure.NotFound, $"can't {verb} {name}, not a single command");

        failure = CommandFailure.None;
        return true;
    }



    // walks down through plain text rows like matching does (case ignored), e.g. "!lurk" through a "!" row into the Match Text
    // after it, then its "lurk" row. Row = the row that is the whole command, or null; Block / Remaining = where it is, or where
    // adding it goes: the first Match Text the walk reaches that has nothing more for it, and what's left of the command
    private static (MatchTextChatBlock Block, string Remaining, MatchTextRow? Row) FindCommand(MatchTextChatBlock block, string command)
    {
        (MatchTextChatBlock, string, MatchTextRow?)? addHere = null;

        foreach (var row in block.Rows.Rows)
        {
            if (row.IsRegex) continue; // a /regex/ row is never a command, and never walked into
            var text = row.Pattern.Trim();
            if (text == "") continue;
            if (string.Equals(text, command, StringComparison.OrdinalIgnoreCase)) return (block, command, row);

            // a prefix row leading into another Match Text, e.g. "!" (only where matching would carry on: "lu" isn't a
            // prefix of "lurk", it would cut the word in half)
            if (!command.StartsWith(text, StringComparison.OrdinalIgnoreCase) || !ChatText.EndsCleanly(command, text.Length, text)) continue;
            if (row.Target.Child.Block is not MatchTextChatBlock child) continue;

            var deeper = FindCommand(child, command[text.Length..]);
            if (deeper.Row != null) return deeper;
            addHere ??= deeper;
        }

        return addHere ?? (block, command, null);
    }



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



    // one step on the way down to a bind box: its text in the command, and the parameters it captures
    private sealed record PathStep(string Text, IReadOnlyList<ParameterInfo> Captured);



    private static string PathText(List<PathStep> path) => string.Concat(path.Select(step => step.Text));



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
                    // a /regex/ shows as written (// adds nothing) and its named groups are captured, plain text joins
                    // the words before it with a space, like matching does
                    List<ParameterInfo> groups = [];
                    string text;
                    if (ChatText.TryGetRegex(row.Pattern, out var body))
                    {
                        text = body == "" ? "" : row.Pattern.Trim();
                        groups = ChatPattern.GroupNames(body)
                            .Select(g => new ParameterInfo(g, ValueKind.Text, $"captured by the (?<{g}>...) group in '{row.Pattern.Trim()}'"))
                            .ToList();
                    }
                    else text = ChatText.Join(PathText(path), row.Pattern.Trim());

                    if (FindPath(new(text, groups), row.Target, isEnd, path)) return true;
                }
                break;

            case HasPermissionChatBlock permission:
                if (FindPath(null, permission.Target, isEnd, path)) return true; // matches no text, so it adds no step
                break;

            case ParameterChatBlock parameter:
                var name = parameter.ParameterName.Trim();
                List<ParameterInfo> captured = name == "" ? []
                    : [new(name, ValueKind.Text, $"captured by the Parse Parameter block ({parameter.Reads.ToLowerInvariant()})")];

                // after a word it reads past the space, so the command shows "!addcom {command} {content}"
                var shown = ChatText.Join(PathText(path), $"{{{(name == "" ? "?" : name)}}}");

                if (FindPath(new(shown, captured), parameter.Target, isEnd, path)) return true;
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
