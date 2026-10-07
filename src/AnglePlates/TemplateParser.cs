using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace SlusserLabs.AnglePlates;

internal class TemplateParser
{
    private readonly TemplateParserOptions _options;
    private readonly ReadOnlyMemory<char> _anyBlockStart;
    private readonly (DirectiveKind Kind, ReadOnlyMemory<char> Prefix)[] _directivePrefixes;

    public TemplateParser(TemplateParserOptions options)
    {
        _options = options;
        _anyBlockStart = new char[] { _options.VariableStart.Span[0], _options.CodeStart.Span[0], _options.CommentStart.Span[0] };

        var directiveStart = _options.DirectiveStart.ToString();
        _directivePrefixes = new (DirectiveKind, ReadOnlyMemory<char>)[DirectiveKindExtensions.All.Length];
        for (int i = 0; i < _directivePrefixes.Length; i++)
        {
            var kind = DirectiveKindExtensions.All[i];
            _directivePrefixes[i] = (kind, (directiveStart + kind.GetName()).AsMemory());
        }
    }

    public TemplateParseResult Parse(ReadOnlySpan<char> text)
    {
        var reader = new SpanReader<char>(text);
        var result = new TemplateParseResult();
        var tokens = result.Tokens;
        Token token;

        // Directives (must be at the top, line-based)
        while (TryParseDirective(ref reader, out token))
        {
            DebugToken(text, token);
            tokens.Add(token);
        }

        result.DirectiveSectionEnd = reader.Position;

        // Find anything that is between blocks or is a block; escapes can split literal text into several tokens
        while (true)
        {
            int count = tokens.Count;
            if (TryParseText(ref reader, tokens))
            {
                for (int i = count; i < tokens.Count; i++)
                {
                    DebugToken(text, tokens[i]);
                }

                continue;
            }

            if (!TryParseBlock(ref reader, result.Errors, out token))
            {
                break;
            }

            DebugToken(text, token);
            tokens.Add(token);
        }

        return result;
    }

    private bool TryParseBlock(ref SpanReader<char> reader, List<TemplateParseError> errors, out Token token)
    {
        token = default;
        int openingStart = reader.Position;
        ReadOnlySpan<char> blockEnd;
        if (reader.IsNext(_options.VariableStart.Span, advancePast: true))
        {
            token.Type = TokenType.Variable;
            blockEnd = _options.VariableEnd.Span;
        }
        else if (reader.IsNext(_options.CodeStart.Span, advancePast: true))
        {
            token.Type = TokenType.Code;
            blockEnd = _options.CodeEnd.Span;
        }
        else if (reader.IsNext(_options.CommentStart.Span, advancePast: true))
        {
            token.Type = TokenType.Comment;
            blockEnd = _options.CommentEnd.Span;
        }
        else
        {
            return false;
        }

        // A marker right after the opener trims whitespace before the block
        int openingLength = reader.Position - openingStart;
        if (reader.IsNext(_options.WhitespaceControl.Span, advancePast: true))
        {
            token.TrimPrevious = true;
        }

        // An unclosed block reports the opener and consumes the rest of the text
        int start = reader.Position;
        if (!reader.TryAdvanceTo(blockEnd))
        {
            errors.Add(new TemplateParseError(openingStart, openingLength, blockEnd.ToString()));
            reader.AdvanceToEnd();
            return false;
        }

        // A marker right before the closer trims whitespace after the block and isn't part of the content
        int end = reader.Position;
        if (reader.Span.Slice(start, end - start).EndsWith(_options.WhitespaceControl.Span))
        {
            end -= _options.WhitespaceControl.Length;
            token.TrimNext = true;
        }

        reader.Advance(blockEnd.Length);
        TrimToken(reader.Span, start, end, ref token);

        return true;
    }

