using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

public sealed partial class TemplateGeneratorTests
{
    private const string _baseAccessModifierKey = "build_property.AnglePlateBaseAccessModifier";

    [Test]
    [Arguments("#nullable enable", null)]
    [Arguments("# Title", null)]
    [Arguments("#!/bin/bash", null)]
    [Arguments(_requiredDirectives + "Hello", null)]
    [Arguments(_requiredDirectives + "Hello", "")]
    [Arguments(_requiredDirectives + "Hello", "Other")]
    [Arguments(_requiredDirectives + "Hello", "Discovered;Explicit")]
    public async Task Initialize_FileNotTaggedByAnglePlates_IsIgnoredWithoutReading(string content, string? anglePlate)
    {
        var file = new TemplateAdditionalText(content, "PublicAPI.Shipped.txt", anglePlate);

        var result = GeneratorTestHelper.Run(file).RunResult;

        await Assert.That(result.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(2);
        await Assert.That(file.GetTextCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("Discovered")]
    [Arguments("discovered")]
    [Arguments("Explicit")]
    [Arguments("EXPLICIT")]
    public async Task Initialize_AnglePlateMetadata_MatchesCaseInsensitively(string anglePlate)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("tagged", anglePlate: anglePlate));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(RenderingTestHelper.Render(result.Compilation).text).IsEqualTo("tagged");
    }

    [Test]
    [Arguments(null, "public")]
    [Arguments("", "public")]
    [Arguments("  ", "public")]
    [Arguments("public", "public")]
    [Arguments(" PUBLIC ", "public")]
    [Arguments("internal", "internal")]
    [Arguments("Internal", "internal")]
    [Arguments("\tinternal ", "internal")]
    public async Task Initialize_BaseAccessModifier_UsesCanonicalSpelling(string? value, string expected)
    {
        // A public template class can't derive from an internal base class, so internal support types need #access internal
        var body = expected == "internal" ? "#access internal\r\naccess" : "access";
        var result = RunWithAccessModifier(value, RenderingTestHelper.Template(body));
        var supportSources = result.RunResult.Results.Single().GeneratedSources.Take(3).Select(source => source.SourceText.ToString()).ToArray();

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(supportSources[0]).Contains($"{expected} class TemplateOptions");
        await Assert.That(supportSources[1]).Contains($"{expected} abstract class TextTemplate<TModel>");
        await Assert.That(supportSources[2]).Contains($"{expected} abstract class Utf8Template<TModel>");
        await Assert.That(RenderingTestHelper.Render(result.Compilation).text).IsEqualTo("access");
    }

