namespace SlusserLabs.AnglePlates;

// The fixed spelling of AnglePlates templates: block delimiters, the trim marker, the directive prefix, and whitespace
internal static class TemplateSyntax
{
    public const string VariableOpen = "[[";
    public const string VariableClose = "]]";
    public const string CodeOpen = "[%";
    public const string CodeClose = "%]";
    public const string CommentOpen = "[*";
    public const string CommentClose = "*]";
    public const char TrimMarker = '-';
    public const char DirectivePrefix = '#';

    // Trim markers and directive values trim these characters, and a directive name must be followed by one of them
    public const string Whitespace = " \t\r\n";

    // Used to find the end of the line that follows the directive section
    public static bool IsLineBreak(char value) => value is '\r' or '\n';
}
