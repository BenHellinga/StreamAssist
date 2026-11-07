using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StreamAssist.Core.Persistence;



// writes lines to a .txt file in the user's Downloads folder (e.g. a console dock's key moments, kept for editing
// after a stream). The name gets the date and time, so saving again never overwrites an earlier file
public static class TextExport
{
    public static string DownloadsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");



    // the file it wrote, or null with the reason
    public static string? TrySaveToDownloads(string name, IEnumerable<string> lines, out string error)
    {
        error = "";
        var safeName = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        if (safeName == "") safeName = "log";

        var path = Path.Combine(DownloadsDir, $"{safeName} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.txt");
        try
        {
            Directory.CreateDirectory(DownloadsDir);
            File.WriteAllLines(path, lines);
            return path;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
