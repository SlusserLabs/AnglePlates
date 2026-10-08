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
using VerifyTUnit;

namespace SlusserLabs.AnglePlates.Tests;

public sealed partial class TemplateGeneratorTests
{
    private const string _requiredDirectives = "#type Example\r\n#model string\r\n#namespace Consumer\r\n";

    private const string _frameworkNames = """
            public class System { }
            public class Convert { }
            public class Globalization { }
            public class CultureInfo { }
            public class Span<T> { }
            public class ReadOnlySpan<T> { }
            public class OperationStatus { }
            public class MethodImplAttribute { }
            public class MethodImplOptions { }
            public class TextWriter { }
            public class ReadOnlyMemory<T> { }
            public class ArrayPool<T> { }
            public class TextEncoder { }
            public class HtmlEncoder { }
            public class Encoding { }
            public class Math { }
            public class Lazy<T> { }
            public class IBufferWriter<T> { }
            public class PipeWriter { }
            public class CancellationToken { }
            public class ValueTask { }
            public class ValueTask<T> { }
            public class FlushResult { }
        """;

    // Names declared in the template's own namespace and in the support type namespace
    private const string _consumerConflicts = $$"""
        namespace Consumer
        {
        {{_frameworkNames}}
            public class SlusserLabs { }
            public class TemplateOptions { }
            public class TextTemplate<T> { }
            public class Utf8Template<T> { }
        }
        namespace SlusserLabs.AnglePlates
        {
        {{_frameworkNames}}
        }
        """;

    // Names brought into scope by a #using directive
    private const string _importedConflicts = """
        namespace Conflicts
        {
            public class System { }
            public class Convert { }
            public class CultureInfo { }
            public class Lazy<T> { }
            public class Span<T> { }
            public class ReadOnlyMemory<T> { }
            public class IBufferWriter<T> { }
            public class TextWriter { }
            public class SlusserLabs { }
            public class TemplateOptions { }
            public class TextTemplate { }
            public class TextTemplate<T> { }
            public class Utf8Template { }
            public class Utf8Template<T> { }
            public class ExampleTextTemplate { }
            public class ExampleUtf8Template { }
        }
        """;

