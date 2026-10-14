using System;

namespace SlusserLabs.AnglePlates;

// Walks forward through template text without allocating
internal ref struct TemplateReader
{
    private readonly ReadOnlySpan<char> _text;
    private int _position;

    public TemplateReader(ReadOnlySpan<char> text)
    {
        _text = text;
    }

    public readonly ReadOnlySpan<char> Text => _text;

    public readonly int Position => _position;

    public void Advance(int count)
    {
        _position += count;
    }

    public void AdvanceToEnd()
    {
        _position = _text.Length;
    }

    public bool IsNext(string value, bool advancePast = false)
    {
        if (!_text.Slice(_position).StartsWith(value.AsSpan()))
        {
            return false;
        }

        if (advancePast)
        {
            Advance(value.Length);
        }

        return true;
    }

    public bool IsNext(char value, bool advancePast = false)
    {
        if (_position >= _text.Length || _text[_position] != value)
        {
            return false;
        }

        if (advancePast)
        {
            Advance(1);
        }

        return true;
    }

    // Stops at the delimiter; returns false without moving when it isn't found
    public bool TryAdvanceTo(string delimiter)
    {
        return TryAdvanceBy(_text.Slice(_position).IndexOf(delimiter.AsSpan()));
    }

    // Stops at the first of any delimiter; returns false without moving when none is found
    public bool TryAdvanceToAny(ReadOnlySpan<char> delimiters)
    {
        return TryAdvanceBy(_text.Slice(_position).IndexOfAny(delimiters));
    }

    // Moves past the end of the current line, including its CR, LF, or CRLF; the last line may have none
    public void AdvancePastLine()
    {
        int index = _text.Slice(_position).IndexOfAny(TemplateSyntax.LineBreaks.AsSpan());
        if (index < 0)
        {
            AdvanceToEnd();
            return;
        }

        Advance(index + 1);
        if (_text[_position - 1] == '\r')
        {
            IsNext('\n', advancePast: true);
        }
    }

    private bool TryAdvanceBy(int index)
    {
        if (index < 0)
        {
            return false;
        }

        Advance(index);

        return true;
    }
}
