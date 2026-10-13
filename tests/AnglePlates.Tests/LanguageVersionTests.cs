using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// The C# 11 floor: SLAP010 below it, and generated code that compiles and renders at or above it
public sealed class LanguageVersionTests
{
    [Test]
    [Arguments(LanguageVersion.CSharp9, "9.0")]
    [Arguments(LanguageVersion.CSharp10, "10.0")]
    public async Task LanguageVersion_BelowFloor_ReportsUnsupportedLanguageVersionAndGeneratesNothing(LanguageVersion languageVersion, string displayVersion)
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
    public async Task LanguageVersion_AtOrAboveFloor_GeneratesAndRenders(LanguageVersion languageVersion)
    {
        var compilation = GeneratorTestHelper.CreateCompilation(languageVersion: languageVersion);

        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("Hello [[ model ]]")], languageVersion: languageVersion));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(6);
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", "Hello &lt;value&gt;");
    }

    [Test]
    public async Task LanguageVersion_BelowFloorWithInvalidBaseAccessModifier_ReportsBoth()
    {
        var options = TestAnalyzerConfigOptionsProvider.WithAccessModifier("private");
        var compilation = GeneratorTestHelper.CreateCompilation(languageVersion: LanguageVersion.CSharp10);
        var driver = GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("ignored")], options, LanguageVersion.CSharp10);

        var result = GeneratorTestHelper.Run(compilation, driver).RunResult;

        await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(new[] { "SLAP010", "SLAP009" });
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }
}
