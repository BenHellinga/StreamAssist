namespace StreamAssist.Components.Chatbot.Model;



// how chat blocks read text: a plain row or Parse Parameter goes by whole words (spaces only matter between words),
// a /regex/ row is matched exactly as written
//   "lurk" matches "lurk", "lurk ahaa" and "lurk!", never "lurker"
//   after a word the spaces before the next piece are skipped ("pay" then "all" matches "pay all"),
//   after a symbol nothing is skipped ("!" then "lurk" matches "!lurk", not "! lurk")
public static class ChatText
{
    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';



    // a /regex/ row (slashes at both ends, "//" is the empty regex: matches nothing, so always), body = what's between
    public static bool TryGetRegex(string pattern, out string body)
    {
        var trimmed = pattern.Trim();
        var isRegex = trimmed.Length >= 2 && trimmed[0] == '/' && trimmed[^1] == '/';
        body = isRegex ? trimmed[1..^1] : "";
        return isRegex;
    }



    // where the next piece starts reading: after a word, past the spaces in between; after a symbol (or at the start),
    // right here. null if the next piece starts with a word but is glued onto the previous one (the middle of a word)
    public static int? SkipGap(string text, int position, bool nextStartsWithWord)
    {
        if (position == 0 || !IsWordChar(text[position - 1])) return position;

        var start = position;
        while (start < text.Length && char.IsWhiteSpace(text[start])) start++;

        if (start == position && nextStartsWithWord && start < text.Length && IsWordChar(text[start])) return null;
        return start;
    }



    // a piece ending at `end` doesn't cut a word in half (it ends in a symbol, or the next character isn't a word character)
    public static bool EndsCleanly(string text, int end, string piece) =>
        piece == "" || !IsWordChar(piece[^1]) || end == text.Length || !IsWordChar(text[end]);



    // the text a step adds to a command's description, with a space in front where two words meet ("!pay" + "all" -> " all"),
    // a {parameter} counting as a word on both sides ("!pay {user} {amount}")
    public static string Join(string before, string piece) =>
        before != "" && piece != "" && (IsWordChar(before[^1]) || before[^1] == '}') && (IsWordChar(piece[0]) || piece[0] == '{') ? " " + piece : piece;
}
