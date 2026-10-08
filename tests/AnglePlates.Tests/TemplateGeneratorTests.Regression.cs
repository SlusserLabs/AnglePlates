using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using VerifyTUnit;

namespace SlusserLabs.AnglePlates.Tests;

public sealed partial class TemplateGeneratorTests
{
    private const string _nullTextSource = "namespace Consumer { public sealed class NullText { public override string? ToString() => null; } }";

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Initialize_InvalidTemplates_GeneratesEveryValidTemplate(int invalidPosition)
    {
        var templates = new List<AdditionalText>
        {
            RenderingTestHelper.Template("first [[ model ]]", "First", path: "Templates/a.html"),
            RenderingTestHelper.Template("second [[ model ]]", "Second", path: "Templates/z.html")
        };
        var invalidPath = new[] { "Templates/0.html", "Templates/m.html", "Templates/zz.html" }[invalidPosition];
        templates.Insert(invalidPosition, new TemplateAdditionalText("#generate Text\r\n[[", invalidPath));
        templates.Add(RenderingTestHelper.Template("[% unclosed", "Bad", path: "Templates/bad.html"));
        templates.Add(new TemplateAdditionalText(_requiredDirectives + "#type Other\r\nbad", "Templates/duplicate.html"));
        var result = GeneratorTestHelper.Run(templates.ToArray());
        var first = RenderingTestHelper.Render(result.Compilation, type: "Consumer.First");
        var second = RenderingTestHelper.Render(result.Compilation, type: "Consumer.Second");

        // Missing directives, unclosed blocks, and directive errors each skip only their own template
        await Assert.That(string.Join(",", result.RunResult.Diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal)))
            .IsEqualTo("SLAP001,SLAP002,SLAP003,SLAP004,SLAP004,SLAP006");
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(7);
        await Assert.That(first.text).IsEqualTo("first &lt;value&gt;");
        await Assert.That(second.text).IsEqualTo("second &lt;value&gt;");
        await Assert.That(first.bytes.SequenceEqual(Encoding.UTF8.GetBytes(first.text))).IsTrue();
        await Assert.That(second.bytes.SequenceEqual(Encoding.UTF8.GetBytes(second.text))).IsTrue();
    }

    [Test]
    public async Task Initialize_SameTypePrefixAcrossNamespaces_GeneratesUniqueStableNames()
    {
        var templates = new[]
        {
            RenderingTestHelper.Template("one", ns: "One", path: "Templates/a.html"),
            RenderingTestHelper.Template("two", ns: "Two", path: "Templates/b.html")
        };
        var result = GeneratorTestHelper.Run(templates);
        var reversed = GeneratorTestHelper.Run(templates.Reverse().ToArray());
        var changed = GeneratorTestHelper.Run(templates[0], RenderingTestHelper.Template("changed", ns: "Two", path: "Templates/b.html"));

        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(7);
        await Assert.That(HintNames(result)).IsEqualTo(HintNames(reversed));
        await Assert.That(HintNames(result)).IsEqualTo(HintNames(changed));
        await Assert.That(RenderingTestHelper.Render(result.Compilation, type: "One.Example").text).IsEqualTo("one");
        await Assert.That(RenderingTestHelper.Render(result.Compilation, type: "Two.Example").text).IsEqualTo("two");
    }

    [Test]
    public async Task Initialize_TwoDuplicatePairs_NumbersEachPairIndependentlyOfFileOrder()
    {
        // Each pair shares a path and declared type, so the compiler reports duplicate members; only the hint names matter here
        var templates = new[]
        {
            new TemplateAdditionalText("#type Alpha\r\n#model string\r\n#namespace Consumer\r\n#generate Text\r\nfirst", "Templates/a.html"),
            new TemplateAdditionalText("#type Alpha\r\n#model string\r\n#namespace Consumer\r\n#generate Text\r\nsecond", "Templates/a.html"),
            new TemplateAdditionalText("#type Beta\r\n#model string\r\n#namespace Consumer\r\n#generate Text\r\nfirst", "Templates/b.html"),
            new TemplateAdditionalText("#type Beta\r\n#model string\r\n#namespace Consumer\r\n#generate Text\r\nsecond", "Templates/b.html")
        };
        var compilation = GeneratorTestHelper.CreateCompilation();

        var result = GeneratorTestHelper.CreateDriver(templates).RunGenerators(compilation).GetRunResult().Results.Single();
        var reversed = GeneratorTestHelper.CreateDriver(templates.Reverse().ToArray()).RunGenerators(compilation).GetRunResult().Results.Single();
        var numbered = result.GeneratedSources.Where(source => source.HintName.EndsWith(".1.g.cs", StringComparison.Ordinal)).ToArray();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.GeneratedSources.Length).IsEqualTo(7);
        await Assert.That(SourcesByHintName(reversed)).IsEquivalentTo(SourcesByHintName(result));
        await Assert.That(numbered.Length).IsEqualTo(2);
        foreach (var source in numbered)
        {
            // Duplicates are sorted by content, so the second template always takes the suffix
            await Assert.That(source.SourceText.ToString()).Contains("\"second\"");
        }

        static string[] SourcesByHintName(GeneratorRunResult runResult)
        {
            return runResult.GeneratedSources.Select(source => $"{source.HintName}={source.SourceText}").ToArray();
        }
    }

    [Test]
    public async Task Initialize_BracketsAndEmptyBlocks_MatchesReviewedSources()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("[ordinary] [[ model ]]\r\na \r\n[[- -]][%- -%][*- -*]\r\nb"));

        await Verifier.Verify(result.RunResult);
    }

    [Test]
    [Arguments("Templates/a\"b.html", "Templates/a&quot;b.html")]
    [Arguments("Templates/a\nb.html", "Templates/a&#10;b.html")]
    [Arguments("Templates/a\r\nb.html", "Templates/a&#13;&#10;b.html")]
    [Arguments("Templates/a\u0085b.html", "Templates/a&#133;b.html")]
    [Arguments("Templates/a\u2028b.html", "Templates/a&#8232;b.html")]
    [Arguments("Templates/a\u2029b.html", "Templates/a&#8233;b.html")]
    public async Task Initialize_PathThatLineDirectivesCannotHold_EscapesDocumentationAndSkipsLineMapping(string path, string documentedPath)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("a[% var b = 1; %][[ model ]]", path: path));
        var sources = result.RunResult.Results.Single().GeneratedSources.Skip(3).Select(source => source.SourceText.ToString()).ToArray();

        await Assert.That(sources.Length).IsEqualTo(2);
        await Assert.That(sources.All(source => source.Contains($"render the contents of '{documentedPath}'.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(sources.Any(source => source.Contains("#line", StringComparison.Ordinal))).IsFalse();
        await Assert.That(RenderingTestHelper.Render(result.Compilation).text).IsEqualTo("a&lt;value&gt;");
    }

    [Test]
    public async Task Initialize_EscapedOpeners_MatchesReviewedSources()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Text\r\nlet a = \\[[1]];\r\n\\\\[% var b = 2; %]\\[*c*]"));

        await Verifier.Verify(result.RunResult);
    }

    [Test]
    [Arguments("1.5", "1.5")]
    [Arguments("1234.5m", "1234.5")]
    [Arguments("new System.DateTime(2026, 10, 7, 13, 5, 0)", "10/07/2026 13:05:00")]
    public async Task Initialize_CSharpVariableUnderNonInvariantCulture_FormatsWithInvariantCulture(string expression, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template($"[[ {expression} ]]"));
        var original = CultureInfo.CurrentCulture;
        (string text, byte[] bytes) output;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            output = RenderingTestHelper.Render(result.Compilation);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("model", null, null)]
    [Arguments("(object?)null", null, null)]
    [Arguments("(System.Uri?)null", null, null)]
    [Arguments("(int?)null", null, null)]
    [Arguments("(dynamic?)null", null, null)]
    [Arguments("new NullText()", null, null)]
    [Arguments("model", "<value>", "&lt;value&gt;")]
    [Arguments("(object?)\"x<\"", "<value>", "x&lt;")]
    [Arguments("new System.Uri(\"http://example.com/\")", "<value>", "http://example.com/")]
    [Arguments("(int?)5", "<value>", "5")]
    [Arguments("(dynamic?)1.5", "<value>", "1.5")]
    [Arguments("(decimal?)1234.5m", "<value>", "1234.5")]
    [Arguments("string.Empty", "<value>", "")]
    public async Task Initialize_CSharpVariable_WritesNullVariableOnlyForNull(string expression, string? model, string? value)
    {
        // A null value writes nothing, or the encoded NullVariable when one is set; other values ignore NullVariable
        var compilation = GeneratorTestHelper.CreateCompilation([_nullTextSource]);
        var template = RenderingTestHelper.Template($"a[[ {expression} ]]b");
        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([template]));
        var withoutNullVariable = "a" + value + "b";
        var withNullVariable = value is null ? "a&lt;null&gt;b" : withoutNullVariable;

        var plain = RenderingTestHelper.Render(result.Compilation, model: model);
        var substituted = RenderingTestHelper.Render(result.Compilation, model: model, nullVariable: "<null>");

        await Assert.That(plain.text).IsEqualTo(withoutNullVariable);
        await Assert.That(plain.bytes.SequenceEqual(Encoding.UTF8.GetBytes(withoutNullVariable))).IsTrue();
        await Assert.That(substituted.text).IsEqualTo(withNullVariable);
        await Assert.That(substituted.bytes.SequenceEqual(Encoding.UTF8.GetBytes(withNullVariable))).IsTrue();
    }

    [Test]
    public async Task Initialize_CSharpVariablesInSameBlock_Compile()
    {
        const string body = "[[ model ]][[ model ]][% foreach (var item in new int?[] { 1, null, 3 }) { %][[ item ]][[ item ]];[% } %]";
        const string expected = "&lt;value&gt;&lt;value&gt;11;--;33;";
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));

        var output = RenderingTestHelper.Render(result.Compilation, nullVariable: "-");

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("[[ null ]]", 38, 50, 14, 6, "CS8117", "Invalid operand for pattern match; value required, but found '<null>'.", 5, 3, 4)]
    [Arguments("[[ default ]]", 38, 50, 15, 7, "CS8716", "There is no target type for the default literal.", 5, 3, 7)]
    [Arguments("[[ 1, 2 ]]", 38, 48, 37, 8, "CS1501", "No overload for method 'ToString' takes 3 arguments", 5, 3, 4)]
    [Arguments("<p>[% var count = 1;\r\n  count += missing; %]</p>", 41, 56, 11, 7, "CS0103", "The name 'missing' does not exist in the current context", 6, 11, 7)]
    public async Task Initialize_InvalidCSharp_ReportsCompilerErrorAtTemplate(
        string body, int line, int asyncLine, int column, int length, string id, string message, int templateLine, int templateColumn, int templateLength)
    {
        // Generate one renderer at a time because the compiler doesn't report errors from separate trees in a stable order.
        // Directives are tokens 0 to 4, so the body starts at token 5 and the Write call is the first statement after Model.
        // Every variable error maps to the whole expression on the template's sixth line, including errors in generated code around it,
        // and a block's second line keeps its template columns, so an undefined name there maps to the seventh line.
        // A UTF-8 class reports the same error again from RenderAsync, at the same template location.
        var mapped = new LinePositionSpan(new LinePosition(templateLine, templateColumn), new LinePosition(templateLine, templateColumn + templateLength));
        foreach (var target in new[] { "Text", "Utf8" })
        {
            var template = RenderingTestHelper.Template($"#generate {target}\r\n{body}");
            var compilation = GeneratorTestHelper.CreateCompilation();
            var driver = GeneratorTestHelper.CreateDriver([template]).RunGenerators(compilation);
            var tree = TemplateTrees(driver).Single();
            var expected = target == "Text"
                ? new[] { CompilerError(tree, line, column, length, id, message, mapped) }
                : new[] { CompilerError(tree, line, column, length, id, message, mapped), CompilerError(tree, asyncLine, column, length, id, message, mapped) };

            var result = GeneratorTestHelper.Run(compilation, driver, expected);

            await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(4);
        }
    }

    [Test]
    public async Task Initialize_RefStructCSharpVariable_ReportsErrorsAtWholeVariable()
    {
        // The value can't be converted in either method, and RenderAsync can't hold it in a pattern variable before C# 13
        var mapped = new LinePositionSpan(new LinePosition(5, 3), new LinePosition(5, 29));
        const string conversionMessage = "Argument 1: cannot convert from 'System.Span<char>' to 'object?'";
        const string asyncLocalMessage = "Feature 'ref and unsafe in async and iterator methods' is not available in C# 11.0. Please use language version 13.0 or greater.";
        foreach (var target in new[] { "Text", "Utf8" })
        {
            var template = RenderingTestHelper.Template($"#generate {target}\r\n[[ default(System.Span<char>) ]]");
            var compilation = GeneratorTestHelper.CreateCompilation();
            var driver = GeneratorTestHelper.CreateDriver([template]).RunGenerators(compilation);
            var tree = TemplateTrees(driver).Single();
            var expected = target == "Text"
                ? new[] { CompilerError(tree, 40, 73, 19, "CS1503", conversionMessage, mapped) }
                : new[]
                {
                    CompilerError(tree, 40, 73, 19, "CS1503", conversionMessage, mapped),
                    CompilerError(tree, 52, 19, 19, "CS9058", asyncLocalMessage, mapped),
                    CompilerError(tree, 52, 73, 19, "CS1503", conversionMessage, mapped)
                };

            var result = GeneratorTestHelper.Run(compilation, driver, expected);

            await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(4);
        }
    }

    [Test]
    public async Task Initialize_UnclosedBlock_MatchesReviewedDiagnostic()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("prefix [[ model"));

        await Verifier.Verify(result.RunResult);
    }

    [Test]
    [Arguments("Templates/other.html")]
    [Arguments("Templates/example.html")]
    [Arguments("Templates\\example.html")]
    public async Task Initialize_DuplicateTypes_DefersErrorsToCompiler(string secondPath)
    {
        var first = new TemplateAdditionalText(_requiredDirectives + "#generate Text\r\n");
        var second = new TemplateAdditionalText(_requiredDirectives + "#generate Text\r\n", secondPath);
        var compilation = GeneratorTestHelper.CreateCompilation();
        var driver = GeneratorTestHelper.CreateDriver([first, second]).RunGenerators(compilation);
        var trees = TemplateTrees(driver);
        const string ambiguousConstructor = "The call is ambiguous between the following methods or properties: " +
            "'ExampleTextTemplate.ExampleTextTemplate(TemplateOptions?)' and 'ExampleTextTemplate.ExampleTextTemplate(TemplateOptions?)'";
        var expected = new[]
        {
            CompilerError(trees[1], 21, 11, 19, "CS0111", "Type 'ExampleTextTemplate' already defines a member called 'ExampleTextTemplate' with the same parameter types"),
            CompilerError(trees[1], 28, 38, 7, "CS0102", "The type 'ExampleTextTemplate' already contains a definition for 'Default'"),
            CompilerError(trees[1], 31, 25, 6, "CS0111", "Type 'ExampleTextTemplate' already defines a member called 'Render' with the same parameter types"),
            CompilerError(trees[0], 28, 61, 19, "CS0121", ambiguousConstructor),
            CompilerError(trees[1], 28, 61, 19, "CS0121", ambiguousConstructor)
        };
        var result = GeneratorTestHelper.Run(compilation, driver, expected);

        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.Results.Single().GeneratedSources.Select(source => source.HintName).Distinct().Count()).IsEqualTo(5);
        // Sources follow the file order, but each hint name must name the same source either way
        var reversedDriver = GeneratorTestHelper.CreateDriver([second, first]).RunGenerators(compilation);
        await Assert.That(SourcesByHintName(reversedDriver.GetRunResult())).IsEqualTo(SourcesByHintName(result.RunResult));
    }

    // The support types change with the generation targets, so pick out template classes by hint name
    private static SyntaxTree[] TemplateTrees(GeneratorDriver driver)
    {
        string[] supportHintNames = ["TemplateOptions.g.cs", "TextTemplate.g.cs", "Utf8Template.g.cs"];

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Where(source => !supportHintNames.Contains(source.HintName, StringComparer.Ordinal))
            .Select(source => source.SyntaxTree)
            .ToArray();
    }

    private static string SourcesByHintName(GeneratorDriverRunResult runResult)
    {
        return string.Join("\n", runResult.Results.Single().GeneratedSources.OrderBy(source => source.HintName, StringComparer.Ordinal)
            .Select(source => source.HintName + "\n" + source.SourceText));
    }

    private static string HintNames(GeneratorTestResult result)
    {
        return string.Join("\n", result.RunResult.Results.Single().GeneratedSources.Select(source => source.HintName).Order(StringComparer.Ordinal));
    }

    private static ExpectedCompilerDiagnostic CompilerError(
        SyntaxTree tree, int line, int column, int length, string id, string message, LinePositionSpan? templateSpan = null)
    {
        var start = new LinePosition(line, column);
        var end = new LinePosition(line, column + length);
        var span = new TextSpan(tree.GetText().Lines.GetPosition(start), length);
        var mappedPath = templateSpan is null ? null : "Templates/example.html";

        return new ExpectedCompilerDiagnostic(id, message, new FileLinePositionSpan(tree.FilePath, start, end), span, LocationKind.SourceFile, mappedPath, templateSpan);
    }
}
