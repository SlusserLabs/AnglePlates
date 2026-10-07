using System;
using System.Collections.Generic;
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
        var result = RenderingTestHelper.Generate(templates.ToArray());
        var first = RenderingTestHelper.Render(result.Compilation, type: "Consumer.First");
        var second = RenderingTestHelper.Render(result.Compilation, type: "Consumer.Second");

        await Assert.That(string.Join(",", result.RunResult.Diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal)))
            .IsEqualTo("SLAP001,SLAP002,SLAP003,SLAP004,SLAP004");
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
        var result = RenderingTestHelper.Generate(templates);
        var reversed = RenderingTestHelper.Generate(templates.Reverse().ToArray());
        var changed = RenderingTestHelper.Generate(templates[0], RenderingTestHelper.Template("changed", ns: "Two", path: "Templates/b.html"));

        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(7);
        await Assert.That(HintNames(result)).IsEqualTo(HintNames(reversed));
        await Assert.That(HintNames(result)).IsEqualTo(HintNames(changed));
        await Assert.That(RenderingTestHelper.Render(result.Compilation, type: "One.Example").text).IsEqualTo("one");
        await Assert.That(RenderingTestHelper.Render(result.Compilation, type: "Two.Example").text).IsEqualTo("two");
    }

    [Test]
    public async Task Initialize_PathIdentity_NormalizesSeparatorsAndPreservesCase()
    {
        string Names(string path, string projectDirectory)
        {
            var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.ProjectDir"] = projectDirectory });
            var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(),
                GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("text", path: path)], options));

            return HintNames(result);
        }

        var expected = Names("Templates/a.html", string.Empty);

        await Assert.That(Names("Templates\\a.html", string.Empty)).IsEqualTo(expected);
        await Assert.That(Names("C:\\repo\\Templates\\a.html", "C:\\repo\\")).IsEqualTo(expected);
        await Assert.That(Names("/repo/Templates/a.html", "/repo/")).IsEqualTo(expected);
        await Assert.That(Names("Templates/A.html", string.Empty)).IsNotEqualTo(expected);
        await Assert.That(Names("Templates/b.html", string.Empty)).IsNotEqualTo(expected);
        await Assert.That(Names("/repository/Templates/a.html", "/repo")).IsNotEqualTo(expected);
    }

    [Test]
    public async Task Initialize_ChangedTemplate_ReusesNamesWithOriginalConsumerCompilation()
    {
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("before")]));
        var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalTexts([RenderingTestHelper.Template("after")]));

        await Assert.That(HintNames(after)).IsEqualTo(HintNames(before));
        await Assert.That(RenderingTestHelper.Render(after.Compilation).text).IsEqualTo("after");
    }

    [Test]
    public async Task Initialize_BracketsAndEmptyBlocks_MatchesReviewedSources()
    {
        var result = RenderingTestHelper.Generate(RenderingTestHelper.Template("[ordinary] [[ model ]]\r\na \r\n[[- -]][%- -%][*- -*]\r\nb"));

        await Verifier.Verify(result.RunResult);
    }

    [Test]
    public async Task Initialize_UnclosedBlock_MatchesReviewedDiagnostic()
    {
        var result = RenderingTestHelper.Generate(RenderingTestHelper.Template("prefix [[ model"));

        await Verifier.Verify(result.RunResult);
    }

    [Test]
    [Arguments("Templates/other.html")]
    [Arguments("Templates/example.html")]
    [Arguments("Templates\\example.html")]
    public async Task Initialize_DuplicateTypes_DefersErrorsToCompiler(string secondPath)
    {
        var first = new TemplateAdditionalText("#type Example\r\n#model string\r\n#namespace Consumer\r\n#generate Text\r\n");
        var second = new TemplateAdditionalText("#type Example\r\n#model string\r\n#namespace Consumer\r\n#generate Text\r\n", secondPath);
        var compilation = GeneratorTestHelper.CreateCompilation();
        var driver = GeneratorTestHelper.CreateDriver([first, second]).RunGenerators(compilation);
        var trees = driver.GetRunResult().GeneratedTrees.Skip(3).ToArray();
        const string ambiguousConstructor = "The call is ambiguous between the following methods or properties: " +
            "'ExampleTextTemplate.ExampleTextTemplate(TemplateOptions?)' and 'ExampleTextTemplate.ExampleTextTemplate(TemplateOptions?)'";
        const string ambiguousField = "Ambiguity between 'ExampleTextTemplate._default' and 'ExampleTextTemplate._default'";
        var expected = new[]
        {
            CompilerError(trees[1], 21, 72, 8, "CS0102", "The type 'ExampleTextTemplate' already contains a definition for '_default'"),
            CompilerError(trees[1], 28, 11, 19, "CS0111", "Type 'ExampleTextTemplate' already defines a member called 'ExampleTextTemplate' with the same parameter types"),
            CompilerError(trees[1], 35, 38, 7, "CS0102", "The type 'ExampleTextTemplate' already contains a definition for 'Default'"),
            CompilerError(trees[1], 38, 25, 6, "CS0111", "Type 'ExampleTextTemplate' already defines a member called 'Render' with the same parameter types"),
            CompilerError(trees[0], 21, 98, 19, "CS0121", ambiguousConstructor),
            CompilerError(trees[1], 21, 98, 19, "CS0121", ambiguousConstructor),
            CompilerError(trees[0], 35, 49, 8, "CS0229", ambiguousField),
            CompilerError(trees[1], 35, 49, 8, "CS0229", ambiguousField)
        };
        var result = GeneratorTestHelper.Run(compilation, driver, expected);

        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.Results.Single().GeneratedSources.Select(source => source.HintName).Distinct().Count()).IsEqualTo(5);
        var reversedDriver = GeneratorTestHelper.CreateDriver([second, first]).RunGenerators(compilation);
        await Assert.That(string.Join("\n", reversedDriver.GetRunResult().Results.Single().GeneratedSources.Select(source => source.HintName)))
            .IsEqualTo(string.Join("\n", result.RunResult.Results.Single().GeneratedSources.Select(source => source.HintName)));
    }

    private static string HintNames(GeneratorTestResult result)
    {
        return string.Join("\n", result.RunResult.Results.Single().GeneratedSources.Select(source => source.HintName).Order(StringComparer.Ordinal));
    }

    private static ExpectedCompilerDiagnostic CompilerError(SyntaxTree tree, int line, int column, int length, string id, string message)
    {
        var start = new LinePosition(line, column);
        var end = new LinePosition(line, column + length);
        var span = new TextSpan(tree.GetText().Lines.GetPosition(start), length);

        return new ExpectedCompilerDiagnostic(id, message, new FileLinePositionSpan(tree.FilePath, start, end), span, LocationKind.SourceFile);
    }
}
