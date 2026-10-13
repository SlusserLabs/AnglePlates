using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Generated file names: readable names from the declared class, and numbered names when two classes share one
public sealed class HintNameTests
{
    [Test]
    [Arguments("#namespace Consumer", "Consumer", "Consumer.Example")]
    [Arguments("#namespace MyApp.Templates", "MyApp.Templates", "MyApp.Templates.Example")]
    // The namespace part uses each identifier's value, without a verbatim '@' or a Unicode escape
    [Arguments("#namespace @class", "class", "class.Example")]
    [Arguments("#namespace My.\\u0041pp", "My.App", "My.App.Example")]
    public async Task HintName_DeclaredClass_NamesTheSourceAfterIt(string namespaceDirective, string hintNamespace, string type)
    {
        var template = new TemplateAdditionalText(TemplateText.WithDirective(namespaceDirective) + "[[ model ]]");
        string[] expected = [$"{hintNamespace}.ExampleTextTemplate.g.cs", $"{hintNamespace}.ExampleUtf8Template.g.cs", .. GeneratedSources.SupportHintNames];

        var result = GeneratorTestHelper.Run(template);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.HintNames()).IsEqualTo(string.Join("\n", expected.Order(StringComparer.Ordinal)));
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, type, "&lt;value&gt;");
    }

    [Test]
    public async Task HintName_SameTypeInTwoNamespaces_IsUniqueAndIndependentOfFileOrderAndContent()
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
        await Assert.That(result.GeneratedSource("One.ExampleTextTemplate.g.cs").SourceText.ToString()).Contains("namespace One;");
        await Assert.That(result.GeneratedSource("Two.ExampleUtf8Template.g.cs").SourceText.ToString()).Contains("namespace Two;");
        await Assert.That(result.HintNames()).IsEqualTo(reversed.HintNames());
        await Assert.That(result.HintNames()).IsEqualTo(changed.HintNames());
        await Assert.That(RenderingTestHelper.Render(result.Compilation, type: "One.Example").text).IsEqualTo("one");
        await Assert.That(RenderingTestHelper.Render(result.Compilation, type: "Two.Example").text).IsEqualTo("two");
    }

    [Test]
    public async Task HintName_TwoClashingPairs_NumbersEachPairIndependentlyOfFileOrder()
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
        var numbered = result.GeneratedSources.Where(source => source.HintName.EndsWith(".2.g.cs", StringComparison.Ordinal)).ToArray();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.GeneratedSources.Length).IsEqualTo(7);
        await Assert.That(SourcesByHintName(reversed)).IsEquivalentTo(SourcesByHintName(result));
        await Assert.That(numbered.Select(source => source.HintName)).IsEquivalentTo(
            new[] { "Consumer.AlphaTextTemplate.2.g.cs", "Consumer.BetaTextTemplate.2.g.cs" });
        foreach (var source in numbered)
        {
            // The clashing sources share a path, so they're sorted by content and the second template always takes the suffix
            await Assert.That(source.SourceText.ToString()).Contains("\"second\"");
        }

        static string[] SourcesByHintName(GeneratorRunResult runResult)
        {
            return runResult.GeneratedSources.Select(source => $"{source.HintName}={source.SourceText}").ToArray();
        }
    }

    [Test]
    [Arguments("Templates/other.html")]
    [Arguments("Templates/example.html")]
    [Arguments("Templates\\example.html")]
    public async Task HintName_SameClassInTwoTemplates_NumbersSourcesAndDefersErrorsToCompiler(string secondPath)
    {
        // The template classes are partial, so the compiler reports their repeated members rather than a repeated class
        var first = new TemplateAdditionalText(TemplateText.Header + "#generate Text\r\n");
        var second = new TemplateAdditionalText(TemplateText.Header + "#generate Text\r\n", secondPath);
        const string firstFile = "Consumer.ExampleTextTemplate.g.cs";
        const string secondFile = "Consumer.ExampleTextTemplate.2.g.cs";
        const string ambiguousConstructor = "The call is ambiguous between the following methods or properties: " +
            "'ExampleTextTemplate.ExampleTextTemplate(TemplateOptions?)' and 'ExampleTextTemplate.ExampleTextTemplate(TemplateOptions?)'";
        var expected = new[]
        {
            ExpectedCompilerDiagnostic.Generated("CS0111", "Type 'ExampleTextTemplate' already defines a member called 'ExampleTextTemplate' with the same parameter types", secondFile),
            ExpectedCompilerDiagnostic.Generated("CS0102", "The type 'ExampleTextTemplate' already contains a definition for 'Default'", secondFile),
            ExpectedCompilerDiagnostic.Generated("CS0111", "Type 'ExampleTextTemplate' already defines a member called 'Render' with the same parameter types", secondFile),
            ExpectedCompilerDiagnostic.Generated("CS0121", ambiguousConstructor, firstFile),
            ExpectedCompilerDiagnostic.Generated("CS0121", ambiguousConstructor, secondFile)
        };
        var compilation = GeneratorTestHelper.CreateCompilation();

        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([first, second]), expected);
        var reversed = GeneratorTestHelper.CreateDriver([second, first]).RunGenerators(compilation).GetRunResult();

        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.GeneratorResult.GeneratedSources.Select(source => source.HintName).Distinct().Count()).IsEqualTo(5);
        // Sources follow the file order, but each hint name must name the same source either way
        await Assert.That(GeneratedSources.ByHintName(reversed)).IsEqualTo(GeneratedSources.ByHintName(result.RunResult));
    }

    [Test]
    public async Task HintName_ClassesDifferingOnlyByCase_NumbersSourcesAndCompiles()
    {
        // App.Page and app.page are different C# classes, but Roslyn compares hint names without case
        var upper = new TemplateAdditionalText("#type Page\r\n#model string\r\n#namespace App\r\nupper [[ model ]]", "Templates/a.html");
        var lower = new TemplateAdditionalText("#type page\r\n#model string\r\n#namespace app\r\nlower [[ model ]]", "Templates/b.html");
        string[] expectedNames =
        [
            "App.PageTextTemplate.g.cs",
            "App.PageUtf8Template.g.cs",
            "app.pageTextTemplate.2.g.cs",
            "app.pageUtf8Template.2.g.cs"
        ];

        var result = GeneratorTestHelper.Run(upper, lower);
        var reversed = GeneratorTestHelper.Run(lower, upper);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(GeneratedSources.Templates(result.RunResult).Select(source => source.HintName)).IsEquivalentTo(expectedNames);
        await Assert.That(GeneratedSources.ByHintName(reversed.RunResult)).IsEqualTo(GeneratedSources.ByHintName(result.RunResult));
        await Assert.That(result.GeneratedSource("app.pageTextTemplate.2.g.cs").SourceText.ToString()).Contains("partial class pageTextTemplate");
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "App.Page", "upper &lt;value&gt;");
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "app.page", "lower &lt;value&gt;");
    }
}
