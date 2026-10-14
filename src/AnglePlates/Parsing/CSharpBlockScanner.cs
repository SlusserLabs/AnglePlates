using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlusserLabs.AnglePlates;

// Finds the closing delimiter of a C# variable or C# block by reading it with Roslyn's lexer.
//
// A plain text search stops at the first "]]" or "%]", even inside a C# string. The lexer reads strings, character
// literals, and /* */ comments as single pieces, so a closer inside one is skipped. The rules:
// - A C# variable ends at a "]" token followed right away by another "]", once every "[" in the variable is closed.
// - A C# block ends at a "%" token followed right away by "]". C# never writes those two together.
// - A // comment or # directive runs to the end of its line, but the first closer on that line still ends the block,
//   so [% // note %] and [% #if DEBUG %] work the way they look.
// - Broken C#, such as a string missing its closing quote, ends at its first closer, and the compiler reports the typo.
//   A verbatim or raw string with no end, or a /* comment with no end, runs to the end of every window, so it gets that
//   rule only when fewer than 16 closers follow it; otherwise there's no closer.
// - When no closer follows the rules, or the C# hides more closers than the last window holds, there's no closer.
// - The scanner reads each # directive line itself and starts the lexer again on the next line. The generator can't
//   know the consumer's preprocessor symbols, and a lexer that saw "#if DEBUG" would read every line after it, to the
//   end of the template, as disabled text. Doing that for every [% #if %] block would make parsing quadratic.
internal static class CSharpBlockScanner
{
    // Fixed options keep scanning independent of the consumer's project. Preview lexes every literal form the compiler
    // knows, and DocumentationMode.None makes /// an ordinary line comment.
    private static readonly CSharpParseOptions _lexerOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Preview)
        .WithDocumentationMode(DocumentationMode.None);

    // The most closers a window can hold. Valid C# decides within a window or two; broken C# that runs to the end of the
    // text gives up here, so every broken block lexes a bounded stretch instead of the rest of the template. Valid C# that
    // hides 16 or more closers in strings, comments, or brackets gives up too.
    private const int _maxWindowClosers = 16;

    // Scan returns this when the window ended before the scan could decide
    private const int _needMore = -2;

    // Returns where the closer starts, or -1 when the C# runs to the end of the text without one or hides more closers
    // than the last window holds. The parser reports -1 as an unclosed block. The block type is Variable or Code; anything
    // else is scanned as a C# block.
    //
    // The lexer reads a window that ends right after a closer that a plain text search finds: the first one, then the
    // second, the fourth, and so on. A token that ends inside the window lexes the same as it would in the whole text, and
    // the window grows only when a token reaches its end. Broken C#, such as a /* comment with no end, would otherwise send
    // the lexer to the end of the template for every block. An unclosed interpolation hole also nests Roslyn one level
    // deeper for each later $"{ it reads, which can overflow the stack.
    public static int FindCloser(string text, int contentStart, TokenType blockType)
    {
        bool isVariable = blockType == TokenType.Variable;
        string closer = isVariable ? TemplateSyntax.VariableClose : TemplateSyntax.CodeClose;
        int closers = 0;
        int searchFrom = contentStart;
        int limit = contentStart;

        for (int target = 1; ; target *= 2)
        {
            // Closers can overlap in "]]]", so each search starts one character after the last match
            while (closers < target)
            {
                int next = text.IndexOf(closer, searchFrom, StringComparison.Ordinal);
                if (next < 0)
                {
                    limit = text.Length;
                    break;
                }

                closers++;
                searchFrom = next + 1;
                limit = next + closer.Length;
            }

            int found = Scan(text, contentStart, limit, closer, isVariable);
            if (found != _needMore)
            {
                return found;
            }

            if (target == _maxWindowClosers)
            {
                return -1;
            }
        }
    }

    // Scans the text from contentStart up to limit. Returns where the closer starts, -1 when limit is the end of the
    // text and no closer comes first, or _needMore when the window ends before the answer is certain.
    private static int Scan(string text, int contentStart, int limit, string closer, bool isVariable)
    {
        bool windowed = limit < text.Length;
        int bracketDepth = 0;
        int position = contentStart;

        while (true)
        {
            // A directive line ends at its first closer; without one, scanning goes on from the line break after it
            int found = ScanTrivia(text, position, limit, closer, out int directive);
            if (found != -1)
            {
                return found;
            }

            if (directive >= 0)
            {
                // A window ends with a closer, so a directive line cut off by the window always holds one
                int lineEnd = FindLineEnd(text, directive, limit);
                found = text.IndexOf(closer, directive, lineEnd - directive, StringComparison.Ordinal);
                if (found >= 0)
                {
                    return found;
                }

                position = lineEnd;
                continue;
            }

            // The lexer reads one token at a time, so it reads no further than this loop asks it to
            foreach (var token in SyntaxFactory.ParseTokens(text.Substring(position, limit - position), 0, position, _lexerOptions))
            {
                // A string or comment that reaches the window's end might keep going in the whole text
                if (windowed && token.FullSpan.End == limit)
                {
                    return _needMore;
                }

                // ScanTrivia already checked the leading trivia, except a /* comment with no end, which runs to the end of the text
                foreach (var trivia in token.LeadingTrivia)
                {
                    found = FindCloserInTrivia(text, trivia, closer);
                    if (found >= 0)
                    {
                        return found;
                    }
                }

                // Broken C# keeps the plain rule: the first closer inside it ends the block
                if (token.ContainsDiagnostics)
                {
                    found = text.IndexOf(closer, token.SpanStart, token.Span.Length, StringComparison.Ordinal);
                    if (found >= 0)
                    {
                        return found;
                    }
                }

                switch (token.Kind())
                {
                    case SyntaxKind.OpenBracketToken when isVariable:
                        bracketDepth++;
                        break;

                    case SyntaxKind.CloseBracketToken when isVariable:
                        if (bracketDepth == 0 && IsAt(text, token.Span.End, ']'))
                        {
                            return token.SpanStart;
                        }

                        // A stray "]" is the compiler's problem, so it can't take the depth below zero
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                        break;

                    case SyntaxKind.PercentToken when !isVariable && IsAt(text, token.Span.End, ']'):
                        return token.SpanStart;
                }

                foreach (var trivia in token.TrailingTrivia)
                {
                    found = FindCloserInTrivia(text, trivia, closer);
                    if (found >= 0)
                    {
                        return found;
                    }
                }

                // Check the trivia before the next token here, so the lexer never reads a directive
                found = ScanTrivia(text, token.FullSpan.End, limit, closer, out directive);
                if (found != -1)
                {
                    return found;
                }

                if (directive >= 0)
                {
                    break;
                }
            }

            // The lexer ends with an end-of-file token, which returns above when the window isn't the whole text, so only
            // a directive gets here with more to read. A directive line holds at least its "#", so each pass moves forward.
            if (directive < 0)
            {
                return -1;
            }

            position = directive;
        }
    }

    // Counts the braces in a C# block for indenting the generated code. Closed is how many braces the block closes that
    // opened before it, and Opened is how many it leaves open. Braces in strings and comments don't count.
    public static (int Closed, int Opened) CountBraces(string code)
    {
        int depth = 0;
        int lowest = 0;
        foreach (var token in SyntaxFactory.ParseTokens(code, options: _lexerOptions))
        {
            if (token.IsKind(SyntaxKind.OpenBraceToken))
            {
                depth++;
            }
            else if (token.IsKind(SyntaxKind.CloseBraceToken))
            {
                depth--;
                lowest = Math.Min(lowest, depth);
            }
        }

        return (-lowest, depth - lowest);
    }

    // True when the last thing in the code is a // comment, which would also comment out anything written after it on the same line
    public static bool EndsWithLineComment(string code)
    {
        SyntaxTrivia last = default;
        foreach (var token in SyntaxFactory.ParseTokens(code, options: _lexerOptions))
        {
            foreach (var trivia in token.LeadingTrivia)
            {
                last = trivia;
            }

            // A token after the leading trivia is the new last thing; the end-of-file token is empty
            if (!token.IsKind(SyntaxKind.EndOfFileToken))
            {
                last = default;
            }

            foreach (var trivia in token.TrailingTrivia)
            {
                last = trivia;
            }
        }

        return last.IsKind(SyntaxKind.SingleLineCommentTrivia);
    }

    // Reads the whitespace, line breaks, and comments from position, which the lexer would read as trivia before the
    // next token. Returns the start of a closer in a // comment, _needMore when a /* comment runs past the window, or -1.
    // directive is where a # directive starts, or -1 when a token or a /* comment with no end comes first; the lexer
    // handles those.
    private static int ScanTrivia(string text, int position, int limit, string closer, out int directive)
    {
        directive = -1;
        int index = position;
        while (index < limit)
        {
            char current = text[index];
            if (SyntaxFacts.IsWhitespace(current) || SyntaxFacts.IsNewLine(current))
            {
                index++;
                continue;
            }

            if (current == '#')
            {
                directive = index;
                return -1;
            }

            if (current != '/' || index + 1 == limit)
            {
                return -1;
            }

            if (text[index + 1] == '/')
            {
                int lineEnd = FindLineEnd(text, index, limit);
                int found = text.IndexOf(closer, index, lineEnd - index, StringComparison.Ordinal);
                if (found >= 0)
                {
                    return found;
                }

                index = lineEnd;
                continue;
            }

            if (text[index + 1] != '*')
            {
                return -1;
            }

            // A comment with no end in the window might end later in the text
            int commentEnd = text.IndexOf("*/", index + 2, limit - index - 2, StringComparison.Ordinal);
            if (commentEnd < 0)
            {
                return limit < text.Length ? _needMore : -1;
            }

            index = commentEnd + 2;
        }

        return -1;
    }

    // Whitespace, line breaks, and well-formed /* */ comments can't end a block. The rest of the trivia the lexer reads
    // here, // comments and a /* comment with no end, ends at its first closer.
    private static int FindCloserInTrivia(string text, SyntaxTrivia trivia, string closer)
    {
        bool canHideCloser = trivia.IsKind(SyntaxKind.WhitespaceTrivia)
            || trivia.IsKind(SyntaxKind.EndOfLineTrivia)
            || (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && !trivia.ContainsDiagnostics);

        return canHideCloser ? -1 : text.IndexOf(closer, trivia.SpanStart, trivia.Span.Length, StringComparison.Ordinal);
    }

    // Returns the first line break at or after start, or limit when none comes first
    private static int FindLineEnd(string text, int start, int limit)
    {
        int index = start;
        while (index < limit && !SyntaxFacts.IsNewLine(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsAt(string text, int index, char value) => index < text.Length && text[index] == value;
}
