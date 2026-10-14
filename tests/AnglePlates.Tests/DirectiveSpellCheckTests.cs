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

// SLAP011: which lines look like misspelled directives, and the two places the generator checks for one
public sealed class DirectiveSpellCheckTests
{
    [Test]
    // Misspellings: one edit, a swap, or two edits for a name longer than four letters
    [Arguments("#modle string", "#model")]
    [Arguments("#tpye Example", "#type")]
    [Arguments("#typ Example", "#type")]
    [Arguments("#types", "#type")]
    [Arguments("#mdl string", "#model")]
    [Arguments("#models X", "#model")]
    [Arguments("#namspace Consumer", "#namespace")]
    [Arguments("#nmaespace Consumer", "#namespace")]
    [Arguments("#acess internal", "#access")]
    [Arguments("#genrate Text", "#generate")]
    [Arguments("#encdoe none", "#encode")]
    [Arguments("#usign System", "#using")]
    [Arguments("#tpye\tExample", "#type")]
    [Arguments("#modle", "#model")]
    // A directive name in another casing
    [Arguments("#Model string", "#model")]
    [Arguments("#Type X", "#type")]
    [Arguments("#USING System", "#using")]
    // CSS, Markdown, shebangs, C# preprocessor lines, and other text that only starts with '#'
    [Arguments("#header { color: red; }", null)]
    [Arguments("#top { margin: 0 }", null)]
    [Arguments("#top", null)]
    [Arguments("# Heading", null)]
    [Arguments("## Release 1.0", null)]
    [Arguments("#!/bin/sh", null)]
    [Arguments("#region Helpers", null)]
    [Arguments("#endregion", null)]
    [Arguments("#if DEBUG", null)]
    [Arguments("#nullable enable", null)]
    [Arguments("#pragma warning disable", null)]
    [Arguments("#typeGreeting", null)]
    [Arguments("#modle{", null)]
    [Arguments("#model1 x", null)]
    [Arguments("#", null)]
    [Arguments("", null)]
    [Arguments(" #modle string", null)]
    [Arguments("modle string", null)]
    [Arguments("<p>#modle</p>", null)]
    // A real directive isn't a misspelling of itself
    [Arguments("#model string", null)]
    public async Task FindIntendedDirective_Line_ReturnsLikelyDirective(string line, string? expected)
    {
        var intended = DirectiveSpellCheck.FindIntendedDirective(line, out var misspelled);

        // The misspelled name is the first word of the line, and there's none when nothing was found
        await Assert.That(intended).IsEqualTo(expected);
        await Assert.That(misspelled).IsEqualTo(expected is null ? string.Empty : line.Split(' ', '\t')[0]);
    }

    [Test]
    [Arguments("\r\n")]
    [Arguments("\n")]
    [Arguments("\r")]
    public async Task SpellCheck_FirstBodyLine_WarnsAndStillRenders(string newline)
    {
        // The typo ends the directive section, so it's written out as body text, which is what the warning is about
        var template = TemplateText.HeaderWith(newline) + "#modle string" + newline + "body";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));
        var diagnostic = result.RunResult.Diagnostics.Single();

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP011");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).IsEqualTo("'#modle' isn't a directive. Did you mean '#model'?");
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(TemplateText.Path, new LinePosition(3, 0), new LinePosition(3, 13)));
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", "#modle string" + newline + "body");
    }

    [Test]
    public async Task SpellCheck_LaterBodyLine_DoesNotWarn()
    {
        // Only the first body line is checked, so a later '#modle' is ordinary text
        var template = RenderingTestHelper.Template("first\r\n#modle string");
        var result = GeneratorTestHelper.Run(template);

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", "first\r\n#modle string");
    }

    [Test]
    public async Task SpellCheck_TypoBeforeModel_WarnsAndReportsMissingModelAtTypo()
    {
        var template = "#type Example\r\n#namespace Consumer\r\n#modle string\r\nbody";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;
        var typo = new FileLinePositionSpan(TemplateText.Path, new LinePosition(2, 0), new LinePosition(2, 13));

        await Assert.That(string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id))).IsEqualTo("SLAP011,SLAP003");
        await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.Location.GetLineSpan() == typo)).IsTrue();
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    [Arguments(TemplateAdditionalText.Discovered, "SLAP011")]
    [Arguments(TemplateAdditionalText.Explicit, "SLAP011,SLAP008")]
    public async Task SpellCheck_MisspelledFirstLine_WarnsWhyTheFileIsntATemplate(string anglePlate, string ids)
    {
        // A discovered file is still skipped and an explicit one still reports SLAP008; the warning says why
        var template = new TemplateAdditionalText("#tpye Example\r\n#model string\r\nbody", "Templates/typo.html", anglePlate);
        var result = GeneratorTestHelper.Run(template).RunResult;
        var firstLine = new FileLinePositionSpan("Templates/typo.html", new LinePosition(0, 0), new LinePosition(0, 13));

        await Assert.That(string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id))).IsEqualTo(ids);
        await Assert.That(result.Diagnostics[0].GetMessage()).IsEqualTo("'#tpye' isn't a directive. Did you mean '#type'?");
        await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.Location.GetLineSpan() == firstLine)).IsTrue();
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    [Arguments("#header { color: red; }\r\nbody")]
    [Arguments("# Heading\r\nbody")]
    [Arguments("#!/bin/sh\r\necho hi")]
    [Arguments("#region x\r\nbody")]
    public async Task SpellCheck_OrdinaryHashLine_StaysQuiet(string body)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body), new TemplateAdditionalText(body, "Templates/notes.md", TemplateAdditionalText.Discovered));
        var output = RenderingTestHelper.Render(result.Compilation, model: string.Empty);

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await Assert.That(output.text).IsEqualTo(body);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(body))).IsTrue();
    }
}
