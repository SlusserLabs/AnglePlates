using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Compiler errors in template C# map through #line to exact template spans, in Render and RenderAsync
public sealed class LineMappingTests
{
    [Test]
    // "1, 2" becomes a third argument, and the error on the method name, before the mapped column, maps to the whole variable
    [Arguments("[[ 1, 2 ]]", "CS1501", "No overload for method 'Write' takes 3 arguments", 5, 3, 4)]
    [Arguments("[[ missing ]]", "CS0103", "The name 'missing' does not exist in the current context", 5, 3, 7)]
    [Arguments("x [[ model + missing ]]", "CS0103", "The name 'missing' does not exist in the current context", 5, 13, 7)]
    [Arguments("<p>[% var count = 1;\r\n  count += missing; %]</p>", "CS0103", "The name 'missing' does not exist in the current context", 6, 11, 7)]
    [Arguments("[% var count = missing; %]", "CS0103", "The name 'missing' does not exist in the current context", 5, 15, 7)]
    // A higher bracket level keeps a closer inside C# from ending the block, and the template lines and columns after it still map exactly
    [Arguments("[[[ \"]]\" + missing ]]]", "CS0103", "The name 'missing' does not exist in the current context", 5, 11, 7)]
    [Arguments("[[[ \"]]\" +\r\n  missing ]]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 2, 7)]
    [Arguments("[[[ model[\r\n  missing[0]] ]]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 2, 7)]
    [Arguments("[[% var s = \"%]\"; s += missing; %]]", "CS0103", "The name 'missing' does not exist in the current context", 5, 23, 7)]
    [Arguments("[[% var s = \"%]\";\r\n  s += missing; %]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 7, 7)]
    [Arguments("[[% /* %] */ var s = $\"{\r\n  missing}%]\"; %]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 2, 7)]
    // Multi-line blocks and variables keep their template columns on every line, whatever the line break style
    [Arguments("[% var a = 1;\n  var b = missing; %]", "CS0103", "The name 'missing' does not exist in the current context", 6, 10, 7)]
    [Arguments("[% var a = 1;\r  var b = missing; %]", "CS0103", "The name 'missing' does not exist in the current context", 6, 10, 7)]
    [Arguments("[% var a = 1;\r\n\r\n    a += missing; %]", "CS0103", "The name 'missing' does not exist in the current context", 7, 9, 7)]
    [Arguments("[[% var a = 1;\r\n  var b = missing; %]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 10, 7)]
    [Arguments("[[[[ model +\r\n   missing ]]]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 3, 7)]
    [Arguments("<p>x</p>[% var a = 1;\r\n  var b = 2;\r\n  b += missing; %]", "CS0103", "The name 'missing' does not exist in the current context", 7, 7, 7)]
    public async Task InvalidCSharp_InVariableOrBlock_ReportsCompilerErrorAtTemplate(
        string body, string id, string message, int templateLine, int templateColumn, int templateLength)
    {
        // Generate one renderer at a time because the compiler doesn't report errors from separate trees in a stable order.
        // An error inside a C# variable or block maps to its exact template span, and an error in the generated call around
        // a variable maps to the whole variable. A block's second line keeps its template columns, so an undefined name there
        // maps to the seventh line. A UTF-8 class reports the same error again from RenderAsync, at the same template location.
        var mapped = new LinePositionSpan(new LinePosition(templateLine, templateColumn), new LinePosition(templateLine, templateColumn + templateLength));
        foreach (var target in new[] { "Text", "Utf8" })
        {
            var template = RenderingTestHelper.Template($"#generate {target}\r\n{body}");
            var error = ExpectedCompilerDiagnostic.Mapped(id, message, TemplateText.Path, mapped, $"Consumer.Example{target}Template.g.cs");
            var expected = target == "Text" ? new[] { error } : new[] { error, error };

            var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

            await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        }
    }

    [Test]
    public async Task BrokenCSharp_UnterminatedString_EndsAtFirstCloserAndReportsAtTemplate()
    {
        // The block ends at the first exact closer, and nothing reads the C# inside it, so the HTML after the closer stays HTML.
        // The string runs to the end of its line, and the compiler reports the typo at the template line. A UTF-8 class reports
        // both errors again from RenderAsync.
        foreach (var target in new[] { "Text", "Utf8" })
        {
            var template = RenderingTestHelper.Template($"#generate {target}\r\n<p>[% var count = 1;\r\n  var s = \"abc %]</p>\r\n[[ model ]]");
            var file = $"Consumer.Example{target}Template.g.cs";
            var newline = ExpectedCompilerDiagnostic.Mapped("CS1010", "Newline in constant", TemplateText.Path, new LinePositionSpan(new LinePosition(6, 10), new LinePosition(6, 10)), file);
            var semicolon = ExpectedCompilerDiagnostic.Mapped("CS1002", "; expected", TemplateText.Path, new LinePositionSpan(new LinePosition(6, 14), new LinePosition(6, 14)), file);
            var expected = target == "Text" ? new[] { newline, semicolon } : new[] { newline, semicolon, newline, semicolon };

            var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

            await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        }
    }

    // The compiler lists a tree's syntax errors in source order before its binding errors, so a UTF-8 file's errors read: Render's
    // syntax errors, RenderAsync's syntax errors, Render's binding errors, RenderAsync's binding errors. A text file has no RenderAsync.
    private static ExpectedCompilerDiagnostic[] Order(
        string target,
        ExpectedCompilerDiagnostic[] renderSyntax,
        ExpectedCompilerDiagnostic[] renderBinding,
        ExpectedCompilerDiagnostic[] renderAsyncSyntax,
        ExpectedCompilerDiagnostic[] renderAsyncBinding)
    {
        return target == "Text"
            ? [.. renderSyntax, .. renderBinding]
            : [.. renderSyntax, .. renderAsyncSyntax, .. renderBinding, .. renderAsyncBinding];
    }

    // Errors on template line 5 (the first body line), given as id, message, and start and end columns
    private static ExpectedCompilerDiagnostic[] ToMapped((string id, string message, int start, int end)[] errors, string file)
    {
        return errors.Select(error => ExpectedCompilerDiagnostic.Mapped(
            error.id, error.message, TemplateText.Path, new LinePositionSpan(new LinePosition(5, error.start), new LinePosition(5, error.end)), file)).ToArray();
    }

    [Test]
    [Arguments("[[ model // note ]]", 5, 8, 5, 16, false)]
    [Arguments("[[ model // note ]]x", 5, 8, 5, 16, true)]
    [Arguments("[[ model +\r\n  model // note\r\n]]", 6, 7, 6, 15, false)]
    public async Task LineCommentVariable_ReportsSyntaxErrors(
        string body, int templateLine, int templateColumn, int variableEndLine, int variableEndColumn, bool literalFollows)
    {
        // A // comment swallows the rest of the generated line, including the call's closing ", writer);". Each missing token is
        // reported where the compiler expected it: right after the last token before the comment, at templateColumn. The row
        // uses model rather than a missing name, so only syntax errors appear. When the call is the last statement of a method,
        // the next token is the closing brace, and the compiler reports the missing ")" and ";" there (CS1026 and CS1002). When
        // another statement follows (a literal, or the flush in RenderAsync), the compiler reads that statement as a second
        // argument: it reports the missing "," at the template location (CS1003), the missing ")" after the following call in the
        // generated file (CS1026), and that call's void result as the second argument (CS1503, also in the generated file). These
        // generated-file errors are the deliberate exception to "every error maps to the template". A call that lost its closing
        // parenthesis has one argument, so the compiler also reports the missing overload (CS1501) on the whole variable.
        var span = new LinePositionSpan(new LinePosition(templateLine, templateColumn), new LinePosition(templateLine, templateColumn));
        var variable = new LinePositionSpan(new LinePosition(5, 3), new LinePosition(variableEndLine, variableEndColumn));
        foreach (var target in new[] { "Text", "Utf8" })
        {
            var file = $"Consumer.Example{target}Template.g.cs";
            var writer = target == "Text" ? "System.IO.TextWriter" : "System.Buffers.IBufferWriter<byte>";
            var closeParen = ExpectedCompilerDiagnostic.Mapped("CS1026", ") expected", TemplateText.Path, span, file);
            var semicolon = ExpectedCompilerDiagnostic.Mapped("CS1002", "; expected", TemplateText.Path, span, file);
            var comma = ExpectedCompilerDiagnostic.Mapped("CS1003", "Syntax error, ',' expected", TemplateText.Path, span, file);
            var oneArgument = ExpectedCompilerDiagnostic.Mapped("CS1501", "No overload for method 'Write' takes 1 arguments", TemplateText.Path, variable, file);
            var generatedCloseParen = ExpectedCompilerDiagnostic.Generated("CS1026", ") expected", file);
            var voidArgument = ExpectedCompilerDiagnostic.Generated("CS1503", $"Argument 2: cannot convert from 'void' to '{writer}'", file);

            // The flush after the call in RenderAsync always follows the variable
            var expected = literalFollows
                ? Order(target, [comma, generatedCloseParen], [voidArgument], [comma, generatedCloseParen], [voidArgument])
                : Order(target, [closeParen, semicolon], [oneArgument], [comma, generatedCloseParen], [voidArgument]);
            var template = RenderingTestHelper.Template($"#generate {target}\r\n{body}");

            var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

            await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        }
    }

    [Test]
    public async Task ClosersInsideCSharp_CloseAtFirstCloser_ReportCompilerErrorsAtTemplate()
    {
        // The parser doesn't read C#, so each block ends at its first exact closer and the compiler reports what is left. None of
        // these is SLAP004. A missing token is reported on the next token when it shares the line: here the ")" that stands where
        // "]" belongs, then the ";" that stands where ")" belongs. The spans are the variable's start plus the content's length plus
        // 2 for ", " and 6 for "writer" (the ")" starts at that column). Semantic errors follow from the broken call.
        // After a C# block, RenderAsync writes the variable with "await WriteAsync(" and passes the cancellation token, so the
        // same missing tokens sit at later columns there; without a block before it, both methods use the same columns.
        var cases = new (string template, (string id, string message, int start, int end)[] syntax, (string id, string message, int start, int end)[] semantic,
            (string id, string message, int start, int end)[]? asyncSyntax, (string id, string message, int start, int end)[]? asyncSemantic)[]
        {
            ("[[ model[0..1]]]",
            [
                ("CS1003", "Syntax error, ']' expected", 21, 22),
                ("CS1026", ") expected", 22, 23),
            ],
            [
                ("CS1501", "No overload for method 'this' takes 2 arguments", 3, 21),
            ],
            null,
            null),
            ("[% var grid = new[] { 7 }; var row = new[] { 0 }; %][[ grid[row[0]] ]]",
            [
                ("CS1003", "Syntax error, ']' expected", 73, 74),
                ("CS1003", "Syntax error, ']' expected", 74, 75),
                ("CS1026", ") expected", 74, 75),
            ],
            [
                ("CS0022", "Wrong number of indices inside []; expected 1", 60, 73),
            ],
            [
                ("CS1003", "Syntax error, ']' expected", 92, 93),
                ("CS1003", "Syntax error, ']' expected", 115, 116),
                ("CS1026", ") expected", 115, 116),
            ],
            [
                ("CS0022", "Wrong number of indices inside []; expected 1", 60, 92),
            ]),
        };

        foreach (var (body, syntax, semantic, asyncSyntax, asyncSemantic) in cases)
        {
            foreach (var target in new[] { "Text", "Utf8" })
            {
                var file = $"Consumer.Example{target}Template.g.cs";
                var syntaxErrors = ToMapped(syntax, file);
                var semanticErrors = ToMapped(semantic, file);
                var expected = Order(target, syntaxErrors, semanticErrors, ToMapped(asyncSyntax ?? syntax, file), ToMapped(asyncSemantic ?? semantic, file));
                var template = RenderingTestHelper.Template($"#generate {target}\r\n{body}");

                var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

                await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
            }
        }
    }

    [Test]
    public async Task ClosersInsideCSharp_BlockWithUnterminatedString_ReportsErrorsAtTemplate()
    {
        // [% var s = "%]"; %] ends at the first %], so the block holds var s = " and the string loses its end quote. As in
        // BrokenCSharp_UnterminatedString_*, the newline error sits on the opening quote (column 3 + 8) and the missing ";" after the string
        foreach (var target in new[] { "Text", "Utf8" })
        {
            var file = $"Consumer.Example{target}Template.g.cs";
            var newline = ExpectedCompilerDiagnostic.Mapped("CS1010", "Newline in constant", TemplateText.Path, new LinePositionSpan(new LinePosition(5, 11), new LinePosition(5, 11)), file);
            var semicolon = ExpectedCompilerDiagnostic.Mapped("CS1002", "; expected", TemplateText.Path, new LinePositionSpan(new LinePosition(5, 12), new LinePosition(5, 12)), file);
            var expected = target == "Text" ? new[] { newline, semicolon } : new[] { newline, semicolon, newline, semicolon };
            var template = RenderingTestHelper.Template($"#generate {target}\r\n[% var s = \"%]\"; %]");

            var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

            await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        }
    }

    [Test]
    public async Task ClosersInsideCSharp_VariableWithUnterminatedString_ReportsNewlineAtTemplateAndParenInGeneratedFile()
    {
        // [[ "]]" + model ]] ends at the first ]], so the call is Write(", writer); and the string runs to the end of the generated
        // line. The newline error maps to the variable's start (column 3). The call's next argument would be the following statement,
        // so the missing "," maps to the end of the string (3 + the 11 characters of ", writer);" = column 14), and the missing ")"
        // lands after that following statement, in the generated file.
        foreach (var target in new[] { "Text", "Utf8" })
        {
            var file = $"Consumer.Example{target}Template.g.cs";
            var newline = ExpectedCompilerDiagnostic.Mapped("CS1010", "Newline in constant", TemplateText.Path, new LinePositionSpan(new LinePosition(5, 3), new LinePosition(5, 3)), file);
            var comma = ExpectedCompilerDiagnostic.Mapped("CS1003", "Syntax error, ',' expected", TemplateText.Path, new LinePositionSpan(new LinePosition(5, 14), new LinePosition(5, 14)), file);
            var closeParen = ExpectedCompilerDiagnostic.Generated("CS1026", ") expected", file);
            var errors = new[] { newline, comma, closeParen };
            var expected = target == "Text" ? errors : errors.Concat(errors).ToArray();
            var template = RenderingTestHelper.Template($"#generate {target}\r\n[[ \"]]\" + model ]]");

            var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

            await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        }
    }

    [Test]
    public async Task RefStructVariable_InRenderAndRenderAsync_ReportsErrorsAtWholeVariable()
    {
        // No Write overload takes a ref struct, and the error on the method name maps to the whole variable
        var mapped = new LinePositionSpan(new LinePosition(5, 3), new LinePosition(5, 29));
        foreach (var (target, writerType) in new[] { ("Text", "TextWriter"), ("Utf8", "IBufferWriter<byte>") })
        {
            var template = RenderingTestHelper.Template($"#generate {target}\r\n[[ default(System.Span<char>) ]]");
            var file = $"Consumer.Example{target}Template.g.cs";
            var message = $"The type 'Span<char>' may not be a ref struct or a type parameter allowing ref structs in order to use it as parameter 'T' " +
                $"in the generic type or method '{target}Template<string>.Write<T>(T, {writerType})'";
            var error = ExpectedCompilerDiagnostic.Mapped("CS9244", message, TemplateText.Path, mapped, file);
            var expected = target == "Text" ? new[] { error } : new[] { error, error };

            var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

            await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        }
    }

    [Test]
    [Arguments("[% System.Span<char> s = stackalloc char[1]; %]", LanguageVersion.CSharp11, false, "CS9058",
        "Feature 'ref and unsafe in async and iterator methods' is not available in C# 11.0. Please use language version 13.0 or greater.", 3, 20)]
    [Arguments("[% System.Span<char> s = stackalloc char[1]; %]x[% s[0] = 'a'; %]", LanguageVersion.CSharp13, false, "CS4007",
        "Instance of type 'System.Span<char>' cannot be preserved across 'await' or 'yield' boundary.", 51, 52)]
    [Arguments("[% lock (model) { %]x[% } %]", LanguageVersion.CSharp11, false, "CS1996", "Cannot await in the body of a lock statement", 20, 21)]
    [Arguments("[% unsafe { %]x[% } %]", LanguageVersion.CSharp11, true, "CS4004", "Cannot await in an unsafe context", 14, 15)]
    public async Task AsyncRestriction_InCSharpBlock_ReportsCompilerErrorAtTemplate(
        string body, LanguageVersion languageVersion, bool allowUnsafe, string id, string message, int templateStart, int templateEnd)
    {
        // Each body compiles in Render, so the only error comes from RenderAsync. A ref struct local maps to the exact span the
        // compiler reports in the template: its type in C# 11, or from C# 13 its use after a flush check. An await inside lock
        // or unsafe maps to the whole literal that flushes, because the await sits before the mapped column.
        var template = RenderingTestHelper.Template("#generate Utf8\r\n" + body);
        var compilation = GeneratorTestHelper.CreateCompilation(languageVersion: languageVersion);
        compilation = compilation.WithOptions(compilation.Options.WithAllowUnsafe(allowUnsafe));
        var mapped = new LinePositionSpan(new LinePosition(5, templateStart), new LinePosition(5, templateEnd));
        var expected = ExpectedCompilerDiagnostic.Mapped(id, message, TemplateText.Path, mapped, "Consumer.ExampleUtf8Template.g.cs");

        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([template], languageVersion: languageVersion), [expected]);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task FlushingLiteral_OnlyLineBreaks_MapsOneColumnAfterStart()
    {
        // In RenderAsync the literal after a C# block flushes, so it gets a mapping even though it holds only line breaks
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("[% _ = model.Length; %]\n\n"));
        var source = result.TemplateSources.Single(source => source.Contains("class ExampleUtf8Template", StringComparison.Ordinal));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(source).Contains("#line (5, 24) - (5, 24) ");
        await Assert.That(Convert.ToHexString(output.bytes)).IsEqualTo("0A0A");
    }

    [Test]
    [Arguments("Templates/a\"b.html", "Templates/a&quot;b.html")]
    [Arguments("Templates/a\nb.html", "Templates/a&#10;b.html")]
    [Arguments("Templates/a\r\nb.html", "Templates/a&#13;&#10;b.html")]
    [Arguments("Templates/a\u0085b.html", "Templates/a&#133;b.html")]
    [Arguments("Templates/a\u2028b.html", "Templates/a&#8232;b.html")]
    [Arguments("Templates/a\u2029b.html", "Templates/a&#8233;b.html")]
    public async Task TemplatePath_ThatLineDirectivesCannotHold_EscapesCommentsAndSkipsLineMapping(string path, string documentedPath)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("a[% var b = 1; %][[ model ]]", path: path));
        var sources = result.TemplateSources;

        // The header and the class summary are comments, so a line break in the path would end them early
        await Assert.That(sources.Length).IsEqualTo(2);
        await Assert.That(sources.All(source => source.Contains($"renders '{documentedPath}'.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(sources.All(source => source.Contains($"//     Generated by SlusserLabs.AnglePlates from {documentedPath}.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(sources.Any(source => source.Contains("#line", StringComparison.Ordinal))).IsFalse();
        await Assert.That((await RenderingTestHelper.RenderAsync(result.Compilation)).text).IsEqualTo("a&lt;value&gt;");
    }
}
