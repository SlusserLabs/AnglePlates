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
    // Closers that the scanner skips inside C# don't move the template lines or columns after them
    [Arguments("[[ \"]]\" + missing ]]", "CS0103", "The name 'missing' does not exist in the current context", 5, 10, 7)]
    [Arguments("[[ \"]]\" +\r\n  missing ]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 2, 7)]
    [Arguments("[[ model[\r\n  missing[0]] ]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 2, 7)]
    [Arguments("[% var s = \"%]\"; s += missing; %]", "CS0103", "The name 'missing' does not exist in the current context", 5, 22, 7)]
    [Arguments("[% var s = \"%]\";\r\n  s += missing; %]", "CS0103", "The name 'missing' does not exist in the current context", 6, 7, 7)]
    [Arguments("[% /* %] */ var s = $\"{\r\n  missing}%]\"; %]", "CS0103", "The name 'missing' does not exist in the current context", 6, 2, 7)]
    // A C# variable that ends in a // comment closes its call on the next generated line
    [Arguments("[[ missing // note ]]", "CS0103", "The name 'missing' does not exist in the current context", 5, 3, 7)]
    [Arguments("[[ model +\r\n  missing // note\r\n]]", "CS0103", "The name 'missing' does not exist in the current context", 6, 2, 7)]
    [Arguments("[[ 1, 2 // note ]]", "CS1501", "No overload for method 'Write' takes 3 arguments", 5, 3, 12)]
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
        // The string runs to the end of its line, so the block ends at the first closer instead of pulling the HTML after it
        // into C#, and the compiler reports the typo at the template line. A UTF-8 class reports both errors again from RenderAsync.
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
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Utf8\r\n[% _ = model.Length; %]\n\n"));
        var source = result.TemplateSources.Single();
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var streamed = RenderingTestHelper.RenderToPipe(generated, "Consumer.ExampleUtf8Template", "<value>");

        await Assert.That(source).Contains("#line (6, 24) - (6, 24) ");
        await Assert.That(Convert.ToHexString(streamed)).IsEqualTo("0A0A");
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
        await Assert.That(RenderingTestHelper.Render(result.Compilation).text).IsEqualTo("a&lt;value&gt;");
    }
}
