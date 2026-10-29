using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// one channel point reward: its exact title (case ignored), and the binding that runs when it's redeemed
public sealed class RedeemRow : IDisposable
{
    // private



    private string _title;
    private readonly ChatTarget _target;



    // public



    public string Title => _title;
    public ChatTarget Target => _target; // binding only, no chat blocks
    public bool IsEmpty => _title == "" && _target.IsEmpty;



    // events



    public event Action? TitleChanged;



    // constructor



    public RedeemRow()
    {
        _title = "";
        _target = new(allowsBlocks: false);
    }



    // public api



    public void SetTitle(string title)
    {
        if (title == _title) return;

        _title = title;
        TitleChanged?.Invoke();
    }



    // exact title, ignoring case and spaces around it (no regex, titles often have ? or ! in them)
    public bool Matches(string rewardTitle) =>
        _title.Trim() != "" && string.Equals(_title.Trim(), rewardTitle.Trim(), StringComparison.OrdinalIgnoreCase);



    // json



    public RedeemRowData GetData() => new() { Title = _title, Bindable = _target.Bindable };



    public void LoadData(RedeemRowData data)
    {
        _title = data.Title;
        _target.LoadData(new() { Bindable = data.Bindable });
    }



    // disposal



    public void Dispose() => _target.Dispose();
}



// how a redeem row is saved
public class RedeemRowData
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("bindable")]
    public BindableModel? Bindable { get; set; }
}
