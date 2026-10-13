using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// What MSBuild hands the generator: the AnglePlate tag on each file, the base access modifier, and the project folder
public sealed class MSBuildOptionTests
{
    [Test]
    [Arguments("#nullable enable", null)]
    [Arguments("# Title", null)]
    [Arguments("#!/bin/bash", null)]
    [Arguments(TemplateText.Header + "Hello", null)]
    [Arguments(TemplateText.Header + "Hello", "")]
    [Arguments(TemplateText.Header + "Hello", "Other")]
    [Arguments(TemplateText.Header + "Hello", "Discovered;Explicit")]
    public async Task AnglePlateTag_MissingOrUnknown_IgnoresFileWithoutReadingIt(string content, string? anglePlate)
    {
        var file = new TemplateAdditionalText(content, "PublicAPI.Shipped.txt", anglePlate);

        var result = GeneratorTestHelper.Run(file).RunResult;

        await Assert.That(result.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
        await Assert.That(file.GetTextCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("Discovered")]
    [Arguments("discovered")]
    [Arguments("Explicit")]
    [Arguments("EXPLICIT")]
    public async Task AnglePlateTag_AnyCase_Matches(string anglePlate)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("tagged", anglePlate: anglePlate));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(6);
        await Assert.That(RenderingTestHelper.Render(result.Compilation).text).IsEqualTo("tagged");
    }

