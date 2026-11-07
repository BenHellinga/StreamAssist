using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Core.Mvvm;



// the file picker for a file inside Assets/ (a block's sound, a widget's image): opens in Assets/, and only a file
// inside it is taken (blocks and widgets can't reach anything else)
public static class AssetPicker
{
    // the picked file's path inside Assets/ (e.g. Sounds/boom.mp3); null if nothing was picked, error set if the file
    // was outside Assets/
    public static async Task<(string? Path, string Error)> PickAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: TopLevel topLevel }) return (null, "");

        AppPaths.EnsureAssetFolders();
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(AppPaths.AssetsDir),
        });

        if (files.Count == 0) return (null, "");
        return AppPaths.TryMakeAssetPath(files[0].Path.LocalPath, out var path)
            ? (path, "")
            : (null, $"can't use '{files[0].Name}', it isn't in the assets folder");
    }
}
