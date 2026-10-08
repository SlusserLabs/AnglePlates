using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

public sealed class TemplateParserTests
{
    [Test]
    [Arguments("[ordinary] [[ model ]]", "[ordinary] &lt;value&gt;")]
    [Arguments("prefix [ordinary] [[ model ]]", "prefix [ordinary] &lt;value&gt;")]
    [Arguments("[[ model ]][ordinary] [[ model ]]", "&lt;value&gt;[ordinary] &lt;value&gt;")]
    [Arguments("[% var unused = 1; %][ordinary] [[ model ]]", "[ordinary] &lt;value&gt;")]
    [Arguments("[* comment *][ordinary] [[ model ]]", "[ordinary] &lt;value&gt;")]
    [Arguments("[a][b][ [[ model ]] [", "[a][b][ &lt;value&gt; [")]
    [Arguments("[", "[")]
    [Arguments("plain text", "plain text")]
    [Arguments("[% // %]tail", "tail")]
    [Arguments("[* first *]rest *]", "rest *]")]
    public async Task Parse_OrdinaryBrackets_RendersFollowingExpressions(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments(@"\[[1, 2]]", "[[1, 2]]")]
    [Arguments(@"\[% x %]", "[% x %]")]
    [Arguments(@"\[* x *]", "[* x *]")]
    [Arguments(@"\\[[ model ]]", @"\&lt;value&gt;")]
    [Arguments(@"\\\[[ model ]]", @"\[[ model ]]")]
    [Arguments(@"\\\\[[ model ]]", @"\\&lt;value&gt;")]
    [Arguments(@"end \[[", "end [[")]
    [Arguments(@"[[ model ]]\[[x]]", "&lt;value&gt;[[x]]")]
    [Arguments(@"\[[[[ model ]]", "[[&lt;value&gt;")]
    [Arguments(@"\[[\[%\[*", "[[[%[*")]
    [Arguments(@"\[[- x -]]", "[[- x -]]")]
    [Arguments(@"C:\temp\file \\", @"C:\temp\file \\")]
    [Arguments(@"C:\out\\[[ model ]]", @"C:\out\&lt;value&gt;")]
    [Arguments("a\r\n\\[[x]]\r\nb", "a\r\n[[x]]\r\nb")]
    [Arguments("a\n\\[[x]]\nb", "a\n[[x]]\nb")]
    [Arguments(@"const grid = \[[1, 2], [3, 4]];", "const grid = [[1, 2], [3, 4]];")]
    [Arguments(@"if \[[ -f ""$file"" ]]; then", @"if [[ -f ""$file"" ]]; then")]
    [Arguments(@"\[[servers]]", "[[servers]]")]
    [Arguments(@"/\[*+?]/", "/[*+?]/")]
    [Arguments(@"See \[[Home]].", "See [[Home]].")]
    // Trim markers next to an escaped opener trim only whitespace
    [Arguments("[[ model -]] \r\n \\[[x]] \r\n [[- model ]]", "&lt;value&gt;[[x]]&lt;value&gt;")]
    [Arguments("[[ model -]] \n \\\\[[- model ]]", @"&lt;value&gt;\&lt;value&gt;")]
    [Arguments("[[ model -]]\\[[x]]", "&lt;value&gt;[[x]]")]
    [Arguments("\\[[x]] \n [[- model ]]", "[[x]]&lt;value&gt;")]
    [Arguments(" \\[[x]] [*- *]", " [[x]]")]
    public async Task Parse_EscapedOpener_WritesOpenerLiterally(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("[[", "]]")]
    [Arguments("[%", "%]")]
    [Arguments("[*", "*]")]
    public async Task Parse_EmptyBlocks_AreNoOpsWithTrimFlags(string open, string close)
    {
        foreach (var newline in new[] { "\n", "\r\n" })
        {
            foreach (var contents in new[] { string.Empty, " ", "\t" + newline })
            {
                foreach (var trimLeft in new[] { false, true })
                {
                    foreach (var trimRight in new[] { false, true })
                    {
                        // A delimiter-adjacent hyphen belongs to the opener; separate a right-only marker
                        var blockContents = contents.Length == 0 && trimRight && !trimLeft ? " " : contents;
                        var body = $"a {newline}{open}{(trimLeft ? "-" : string.Empty)}{blockContents}{(trimRight ? "-" : string.Empty)}{close}{newline} b";
                        var expected = "a" + (trimLeft ? string.Empty : " " + newline) + (trimRight ? string.Empty : newline + " ") + "b";
                        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
                        var output = RenderingTestHelper.Render(result.Compilation);

                        await Assert.That(output.text).IsEqualTo(expected);
                        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
                    }
                }
            }
        }
    }

    [Test]
    [Arguments("[[", "]]")]
    [Arguments("[%", "%]")]
    [Arguments("[*", "*]")]
    public async Task Parse_UnclosedBlock_ReportsOpeningDelimiterLocation(string open, string close)
    {
        foreach (var newline in new[] { "\n", "\r\n" })
        {
            foreach (var contents in new[] { string.Empty, "   ", "- model", " model" })
            {
                var header = $"#type Example{newline}#model string{newline}#namespace Consumer{newline}";
                var template = new TemplateAdditionalText(header + "prefix " + open + contents);
                var result = GeneratorTestHelper.Run(template);
                var diagnostic = result.RunResult.Diagnostics.Single();

                await Assert.That(diagnostic.Id).IsEqualTo("SLAP004");
                await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Template block is missing its closing delimiter '{close}'");
                await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
                await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Unclosed template block");
                await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
                await Assert.That(diagnostic.Descriptor.IsEnabledByDefault).IsTrue();
                await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(new TextSpan(header.Length + 7, 2));
                await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(template.Path,
                    new LinePosition(3, 7), new LinePosition(3, 9)));
                await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(2);
            }
        }
    }

    [Test]
    [Arguments("a[[]][%%][**]b", "ab")]
    [Arguments("a \n[[-]][%- -%][*- -*]\n b", "ab")]
    [Arguments("a [[-1]] b", "a1 b")]
    [Arguments("a [[ -1 ]] b", "a -1 b")]
    [Arguments("a [[(-1)]] b", "a -1 b")]
    [Arguments("a [[- 1 -]] \r\n [[- 2 ]] b", "a12 b")]
    public async Task Parse_AdjacentBlocks_PreservesTrimAndNegativeExpressionBehavior(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }
}
