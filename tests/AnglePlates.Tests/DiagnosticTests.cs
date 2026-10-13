using System;
using System.Collections.Generic;
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

// Template diagnostics: their IDs, descriptors, messages, and locations, and which templates still generate
public sealed class DiagnosticTests
{
    [Test]
    [Arguments("type", "SLAP001", "Missing type", "type directive such as '#type MyTemplate'")]
    [Arguments("namespace", "SLAP002", "Missing namespace", "namespace directive such as '#namespace MyNamespace'")]
    [Arguments("model", "SLAP003", "Missing model type", "model type directive such as '#model MyModel'")]
    public async Task MissingDirective_RequiredDirective_ReportsAtDirectiveSectionEnd(string directive, string id, string title, string message)
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
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(TemplateText.Path,
            new LinePosition(2, 0), new LinePosition(2, 5)));
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    public async Task MissingDirective_AllRequiredDirectives_ReportsEachOne()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText("#generate Text\r\nHello")).RunResult;

        await Assert.That(string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id))).IsEqualTo("SLAP001,SLAP002,SLAP003");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
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
    [Arguments(null, "Templates/unreadable.html", 0)]
    public async Task NoDirectives_DiscoveredOrExplicitFile_SkipsDiscoveredAndReportsExplicit(string? content, string path, int firstLineLength)
    {
        // A bare # isn't a known directive either, and the check looks only at content, so the file name doesn't matter.
        // A file Roslyn can't read counts as empty.
        var valid = RenderingTestHelper.Template("valid", path: "Templates/valid.html");
        var discovered = GeneratorTestHelper.Run(new TemplateAdditionalText(content, path), valid);
        var explicitFile = GeneratorTestHelper.Run(new TemplateAdditionalText(content, path, TemplateAdditionalText.Explicit)).RunResult;

        await Assert.That(discovered.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(discovered.RunResult.GeneratedTrees.Length).IsEqualTo(6);
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
        await Assert.That(explicitFile.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    [MethodDataSource(nameof(UnclosedBlockCases))]
    public async Task UnclosedBlock_AnyBlockKind_ReportsAtOpeningDelimiter(string header, string open, string contents, string close)
    {
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
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(4);
    }

    public static IEnumerable<(string header, string open, string contents, string close)> UnclosedBlockCases()
    {
        foreach (var (open, close) in new[] { ("[[", "]]"), ("[%", "%]"), ("[*", "*]") })
        {
            foreach (var newline in new[] { "\n", "\r\n" })
            {
                foreach (var contents in new[] { string.Empty, "   ", "- model", " model" })
                {
                    yield return (TemplateText.HeaderWith(newline), open, contents, close);
                }
            }
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task InvalidTemplates_AmongValidOnes_GeneratesEveryValidTemplate(int invalidPosition)
    {
        var templates = new List<AdditionalText>
        {
            RenderingTestHelper.Template("first [[ model ]]", "First", path: "Templates/a.html"),
            RenderingTestHelper.Template("second [[ model ]]", "Second", path: "Templates/z.html")
        };
        var invalidPath = new[] { "Templates/0.html", "Templates/m.html", "Templates/zz.html" }[invalidPosition];
        templates.Insert(invalidPosition, new TemplateAdditionalText("#generate Text\r\n[[", invalidPath));
        templates.Add(RenderingTestHelper.Template("[% unclosed", "Bad", path: "Templates/bad.html"));
        templates.Add(new TemplateAdditionalText(TemplateText.Header + "#type Other\r\nbad", "Templates/duplicate.html"));
        var result = GeneratorTestHelper.Run(templates.ToArray());
        var first = RenderingTestHelper.Render(result.Compilation, type: "Consumer.First");
        var second = RenderingTestHelper.Render(result.Compilation, type: "Consumer.Second");

        // Missing directives, unclosed blocks, and directive errors each skip only their own template
        await Assert.That(string.Join(",", result.RunResult.Diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal)))
            .IsEqualTo("SLAP001,SLAP002,SLAP003,SLAP004,SLAP004,SLAP006");
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(8);
        await Assert.That(first.text).IsEqualTo("first &lt;value&gt;");
        await Assert.That(second.text).IsEqualTo("second &lt;value&gt;");
        await Assert.That(first.bytes.SequenceEqual(Encoding.UTF8.GetBytes(first.text))).IsTrue();
        await Assert.That(second.bytes.SequenceEqual(Encoding.UTF8.GetBytes(second.text))).IsTrue();
    }
}
