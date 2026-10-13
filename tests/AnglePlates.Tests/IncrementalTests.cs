using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Reruns the driver to check which steps the generator reuses after templates, settings, or other files change
public sealed class IncrementalTests
{
    [Test]
    public async Task Rerun_OneOfTwoTemplatesEdited_ReprocessesOnlyThatTemplate()
    {
        var first = RenderingTestHelper.Template("first", type: "First", path: "Templates/first.html");
        var second = RenderingTestHelper.Template("second", type: "Second", path: "Templates/second.html");
        var edited = RenderingTestHelper.Template("edited", type: "Second", path: "Templates/second.html");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([first, second]));
        var firstHintNames = before.GeneratorResult.GeneratedSources
            .Where(source => source.SourceText.ToString().Contains("class FirstTextTemplate", StringComparison.Ordinal) ||
                source.SourceText.ToString().Contains("class FirstUtf8Template", StringComparison.Ordinal))
            .Select(source => source.HintName)
            .ToArray();

        var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(second, edited));
        var outputReasons = after.GeneratorResult.TrackedOutputSteps.SelectMany(step => step.Value).SelectMany(step => step.Outputs)
            .Select(output => output.Reason).ToArray();

        await Assert.That(after.StepReasons(TemplateGenerator.TemplateResultsStep))
            .IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified });
        await Assert.That(outputReasons.Count(reason => reason == IncrementalStepRunReason.Modified)).IsEqualTo(1);
        await Assert.That(first.GetTextCount).IsEqualTo(1);
        await Assert.That(edited.GetTextCount).IsEqualTo(1);
        await Assert.That(after.HintNames()).IsEqualTo(before.HintNames());
        // Roslyn 4.14 parses a new tree for every generated source on each run, so the reused text is what proves the cache
        await Assert.That(firstHintNames.Length).IsEqualTo(2);
        foreach (var hintName in firstHintNames)
        {
            await Assert.That(ReferenceEquals(after.GeneratedSource(hintName).SourceText, before.GeneratedSource(hintName).SourceText)).IsTrue();
        }

        await RenderingTestHelper.AssertRendersAsync(after.Compilation, "Consumer.First", "first");
        await RenderingTestHelper.AssertRendersAsync(after.Compilation, "Consumer.Second", "edited");
    }

    [Test]
    public async Task Rerun_UnrelatedConsumerSourceChanged_KeepsEveryTemplateCached()
    {
        var template = RenderingTestHelper.Template("template");
        var before = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]));
        var changed = GeneratorTestHelper.CreateCompilation(["namespace Consumer; public static class Unrelated { }"]);

        var after = GeneratorTestHelper.Run(changed, before.Driver);

        await Assert.That(after.StepReasons(TemplateGenerator.TemplateFilesStep)).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(after.StepReasons(TemplateGenerator.TemplateResultsStep)).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await Assert.That(after.HintNames()).IsEqualTo(before.HintNames());
    }

    [Test]
    public async Task Rerun_UntaggedFileEdited_DoesNotRerunTemplateStepsOrReadIt()
    {
        var template = RenderingTestHelper.Template("template");
        var untagged = new TemplateAdditionalText("#nullable enable", "PublicAPI.Shipped.txt", anglePlate: null);
        var edited = new TemplateAdditionalText("#nullable enable\r\nA.B()", "PublicAPI.Shipped.txt", anglePlate: null);
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([template, untagged]));

        var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(untagged, edited));

        await Assert.That(untagged.GetTextCount).IsEqualTo(0);
        await Assert.That(edited.GetTextCount).IsEqualTo(0);
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await Assert.That(after.StepReasons(TemplateGenerator.AnglePlateFilesStep)
            .All(reason => reason == IncrementalStepRunReason.Unchanged || reason == IncrementalStepRunReason.Cached)).IsTrue();
        await Assert.That(after.StepReasons(TemplateGenerator.TemplateFilesStep)).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(after.HintNames()).IsEqualTo(before.HintNames());
    }

    [Test]
    public async Task Rerun_TemplateAddedThenRemoved_LeavesExistingTemplateCached()
    {
        var first = RenderingTestHelper.Template("first", type: "First", path: "Templates/first.html");
        var added = RenderingTestHelper.Template("added", type: "Added", path: "Templates/added.html");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([first]));

        var withAdded = GeneratorTestHelper.Run(compilation, before.Driver.AddAdditionalTexts([added]));
        var addedReasons = withAdded.StepReasons(TemplateGenerator.TemplateResultsStep);
        var afterRemoval = GeneratorTestHelper.Run(compilation, withAdded.Driver.RemoveAdditionalTexts([added]));

        await Assert.That(addedReasons).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.New });
        await RenderingTestHelper.AssertRendersAsync(withAdded.Compilation, "Consumer.Added", "added");
        await Assert.That(afterRemoval.StepReasons(TemplateGenerator.TemplateResultsStep))
            .IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Removed });
        await Assert.That(first.GetTextCount).IsEqualTo(1);
        await Assert.That(afterRemoval.HintNames()).IsEqualTo(before.HintNames());
        await RenderingTestHelper.AssertRendersAsync(afterRemoval.Compilation, "Consumer.First", "first");
    }

    [Test]
    public async Task Rerun_BaseAccessModifierChanged_RegeneratesSupportTypesFromCachedTemplates()
    {
        // The default internal template class works under both public and internal support types
        var template = RenderingTestHelper.Template("access");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([template], TestAnalyzerConfigOptionsProvider.WithAccessModifier("public")));

        var changed = GeneratorTestHelper.Run(compilation, before.Driver.WithUpdatedAnalyzerConfigOptions(TestAnalyzerConfigOptionsProvider.WithAccessModifier("internal")));
        var changedReasons = changed.StepReasons(TemplateGenerator.TemplateResultsStep);
        var invalid = GeneratorTestHelper.Run(compilation, changed.Driver.WithUpdatedAnalyzerConfigOptions(TestAnalyzerConfigOptionsProvider.WithAccessModifier("private")));
        var invalidReasons = invalid.StepReasons(TemplateGenerator.TemplateResultsStep);
        var restored = GeneratorTestHelper.Run(compilation, invalid.Driver.WithUpdatedAnalyzerConfigOptions(TestAnalyzerConfigOptionsProvider.WithAccessModifier("internal")));

        await Assert.That(changedReasons).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(changed.GeneratedSource("SlusserLabs.AnglePlates.TemplateOptions.g.cs").SourceText.ToString()).Contains("internal class TemplateOptions");
        await Assert.That(invalidReasons).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(invalid.RunResult.Diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(new[] { "SLAP009" });
        await Assert.That(invalid.RunResult.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(restored.StepReasons(TemplateGenerator.TemplateResultsStep)).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(restored.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(restored.HintNames()).IsEqualTo(changed.HintNames());
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await RenderingTestHelper.AssertRendersAsync(restored.Compilation, "Consumer.Example", "access");
    }

    [Test]
    public async Task Rerun_LanguageVersionRaisedToFloor_GeneratesFromCachedTemplates()
    {
        var template = RenderingTestHelper.Template("version");
        var before = GeneratorTestHelper.Run(
            GeneratorTestHelper.CreateCompilation(languageVersion: LanguageVersion.CSharp10), GeneratorTestHelper.CreateDriver([template], languageVersion: LanguageVersion.CSharp10));

        var after = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), before.Driver.WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.CSharp11)));

        await Assert.That(before.RunResult.Diagnostics.Select(diagnostic => diagnostic.Id)).IsEquivalentTo(new[] { "SLAP010" });
        await Assert.That(before.RunResult.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(after.StepReasons(TemplateGenerator.TemplateResultsStep)).IsEquivalentTo(new[] { IncrementalStepRunReason.Cached });
        await Assert.That(after.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(template.GetTextCount).IsEqualTo(1);
        await RenderingTestHelper.AssertRendersAsync(after.Compilation, "Consumer.Example", "version");
    }

    [Test]
    public async Task Rerun_OtherTemplateEdited_ReportsCachedDiagnosticsAgain()
    {
        var broken = new TemplateAdditionalText(TemplateText.Header + "a\r\n[[ model", "Templates/broken.html");
        var good = RenderingTestHelper.Template("good", type: "Good", path: "Templates/good.html");
        var edited = RenderingTestHelper.Template("edited", type: "Good", path: "Templates/good.html");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([broken, good]));

        var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(good, edited));

        await Assert.That(after.StepReasons(TemplateGenerator.TemplateResultsStep))
            .IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified });
        await Assert.That(before.RunResult.Diagnostics.Select(diagnostic => diagnostic.ToString())).IsEquivalentTo(new[]
        {
            "Templates/broken.html(5,1): error SLAP004: Template block is missing its closing delimiter ']]'"
        });
        await Assert.That(after.RunResult.Diagnostics.Select(diagnostic => diagnostic.ToString()))
            .IsEquivalentTo(before.RunResult.Diagnostics.Select(diagnostic => diagnostic.ToString()));
        await Assert.That(broken.GetTextCount).IsEqualTo(1);
        await RenderingTestHelper.AssertRendersAsync(after.Compilation, "Consumer.Good", "edited");
    }

    [Test]
    public async Task Rerun_OneClashingTemplateEdited_KeepsNumberedHintNames()
    {
        // Identical paths and declared types collide, so the compiler reports duplicate members; only the hint names matter here
        var first = new TemplateAdditionalText(TemplateText.Header + "#generate Text\r\nfirst");
        var second = new TemplateAdditionalText(TemplateText.Header + "#generate Text\r\nsecond");
        var edited = new TemplateAdditionalText(TemplateText.Header + "#generate Text\r\nedited");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.CreateDriver([first, second]).RunGenerators(compilation);

        var after = before.ReplaceAdditionalText(second, edited).RunGenerators(compilation);
        var result = after.GetRunResult().Results.Single();
        var hintNames = result.GeneratedSources.Select(source => source.HintName).ToArray();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.TrackedSteps[TemplateGenerator.TemplateResultsStep].SelectMany(step => step.Outputs).Select(output => output.Reason))
            .IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified });
        await Assert.That(hintNames).IsEquivalentTo(before.GetRunResult().Results.Single().GeneratedSources.Select(source => source.HintName));
        await Assert.That(hintNames.Count(name => name == "Consumer.ExampleTextTemplate.2.g.cs")).IsEqualTo(1);
    }

    [Test]
    public async Task Rerun_GenerationTargetEdited_KeepsSupportTypesCached()
    {
        var textOnly = new TemplateAdditionalText(TemplateText.Header + "#generate Text\r\ntarget");
        var both = new TemplateAdditionalText(TemplateText.Header + "target");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([textOnly]));

        var widened = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(textOnly, both));

        // The support types depend only on the settings, so editing a template never regenerates them
        foreach (var hintName in GeneratedSources.SupportHintNames)
        {
            await Assert.That(ReferenceEquals(widened.GeneratedSource(hintName).SourceText, before.GeneratedSource(hintName).SourceText)).IsTrue();
        }

        await RenderingTestHelper.AssertRendersAsync(widened.Compilation, "Consumer.Example", "target");
    }
}
