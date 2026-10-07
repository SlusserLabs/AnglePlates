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

        // Find anything that is between blocks or is a block
        while (TryParseText(ref reader, out token) || TryParseBlock(ref reader, result.Errors, out token))
        {
            DebugToken(text, token);
            tokens.Add(token);
        }

        return result;
    }

    private bool TryParseBlock(ref SpanReader<char> reader, List<TemplateParseError> errors, out Token token)
    {
        token = default;
        ReadOnlySpan<char> blockEnd = ReadOnlySpan<char>.Empty;
        int start = reader.Position;
        int openingStart = start;

        if (reader.IsNext(_options.VariableStart.Span, advancePast: true))
        {
            start = reader.Position;
            token = new Token { Type = TokenType.Variable };
            blockEnd = _options.VariableEnd.Span;
        }
        else if (reader.IsNext(_options.CodeStart.Span, advancePast: true))
        {
            start = reader.Position;
            token = new Token { Type = TokenType.Code };
            blockEnd = _options.CodeEnd.Span;
        }
        else if (reader.IsNext(_options.CommentStart.Span, advancePast: true))
        {
            start = reader.Position;
            token = new Token { Type = TokenType.Comment };
            blockEnd = _options.CommentEnd.Span;
        }

        // Find something?
        if (blockEnd.Length > 0)
        {
            int openingLength = reader.Position - openingStart;
            // Whitespace indicator?
            if (reader.IsNext(_options.WhitespaceControl.Span, advancePast: true))
            {
                start = reader.Position;
                token.TrimPrevious = true;
            }

            int end;
            if (reader.TryAdvanceTo(blockEnd, advancePastDelimiter: false))
            {
                // Whitespace indicator?
                end = reader.Position;
                if (reader.Span.Slice(start, reader.Position - start).EndsWith(_options.WhitespaceControl.Span))
                {
                    // Exclude the trailing whitespace-control marker
                    end -= _options.WhitespaceControl.Length;
                    token.TrimNext = true;
                }

                reader.Advance(blockEnd.Length); // Now consume end block
            }
            else
            {
                errors.Add(new TemplateParseError(openingStart, openingLength, blockEnd.ToString()));
                reader.AdvanceToEnd();
                return false;
            }

            TrimToken(reader.Span, start, end, ref token);
            return true;
        }

        return false;
    }

    private bool TryParseText(ref SpanReader<char> reader, out Token token)
    {
        token = default;
        int start = reader.Position;
        while (!reader.End)
        {
            // Fast look for just one char
            if (reader.TryAdvanceToAny(_anyBlockStart.Span, advancePastDelimiter: false))
            {
                // We've matched on one char, but is it actually a block start?
                if (reader.IsNext(_options.VariableStart.Span) || reader.IsNext(_options.CodeStart.Span) || reader.IsNext(_options.CommentStart.Span))
                {
                    break;
                }

                // Not actually a token, advance and keep going
                reader.Advance(1);
                continue;
            }

            reader.AdvanceToEnd();
            break;
        }

        if (start != reader.Position)
        {
            token = new Token
            {
                Type = TokenType.Literal,
                Start = start,
                End = reader.Position
            };

            return true;
        }

        return false;
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

            // Consume the whole line, including its terminator, so the body starts on the next line
            if (!reader.TryReadTo(out _, '\n', advancePastDelimiter: true))
            {
                // No line break? Can only be EOF
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