    [Test]
    [Arguments("private")]
    [Arguments(" private ")]
    [Arguments("protected internal")]
    [Arguments("Internal;")]
    [Arguments("pub lic")]
    public async Task Initialize_InvalidBaseAccessModifier_ReportsInvalidMSBuildPropertyAndGeneratesNothing(string value)
    {
        var result = RunWithAccessModifier(value, RenderingTestHelper.Template("ignored"), new TemplateAdditionalText("<p>No directives</p>", "Emails/a.html", TemplateAdditionalText.Explicit));

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

    [Test]
    [Arguments("C:\\work\\App\\Templates\\a.html", "C:\\work\\App\\", "Templates/a.html")]
    [Arguments("C:\\Work\\App\\Templates\\A.html", "c:\\work\\app\\", "Templates/A.html")]
    [Arguments("C:\\work\\Shared\\receipt.html", "C:\\work\\App\\", "../Shared/receipt.html")]
    [Arguments("D:\\elsewhere\\Shared\\receipt.html", "D:\\elsewhere\\App", "../Shared/receipt.html")]
    [Arguments("C:\\Work\\Shared\\receipt.html", "c:\\work\\App\\Sub\\", "../../Shared/receipt.html")]
    [Arguments("C:\\Templates\\a.html", "C:\\", "Templates/a.html")]
    [Arguments("\\\\server\\share\\App\\Templates\\a.html", "\\\\SERVER\\share\\App\\", "Templates/a.html")]
    [Arguments("\\\\server\\share\\Shared\\receipt.html", "\\\\server\\share\\App\\", "../Shared/receipt.html")]
    [Arguments("/home/me/App/Templates/a.html", "/home/me/App/", "Templates/a.html")]
    [Arguments("/srv/build/Shared/receipt.html", "/srv/build/App", "../Shared/receipt.html")]
    [Arguments("/Repo/App/Templates/a.html", "/repo/app/", "../../Repo/App/Templates/a.html")]
    [Arguments("/Templates/a.html", "/", "Templates/a.html")]
    public async Task Initialize_TemplatePath_IsRelativeToProjectFolder(string path, string projectDir, string expected)
    {
        var result = RunWithProjectDir(path, projectDir);
        var sources = result.RunResult.Results.Single().GeneratedSources.Skip(3).Select(source => source.SourceText.ToString()).ToArray();

        await Assert.That(sources.Length).IsEqualTo(2);
        await Assert.That(sources.All(source => source.Contains($"render the contents of '{expected}'.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(HintNames(result)).IsEqualTo(HintNames(RunWithProjectDir(expected, string.Empty)));
        // #line keeps the full path so compiler errors still open the template
        await Assert.That(sources.All(source => source.Contains($"\"{path}\"", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Arguments("D:\\Templates\\a.html", "C:\\App\\", "D:/Templates/a.html")]
    [Arguments("/Templates/a.html", "C:\\App\\", "/Templates/a.html")]
    [Arguments("C:\\Templates\\a.html", "/App/", "C:/Templates/a.html")]
    [Arguments("\\\\server\\other\\Templates\\a.html", "\\\\server\\share\\App\\", "//server/other/Templates/a.html")]
    [Arguments("C:\\Templates\\a.html", "", "C:/Templates/a.html")]
    public async Task Initialize_TemplatePathWithoutRelativeForm_UsesFullPath(string path, string projectDir, string expected)
    {
        var result = RunWithProjectDir(path, projectDir);
        var sources = result.RunResult.Results.Single().GeneratedSources.Skip(3).Select(source => source.SourceText.ToString()).ToArray();

        await Assert.That(sources.All(source => source.Contains($"render the contents of '{expected}'.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(HintNames(result)).IsEqualTo(HintNames(RunWithProjectDir(expected, string.Empty)));
    }

    [Test]
    public async Task Initialize_TemplatePath_IsStableAcrossProjectLocations()
    {
        var windows = HintNames(RunWithProjectDir("C:\\agent\\1\\s\\Shared\\receipt.html", "C:\\agent\\1\\s\\App\\"));

        await Assert.That(HintNames(RunWithProjectDir("D:\\dev\\repo\\Shared\\receipt.html", "D:\\dev\\repo\\App"))).IsEqualTo(windows);
        await Assert.That(HintNames(RunWithProjectDir("/home/runner/work/repo/Shared/receipt.html", "/home/runner/work/repo/App/"))).IsEqualTo(windows);
        await Assert.That(HintNames(RunWithProjectDir("/home/runner/work/repo/Shared/Receipt.html", "/home/runner/work/repo/App/"))).IsNotEqualTo(windows);
    }

    [Test]
    public async Task Initialize_EditedUntaggedFile_DoesNotRerunTemplateStepsOrReadIt()
    {
        var template = RenderingTestHelper.Template("template");
        var untagged = new TemplateAdditionalText("#nullable enable", "PublicAPI.Shipped.txt", anglePlate: null);
        var edited = new TemplateAdditionalText("#nullable enable\r\nA.B()", "PublicAPI.Shipped.txt", anglePlate: null);
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([template, untagged]));

        var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(untagged, edited));
        var steps = after.RunResult.Results.Single().TrackedSteps;

        await Assert.That(untagged.GetTextCount).IsEqualTo(0);
        await Assert.That(edited.GetTextCount).IsEqualTo(0);
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await Assert.That(steps["AnglePlateFiles"].SelectMany(step => step.Outputs).All(output => output.Reason == IncrementalStepRunReason.Unchanged ||
            output.Reason == IncrementalStepRunReason.Cached)).IsTrue();
        await Assert.That(steps["TemplateFiles"].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray())
            .IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(HintNames(after)).IsEqualTo(HintNames(before));
    }

    private static GeneratorTestResult RunWithAccessModifier(string? baseAccessModifier, params AdditionalText[] templates)
    {
        IEnumerable<KeyValuePair<string, string>> globalOptions = baseAccessModifier is null ? [] : [new(_baseAccessModifierKey, baseAccessModifier)];
        var options = new TestAnalyzerConfigOptionsProvider(globalOptions);

        return GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver(templates, options));
    }

    private static GeneratorTestResult RunWithProjectDir(string path, string projectDir)
    {
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.ProjectDir"] = projectDir });

        return GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(),
            GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("a[[ model ]]", path: path)], options));
    }
}
