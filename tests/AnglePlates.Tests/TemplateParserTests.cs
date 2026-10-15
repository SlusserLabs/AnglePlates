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
    // A block ends at the first exact closer, so a closer inside a C# string needs a higher level
    [Arguments("[[ \"]]\" ]]", "Variable 3-4, Literal 6-10")]
    [Arguments("[% var s = \"%]\"; %]x", "Code 3-12, Literal 14-20")]
    [Arguments("[% // note %]tail", "Code 3-10, Literal 13-17")]
    [Arguments("[[ a-- -]]", "Variable 3-6 trim-next")]
    [Arguments("x[[ a[ ]]y", "Literal 0-1, Variable 4-6, Literal 9-10")]
    // Bracket levels
    [Arguments("[[[ x ]]]", "Variable 4-5")]
    [Arguments("[[[[ x ]]]]", "Variable 5-6")]
    [Arguments("[% x %]", "Code 3-4")]
    [Arguments("[[% x %]]", "Code 4-5")]
    [Arguments("[[[% x %]]]", "Code 5-6")]
    [Arguments("[[[[% x %]]]]", "Code 6-7")]
    [Arguments("[* c *]", "Comment 3-4")]
    [Arguments("[[* c *]]", "Comment 4-5")]
    [Arguments("[[[* c *]]]", "Comment 5-6")]
    [Arguments("[[[[* c *]]]]", "Comment 6-7")]
    // Only the last four brackets of a longer run open a block
    [Arguments("[[[[[ x ]]]]]", "Literal 0-1, Variable 6-7, Literal 12-13")]
    [Arguments("[[[[[% x %]]]]]", "Literal 0-1, Code 7-8, Literal 14-15")]
    [Arguments("[[[[[[[[ x ]]]]", "Literal 0-4, Variable 9-10")]
    [Arguments("[ x ]", "Literal 0-5")]
    [Arguments("a[b", "Literal 0-3")]
    [Arguments("[", "Literal 0-1")]
    [Arguments("[[*ptr*]]", "Comment 3-6")]
    [Arguments("[[* [% x(); %] [[ y ]] *]]", "Comment 4-22")]
    [Arguments("a [[[- x -]]] b", "Literal 0-2, Variable 7-8 trim-previous trim-next, Literal 13-15")]
    [Arguments("[[[[%- x -%]]]]", "Code 7-8 trim-previous trim-next")]
    [Arguments("[[[*- c -*]]]", "Comment 6-7 trim-previous trim-next")]
    [Arguments("[[ count--]]", "Variable 3-9 trim-next")]
    [Arguments("[[-]]", "Variable 3-3 trim-previous")]
    [Arguments("[[ a[b[0]] ]]", "Variable 3-8, Literal 10-13")]
    [Arguments("[[% var s = \"%]\"; %]]", "Code 4-17")]
    [Arguments("[[ x ]]]", "Variable 3-4, Literal 7-8")]
    [Arguments("[[]]", "Variable 2-2")]
    [Arguments("[[[]]]", "Variable 3-3")]
    [Arguments("[%%]", "Code 2-2")]
    [Arguments("[[[[**]]]]", "Comment 5-5")]
    // Backslashes have no meaning
    [Arguments("\\[[ x ]]", "Literal 0-1, Variable 4-5")]
    [Arguments("\\[% x %]", "Literal 0-1, Code 4-5")]
    [Arguments("]] text ]]]]", "Literal 0-12")]
    [Arguments("[[% a;\r\nb; %]]", "Code 4-10")]
    [Arguments("[[% a;\nb; %]]", "Code 4-9")]
    [Arguments("[[% a;\rb; %]]", "Code 4-9")]
    public async Task Parse_Text_ReturnsTokens(string text, string expected)
    {
        var result = TemplateParser.Parse(text, CancellationToken.None);

        await Assert.That(Describe(result)).IsEqualTo(expected);
        await Assert.That(result.Errors).IsEmpty();

        // The class writer relies on the parser never emitting two literals in a row
        var adjacentLiterals = result.Tokens.Zip(result.Tokens.Skip(1), (a, b) => a.Type == TokenType.Literal && b.Type == TokenType.Literal);
        await Assert.That(adjacentLiterals.Any(adjacent => adjacent)).IsFalse();
    }

    [Test]
    [Arguments("a[[b", 1, 2, "]]")]
    [Arguments("[% x", 0, 2, "%]")]
    [Arguments("ab [* note", 3, 2, "*]")]
    [Arguments("ab [[", 3, 2, "]]")]
    [Arguments("[% \"abc", 0, 2, "%]")]
    [Arguments("[[ a[0", 0, 2, "]]")]
    [Arguments("[[[ x ]]", 0, 3, "]]]")]
    [Arguments("[[[[ x", 0, 4, "]]]]")]
    [Arguments("[[[[[ x", 1, 4, "]]]]")]
    [Arguments("[[% x %]", 0, 3, "%]]")]
    [Arguments("a[[[[% x %]]]", 1, 5, "%]]]]")]
    [Arguments("[[* c *]", 0, 3, "*]]")]
    [Arguments("[[*ptr ]]", 0, 3, "*]]")]
    [Arguments("[[[]]", 0, 3, "]]]")]
    [Arguments("[%]", 0, 2, "%]")]
    [Arguments("[[", 0, 2, "]]")]
    public async Task Parse_UnclosedBlock_ReportsOpenerSpanAndCloser(string text, int start, int openerLength, string closingDelimiter)
    {
        var result = TemplateParser.Parse(text, CancellationToken.None);
        var error = result.Errors.Single();

        await Assert.That(error.Descriptor).IsEqualTo(DiagnosticDescriptors.UnclosedBlock);
        await Assert.That(error.Span.Start).IsEqualTo(start);
        await Assert.That(error.Span.Length).IsEqualTo(openerLength);
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

    // The parser never reads C#, so broken C# can't make it revisit text: every block ends at its first closer
    [Test]
    [Arguments("[% var s = $\"{ x %]")]
    [Arguments("[[ a[0 ]]")]
    [Arguments("[% var s = @\"x %]")]
    [Arguments("[% /* x %]")]
    public async Task Parse_ManyBlocksWithBrokenCSharp_EndsEachAtFirstCloser(string block)
    {
        const int count = 3000;
        var text = string.Concat(Enumerable.Repeat(block + "<p class=\"row\">It's #1 { </p>\n", count));

        var result = TemplateParser.Parse(text, CancellationToken.None);

        await Assert.That(result.Tokens.Count(token => token.Type != TokenType.Literal)).IsEqualTo(count);
        await Assert.That(result.Errors).IsEmpty();
    }

    // Only the last four brackets open a block, and the rest of the run stays in the literal before it
    [Test]
    public async Task Parse_LongBracketRun_KeepsLastFourAsOpener()
    {
        var text = new string('[', 100_000) + " x ]]]]";

        var result = TemplateParser.Parse(text, CancellationToken.None);

        await Assert.That(Describe(result)).IsEqualTo("Literal 0-99996, Variable 100001-100002");
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
