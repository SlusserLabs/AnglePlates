using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// One directive line; Name covers the '#' and the name, and Value is the trimmed rest of the line
internal readonly struct DirectiveToken
{
    public DirectiveToken(DirectiveKind kind, TextSpan name, TextSpan value)
    {
        Kind = kind;
        Name = name;
        Value = value;
    }

    public DirectiveKind Kind { get; }

    public TextSpan Name { get; }

    // A blank value is an empty span right after the name
    public TextSpan Value { get; }
}
