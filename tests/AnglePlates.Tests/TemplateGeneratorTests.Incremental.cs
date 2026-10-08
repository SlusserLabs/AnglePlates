using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

public sealed partial class TemplateGeneratorTests
{
    [Test]
    public async Task Initialize_OneOfTwoTemplatesEdited_ReprocessesOnlyThatTemplate()
    {
        var first = RenderingTestHelper.Template("first", type: "First", path: "Templates/first.html");
        var second = RenderingTestHelper.Template("second", type: "Second", path: "Templates/second.html");
        var edited = RenderingTestHelper.Template("edited", type: "Second", path: "Templates/second.html");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([first, second]));
        var firstHintNames = before.RunResult.Results.Single().GeneratedSources
            .Where(source => source.SourceText.ToString().Contains("class FirstTextTemplate", StringComparison.Ordinal) ||
                source.SourceText.ToString().Contains("class FirstUtf8Template", StringComparison.Ordinal))
            .Select(source => source.HintName)
            .ToArray();

        var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(second, edited));
        var outputReasons = after.RunResult.Results.Single().TrackedOutputSteps.SelectMany(step => step.Value).SelectMany(step => step.Outputs)
            .Select(output => output.Reason).ToArray();

        await Assert.That(StepReasons(after, "TemplateResults")).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified });
        await Assert.That(outputReasons.Count(reason => reason == IncrementalStepRunReason.Modified)).IsEqualTo(1);
        await Assert.That(first.GetTextCount).IsEqualTo(1);
        await Assert.That(edited.GetTextCount).IsEqualTo(1);
        await Assert.That(HintNames(after)).IsEqualTo(HintNames(before));
        // Roslyn 4.14 parses a new tree for every generated source on each run, so the reused text is what proves the cache
        await Assert.That(firstHintNames.Length).IsEqualTo(2);
        foreach (var hintName in firstHintNames)
        {
            await Assert.That(ReferenceEquals(GeneratedSource(after, hintName).SourceText, GeneratedSource(before, hintName).SourceText)).IsTrue();
        }

        await AssertRenders(after.Compilation, "Consumer.First", "first");
        await AssertRenders(after.Compilation, "Consumer.Second", "edited");
    }

    [Test]
    public async Task Initialize_UnrelatedConsumerSourceChanged_KeepsEveryTemplateCached()
    {
        var template = RenderingTestHelper.Template("template");
        var before = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]));
        var changed = GeneratorTestHelper.CreateCompilation(["namespace Consumer; public static class Unrelated { }"]);

        var after = GeneratorTestHelper.Run(changed, before.Driver);

        await Assert.That(StepReasons(after, "TemplateFiles")).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(StepReasons(after, "TemplateResults")).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await Assert.That(HintNames(after)).IsEqualTo(HintNames(before));
    }

    [Test]
    public async Task Initialize_TemplateAddedThenRemoved_LeavesExistingTemplateCached()
    {
        var first = RenderingTestHelper.Template("first", type: "First", path: "Templates/first.html");
        var added = RenderingTestHelper.Template("added", type: "Added", path: "Templates/added.html");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([first]));

        var withAdded = GeneratorTestHelper.Run(compilation, before.Driver.AddAdditionalTexts([added]));
        var addedReasons = StepReasons(withAdded, "TemplateResults");
        var afterRemoval = GeneratorTestHelper.Run(compilation, withAdded.Driver.RemoveAdditionalTexts([added]));

        await Assert.That(addedReasons).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.New });
        await AssertRenders(withAdded.Compilation, "Consumer.Added", "added");
        await Assert.That(StepReasons(afterRemoval, "TemplateResults")).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Removed });
        await Assert.That(first.GetTextCount).IsEqualTo(1);
        await Assert.That(HintNames(afterRemoval)).IsEqualTo(HintNames(before));
        await AssertRenders(afterRemoval.Compilation, "Consumer.First", "first");
    }

    [Test]
    public async Task Initialize_BaseAccessModifierChanged_RegeneratesSupportTypesFromCachedTemplates()
    {
        // The default internal template class works under both public and internal support types
        var template = RenderingTestHelper.Template("access");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([template], AccessModifierOptions("public")));

        var changed = GeneratorTestHelper.Run(compilation, before.Driver.WithUpdatedAnalyzerConfigOptions(AccessModifierOptions("internal")));
        var changedReasons = StepReasons(changed, "TemplateResults");
        var invalid = GeneratorTestHelper.Run(compilation, changed.Driver.WithUpdatedAnalyzerConfigOptions(AccessModifierOptions("private")));
        var invalidReasons = StepReasons(invalid, "TemplateResults");
        var restored = GeneratorTestHelper.Run(compilation, invalid.Driver.WithUpdatedAnalyzerConfigOptions(AccessModifierOptions("internal")));

        await Assert.That(changedReasons).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(changed.RunResult.Results.Single().GeneratedSources[0].SourceText.ToString()).Contains("internal class TemplateOptions");
        await Assert.That(invalidReasons).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(invalid.RunResult.Diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(new[] { "SLAP009" });
        await Assert.That(invalid.RunResult.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(StepReasons(restored, "TemplateResults")).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(restored.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(HintNames(restored)).IsEqualTo(HintNames(changed));
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await AssertRenders(restored.Compilation, "Consumer.Example", "access");
    }

    [Test]
    public async Task Initialize_LanguageVersionRaisedToFloor_GeneratesFromCachedTemplates()
    {
        var template = RenderingTestHelper.Template("version");
        var before = GeneratorTestHelper.Run(
            GeneratorTestHelper.CreateCompilation(languageVersion: LanguageVersion.CSharp10), GeneratorTestHelper.CreateDriver([template], languageVersion: LanguageVersion.CSharp10));

        var after = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), before.Driver.WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.CSharp11)));

        await Assert.That(before.RunResult.Diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(new[] { "SLAP010" });
        await Assert.That(before.RunResult.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(StepReasons(after, "TemplateResults")).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(after.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await AssertRenders(after.Compilation, "Consumer.Example", "version");
    }

    [Test]
    public async Task Initialize_OtherTemplateEdited_ReportsCachedDiagnosticsAgain()
    {
        var broken = new TemplateAdditionalText(_requiredDirectives + "a\r\n[[ model", "Templates/broken.html");
        var good = RenderingTestHelper.Template("good", type: "Good", path: "Templates/good.html");
        var edited = RenderingTestHelper.Template("edited", type: "Good", path: "Templates/good.html");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([broken, good]));

        var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(good, edited));

        await Assert.That(StepReasons(after, "TemplateResults")).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified });
        await Assert.That(before.RunResult.Diagnostics.Select(diagnostic => diagnostic.ToString())).IsEquivalentTo(new[]
        {
            "Templates/broken.html(5,1): error SLAP004: Template block is missing its closing delimiter ']]'"
        });
        await Assert.That(after.RunResult.Diagnostics.Select(diagnostic => diagnostic.ToString()))
            .IsEquivalentTo(before.RunResult.Diagnostics.Select(diagnostic => diagnostic.ToString()));
        await Assert.That(broken.GetTextCount).IsEqualTo(1);
        await AssertRenders(after.Compilation, "Consumer.Good", "edited");
    }

    [Test]
    public async Task Initialize_OneDuplicateEdited_KeepsNumberedHintNames()
    {
        // Identical paths and declared types collide, so the compiler reports duplicate members; only the hint names matter here
        var first = new TemplateAdditionalText(_requiredDirectives + "#generate Text\r\nfirst");
        var second = new TemplateAdditionalText(_requiredDirectives + "#generate Text\r\nsecond");
        var edited = new TemplateAdditionalText(_requiredDirectives + "#generate Text\r\nedited");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.CreateDriver([first, second]).RunGenerators(compilation);

        var after = before.ReplaceAdditionalText(second, edited).RunGenerators(compilation);
        var result = after.GetRunResult().Results.Single();
        var hintNames = result.GeneratedSources.Select(source => source.HintName).ToArray();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.TrackedSteps["TemplateResults"].SelectMany(step => step.Outputs).Select(output => output.Reason))
            .IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified });
        await Assert.That(hintNames).IsEquivalentTo(before.GetRunResult().Results.Single().GeneratedSources.Select(source => source.HintName));
        await Assert.That(hintNames.Count(name => name.EndsWith(".1.g.cs", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task Initialize_GenerationTargetEdited_KeepsSupportTypesCached()
    {
        var textOnly = new TemplateAdditionalText(_requiredDirectives + "#generate Text\r\ntarget");
        var both = new TemplateAdditionalText(_requiredDirectives + "target");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([textOnly]));

        var widened = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(textOnly, both));

        // The support types depend only on the settings, so editing a template never regenerates them
        foreach (var hintName in new[] { "TemplateOptions.g.cs", "TextTemplate.g.cs", "Utf8Template.g.cs" })
        {
            await Assert.That(ReferenceEquals(GeneratedSource(widened, hintName).SourceText, GeneratedSource(before, hintName).SourceText)).IsTrue();
        }

        await AssertRenders(widened.Compilation, "Consumer.Example", "target");
    }

    private static IncrementalStepRunReason[] StepReasons(GeneratorTestResult result, string stepName)
    {
        return result.RunResult.Results.Single().TrackedSteps[stepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
    }

    private static GeneratedSourceResult GeneratedSource(GeneratorTestResult result, string hintName)
    {
        return result.RunResult.Results.Single().GeneratedSources.Single(source => source.HintName == hintName);
    }

    private static TestAnalyzerConfigOptionsProvider AccessModifierOptions(string value)
    {
        return new TestAnalyzerConfigOptionsProvider([new KeyValuePair<string, string>(_baseAccessModifierKey, value)]);
    }

    private static async Task AssertRenders(Compilation compilation, string type, string expected)
    {
        var (text, bytes) = RenderingTestHelper.Render(compilation, type: type);

        await Assert.That(text).IsEqualTo(expected);
        await Assert.That(bytes).IsEquivalentTo(Encoding.UTF8.GetBytes(expected));
    }
}
