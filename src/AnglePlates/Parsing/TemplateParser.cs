using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// Splits template text into directives and body tokens; a block ends at the first exact match of its closer
internal static class TemplateParser
{
    private static readonly (DirectiveKind Kind, string Prefix)[] _directivePrefixes = CreateDirectivePrefixes();

    public static ParsedTemplate Parse(string text, CancellationToken cancellationToken)
    {
        var directives = new List<DirectiveToken>();
        var tokens = new List<Token>();
        var errors = new List<TemplateError>();
        int position = 0;

        // Directives come first, one per line, and end at the first line that doesn't start with a known directive
        while (TryParseDirective(text, ref position, out var directive))
        {
            directives.Add(directive);
        }

        int bodyStart = position;
        int literalStart = position;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryFindOpener(text, ref position, out int openerStart, out var type, out int level))
            {
                AddLiteral(tokens, literalStart, text.Length);
                break;
            }

            AddLiteral(tokens, literalStart, openerStart);
            if (!TryParseBlock(text, openerStart, type, level, ref position, tokens, errors))
            {
                break;
            }

            literalStart = position;
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

    // A run of '[' opens a block with its last MaxBracketLevel brackets, and the brackets before those stay literal text.
    // On success, position is where the block's content starts.
    private static bool TryFindOpener(string text, ref int position, out int openerStart, out TokenType type, out int level)
    {
        while (true)
        {
            int runStart = text.IndexOf(TemplateSyntax.OpenBracket, position);
            if (runStart < 0)
            {
                (openerStart, type, level) = (text.Length, TokenType.Literal, 0);
                return false;
            }

            int runEnd = runStart + 1;
            while (runEnd < text.Length && text[runEnd] == TemplateSyntax.OpenBracket)
            {
                runEnd++;
            }

            level = Math.Min(runEnd - runStart, TemplateSyntax.MaxBracketLevel);
            openerStart = runEnd - level;
            char marker = runEnd < text.Length ? text[runEnd] : '\0';
            type = marker switch
            {
                TemplateSyntax.CodeMarker => TokenType.Code,
                TemplateSyntax.CommentMarker => TokenType.Comment,
                _ => TokenType.Variable,
            };

            // A marker belongs to the opener; a C# variable has none
            position = type == TokenType.Variable ? runEnd : runEnd + 1;
            if (type != TokenType.Variable || level >= TemplateSyntax.MinVariableLevel)
            {
                return true;
            }
        }
    }

    // A block ends at the first exact match of its closer. Nothing inside it is read, so a closer inside a C# string
    // ends the block too; a higher bracket level is how a template avoids that.
    private static bool TryParseBlock(
        string text, int openerStart, TokenType type, int level, ref int position, List<Token> tokens, List<TemplateError> errors)
    {
        int contentStart = position;
        string closer = TemplateSyntax.GetCloser(type, level);

        // A marker right after the opener trims whitespace before the block
        bool trimPrevious = contentStart < text.Length && text[contentStart] == TemplateSyntax.TrimMarker;
        int start = trimPrevious ? contentStart + 1 : contentStart;
        int closerStart = text.IndexOf(closer, start, StringComparison.Ordinal);
        if (closerStart < 0)
        {
            // The rest of the text is consumed and the error makes the generator skip the template
            errors.Add(new TemplateError(DiagnosticDescriptors.UnclosedBlock, TextSpan.FromBounds(openerStart, contentStart), closer));
            position = text.Length;
            return false;
        }

        // A marker right before the closer trims whitespace after the block and isn't part of the content
        bool trimNext = closerStart > start && text[closerStart - 1] == TemplateSyntax.TrimMarker;
        tokens.Add(new Token(type, Trim(text, start, trimNext ? closerStart - 1 : closerStart), trimPrevious, trimNext));
        position = closerStart + closer.Length;

        return true;
    }

    // Adds the literal text between two blocks, if there is any
    private static void AddLiteral(List<Token> tokens, int start, int end)
    {
        if (start < end)
        {
            tokens.Add(new Token(TokenType.Literal, TextSpan.FromBounds(start, end)));
        }
    }

    private static bool TryParseDirective(string text, ref int position, out DirectiveToken directive)
    {
        directive = default;
        foreach (var (kind, prefix) in _directivePrefixes)
        {
            if (!text.AsSpan(position).StartsWith(prefix.AsSpan(), StringComparison.Ordinal))
            {
                continue;
            }

            // A known name only counts when whitespace or the end of the text follows it
            int nameEnd = position + prefix.Length;
            if (nameEnd < text.Length && TemplateSyntax.Whitespace.IndexOf(text[nameEnd]) < 0)
            {
                continue;
            }

            // The value runs to the end of the line, and a blank one is an empty span right after the name
            int lineEnd = TemplateSyntax.LineEnd(text, nameEnd);
            var value = Trim(text, nameEnd, lineEnd);
            if (value.IsEmpty)
            {
                value = new TextSpan(nameEnd, 0);
            }

            directive = new DirectiveToken(kind, TextSpan.FromBounds(position, nameEnd), value);

            // The body starts on the next line
            position = TemplateSyntax.NextLineStart(text, lineEnd);

            return true;
        }

        return false;
    }

    // Returns the bounds without whitespace at either end
    private static TextSpan Trim(string text, int start, int end)
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
