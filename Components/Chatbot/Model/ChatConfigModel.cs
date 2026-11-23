using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.ChatBlocks;

namespace StreamAssist.Components.Chatbot.Model;



// one config of the Chat tab: its chat block tree (what !addcom changes is the Editable Commands blocks in it).
// Switching configs swaps the tree (see ChatbotComponentModel.ChatConfigs)
public sealed class ChatConfigModel : INamedConfig
{
    // private



    private string _name;
    private readonly BlockSlot _root;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public BlockSlot Root => _root;



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




    // events



    public event Action? Changed;



    // constructor



    public ChatConfigModel()
    {
        _name = "";
        _root = new("", typeof(ChatBlock));

        // a fresh config tries an empty Match Text and an empty Editable Commands (for !addcom) on every message
        var tryAll = new TryAllChatBlock();
        tryAll.Children.TryInsertBlock(new MatchTextChatBlock(), 0);
        tryAll.Children.TryInsertBlock(new EditableCommandsChatBlock(), 1);
        _root.TrySetBlock(tryAll);

        _root.Changed += OnContentsChanged;
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
        _root.Dispose();
    }
}
