using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Chatbot.Model;



// a regex that has to match right where the previous chat block stopped (not somewhere later in the message)
// compiled once and reused until the pattern or case setting changes
public sealed class ChatPattern
{
    // private



    // a pattern that takes longer than this is treated as not matching (protects against runaway regexes)
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    private string? _pattern;
    private bool _ignoreCase;
    private Regex? _regex;
    private string[] _groupNames = [];



    // public api



    // the match at state.Position, or null; a broken pattern is logged once and never matches
    public Match? MatchAt(string pattern, bool ignoreCase, string text, int position, string blockName)
    {
        if (pattern != _pattern || ignoreCase != _ignoreCase) Compile(pattern, ignoreCase, blockName);
        if (_regex == null) return null;

        try
        {
            var match = _regex.Match(text, position);
            return match.Success ? match : null;
        }
        catch (RegexMatchTimeoutException)
        {
            AppLog.WriteWarning($"{blockName}: /{pattern}/ too slow, skipped");
            return null;
        }
    }



    // the pattern's named groups that matched, e.g. (?<amount>\d+) -> ("amount", "50")
    public IEnumerable<KeyValuePair<string, string>> NamedGroups(Match match) =>
        _groupNames
            .Where(name => match.Groups[name].Success)
            .Select(name => new KeyValuePair<string, string>(name, match.Groups[name].Value));



    // the named groups a pattern has, e.g. (?<amount>\d+) -> amount, without matching anything (none if it's invalid)
    public static IReadOnlyList<string> GroupNames(string pattern)
    {
        try
        {
            return new Regex(pattern).GetGroupNames().Where(name => !int.TryParse(name, out _)).ToList();
        }
        catch (ArgumentException)
        {
            return [];
        }
    }



    // helpers



    private void Compile(string pattern, bool ignoreCase, string blockName)
    {
        _pattern = pattern;
        _ignoreCase = ignoreCase;
        _regex = null;
        _groupNames = [];

        try
        {
            // \G pins the match to the start position, so "lurk" doesn't match "!hi lurk"
            var options = RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            _regex = new Regex($@"\G(?:{pattern})", options, Timeout);
            _groupNames = _regex.GetGroupNames().Where(name => !int.TryParse(name, out _)).ToArray();
        }
        catch (ArgumentException ex)
        {
            AppLog.WriteError($"{blockName}: bad regex /{pattern}/ ({ex.Message})");
        }
    }
}
