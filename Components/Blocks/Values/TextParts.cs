using System;
using System.Collections.Generic;
using System.Linq;

namespace StreamAssist.Components.Blocks.Values;



// splitting a text into parts, the same way for Get Part, Text Length (words) and For Each
public static class TextParts
{
    // the parts between each separator, trimmed, empty ones left out. An empty (or only spaces) separator splits
    // between words, however many spaces there are
    public static List<string> Split(string text, string separator)
    {
        var parts = separator.Trim() == ""
            ? text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            : text.Split(separator.Trim(), StringSplitOptions.None);

        return parts.Select(p => p.Trim()).Where(p => p != "").ToList();
    }
}
