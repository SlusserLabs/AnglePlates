using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace SlusserLabs.AnglePlates;

internal class TemplateParser
{
    private readonly TemplateParserOptions _options;
    private readonly ReadOnlyMemory<char> _anyBlockStart;

    public TemplateParser(TemplateParserOptions options)
    {
        _options = options;
        _anyBlockStart = new char[] { _options.VariableStart.Span[0], _options.CodeStart.Span[0], _options.CommentStart.Span[0] };
    }

    public List<Token> Parse(ReadOnlySpan<char> text)
    {
        var reader = new SpanReader<char>(text);
        var tokens = new List<Token>();
        Token token;

        // Directives (must be at the top, line-based)
        while (TryParseDirective(ref reader, out token))
        {
            DebugToken(text, token);
            tokens.Add(token);
        }

        // Find anything that is between blocks or is a block
        while (TryParseText(ref reader, out token) || TryParseBlock(ref reader, out token))
        {
            DebugToken(text, token);
            tokens.Add(token);
        }

        // Anything remaining is text
        if (!reader.End)
        {
            int start = reader.Position;
            reader.AdvanceToEnd();
            token = new Token
            {
                Type = TokenType.Literal,
                Start = start,
                End = reader.Position
            };

            DebugToken(text, token);
            tokens.Add(token);
        }

        return tokens;
    }

    private bool TryParseBlock(ref SpanReader<char> reader, out Token token)
    {
        token = default;
        ReadOnlySpan<char> blockEnd = ReadOnlySpan<char>.Empty;
        int start = reader.Position;

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
                // Unclosed block?
                reader.AdvanceToEnd();
                end = reader.Position;
            }

            token.Start = reader.Span.TrimStartPosition(start, _options.Whitespace.Span);
            token.End = reader.Span.TrimEndPosition(end, _options.Whitespace.Span);
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
            if (reader.TryAdvanceToAny(_anyBlockStart.Span, advancePastDelimiter: false) && start != reader.Position)
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
        if (reader.IsNext(_options.DirectiveStart.Span, advancePast: true))
        {
            var start = reader.Position;
            if (!reader.TryReadTo(out _, '\n', advancePastDelimiter: true))
            {
                // No line break? Can only be EOF
                reader.AdvanceToEnd();
            }

            token = new Token
            {
                Type = TokenType.Directive,
                Start = reader.Span.TrimStartPosition(start, _options.Whitespace.Span),
                End = reader.Span.TrimEndPosition(reader.Position, _options.Whitespace.Span)
            };

            return true;
        }

        return false;
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
