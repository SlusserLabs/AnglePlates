using System;
using System.Collections.Generic;
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

public sealed partial class TemplateGeneratorTests
{
    [Test]
    [Arguments("", true, true, 5)]
    [Arguments("#generate Text\r\n", true, false, 4)]
    [Arguments("#generate Utf8\r\n", false, true, 4)]
    public async Task Initialize_GenerateDirective_GeneratesSelectedTypes(string generateDirective, bool hasTextType, bool hasUtf8Type, int treeCount)
    {
        var template = _requiredDirectives + generateDirective + "Hello [[ model ]]";

        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;
        var generatedSource = string.Join(Environment.NewLine, result.GeneratedTrees.Select(tree => tree.GetText().ToString()));

        // Both base classes are generated whatever the template's targets
        await Assert.That(result.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(treeCount);
        await Assert.That(generatedSource.Contains("internal partial class ExampleTextTemplate", StringComparison.Ordinal)).IsEqualTo(hasTextType);
        await Assert.That(generatedSource.Contains("internal partial class ExampleUtf8Template", StringComparison.Ordinal)).IsEqualTo(hasUtf8Type);
        await Assert.That(generatedSource.Contains("internal abstract class TextTemplate<TModel>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generatedSource.Contains("internal abstract class Utf8Template<TModel>", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    [Arguments(LanguageVersion.CSharp9, "9.0")]
    [Arguments(LanguageVersion.CSharp10, "10.0")]
    public async Task Initialize_LanguageVersionBelowFloor_ReportsUnsupportedLanguageVersionAndGeneratesNothing(LanguageVersion languageVersion, string displayVersion)
    {
        var templates = new[] { RenderingTestHelper.Template("ignored"), new TemplateAdditionalText("<p>No directives</p>", "Emails/a.html", TemplateAdditionalText.Explicit) };
        var compilation = GeneratorTestHelper.CreateCompilation(languageVersion: languageVersion);

        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver(templates, languageVersion: languageVersion)).RunResult;

        await Assert.That(result.Diagnostics.Length).IsEqualTo(1);
        var diagnostic = result.Diagnostics[0];
        await Assert.That(diagnostic.Id).IsEqualTo("SLAP010");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Unsupported C# language version");
        await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Descriptor.IsEnabledByDefault).IsTrue();
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            $"AnglePlates requires C# 11 or later, but this project uses C# {displayVersion}; set LangVersion to 11 or later");
        await Assert.That(diagnostic.Location).IsEqualTo(Location.None);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    [Arguments(LanguageVersion.CSharp11)]
    [Arguments(LanguageVersion.CSharp12)]
    [Arguments(LanguageVersion.Latest)]
    [Arguments(LanguageVersion.Preview)]
    public async Task Initialize_LanguageVersionAtOrAboveFloor_GeneratesAndRenders(LanguageVersion languageVersion)
    {
        var compilation = GeneratorTestHelper.CreateCompilation(languageVersion: languageVersion);

        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("Hello [[ model ]]")], languageVersion: languageVersion));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await AssertRenders(result.Compilation, "Consumer.Example", "Hello &lt;value&gt;");
    }

    [Test]
    public async Task Initialize_LanguageVersionAndBaseAccessModifierInvalid_ReportsBoth()
    {
        var options = new TestAnalyzerConfigOptionsProvider([new KeyValuePair<string, string>(_baseAccessModifierKey, "private")]);
        var compilation = GeneratorTestHelper.CreateCompilation(languageVersion: LanguageVersion.CSharp10);
        var driver = GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("ignored")], options, LanguageVersion.CSharp10);

        var result = GeneratorTestHelper.Run(compilation, driver).RunResult;

        await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(new[] { "SLAP010", "SLAP009" });
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    [Arguments("type", "SLAP001", "Missing type", "type directive such as '#type MyTemplate'")]
    [Arguments("namespace", "SLAP002", "Missing namespace", "namespace directive such as '#namespace MyNamespace'")]
    [Arguments("model", "SLAP003", "Missing model type", "model type directive such as '#model MyModel'")]
    public async Task Initialize_MissingRequiredDirective_ReportsStableDiagnostic(string directive, string id, string title, string message)
    {
        var template = string.Join("\r\n", new[] { "#type Example", "#namespace Consumer", "#model string" }
            .Where(line => !line.StartsWith("#" + directive + " ", StringComparison.Ordinal))) + "\r\nHello";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;

        await Assert.That(result.Diagnostics.Length).IsEqualTo(1);
        var diagnostic = result.Diagnostics[0];
        await Assert.That(diagnostic.Id).IsEqualTo(id);
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo(title);
        await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Descriptor.IsEnabledByDefault).IsTrue();
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Template file 'Templates/example.html' is missing a {message}");
        // The directive section ends at the body, so that's where the missing directive is reported
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan("Templates/example.html",
            new LinePosition(2, 0), new LinePosition(2, 5)));
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Initialize_AllRequiredDirectivesMissing_ReportsAllDiagnostics()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText("#generate Text\r\nHello")).RunResult;

        await Assert.That(string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id))).IsEqualTo("SLAP001,SLAP002,SLAP003");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(3);
    }

    [Test]
    [Arguments("## Release 1.0\r\n### New Rules", "Templates/README.md", 14)]
    [Arguments("## Release 1.0\r\n### New Rules", "AnalyzerReleases.Shipped.md", 14)]
    [Arguments("#tyep Example\r\n#model string\r\n#namespace Consumer\r\nHello", "Templates/typo.html", 13)]
    [Arguments("\r\n#type Example\r\n#model string\r\n#namespace Consumer\r\nHello", "Templates/blank.html", 0)]
    [Arguments("<p>Hello</p>", "Emails/welcome.html", 12)]
    [Arguments("# Notes", "Templates/notes.md", 7)]
    [Arguments("#", "Templates/hash.html", 1)]
    [Arguments("#   ", "Templates/padded.html", 4)]
    [Arguments("#\r\n#\t\r\n", "Templates/hashes.html", 1)]
    [Arguments("", "Templates/empty.html", 0)]
    public async Task Initialize_FileWithoutDirectives_SkipsDiscoveredAndReportsExplicit(string content, string path, int firstLineLength)
    {
        // A bare # isn't a known directive either, and the check looks only at content, so the file name doesn't matter
        var valid = RenderingTestHelper.Template("valid", path: "Templates/valid.html");
        var discovered = GeneratorTestHelper.Run(new TemplateAdditionalText(content, path), valid);
        var explicitFile = GeneratorTestHelper.Run(new TemplateAdditionalText(content, path, TemplateAdditionalText.Explicit)).RunResult;

        await Assert.That(discovered.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(discovered.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(RenderingTestHelper.Render(discovered.Compilation).text).IsEqualTo("valid");

        await Assert.That(explicitFile.Diagnostics.Length).IsEqualTo(1);
        var diagnostic = explicitFile.Diagnostics[0];
        await Assert.That(diagnostic.Id).IsEqualTo("SLAP008");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Template has no directives");
        await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            $"Template file '{path}' has no directives; a file added with an AnglePlate Include must start with a directive such as '#type MyTemplate'");
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(path, new LinePosition(0, 0), new LinePosition(0, firstLineLength)));
        await Assert.That(explicitFile.GeneratedTrees.Length).IsEqualTo(3);
    }
}
