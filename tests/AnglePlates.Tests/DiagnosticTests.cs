using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// The diagnostics registry: one test per rule for its ID, severity, title, message, location, and help link, plus the
// help anchors in docs/diagnostics.md and which templates still generate
public sealed class DiagnosticTests
{
    [Test]
    // The section ends at the first body line. When the text ends on a directive line, the empty span sits at its end.
    [Arguments("#type Example\r\n#namespace Consumer\r\nHello", 2, 0, 5)]
    [Arguments("#type Example\r\n#namespace Consumer\r\n", 2, 0, 0)]
    [Arguments("#type Example\r\n#namespace Consumer", 1, 19, 19)]
    public async Task Slap003_NoModel_ReportsAtDirectiveSectionEnd(string template, int line, int startColumn, int endColumn)
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP003", DiagnosticSeverity.Error, "Template has no #model directive");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo("The template has no '#model' directive; add one such as '#model MyApp.GreetingModel'");
        await AssertLineSpan(diagnostic, TemplateText.Path, line, startColumn, line, endColumn);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    [MethodDataSource(nameof(UnclosedBlockCases))]
    public async Task Slap004_UnclosedBlock_ReportsAtOpeningDelimiter(string header, string open, string contents, string close)
    {
        var template = new TemplateAdditionalText(header + "prefix " + open + contents);
        var result = GeneratorTestHelper.Run(template);
        var diagnostic = result.RunResult.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP004", DiagnosticSeverity.Error, "Unclosed template block");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(UnclosedBlockMessage(close));
        await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(new TextSpan(header.Length + 7, open.Length));
        await AssertLineSpan(diagnostic, template.Path, 3, 7, 3, 7 + open.Length);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(4);
    }

    // A closer of another level doesn't close the block, and extra brackets before a four-bracket opener are literal text
    [Test]
    [Arguments("prefix [[[[[ model", 8, 4, "]]]]")]
    [Arguments("prefix [[[ model ]]", 7, 3, "]]]")]
    public async Task Slap004_UnclosedLevel_ReportsAtOpener(string body, int column, int length, string closer)
    {
        var template = new TemplateAdditionalText(TemplateText.Header + body);
        var result = GeneratorTestHelper.Run(template);
        var diagnostic = result.RunResult.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP004", DiagnosticSeverity.Error, "Unclosed template block");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(UnclosedBlockMessage(closer));
        await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(new TextSpan(TemplateText.Header.Length + column, length));
        await AssertLineSpan(diagnostic, template.Path, 3, column, 3, column + length);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(4);
    }

    public static IEnumerable<(string header, string open, string contents, string close)> UnclosedBlockCases()
    {
        // Every opener level with its matching closer: variables at 2 to 4, code and comments at 1 to 4
        var pairs = new List<(string, string)>();
        for (int level = 2; level <= 4; level++)
        {
            pairs.Add((new string('[', level), new string(']', level)));
        }

        for (int level = 1; level <= 4; level++)
        {
            pairs.Add((new string('[', level) + "%", "%" + new string(']', level)));
            pairs.Add((new string('[', level) + "*", "*" + new string(']', level)));
        }

        foreach (var (open, close) in pairs)
        {
            foreach (var newline in new[] { "\n", "\r\n", "\r" })
            {
                foreach (var contents in new[] { string.Empty, "   ", "- model", " model" })
                {
                    yield return (TemplateText.HeaderWith(newline), open, contents, close);
                }
            }
        }
    }