    [Test]
    [Arguments(null, "internal")]
    [Arguments("", "internal")]
    [Arguments("  ", "internal")]
    [Arguments("public", "public")]
    [Arguments(" PUBLIC ", "public")]
    [Arguments("internal", "internal")]
    [Arguments("Internal", "internal")]
    [Arguments("\tinternal ", "internal")]
    public async Task BaseAccessModifier_ValidValue_UsesCanonicalSpelling(string? value, string expected)
    {
        // Templates are internal by default, so public support types also get a public template to check both together
        var body = expected == "public" ? "#access public\r\naccess" : "access";
        var result = GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithAccessModifier(value), RenderingTestHelper.Template(body));
        var accessibility = expected == "public" ? Accessibility.Public : Accessibility.Internal;
        string[] typeNames =
        [
            "SlusserLabs.AnglePlates.TemplateOptions",
            "SlusserLabs.AnglePlates.TextTemplate`1",
            "SlusserLabs.AnglePlates.Utf8Template`1",
            "SlusserLabs.AnglePlates.TemplateExtensions",
            "Consumer.ExampleTextTemplate",
            "Consumer.ExampleUtf8Template"
        ];

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(6);
        await Assert.That(SupportSource(result, "TemplateOptions")).Contains($"{expected} sealed class TemplateOptions");
        await Assert.That(SupportSource(result, "TextTemplate")).Contains($"{expected} abstract class TextTemplate<TModel>");
        await Assert.That(SupportSource(result, "Utf8Template")).Contains($"{expected} abstract class Utf8Template<TModel>");
        await Assert.That(SupportSource(result, "TemplateExtensions")).Contains($"{expected} static class TemplateExtensions");
        foreach (var typeName in typeNames)
        {
            await Assert.That(result.Compilation.GetTypeByMetadataName(typeName)!.DeclaredAccessibility).IsEqualTo(accessibility);
        }

        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", "access");
    }

    [Test]
    [Arguments("private")]
    [Arguments(" private ")]
    [Arguments("protected internal")]
    [Arguments("Internal;")]
    [Arguments("pub lic")]
    public async Task BaseAccessModifier_InvalidValue_ReportsInvalidMSBuildPropertyAndGeneratesNothing(string value)
    {
        var result = GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithAccessModifier(value),
            RenderingTestHelper.Template("ignored"), new TemplateAdditionalText("<p>No directives</p>", "Emails/a.html", TemplateAdditionalText.Explicit));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(1);
        var diagnostic = result.RunResult.Diagnostics[0];
        await Assert.That(diagnostic.Id).IsEqualTo("SLAP009");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Invalid AnglePlates MSBuild property");
        await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            $"MSBuild property 'AnglePlateBaseAccessModifier' has invalid value '{value}'; expected 'public' or 'internal'");
        await Assert.That(diagnostic.Location).IsEqualTo(Location.None);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(0);
    }

    // TemplatePathTests holds the path edge cases; these rows check that the result reaches the generated sources
    [Test]
    [Arguments("C:\\work\\App\\Templates\\a.html", "C:\\work\\App\\", "Templates/a.html")]
    [Arguments("/srv/build/Shared/receipt.html", "/srv/build/App", "../Shared/receipt.html")]
    [Arguments("\\\\server\\share\\App\\Templates\\a.html", "\\\\SERVER\\share\\App\\", "Templates/a.html")]
    // Paths with no relative form fall back to the full path
    [Arguments("D:\\Templates\\a.html", "C:\\App\\", "D:/Templates/a.html")]
    public async Task ProjectDir_TemplatePath_DocumentsPathRelativeToProjectFolderOrFull(string path, string projectDir, string expected)
    {
        var result = RunWithProjectDir(path, projectDir);
        var sources = result.TemplateSources;

        await Assert.That(sources.Length).IsEqualTo(2);
        await Assert.That(sources.All(source => source.Contains($"renders '{expected}'.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(sources.All(source => source.Contains($"//     Generated by SlusserLabs.AnglePlates from {expected}.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.HintNames()).IsEqualTo(RunWithProjectDir(expected, string.Empty).HintNames());
        // #line keeps the full path so compiler errors still open the template
        await Assert.That(sources.All(source => source.Contains($"\"{path}\"", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Arguments("Shared/receipt.html", "", "Shared/receipt.html")]
    [Arguments("Shared\\receipt.html", "", "Shared/receipt.html")]
    [Arguments("C:\\repo\\Shared\\receipt.html", "C:\\repo\\", "Shared/receipt.html")]
    [Arguments("/repo/Shared/receipt.html", "/repo/", "Shared/receipt.html")]
    [Arguments("Shared/Receipt.html", "", "Shared/Receipt.html")]
    [Arguments("Shared/invoice.html", "", "Shared/invoice.html")]
    [Arguments("/repository/Shared/receipt.html", "/repo", "../repository/Shared/receipt.html")]
    [Arguments("C:\\agent\\1\\s\\Shared\\receipt.html", "C:\\agent\\1\\s\\App\\", "../Shared/receipt.html")]
    [Arguments("D:\\dev\\repo\\Shared\\receipt.html", "D:\\dev\\repo\\App", "../Shared/receipt.html")]
    [Arguments("/home/runner/work/repo/Shared/receipt.html", "/home/runner/work/repo/App/", "../Shared/receipt.html")]
    [Arguments("/home/runner/work/repo/Shared/Receipt.html", "/home/runner/work/repo/App/", "../Shared/Receipt.html")]
    public async Task ProjectDir_AnyTemplateLocation_KeepsHintNamesAndDocumentsRelativePath(string path, string projectDir, string expected)
    {
        // Hint names come from the declared class, so only the documented path follows the template's location
        var result = RunWithProjectDir(path, projectDir);

        await Assert.That(GeneratedSources.Templates(result.RunResult).Select(source => source.HintName))
            .IsEquivalentTo(new[] { "Consumer.ExampleTextTemplate.g.cs", "Consumer.ExampleUtf8Template.g.cs" });
        await Assert.That(result.TemplateSources.All(source => source.Contains($"from {expected}.", StringComparison.Ordinal))).IsTrue();
    }

    private static GeneratorTestResult RunWithProjectDir(string path, string projectDir)
    {
        return GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithProjectDir(projectDir), RenderingTestHelper.Template("a[[ model ]]", path: path));
    }

    private static string SupportSource(GeneratorTestResult result, string typeName)
    {
        return result.GeneratedSource($"SlusserLabs.AnglePlates.{typeName}.g.cs").SourceText.ToString();
    }
}
