using System;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Token-level parser results: types, spans of the trimmed content, trim flags, and unclosed-block errors
public sealed class TemplateParserTests
{
    [Test]
    [Arguments("a[[ b ]]c", "Literal 0-1, Variable 4-5, Literal 8-9")]
    [Arguments("[%x%][*y*]", "Code 2-3, Comment 7-8")]
    [Arguments("a [[- b -]] c", "Literal 0-2, Variable 6-7 trim-previous trim-next, Literal 11-13")]
    [Arguments("a\r\n[%- x %]", "Literal 0-3, Code 7-8 trim-previous")]
    [Arguments("#type X\nbody", "Directive 6-7, Literal 8-12")]
    [Arguments("plain", "Literal 0-5")]
    [Arguments("", "")]
    public async Task Parse_Text_ReturnsTokens(string text, string expected)
    {
        var result = new TemplateParser(TemplateParserOptions.Default).Parse(text.AsSpan());

        await Assert.That(string.Join(", ", result.Tokens.Select(Describe))).IsEqualTo(expected);
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    [Arguments("a[[b", 1, "]]")]
    [Arguments("[% x", 0, "%]")]
    [Arguments("ab [* note", 3, "*]")]
    public async Task Parse_UnclosedBlock_ReportsOpenerSpanAndCloser(string text, int start, string closingDelimiter)
    {
        var result = new TemplateParser(TemplateParserOptions.Default).Parse(text.AsSpan());
        var error = result.Errors.Single();

        await Assert.That(error.Start).IsEqualTo(start);
        await Assert.That(error.Length).IsEqualTo(2);
        await Assert.That(error.ClosingDelimiter).IsEqualTo(closingDelimiter);
    }

    [Test]
    [Arguments("#type X\r\n#model string\r\nbody", 24)]
    [Arguments("#type X", 7)]
    [Arguments("body", 0)]
    public async Task Parse_DirectiveSection_EndsAtFirstBodyLine(string text, int directiveSectionEnd)
    {
        var result = new TemplateParser(TemplateParserOptions.Default).Parse(text.AsSpan());

        await Assert.That(result.DirectiveSectionEnd).IsEqualTo(directiveSectionEnd);
    }

    private static string Describe(Token token)
    {
        var trimPrevious = token.TrimPrevious ? " trim-previous" : string.Empty;
        var trimNext = token.TrimNext ? " trim-next" : string.Empty;

        return $"{token.Type} {token.Start}-{token.End}{trimPrevious}{trimNext}";
    }
}
