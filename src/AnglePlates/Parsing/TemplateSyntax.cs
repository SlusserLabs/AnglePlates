namespace SlusserLabs.AnglePlates;

// The fixed spelling of AnglePlates templates. An opener repeats '[' one to four times, and its closer repeats ']' as often.
internal static class TemplateSyntax
{
    public const char OpenBracket = '[';
    public const char CloseBracket = ']';
    public const char CodeMarker = '%';
    public const char CommentMarker = '*';

    // Four is plenty, and a fixed limit keeps the parser from handling runs of any length
    public const int MaxBracketLevel = 4;

    // One '[' followed by anything but a marker is ordinary text, so C# variables start at two
    public const int MinVariableLevel = 2;

    public const char TrimMarker = '-';
    public const char DirectivePrefix = '#';

    // Trim markers and directive values trim these characters, and a directive name must be followed by one of them
    public const string Whitespace = " \t\r\n";

    // Closers by level, built once so the parser never allocates one per block
    private static readonly string[] _variableClosers = CreateClosers(string.Empty);
    private static readonly string[] _codeClosers = CreateClosers(CodeMarker.ToString());
    private static readonly string[] _commentClosers = CreateClosers(CommentMarker.ToString());

    // The closer for a block of this kind and level, such as "]]]" or "%]]"
    public static string GetCloser(TokenType type, int level)
    {
        return type == TokenType.Code ? _codeClosers[level] : type == TokenType.Comment ? _commentClosers[level] : _variableClosers[level];
    }

    // CR, LF, and CRLF all end a line, in directives and everywhere else
    public static bool IsLineBreak(char value) => value is '\r' or '\n';

    // Where the line that starts at the position ends, before its line break
    public static int LineEnd(string text, int start)
    {
        int end = start;
        while (end < text.Length && !IsLineBreak(text[end]))
        {
            end++;
        }

        return end;
    }

    // Where the next line starts after a line that ends at lineEnd; skips one CR, LF, or CRLF
    public static int NextLineStart(string text, int lineEnd)
    {
        if (lineEnd < text.Length && text[lineEnd] == '\r')
        {
            lineEnd++;
        }

        if (lineEnd < text.Length && text[lineEnd] == '\n')
        {
            lineEnd++;
        }

        return lineEnd;
    }

    private static string[] CreateClosers(string marker)
    {
        var closers = new string[MaxBracketLevel + 1];
        for (int level = 1; level <= MaxBracketLevel; level++)
        {
            closers[level] = marker + new string(CloseBracket, level);
        }

        return closers;
    }
}
