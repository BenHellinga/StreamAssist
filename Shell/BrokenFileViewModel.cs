using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Shell;



// shown instead of a tab whose file can't be read (keyboard.json, chatbot.json, auth.json). Nothing is saved to the
// file meanwhile, so it can be fixed by hand (then restart the app), or backed up so that part starts empty
public partial class BrokenFileViewModel : ViewModelBase
{
    // private



    private readonly string _fileName;



    // public



    public string Title => $"{_fileName}.json can't be read";
    public string Reason => $"Where it went wrong: {JsonFileStore.BrokenReason(_fileName) ?? "unknown"}";
    public string Explanation =>
        "Nothing is saved to it, so nothing in it is lost. Either close the app, fix the file in Config/ and reopen the app, " +
        "or back it up (it's renamed to " + _fileName + ".broken-<date>.json, nothing is deleted) and start this part empty.";



    // activator



    public static BrokenFileViewModel ActivateTab(string fileName, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(fileName);
    }



    // constructor



    public BrokenFileViewModel(string fileName)
    {
        _fileName = fileName;
    }



    // ui events



    // the tab comes back on its own once the file is out of the way (the main window listens to JsonFileStore)
    [RelayCommand] private void OnBackUpPressed() => JsonFileStore.TryBackUpAndStartFresh(_fileName);



    // disposal



    public override void Dispose() { }
}
