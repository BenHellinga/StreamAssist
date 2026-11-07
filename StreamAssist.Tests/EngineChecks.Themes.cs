using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Actions;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Spotify.Values;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.Data.Background;
using StreamAssist.Components.Data.Actions;
using StreamAssist.Components.Data.Containers;
using StreamAssist.Components.Data.Values;
using System.Net;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Conditions;
using StreamAssist.Components.OBS.Values;
using StreamAssist.Components.OBS.Actions;
using StreamAssist.Components.OutputConsole.Actions;
using StreamAssist.Components.Input.Conditions;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Core.Connections;
using StreamAssist.Components.Chatbot.Values;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Blocks.Values;
using StreamAssist.Components.Blocks.Actions;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.ChatBlocks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Blocks.Conditions;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Input.UI;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.UI;

namespace StreamAssist.Tests;



// engine checks: themes keep only their changes
internal static partial class EngineChecks
{
    // ---- theme: only changes are kept, refused values change nothing ----
    private static void ThemeChanges()
    {
        // the repository (from this file's path: the tests may be built into another folder)
        static string RepoDir([System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
            System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(file))!;

        var theme = new StreamAssist.Components.Theme.Model.ThemeComponentModel();
        theme.SetDefaults(new Dictionary<string, string> { ["ColorEnabled"] = "#3d9963", ["ColorLine"] = "#2e2e2e" },
            new Dictionary<string, double> { ["LineGap"] = 5, ["FontSizeSmall"] = 12 });
        Check("theme: with no theme the defaults are used and nothing can be changed", theme.GetColor("ColorEnabled") == "#3d9963"
            && !theme.TrySetColor("ColorEnabled", "red") && !theme.TrySetValue("LineGap", "7") && theme.GetValue("LineGap") == 5);

        theme.Configs.TryCreate("Dark");
        var colorEvents = 0;
        var saves = 0;
        theme.ColorChanged += _ => colorEvents++;
        theme.Changed += () => saves++;

        Check("theme: a new theme starts as the defaults", theme.GetColor("ColorEnabled") == "#3d9963" && theme.GetValue("LineGap") == 5
            && !theme.IsColorChanged("ColorEnabled") && !theme.IsValueChanged("LineGap"));
        Check("theme: a color is kept as #rrggbb, any way it's typed", theme.TrySetColor("ColorEnabled", " #C08A2E ") && theme.GetColor("ColorEnabled") == "#c08a2e"
            && theme.IsColorChanged("ColorEnabled") && theme.TrySetColor("ColorEnabled", "red") && theme.GetColor("ColorEnabled") == "#ff0000");
        Check("theme: not a color, an unknown key or a number out of its range is refused",
            !theme.TrySetColor("ColorEnabled", "greenish") && theme.GetColor("ColorEnabled") == "#ff0000" && !theme.TrySetColor("ColorNope", "#000000")
            && !theme.TrySetValue("LineGap", "-1") && !theme.TrySetValue("LineGap", "abc") && !theme.TrySetValue("LineGapz", "5")
            && !theme.TrySetValue("LineGap", "101") && theme.GetValue("LineGap") == 5
            && !theme.TrySetValue("FontSizeSmall", "0") && !theme.TrySetValue("FontSizeSmall", "NaN") && theme.GetValue("FontSizeSmall") == 12);
        Check("theme: typing the default again counts as unchanged", theme.TrySetValue("LineGap", "7.5") && theme.IsValueChanged("LineGap")
            && theme.TrySetValue("LineGap", "5") && !theme.IsValueChanged("LineGap"));
        theme.TrySetValue("FontSizeSmall", "13");
        theme.ResetAll(StreamAssist.Styles.SpacingRecipes.BaseValues);
        Check("theme: resetting the spacing leaves the styles", theme.IsValueChanged("FontSizeSmall") && theme.GetValue("FontSizeSmall") == 13);
        theme.ResetAll(StreamAssist.Styles.SpacingRecipes.StyleValues);
        theme.ResetAllColors();
        Check("theme: reset goes back to the default, each change is one event and one save",
            theme.GetColor("ColorEnabled") == "#3d9963" && !theme.IsColorChanged("ColorEnabled") && !theme.IsValueChanged("FontSizeSmall") && colorEvents == 3 && saves == 7);

        theme.TrySetColor("ColorLine", "#555555");
        theme.Configs.TryCreate("Light");
        Check("theme: a new theme copies the one in use", theme.GetColor("ColorLine") == "#555555");
        theme.TrySetColor("ColorLine", "#111111");
        colorEvents = 0;
        theme.Configs.TrySelect("Dark");
        Check("theme: switching themes shows (and tells about) every color of the new one",
            theme.GetColor("ColorLine") == "#555555" && colorEvents == StreamAssist.Styles.ThemePalette.Colors.Count);
        theme.Configs.TryDelete("Dark");
        theme.Configs.TryDelete("Light");
        Check("theme: deleting the last theme goes back to the defaults", theme.Configs.Selected == null && theme.GetColor("ColorLine") == "#2e2e2e");
        theme.Dispose();

        var loaded = System.Text.Json.JsonSerializer.Deserialize<StreamAssist.Components.Theme.Model.ThemeComponentModel>(
            """{"configs": [{"name": "Dark", "colors": {"ColorLine": "#555555", "ColorGone": "#000000", "ColorText": "nope"}, "values": {"ContentGap": 12, "LineGap": -3, "Old": 1, "FontSizeTitle": 2}}, {"name": "Other"}], "selectedConfig": "Dark"}""")!;
        Check("theme: loading keeps the selected theme, drops unknown keys and unusable values", loaded.Configs.SelectedName == "Dark"
            && loaded.IsColorChanged("ColorLine") && loaded.GetColor("ColorLine") == "#555555"
            && !loaded.IsColorChanged("ColorText") && !loaded.IsColorChanged("ColorGone") && loaded.IsValueChanged("ContentGap")
            && loaded.GetValue("ContentGap") == 12 && !loaded.IsValueChanged("LineGap") && !loaded.IsValueChanged("FontSizeTitle"));
        string Resources(params string[] files) => string.Concat(files.Select(f => System.IO.File.ReadAllText(System.IO.Path.Combine(RepoDir(), "Styles", "Resources", f))));
        Check("theme: every palette color and number is in the app's resource files, letters unique",
            StreamAssist.Styles.ThemePalette.Colors.All(c => Resources("Brushes.axaml").Contains($"x:Key=\"{c.Key}\""))
            && StreamAssist.Styles.SpacingRecipes.AllValues.All(v => Resources("Spacing.axaml", "Sizes.axaml", "Typography.axaml").Contains($"x:Key=\"{v.Key}\""))
            && StreamAssist.Styles.SpacingRecipes.AllValues.Select(v => v.Letter).Distinct().Count() == StreamAssist.Styles.SpacingRecipes.AllValues.Count());
        Check("theme: every spacing recipe only uses known letters", StreamAssist.Styles.SpacingRecipes.Recipes.Keys.All(name =>
        {
            try { return StreamAssist.Styles.SpacingRecipes.Compute(name) != null; }
            catch (InvalidOperationException) { return false; }
        }));
    }
}