    private bool TryParseText(ref SpanReader<char> reader, List<Token> tokens)
    {
        int count = tokens.Count;
        int start = reader.Position;
        while (true)
        {
            // Fast look for just one char
            if (!reader.TryAdvanceToAny(_anyBlockStart.Span))
            {
                reader.AdvanceToEnd();
                break;
            }

            // We've matched on one char, but is it actually a block start?
            int openerLength = GetBlockStartLength(ref reader);
            if (openerLength == 0)
            {
                reader.Advance(1);
                continue;
            }

            // Backslashes right before an opener escape it in pairs: each pair writes one backslash, and an odd one
            // left over makes the opener literal. Dropping the first half of the run keeps every token a slice of the text.
            int opener = reader.Position;
            int backslashes = CountBackslashes(reader.Span, start, opener);
            int runStart = opener - backslashes;
            int kept = backslashes / 2;
            if (backslashes % 2 == 0)
            {
                AddLiteral(tokens, start, runStart + kept);
                start = opener;
                break;
            }

            // The escaped opener starts the next literal after the surviving backslashes, and scanning resumes past it
            AddLiteral(tokens, start, runStart);
            start = opener - kept;
            reader.Advance(openerLength);
        }

        AddLiteral(tokens, start, reader.Position);

        return tokens.Count > count;
    }

    private int GetBlockStartLength(ref SpanReader<char> reader)
    {
        if (reader.IsNext(_options.VariableStart.Span))
        {
            return _options.VariableStart.Length;
        }

        if (reader.IsNext(_options.CodeStart.Span))
        {
            return _options.CodeStart.Length;
        }

        return reader.IsNext(_options.CommentStart.Span) ? _options.CommentStart.Length : 0;
    }

    private static int CountBackslashes(ReadOnlySpan<char> text, int start, int end)
    {
        int index = end;
        while (index > start && text[index - 1] == '\\')
        {
            index--;
        }

        return end - index;
    }

    private static void AddLiteral(List<Token> tokens, int start, int end)
    {
        if (start < end)
        {
            tokens.Add(new Token { Type = TokenType.Literal, Start = start, End = end });
        }
    }

    private bool TryParseDirective(ref SpanReader<char> reader, out Token token)
    {
        token = default;
        foreach (var (kind, prefix) in _directivePrefixes)
        {
            if (!reader.IsNext(prefix.Span))
            {
                continue;
            }

            // A known name only counts when whitespace or the end of the text follows it
            int nameStart = reader.Position;
            int nameEnd = nameStart + prefix.Length;
            if (nameEnd < reader.Span.Length && _options.Whitespace.Span.IndexOf(reader.Span[nameEnd]) < 0)
            {
                continue;
            }

            reader.Advance(prefix.Length);

            // Consume the whole line, including its terminator, so the body starts on the next line; the last line may have none
            if (!reader.TryAdvancePast('\n'))
            {
                reader.AdvanceToEnd();
            }

            token = new Token
            {
                Type = TokenType.Directive,
                Directive = kind,
                NameStart = nameStart,
                NameEnd = nameEnd
            };
            TrimToken(reader.Span, nameEnd, reader.Position, ref token);

            // Trimming a blank value runs past the line break, so keep its empty span right after the name
            if (token.Start == token.End)
            {
                token.Start = nameEnd;
                token.End = nameEnd;
            }

            return true;
        }

        return false;
    }

    private void TrimToken(ReadOnlySpan<char> text, int start, int end, ref Token token)
    {
        while (start < end && _options.Whitespace.Span.IndexOf(text[start]) >= 0)
        {
            start++;
        }

        while (end > start && _options.Whitespace.Span.IndexOf(text[end - 1]) >= 0)
        {
            end--;
        }

        token.Start = start;
        token.End = end;
    }

    [Conditional("DEBUG")]
    private static void DebugToken(scoped ReadOnlySpan<char> span, Token token)
    {
        Debug.WriteLine(token.Type);
        Debug.WriteLine($"  Start:        {token.Start}");
        Debug.WriteLine($"  End:          {token.End}");
        Debug.WriteLine($"  TrimPrevious: {token.TrimPrevious}");
        Debug.WriteLine($"  TrimNext:     {token.TrimNext}");

        var content = span.Slice(token.Start, token.End - token.Start);
        Debug.WriteLine($"  Content: {SyntaxFactory.Literal(content.ToString()).ToFullString()}");

        Debug.WriteLine(string.Empty);
    }
}