    [Test]
    public async Task Slap005_PublicTemplateWithInternalSupportTypes_ReportsAtAccessValue()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(TemplateText.Header + "#access public\r\nbody")).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP005", DiagnosticSeverity.Error, "Public template needs public support types");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            "'#access public' needs public support types; set AnglePlateBaseAccessModifier to 'public' or make the template internal");
        await AssertLineSpan(diagnostic, TemplateText.Path, 3, 8, 3, 14);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    public async Task Slap006_DuplicateDirective_ReportsAtRepeat()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(TemplateText.Header + "#type Other\r\nbody")).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP006", DiagnosticSeverity.Error, "Duplicate directive");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo("Directive '#type' appears more than once");
        await AssertLineSpan(diagnostic, TemplateText.Path, 3, 0, 3, 11);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    public async Task Slap007_InvalidDirectiveValue_ReportsAtValue()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(TemplateText.Header + "#access privat\r\nbody")).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP007", DiagnosticSeverity.Error, "Invalid directive value");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo("Directive '#access' has invalid value 'privat'; expected 'public' or 'internal'");
        await AssertLineSpan(diagnostic, TemplateText.Path, 3, 8, 3, 14);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    [Arguments("## Release 1.0\r\n### New Rules", "Templates/README.md", 14)]
    [Arguments("## Release 1.0\r\n### New Rules", "AnalyzerReleases.Shipped.md", 14)]
    [Arguments("\r\n#type Example\r\n#model string\r\n#namespace Consumer\r\nHello", "Templates/blank.html", 0)]
    [Arguments("<p>Hello</p>", "Emails/welcome.html", 12)]
    [Arguments("# Notes", "Templates/notes.md", 7)]
    [Arguments("#", "Templates/hash.html", 1)]
    [Arguments("#   ", "Templates/padded.html", 4)]
    [Arguments("#\r\n#\t\r\n", "Templates/hashes.html", 1)]
    [Arguments("", "Templates/empty.html", 0)]
    [Arguments(null, "Templates/unreadable.html", 0)]
    public async Task Slap008_NotATemplate_SkipsDiscoveredAndReportsExplicitAtFirstLine(string? content, string path, int firstLineLength)
    {
        // A bare # isn't a known directive either, and the check looks only at content, so the file name doesn't matter.
        // A file Roslyn can't read counts as empty.
        var valid = RenderingTestHelper.Template("valid", path: "Templates/valid.html");
        var discovered = GeneratorTestHelper.Run(new TemplateAdditionalText(content, path), valid);
        var explicitFile = GeneratorTestHelper.Run(new TemplateAdditionalText(content, path, TemplateAdditionalText.Explicit)).RunResult;

        await Assert.That(discovered.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(discovered.RunResult.GeneratedTrees.Length).IsEqualTo(6);
        await Assert.That(RenderingTestHelper.Render(discovered.Compilation).text).IsEqualTo("valid");

        var diagnostic = explicitFile.Diagnostics.Single();
        await AssertRule(diagnostic, "SLAP008", DiagnosticSeverity.Error, "File isn't a template");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            "A file added with an AnglePlate Include must be a template, but this one is empty, unreadable, or doesn't start with a directive");
        await AssertLineSpan(diagnostic, path, 0, 0, 0, firstLineLength);
        await Assert.That(explicitFile.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    public async Task Slap009_InvalidMSBuildSetting_ReportsWithoutLocation()
    {
        var result = GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithAccessModifier("private"), RenderingTestHelper.Template("ignored")).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP009", DiagnosticSeverity.Error, "Invalid MSBuild setting");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo("MSBuild property 'AnglePlateBaseAccessModifier' has invalid value 'private'; expected 'public' or 'internal'");
        await Assert.That(diagnostic.Location).IsEqualTo(Location.None);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Slap010_LanguageVersionTooOld_ReportsWithoutLocation()
    {
        var compilation = GeneratorTestHelper.CreateCompilation(languageVersion: LanguageVersion.CSharp10);
        var driver = GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("ignored")], languageVersion: LanguageVersion.CSharp10);
        var result = GeneratorTestHelper.Run(compilation, driver).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP010", DiagnosticSeverity.Error, "C# version too old");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo("AnglePlates requires C# 11 or later, but this project uses C# 10.0; set LangVersion to 11 or later");
        await Assert.That(diagnostic.Location).IsEqualTo(Location.None);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    [Arguments("#modal { color: red; }")]
    [Arguments("#modle x")]
    public async Task UnknownDirectiveAfterHeader_IsLiteralText(string body)
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(TemplateText.Header + body));

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", body);
    }

    [Test]
    public async Task Registry_EveryDescriptor_HasDocumentedHelpAnchor()
    {
        // Each rule's heading is exactly "## SLAPnnn", which GitHub turns into the #slapnnn anchor the help link names,
        // and the next line repeats the descriptor's title in bold. Retired IDs keep their headings.
        var path = RepositoryFiles.GetPath("docs/diagnostics.md");
        var lines = File.ReadAllLines(path);
        var descriptors = AllDescriptors();

        await Assert.That(descriptors.Length).IsEqualTo(8);
        await Assert.That(descriptors.Select(descriptor => descriptor.Id).Distinct().Count()).IsEqualTo(descriptors.Length);
        foreach (var descriptor in descriptors)
        {
            var heading = Array.IndexOf(lines, "## " + descriptor.Id);
            await Assert.That(heading).IsGreaterThan(0);
            await Assert.That(lines[heading + 2]).IsEqualTo($"**{descriptor.Title}**");
            await Assert.That(descriptor.HelpLinkUri).IsEqualTo(HelpLink(descriptor.Id));
        }
    }

    [Test]
    [Arguments("SLAP001")]
    [Arguments("SLAP002")]
    [Arguments("SLAP011")]
    public async Task Registry_RetiredRule_IsDocumentedAndNeverUsed(string id)
    {
        // IDs retired before any release stay documented and are never reused, but have no AnalyzerReleases entry
        var lines = File.ReadAllLines(RepositoryFiles.GetPath("docs/diagnostics.md"));
        var unshipped = File.ReadAllText(RepositoryFiles.GetPath("src/AnglePlates/AnalyzerReleases.Unshipped.md"));
        var heading = Array.IndexOf(lines, "## " + id);

        await Assert.That(heading).IsGreaterThan(0);
        await Assert.That(lines[heading + 4]).Contains("Retired in 1.1");
        await Assert.That(AllDescriptors().Any(descriptor => descriptor.Id == id)).IsFalse();
        await Assert.That(unshipped).DoesNotContain(id);
    }

    [Test]
    public async Task Registry_EveryDescriptor_IsInAnalyzerReleases()
    {
        var unshipped = File.ReadAllText(RepositoryFiles.GetPath("src/AnglePlates/AnalyzerReleases.Unshipped.md"));

        foreach (var descriptor in AllDescriptors())
        {
            var severity = descriptor.DefaultSeverity.ToString();
            await Assert.That(unshipped).Contains($"{descriptor.Id} | {descriptor.Category} | {severity} |");
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
        templates.Insert(invalidPosition, new TemplateAdditionalText("#generate Text\r\n#type Broken\r\n[[", invalidPath));
        templates.Add(RenderingTestHelper.Template("[% unclosed", "Bad", path: "Templates/bad.html"));
        templates.Add(new TemplateAdditionalText(TemplateText.Header + "#type Other\r\nbad", "Templates/duplicate.html"));
        var result = GeneratorTestHelper.Run(templates.ToArray());
        var first = RenderingTestHelper.Render(result.Compilation, type: "Consumer.First");
        var second = RenderingTestHelper.Render(result.Compilation, type: "Consumer.Second");

        // Missing directives, unclosed blocks, and directive errors each skip only their own template
        await Assert.That(string.Join(",", result.RunResult.Diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal)))
            .IsEqualTo("SLAP003,SLAP004,SLAP004,SLAP006");
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(8);
        await Assert.That(first.text).IsEqualTo("first &lt;value&gt;");
        await Assert.That(second.text).IsEqualTo("second &lt;value&gt;");
        await Assert.That(first.bytes.SequenceEqual(Encoding.UTF8.GetBytes(first.text))).IsTrue();
        await Assert.That(second.bytes.SequenceEqual(Encoding.UTF8.GetBytes(second.text))).IsTrue();
    }

    private static string UnclosedBlockMessage(string close)
    {
        return $"Template block is missing its closing delimiter '{close}'";
    }

    private static async Task AssertRule(Diagnostic diagnostic, string id, DiagnosticSeverity severity, string title)
    {
        await Assert.That(diagnostic.Id).IsEqualTo(id);
        await Assert.That(diagnostic.Severity).IsEqualTo(severity);
        await Assert.That(diagnostic.Descriptor.DefaultSeverity).IsEqualTo(severity);
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo(title);
        await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
        await Assert.That(diagnostic.Descriptor.IsEnabledByDefault).IsTrue();
        await Assert.That(diagnostic.Descriptor.HelpLinkUri).IsEqualTo(HelpLink(id));
    }

    private static async Task AssertLineSpan(Diagnostic diagnostic, string path, int startLine, int startColumn, int endLine, int endColumn)
    {
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(path,
            new LinePosition(startLine, startColumn), new LinePosition(endLine, endColumn)));
    }

    private static string HelpLink(string id)
    {
        return "https://github.com/SlusserLabs/AnglePlates/blob/main/docs/diagnostics.md#" + id.ToLowerInvariant();
    }

    private static DiagnosticDescriptor[] AllDescriptors()
    {
        return typeof(DiagnosticDescriptors).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
            .ToArray();
    }
}
