using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// One piece of the template body; a block's span covers its trimmed content without the delimiters
internal readonly struct Token
{
    public Token(TokenType type, TextSpan span, bool trimPrevious = false, bool trimNext = false)
    {
        Type = type;
        Span = span;
        TrimPrevious = trimPrevious;
        TrimNext = trimNext;
    }

    public TokenType Type { get; }

    public TextSpan Span { get; }

    // A trim marker after the opener removes whitespace from the end of the literal before the block
    public bool TrimPrevious { get; }

    // A trim marker before the closer removes whitespace from the start of the literal after the block
    public bool TrimNext { get; }
}