    [Test]
    [Arguments("#typeGreeting")]
    [Arguments("#models X")]
    [Arguments("#Type X")]
    [Arguments("# Heading")]
    [Arguments("#Heading")]
    [Arguments("#!/bin/sh")]
    [Arguments("#nullable enable")]
    [Arguments("#header { color: red; }")]
    [Arguments("#")]
    [Arguments(" #type Other")]
    [Arguments("\r\n#type Other")]
    [Arguments("#acess internal")]
    [Arguments("#not directive")]
    [Arguments("</html>\r\n#not directive")]
    public async Task Initialize_NonDirectiveLine_EndsDirectiveSectionAsLiteral(string body)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation, model: string.Empty);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(body);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(body))).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(LineConsumptionCases))]
    public async Task Initialize_DirectiveLines_AreConsumedWithTheirLineBreaks(string template, string expected)
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    public static IEnumerable<(string template, string expected)> LineConsumptionCases()
    {
        foreach (var newline in new[] { "\n", "\r\n" })
        {
            var header = $"#type Example{newline}#model string{newline}#namespace Consumer";
            yield return (header + newline + "body", "body");
            yield return (header + " " + newline + "body", "body");
            yield return (header + "\t " + newline + "body", "body");
            yield return (header + newline + newline + "body", newline + "body");
            yield return (header + newline + "  body", "  body");
            yield return (header, string.Empty);
            yield return (header + newline, string.Empty);
            yield return ($"#type\tExample{newline}#model string{newline}#namespace\tConsumer{newline}body", "body");
        }
    }

    [Test]
    public async Task Initialize_DirectiveErrorsWithLf_MapLines()
    {
        var template = "#type Example\n#model string\n#namespace Consumer\n#access privat\n#model int\nbody";
        var diagnostics = GenerateDiagnostics(template);

        await Assert.That(string.Join(",", diagnostics.Select(d => d.Id))).IsEqualTo("SLAP007,SLAP006");
        await AssertLocation(diagnostics[0], template, 3, "#access ".Length, "privat".Length);
        await AssertLocation(diagnostics[1], template, 4, 0, "#model int".Length);
    }

    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    public async Task Initialize_TypoBeforeRequiredDirectives_ReportsAtTypo(string newline)
    {
        var template = $"#type Example{newline}#namspace Consumer{newline}#model string{newline}body";
        var diagnostics = GenerateDiagnostics(template);

        await Assert.That(string.Join(",", diagnostics.Select(d => d.Id))).IsEqualTo("SLAP002,SLAP003");
        foreach (var diagnostic in diagnostics)
        {
            await AssertLocation(diagnostic, template, 1, 0, "#namspace Consumer".Length);
        }
    }

    [Test]
    [Arguments("#type", "\r\nbody", "SLAP001")]
    [Arguments("#type  \t", "\r\nbody", "SLAP001")]
    [Arguments("#model", "\r\nbody", "SLAP003")]
    [Arguments("#namespace ", "\r\nbody", "SLAP002")]
    [Arguments("#type", "", "SLAP001")]
    [Arguments("#type", "\n", "SLAP001")]
    [Arguments("#type", "\r\n", "SLAP001")]
    public async Task Initialize_BlankRequiredDirective_ReportsAtDirectiveName(string line, string tail, string id)
    {
        // The blank directive is last so the tail can end the line, the text, or neither
        var template = ReplaceDirective(line, last: true).TrimEnd('\r', '\n') + tail;
        var diagnostic = GenerateDiagnostics(template).Single();
        var name = line.TrimEnd();

        await Assert.That(diagnostic.Id).IsEqualTo(id);
        await AssertLocation(diagnostic, template, 2, 0, name.Length);
    }

    [Test]
    [Arguments("#type Example", "#type")]
    [Arguments("#type Other", "#type")]
    [Arguments("#model string", "#model")]
    [Arguments("#namespace Other", "#namespace")]
    [Arguments("#generate Text", "#generate")]
    [Arguments("#access public", "#access")]
    [Arguments("#using System", "#using System")]
    [Arguments("#type\t", "#type")]
    public async Task Initialize_RepeatedDirective_ReportsDuplicate(string line, string label)
    {
        // Required directives already appear once, so only optional ones need a first occurrence
        var name = line.Split(' ', '\t')[0];
        var isRequired = _requiredDirectives.Contains(name + " ", StringComparison.Ordinal);
        var template = _requiredDirectives + (isRequired ? string.Empty : line + "\r\n") + line + "\r\nbody";
        var diagnostic = GenerateDiagnostics(template).Single();

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP006");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Directive '{label}' appears more than once");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Duplicate directive");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await AssertLocation(diagnostic, template, template.Count(c => c == '\n') - 1, 0, line.TrimEnd().Length);
    }

    [Test]
    [Arguments("Bad")]
    [Arguments("Text, Utf8")]
    public async Task Initialize_InvalidThenValidGenerate_ReportsBoth(string target)
    {
        var template = _requiredDirectives + $"#generate {target}\r\n#generate Text\r\nbody";
        var diagnostics = GenerateDiagnostics(template);

        await Assert.That(string.Join(",", diagnostics.Select(d => d.Id))).IsEqualTo("SLAP005,SLAP006");
        await Assert.That(diagnostics[0].GetMessage()).IsEqualTo(
            $"Template file 'Templates/example.html' has invalid generation target '{target}'; expected 'Text' or 'Utf8'");
        await AssertLocation(diagnostics[0], template, 3, "#generate ".Length, target.Length);
        await AssertLocation(diagnostics[1], template, 4, 0, "#generate Text".Length);
    }

    [Test]
    [Arguments("#type My-Template", "a C# identifier")]
    [Arguments("#type @class", "a C# identifier")]
    [Arguments("#type Two Words", "a C# identifier")]
    [Arguments("#model List<", "a C# type name")]
    [Arguments("#model string // comment", "a C# type name")]
    [Arguments("#model string model", "a C# type name")]
    [Arguments("#namespace global::Consumer", "a namespace name such as 'MyApp.Templates'")]
    [Arguments("#namespace Consumer<T>", "a namespace name such as 'MyApp.Templates'")]
    [Arguments("#namespace Consumer.", "a namespace name such as 'MyApp.Templates'")]
    [Arguments("#namespace My Consumer", "a namespace name such as 'MyApp.Templates'")]
    [Arguments("#namespace class", "a namespace name such as 'MyApp.Templates'")]
    [Arguments("#access privat", "'public' or 'internal'")]
    [Arguments("#access Public", "'public' or 'internal'")]
    [Arguments("#access", "'public' or 'internal'")]
    [Arguments("#generate", "'Text' or 'Utf8'")]
    [Arguments("#using static System.Math", "a namespace name such as 'System.Linq'")]
    [Arguments("#using Text = System.Text", "a namespace name such as 'System.Linq'")]
    [Arguments("#using System;", "a namespace name such as 'System.Linq'")]
    [Arguments("#using System, System.Linq", "a namespace name such as 'System.Linq'")]
    [Arguments("#using global::System", "a namespace name such as 'System.Linq'")]
    [Arguments("#using System.Collections.Generic.List<int>", "a namespace name such as 'System.Linq'")]
    [Arguments("#using System // comment", "a namespace name such as 'System.Linq'")]
    [Arguments("#using", "a namespace name such as 'System.Linq'")]
    public async Task Initialize_InvalidDirectiveValue_ReportsAtValue(string line, string expected)
    {
        var template = ReplaceDirective(line) + "body";
        var diagnostic = GenerateDiagnostics(template).Single();
        var separator = line.IndexOf(' ');
        var name = separator < 0 ? line.Substring(1) : line.Substring(1, separator - 1);
        var value = separator < 0 ? string.Empty : line.Substring(separator + 1);
        var lineIndex = LineOf(template, line);

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP007");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Directive '#{name}' has invalid value '{value}'; expected {expected}");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Invalid directive value");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        if (value.Length == 0)
        {
            await AssertLocation(diagnostic, template, lineIndex, 0, line.Length);
        }
        else
        {
            await AssertLocation(diagnostic, template, lineIndex, separator + 1, value.Length);
        }
    }

    [Test]
    [Arguments("#type class", "Consumer.class")]
    [Arguments("#type _Example1", "Consumer._Example1")]
    [Arguments("#model global::System.String", "Consumer.Example")]
    [Arguments("#model string?", "Consumer.Example")]
    [Arguments("#model System.String", "Consumer.Example")]
    [Arguments("#namespace Consumer.Templates", "Consumer.Templates.Example")]
    [Arguments("#namespace @class", "class.Example")]
    [Arguments("#access public", "Consumer.Example")]
    [Arguments("#generate utf8", "Consumer.Example")]
    public async Task Initialize_ValidDirectiveValue_Generates(string line, string type)
    {
        // A public template class needs public support types
        var baseAccessModifier = line.StartsWith("#access", StringComparison.Ordinal) ? "public" : null;
        var result = RunWithAccessModifier(baseAccessModifier, new TemplateAdditionalText(ReplaceDirective(line) + "[[ model ]]"));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        if (!line.StartsWith("#generate", StringComparison.Ordinal))
        {
            var output = RenderingTestHelper.Render(result.Compilation, type: type);
            await Assert.That(output.text).IsEqualTo("&lt;value&gt;");
            await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(output.text))).IsTrue();
        }
    }

    [Test]
    public async Task Initialize_UsingDirectives_EmitImportsInTemplateOrder()
    {
        var template = "#type Example\r\n#using System.Text\r\n#model IEnumerable<char>\r\n#using System.Collections.Generic\r\n#namespace Consumer\r\n" +
            "[% foreach (var c in model) { %][[ c ]][% } %][[ new StringBuilder(\"!\") ]]";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));
        var output = RenderingTestHelper.Render(result.Compilation);
        var sources = result.RunResult.GeneratedTrees.Skip(3).Select(tree => tree.GetText().ToString()).ToArray();

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(sources.Length).IsEqualTo(2);
        foreach (var source in sources)
        {
            var text = source.IndexOf("using System.Text;", StringComparison.Ordinal);
            var collections = source.IndexOf("using System.Collections.Generic;", StringComparison.Ordinal);
            var ns = source.IndexOf("namespace Consumer;", StringComparison.Ordinal);
            await Assert.That(text).IsGreaterThan(0);
            await Assert.That(collections).IsGreaterThan(text);
            await Assert.That(ns).IsGreaterThan(collections);
        }

        await Assert.That(output.text).IsEqualTo("&lt;value&gt;!");
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(output.text))).IsTrue();
    }

    [Test]
    [Arguments("", "", "Hello [[ model ]]", "Hello &lt;value&gt;")]
    [Arguments(_consumerConflicts, "", "Hello [[ model ]]", "Hello &lt;value&gt;")]
    [Arguments(_importedConflicts, "#using Conflicts\r\n", "Hello [[ model ]]", "Hello &lt;value&gt;")]
    [Arguments("global using System.Text;", "#using System.Text\r\n", "> [[ new StringBuilder(model) ]]", "> &lt;value&gt;")]
    public async Task Initialize_ConsumerSources_CompileWithEmittedCodeWithoutWarnings(string consumerSource, string usingDirective, string body, string expected)
    {
        // Run fails the test when the generated consumer has any emit error; the consumer has no implicit imports
        var template = _requiredDirectives + usingDirective + body;
        var compilation = GeneratorTestHelper.CreateCompilation(consumerSource.Length == 0 ? [] : [consumerSource]);
        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([new TemplateAdditionalText(template)]));
        var warnings = result.Compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning);
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(string.Join(", ", warnings.Select(d => $"{d.Id} {d.GetMessage()}"))).IsEqualTo(string.Empty);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    public async Task Initialize_DirectiveErrors_MatchesReviewedDiagnostics()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(_requiredDirectives + "#access privat\r\n#type Other\r\nbody"));

        await Verifier.Verify(result.RunResult);
    }

    private static Diagnostic[] GenerateDiagnostics(string template)
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));
        if (result.RunResult.GeneratedTrees.Length != 3)
        {
            throw new InvalidOperationException("A template with directive errors should not generate.");
        }

        return result.RunResult.Diagnostics.ToArray();
    }

    // Swaps the directive with the same name into the required set, or appends it; last moves the swapped directive to the end
    private static string ReplaceDirective(string line, bool last = false)
    {
        var separator = line.IndexOf(' ');
        var name = (separator < 0 ? line : line.Substring(0, separator)) + " ";
        var lines = _requiredDirectives.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();
        var index = lines.FindIndex(l => l.StartsWith(name, StringComparison.Ordinal));
        if (index >= 0 && !last)
        {
            lines[index] = line;
        }
        else
        {
            if (index >= 0)
            {
                lines.RemoveAt(index);
            }

            lines.Add(line);
        }

        return string.Join("\r\n", lines) + "\r\n";
    }

    private static int LineOf(string template, string line)
    {
        return template.Split('\n').Select(l => l.TrimEnd('\r')).ToList().IndexOf(line);
    }

    private static async Task AssertLocation(Diagnostic diagnostic, string template, int line, int column, int length)
    {
        var lines = SourceText.From(template).Lines;
        var start = lines[line].Start + column;

        await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(new TextSpan(start, length));
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan("Templates/example.html",
            new LinePosition(line, column), new LinePosition(line, column + length)));
    }
}
