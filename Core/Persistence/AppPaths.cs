using System;
using System.IO;

namespace StreamAssist.Core.Persistence;



// where the app keeps its folders: Config/ (settings, data.db) and Assets/ (the user's sounds, images, videos), both in
// the app's own folder. That's the folder with the exe, or the project folder while running from a build in its bin/
// (dotnet run), so nothing here names a user's own folders
public static class AppPaths
{
    // constants



    public static readonly string[] AssetFolders = ["Sounds", "Images", "Videos"]; // made at startup if missing



    // public



    public static string Root { get; } = FindRoot();
    public static string ConfigDir => Path.Combine(Root, "Config");
    public static string AssetsDir => Path.Combine(Root, "Assets");



    // public api



    public static void EnsureAssetFolders()
    {
        foreach (var folder in AssetFolders)
            Directory.CreateDirectory(Path.Combine(AssetsDir, folder));
    }



    // a path typed in a block, relative to Assets/ (e.g. "Sounds/boom.mp3", a leading "Assets/" is fine too), as a full
    // path. A full path, or one that climbs out with "..", is refused: blocks only ever reach files inside Assets/
    public static bool TryResolveAsset(string path, out string fullPath, out string error)
    {
        fullPath = "";
        var relative = path.Trim().Replace('\\', '/');
        if (relative.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) relative = relative["assets/".Length..];

        if (relative == "")
        {
            error = "no file given";
            return false;
        }

        if (Path.IsPathRooted(relative) || relative.Contains(':'))
        {
            error = "must be a path inside assets, e.g. Sounds/boom.mp3";
            return false;
        }

        var assets = Path.GetFullPath(AssetsDir) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(assets, relative));
        if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
        {
            error = "can't leave the assets folder";
            return false;
        }

        fullPath = full;
        error = "";
        return true;
    }



    // a file picked anywhere, as the path a block saves (relative to Assets/, with /), false if it isn't inside Assets/
    public static bool TryMakeAssetPath(string fullPath, out string path)
    {
        var assets = Path.GetFullPath(AssetsDir) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(fullPath);
        path = full.StartsWith(assets, StringComparison.OrdinalIgnoreCase) ? full[assets.Length..].Replace('\\', '/') : "";
        return path != "";
    }



    // helpers



    // up from the exe's folder to the project folder (the one with StreamAssist.csproj) if there is one, else the exe's
    private static string FindRoot()
    {
        var exeDir = AppContext.BaseDirectory;
        for (var dir = new DirectoryInfo(exeDir); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "StreamAssist.csproj"))) return dir.FullName;

        return exeDir;
    }
}
