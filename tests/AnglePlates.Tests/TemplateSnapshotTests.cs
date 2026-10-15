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
    public Task Snapshot_BracketLevels_MatchesReviewedSources()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(
            "#generate Text\r\n[[[ \"]]\" ]]][[% var b = \"%]\"; %]][[[* c *]]][[[[[ model ]]]]]\\[[ model ]]"));

        return GeneratorSnapshot.Verify(result.RunResult, "BracketLevels", source => !GeneratedSources.IsSupportType(source));
    }

    [Test]
    public Task Snapshot_MultiLineCSharp_MatchesReviewedSources()
    {
        // Multi-line C# keeps its line breaks and columns under one #line mapping per token, with every line at column 0
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(
            "#generate Utf8\r\n[% var s = @\"a\r\n  b\";\r\n  var r = \"\"\"\r\n    x\r\n    \"\"\"; %][[ s +\r\n  r ]][% if (true) { %]a[% { %]b[% } } %][[[% var t = \"]]\"; %]]][[ t ]]"));

        return GeneratorSnapshot.Verify(result.RunResult, "MultiLineCSharp", source => !GeneratedSources.IsSupportType(source));
    }

    [Test]
    public Task Snapshot_MergedLiteralRuns_MatchesReviewedSources()
    {
        // A comment, empty blocks, and a level-3 comment between literals write one literal; the trim marker ends the run.
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Text\r\na[* c *]b[%%]c[[[* d *]]]e[[]]f [*- x *]g"));

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
