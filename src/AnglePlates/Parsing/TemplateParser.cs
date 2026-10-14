using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// Splits template text into directives and body tokens; CSharpBlockScanner finds where C# variables and C# blocks end
internal static class TemplateParser
{
    // Every opener starts with the same character, so the text scan looks for that one first
    private static readonly char[] _blockStarts = [TemplateSyntax.VariableOpen[0], TemplateSyntax.CodeOpen[0], TemplateSyntax.CommentOpen[0]];
    private static readonly (DirectiveKind Kind, string Prefix)[] _directivePrefixes = CreateDirectivePrefixes();

    public static ParsedTemplate Parse(string text, CancellationToken cancellationToken)
    {
        var reader = new TemplateReader(text.AsSpan());
        var directives = new List<DirectiveToken>();
        var tokens = new List<Token>();
        var errors = new List<TemplateError>();

        // Directives come first, one per line, and end at the first line that doesn't start with a known directive
        while (TryParseDirective(ref reader, out var directive))
        {
            directives.Add(directive);
        }

        int bodyStart = reader.Position;

        // Escaped openers can split literal text into several tokens, so a text run may add more than one
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryParseText(ref reader, tokens))
            {
                continue;
            }

            if (!TryParseBlock(text, ref reader, errors, out var token))
            {
                break;
            }

            tokens.Add(token);
        }

        return new ParsedTemplate(text, directives, tokens, errors, bodyStart);
    }

    private static (DirectiveKind Kind, string Prefix)[] CreateDirectivePrefixes()
    {
        var prefixes = new (DirectiveKind Kind, string Prefix)[DirectiveKindExtensions.All.Length];
        for (int i = 0; i < prefixes.Length; i++)
        {
            var kind = DirectiveKindExtensions.All[i];
            prefixes[i] = (kind, TemplateSyntax.DirectivePrefix + kind.GetName());
        }

        return prefixes;
    }

    private static bool TryParseBlock(string text, ref TemplateReader reader, List<TemplateError> errors, out Token token)
    {
        token = default;
        int openingStart = reader.Position;
        TokenType type;
        string closer;
        if (reader.IsNext(TemplateSyntax.VariableOpen, advancePast: true))
        {
            type = TokenType.Variable;
            closer = TemplateSyntax.VariableClose;
        }
        else if (reader.IsNext(TemplateSyntax.CodeOpen, advancePast: true))
        {
            type = TokenType.Code;
            closer = TemplateSyntax.CodeClose;
        }
        else if (reader.IsNext(TemplateSyntax.CommentOpen, advancePast: true))
        {
            type = TokenType.Comment;
            closer = TemplateSyntax.CommentClose;
        }
        else
        {
            return false;
        }

        // A marker right after the opener trims whitespace before the block
        int openingLength = reader.Position - openingStart;
        bool trimPrevious = reader.IsNext(TemplateSyntax.TrimMarker, advancePast: true);

        // C# variables and C# blocks end where the scanner says, skipping closers inside C# strings and comments. Comments
        // aren't C#, so they end at the first closer. A block with no closer reports the opener and consumes the rest.
        int start = reader.Position;
        bool closed;
        if (type == TokenType.Comment)
        {
            closed = reader.TryAdvanceTo(closer);
        }
        else
        {
            int closerStart = CSharpBlockScanner.FindCloser(text, start, type);
            closed = closerStart >= 0;
            if (closed)
            {
                reader.Advance(closerStart - start);
            }
        }

        if (!closed)
        {
            errors.Add(new TemplateError(DiagnosticDescriptors.UnclosedBlock, new TextSpan(openingStart, openingLength), closer));
            reader.AdvanceToEnd();
            return false;
        }

        // A marker right before the closer trims whitespace after the block and isn't part of the content
        int end = reader.Position;
        bool trimNext = end > start && reader.Text[end - 1] == TemplateSyntax.TrimMarker;
        if (trimNext)
        {
            end--;
        }

        reader.Advance(closer.Length);
        token = new Token(type, Trim(reader.Text, start, end), trimPrevious, trimNext);

        return true;
    }

    private static bool TryParseText(ref TemplateReader reader, List<Token> tokens)
    {
        int count = tokens.Count;
        int start = reader.Position;
        while (true)
        {
            if (!reader.TryAdvanceToAny(_blockStarts))
            {
                reader.AdvanceToEnd();
                break;
            }

            // The first character matched, but the next one decides whether this is an opener
            int openerLength = GetOpenerLength(ref reader);
            if (openerLength == 0)
            {
                reader.Advance(1);
                continue;
            }

            // Backslashes right before an opener escape it in pairs: each pair writes one backslash, and an odd one
            // left over makes the opener literal. Dropping the first half of the run keeps every token a slice of the text.
            int opener = reader.Position;
            int backslashes = CountBackslashes(reader.Text, start, opener);
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

    private static int GetOpenerLength(ref TemplateReader reader)
    {
        if (reader.IsNext(TemplateSyntax.VariableOpen))
        {
            return TemplateSyntax.VariableOpen.Length;
        }

        if (reader.IsNext(TemplateSyntax.CodeOpen))
        {
            return TemplateSyntax.CodeOpen.Length;
        }

        return reader.IsNext(TemplateSyntax.CommentOpen) ? TemplateSyntax.CommentOpen.Length : 0;
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
            tokens.Add(new Token(TokenType.Literal, TextSpan.FromBounds(start, end)));
        }
    }

    private static bool TryParseDirective(ref TemplateReader reader, out DirectiveToken directive)
    {
        directive = default;
        foreach (var (kind, prefix) in _directivePrefixes)
        {
            if (!reader.IsNext(prefix))
            {
                continue;
            }

            // A known name only counts when whitespace or the end of the text follows it
            int nameStart = reader.Position;
            int nameEnd = nameStart + prefix.Length;
            if (nameEnd < reader.Text.Length && TemplateSyntax.Whitespace.IndexOf(reader.Text[nameEnd]) < 0)
            {
                continue;
            }

            reader.Advance(prefix.Length);

            // Consume the whole line, including its terminator, so the body starts on the next line
            reader.AdvancePastLine();

            // Trimming a blank value runs past the line break, so keep its empty span right after the name
            var value = Trim(reader.Text, nameEnd, reader.Position);
            if (value.IsEmpty)
            {
                value = new TextSpan(nameEnd, 0);
            }

            directive = new DirectiveToken(kind, TextSpan.FromBounds(nameStart, nameEnd), value);

            return true;
        }

        return false;
    }

    // Returns the bounds without whitespace at either end
    private static TextSpan Trim(ReadOnlySpan<char> text, int start, int end)
    {
        while (start < end && TemplateSyntax.Whitespace.IndexOf(text[start]) >= 0)
        {
            start++;
        }

        while (end > start && TemplateSyntax.Whitespace.IndexOf(text[end - 1]) >= 0)
        {
            end--;
        }

        return TextSpan.FromBounds(start, end);
    }
}
