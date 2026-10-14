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
    public async Task Slap001_UnderivableClassName_ReportsAtFirstLine()
    {
        const string path = "Templates/2fa-code.html";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText("#model string\r\n#namespace Consumer\r\nbody", path)).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP001", DiagnosticSeverity.Error, "Template class name can't be derived");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            "The file name gives the class name '2faCode', which isn't a C# identifier; add a directive such as '#type MyTemplate'");
        await AssertLineSpan(diagnostic, path, 0, 0, 0, 13);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    public async Task Slap002_UnderivableNamespace_ReportsAtFirstLine()
    {
        // Hand-built compilations supply no RootNamespace, so there's nothing to derive the namespace from
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText("#type Example\r\n#model string\r\nbody")).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP002", DiagnosticSeverity.Error, "Template namespace can't be derived");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            "The template's namespace can't be derived, because RootNamespace is empty; add a directive such as '#namespace MyApp.Templates'");
        await AssertLineSpan(diagnostic, TemplateText.Path, 0, 0, 0, 13);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

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
        await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(new TextSpan(header.Length + 7, 2));
        await AssertLineSpan(diagnostic, template.Path, 3, 7, 3, 9);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(4);
    }

    // Closers the scanner skips don't close the block, so C# that hides every closer it has reports SLAP004 at the opener
    // and consumes the rest of the template, which leaves no generated code for the compiler to report
    [Test]
    [MethodDataSource(nameof(HiddenCloserCases))]
    public async Task Slap004_OnlyHiddenClosers_ReportsAtOpeningDelimiter(string block, string close)
    {
        var template = new TemplateAdditionalText(TemplateText.Header + "prefix " + block + "\r\n<p>tail</p>");
        var result = GeneratorTestHelper.Run(template);
        var diagnostic = result.RunResult.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP004", DiagnosticSeverity.Error, "Unclosed template block");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(UnclosedBlockMessage(close));
        await AssertLineSpan(diagnostic, template.Path, 3, 7, 3, 9);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(4);
    }

    public static IEnumerable<(string block, string close)> HiddenCloserCases()
    {
        // An unmatched "[" in a C# variable, and closers that sit only inside C# strings or comments
        yield return ("[[ model[0 ]]", "]]");
        yield return ("[[ \"]]\" + model", "]]");
        yield return ("[% var s = \"%]\"; /* %] */", "%]");

        // The last window holds 16 closers, so 16 hidden ones leave the real closer outside it
        var hidden = string.Concat(Enumerable.Repeat("\"%]\", ", 16));
        yield return ($"[% var codes = new[] {{ {hidden}\"x\" }}; %]", "%]");
    }

    public static IEnumerable<(string header, string open, string contents, string close)> UnclosedBlockCases()
    {
        foreach (var (open, close) in new[] { ("[[", "]]"), ("[%", "%]"), ("[*", "*]") })
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
    public async Task Slap011_PossibleMisspelledDirective_WarnsAtLine()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(TemplateText.Header + "#modle x\r\nbody"));
        var diagnostic = result.RunResult.Diagnostics.Single();

        await AssertRule(diagnostic, "SLAP011", DiagnosticSeverity.Warning, "Possible misspelled directive");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo("'#modle' isn't a directive. Did you mean '#model'?");
        await AssertLineSpan(diagnostic, TemplateText.Path, 3, 0, 3, 8);
        // A warning doesn't stop the template from generating
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", "#modle x\r\nbody");
    }

    [Test]
    public async Task Registry_EveryDescriptor_HasDocumentedHelpAnchor()
    {
        // Each rule's heading is exactly "## SLAPnnn", which GitHub turns into the #slapnnn anchor the help link names,
        // and the next line repeats the descriptor's title in bold. DocumentationLinkTests uses the same anchor rules.
        var path = RepositoryFiles.GetPath("docs/diagnostics.md");
        var lines = File.ReadAllLines(path);
        var anchors = MarkdownDocument.Load(path).Anchors;
        var descriptors = AllDescriptors();

        await Assert.That(descriptors.Length).IsEqualTo(11);
        await Assert.That(descriptors.Select(descriptor => descriptor.Id).Distinct().Count()).IsEqualTo(descriptors.Length);
        foreach (var descriptor in descriptors)
        {
            var heading = Array.IndexOf(lines, "## " + descriptor.Id);
            await Assert.That(heading).IsGreaterThan(0);
            await Assert.That(lines[heading + 2]).IsEqualTo($"**{descriptor.Title}**");
            await Assert.That(descriptor.HelpLinkUri).IsEqualTo(HelpLink(descriptor.Id));
            await Assert.That(anchors).Contains(new Uri(descriptor.HelpLinkUri).Fragment.TrimStart('#'));
        }
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
            .IsEqualTo("SLAP002,SLAP003,SLAP004,SLAP004,SLAP006");
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
