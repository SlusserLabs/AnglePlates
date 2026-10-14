using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Parser results: directive and body token spans of the trimmed content, trim flags, and unclosed-block errors
public sealed class TemplateParserTests
{
    [Test]
    [Arguments("a[[ b ]]c", "Literal 0-1, Variable 4-5, Literal 8-9")]
    [Arguments("[%x%][*y*]", "Code 2-3, Comment 7-8")]
    [Arguments("a [[- b -]] c", "Literal 0-2, Variable 6-7 trim-previous trim-next, Literal 11-13")]
    [Arguments("a\r\n[%- x %]", "Literal 0-3, Code 7-8 trim-previous")]
    [Arguments("#type X\nbody", "Directive 6-7, Literal 8-12")]
    [Arguments("#type X\rbody", "Directive 6-7, Literal 8-12")]
    [Arguments("#type X\r\nbody", "Directive 6-7, Literal 9-13")]
    [Arguments("#type X\r\rbody", "Directive 6-7, Literal 8-13")]
    [Arguments("plain", "Literal 0-5")]
    [Arguments("", "")]
    // C# strings and comments hide closers, and an unmatched "[" falls back to the plain search
    [Arguments("[[ \"]]\" ]]", "Variable 3-7")]
    [Arguments("[% var s = \"%]\"; %]x", "Code 3-16, Literal 19-20")]
    [Arguments("[% // note %]tail", "Code 3-10, Literal 13-17")]
    [Arguments("x[[ a[ ]]y", "Literal 0-1, Variable 4-6, Literal 9-10")]
    [Arguments("[% /* %] */ x", "Code 3-5, Literal 8-13")]
    [Arguments("[[ a-- -]]", "Variable 3-6 trim-next")]
    public async Task Parse_Text_ReturnsTokens(string text, string expected)
    {
        var result = TemplateParser.Parse(text, CancellationToken.None);

        await Assert.That(Describe(result)).IsEqualTo(expected);
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    [Arguments("a[[b", 1, "]]")]
    [Arguments("[% x", 0, "%]")]
    [Arguments("ab [* note", 3, "*]")]
    [Arguments("ab [[", 3, "]]")]
    [Arguments("[% \"abc", 0, "%]")]
    [Arguments("[[ a[0", 0, "]]")]
    public async Task Parse_UnclosedBlock_ReportsOpenerSpanAndCloser(string text, int start, string closingDelimiter)
    {
        var result = TemplateParser.Parse(text, CancellationToken.None);
        var error = result.Errors.Single();

        await Assert.That(error.Descriptor).IsEqualTo(DiagnosticDescriptors.UnclosedBlock);
        await Assert.That(error.Span.Start).IsEqualTo(start);
        await Assert.That(error.Span.Length).IsEqualTo(2);
        await Assert.That(error.Arguments).IsEquivalentTo(new[] { closingDelimiter });
    }

    [Test]
    [Arguments("#type X\r\n#model string\r\nbody", 24)]
    // A lone CR ends a directive line too, so the rest of the file doesn't become the first directive's value
    [Arguments("#type X\r#model string\rbody", 22)]
    [Arguments("#type X\n#model string\nbody", 22)]
    [Arguments("#type X", 7)]
    [Arguments("body", 0)]
    public async Task Parse_DirectiveSection_EndsAtFirstBodyLine(string text, int bodyStart)
    {
        var result = TemplateParser.Parse(text, CancellationToken.None);

        await Assert.That(result.BodyStart).IsEqualTo(bodyStart);
    }

    [Test]
    [Arguments("#type X\r\n", "Type #type 0-5 value 6-7")]
    [Arguments("#model\r\n#using A.B", "Model #model 0-6 value 6-6, Using #using 8-14 value 15-18")]
    [Arguments("#type X\r#model Y\r", "Type #type 0-5 value 6-7, Model #model 8-14 value 15-16")]
    [Arguments("#model\r#using A.B", "Model #model 0-6 value 6-6, Using #using 7-13 value 14-17")]
    public async Task Parse_Directives_ReturnsNameAndValueSpans(string text, string expected)
    {
        var result = TemplateParser.Parse(text, CancellationToken.None);
        var directives = result.Directives.Select(directive =>
            $"{directive.Kind} {text.Substring(directive.Name.Start, directive.Name.Length)} {directive.Name.Start}-{directive.Name.End} value {directive.Value.Start}-{directive.Value.End}");

        await Assert.That(string.Join(", ", directives)).IsEqualTo(expected);
        await Assert.That(result.Tokens).IsEmpty();
    }

    // Each broken block ends at its own closer. An unclosed interpolation hole that read on through every later block
    // used to overflow the stack, and the others took seconds.
    [Test]
    [Arguments("[[ a[0 ]]")]
    [Arguments("[% var s = $\"{ x %]")]
    [Arguments("[% var s = @\"x %]")]
    [Arguments("[% /* x %]")]
    public async Task Parse_ManyBrokenBlocks_EndsEachAtItsFirstCloser(string block)
    {
        const int count = 3000;
        var text = string.Concat(Enumerable.Repeat(block + "<p class=\"row\">It's #1 { </p>\n", count));

        var result = TemplateParser.Parse(text, CancellationToken.None);

        await Assert.That(result.Tokens.Count(token => token.Type != TokenType.Literal)).IsEqualTo(count);
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task Parse_CanceledToken_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() => TemplateParser.Parse("#type X\nbody", cancellation.Token)).Throws<OperationCanceledException>();
    }

    // Directives come first in the description because they come first in the template
    private static string Describe(ParsedTemplate result)
    {
        var directives = result.Directives.Select(directive => $"Directive {directive.Value.Start}-{directive.Value.End}");
        var tokens = result.Tokens.Select(token =>
        {
            var trimPrevious = token.TrimPrevious ? " trim-previous" : string.Empty;
            var trimNext = token.TrimNext ? " trim-next" : string.Empty;

            return $"{token.Type} {token.Span.Start}-{token.Span.End}{trimPrevious}{trimNext}";
        });

        return string.Join(", ", directives.Concat(tokens));
    }
}
