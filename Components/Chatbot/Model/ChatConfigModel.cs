using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.ChatBlocks;

namespace StreamAssist.Components.Chatbot.Model;



// one config of the Chat tab: both chat block trees (Built-in, tried first, and Custom, what !addcom changes).
// Switching configs swaps both at once (see ChatbotComponentModel.ChatConfigs)
public sealed class ChatConfigModel : INamedConfig
{
    // private



    private string _name;
    private readonly BlockSlot _root;
    private readonly BlockSlot _editableRoot;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public BlockSlot Root => _root;
    [JsonIgnore] public BlockSlot EditableRoot => _editableRoot;



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
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



    // events



    public event Action? Changed;



    // constructor



    public ChatConfigModel()
    {
        _name = "";
        _root = new("", typeof(ChatBlock));
        _root.TrySetBlock(new MatchTextChatBlock()); // a fresh config starts with an empty Match Text
        _editableRoot = new("", typeof(ChatBlock)) { Filter = block => block is MatchTextChatBlock }; // commands are its rows
        _editableRoot.TrySetBlock(new MatchTextChatBlock()); // and an empty one for the commands chat can change

        _root.Changed += OnContentsChanged;
        _editableRoot.Changed += OnContentsChanged;
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



    // disposal



    public void Dispose()
    {
        _root.Changed -= OnContentsChanged;
        _editableRoot.Changed -= OnContentsChanged;
        _root.Dispose();
        _editableRoot.Dispose();
    }
}
