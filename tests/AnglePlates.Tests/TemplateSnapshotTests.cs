using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Core;
using VerifyTests;
using VerifyTUnit;

namespace SlusserLabs.AnglePlates.Tests;

// Reviewed snapshots of generated template classes and diagnostics. The support types have their own snapshot, so they're left out here.
public sealed class TemplateSnapshotTests
{
    [Test]
    public Task Snapshot_BracketsAndEmptyBlocks_MatchesReviewedSources()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("[ordinary] [[ model ]]\r\na \r\n[[- -]][%- -%][*- -*]\r\nb"));

        return GeneratorSnapshot.Verify(result.RunResult, "BracketsAndEmptyBlocks", source => !GeneratedSources.IsSupportType(source));
    }

    [Test]
    public Task Snapshot_EscapedOpeners_MatchesReviewedSources()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Text\r\nlet a = \\[[1]];\r\n\\\\[% var b = 2; %]\\[*c*]"));

        return GeneratorSnapshot.Verify(result.RunResult, "EscapedOpeners", source => !GeneratedSources.IsSupportType(source));
    }

    [Test]
    public Task Snapshot_MergedLiteralRuns_MatchesReviewedSources()
    {
        // A comment, empty blocks, and an escaped opener between literals write one literal; the trim marker ends the run.
        // #encode none makes the constructor fall back to the Unencoded preset.
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Text\r\n#encode none\r\na[* c *]b[%%]c\\[[d[[]]e [*- x *]f"));

        return GeneratorSnapshot.Verify(result.RunResult, "MergedLiteralRuns", source => !GeneratedSources.IsSupportType(source));
    }

    [Test]
    public Task Snapshot_RenderAsync_MatchesReviewedSources()
    {
        // Literals, C# variables, a loop, brace-less C#, a comment after a block, and a trimmed literal that disappears
        const string body = "#generate Utf8\r\n<ul>[% foreach (var c in model) { %]<li>[[ c ]]</li>[% } %][* note *][[ model ]]" +
            "[% if (model.Length > 0) %]a[% else %][[ model.Length ]][% var n = 1; -%]  \r\n[%- n++; %]";
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));

        return GeneratorSnapshot.Verify(result.RunResult, "RenderAsync", source => !GeneratedSources.IsSupportType(source));
    }

    [Test]
    public Task Snapshot_UnclosedBlock_MatchesReviewedDiagnostic()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("prefix [[ model"));

        return GeneratorSnapshot.Verify(result.RunResult, "UnclosedBlock", source => !GeneratedSources.IsSupportType(source));
    }

    [Test]
    public Task Snapshot_DirectiveErrors_MatchesReviewedDiagnostics()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(TemplateText.Header + "#access privat\r\n#type Other\r\nbody"));

        return GeneratorSnapshot.Verify(result.RunResult, "DirectiveErrors", source => !GeneratedSources.IsSupportType(source));
    }
}
