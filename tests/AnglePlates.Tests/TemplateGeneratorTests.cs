using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

public sealed partial class TemplateGeneratorTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Initialize_ConsumerWithoutImplicitImports_Compiles(bool conflictingNames)
    {
        // Run fails the test when the generated consumer has any emit error
        var compilation = GeneratorTestHelper.CreateCompilation(conflictingNames ? [CreateConflictingTypes()] : []);
        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([new TemplateAdditionalText(_requiredDirectives + "Hello [[ model ]]")]));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
    }

    [Test]
    public async Task Initialize_GenerateDirectiveOmitted_GeneratesTextAndUtf8Types()
    {
        const string template = _requiredDirectives + "Hello [[ model ]]";

        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;
        var generatedSource = string.Join(Environment.NewLine, result.GeneratedTrees.Select(tree => tree.GetText().ToString()));

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(generatedSource).Contains("public partial class ExampleTextTemplate");
        await Assert.That(generatedSource).Contains("public partial class ExampleUtf8Template");
    }

    [Test]
    [Arguments("Text", true, false, 3)]
    [Arguments("Utf8", false, true, 4)]
    public async Task Initialize_GenerateDirectiveSpecified_GeneratesSelectedType(string generationTarget, bool hasTextType, bool hasUtf8Type, int treeCount)
    {
        var template = _requiredDirectives + $"#generate {generationTarget}\r\nHello [[ model ]]";

        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;
        var generatedSource = string.Join(Environment.NewLine, result.GeneratedTrees.Select(tree => tree.GetText().ToString()));

        // The UTF-8 base class comes only with a UTF-8 template class
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(treeCount);
        await Assert.That(generatedSource.Contains("public partial class ExampleTextTemplate", StringComparison.Ordinal)).IsEqualTo(hasTextType);
        await Assert.That(generatedSource.Contains("public partial class ExampleUtf8Template", StringComparison.Ordinal)).IsEqualTo(hasUtf8Type);
        await Assert.That(generatedSource.Contains("public abstract class Utf8Template<TModel>", StringComparison.Ordinal)).IsEqualTo(hasUtf8Type);
    }

    [Test]
    public async Task Initialize_TextOnlyTemplate_CompilesWithoutPipelines()
    {
        var allReferences = GeneratorTestHelper.CreateCompilation().References.ToArray();
        var references = allReferences
            .Where(reference => !string.Equals(Path.GetFileName(reference.Display), "System.IO.Pipelines.dll", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var compilation = GeneratorTestHelper.CreateCompilation(references: references);
        var template = _requiredDirectives + "#generate Text\r\nHello [[ model ]]";

        // Run fails the test when the generated consumer has any emit error
        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([new TemplateAdditionalText(template)]));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var options = Activator.CreateInstance(generated.Assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions", throwOnError: true)!);
        using var writer = new StringWriter();
        RenderingTestHelper.Render(generated, "Consumer.ExampleTextTemplate", "World", writer, options);

        await Assert.That(references.Length).IsEqualTo(allReferences.Length - 1);
        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Select(tree => Path.GetFileName(tree.FilePath))).DoesNotContain("Utf8Template.g.cs");
        await Assert.That(writer.ToString()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Initialize_GenerateDirectiveWithMultipleValues_ReportsDiagnostic()
    {
        const string template = _requiredDirectives + "#generate Text, Utf8\r\nHello [[ model ]]";

        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;

        await Assert.That(result.Diagnostics.Length).IsEqualTo(1);
        await Assert.That(result.Diagnostics[0].Id).IsEqualTo("SLAP005");
        await Assert.That(result.Diagnostics[0].GetMessage()).IsEqualTo(
            "Template file 'Templates/example.html' has invalid generation target 'Text, Utf8'; expected 'Text' or 'Utf8'");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(2);
    }

    [Test]
    [Arguments("type", "SLAP001", "Missing type", "type directive such as '#type MyTemplate'")]
    [Arguments("namespace", "SLAP002", "Missing namespace", "namespace directive such as '#namespace MyNamespace'")]
    [Arguments("model", "SLAP003", "Missing model type", "model type directive such as '#model MyModel'")]
    public async Task Initialize_MissingRequiredDirective_ReportsStableDiagnostic(string directive, string id, string title, string message)
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
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan("Templates/example.html",
            new LinePosition(2, 0), new LinePosition(2, 5)));
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Initialize_AllRequiredDirectivesMissing_ReportsAllDiagnostics()
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText("#generate Text\r\nHello")).RunResult;

        await Assert.That(string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id))).IsEqualTo("SLAP001,SLAP002,SLAP003");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(2);
    }

    [Test]
    [Arguments("## Release 1.0\r\n### New Rules", "Templates/invalid.html", 14)]
    [Arguments("## Release 1.0\r\n### New Rules", "AnalyzerReleases.Shipped.md", 14)]
    [Arguments("#tyep Example\n#model string", "Templates/typo.html", 13)]
    [Arguments("<p>Hello</p>", "Emails/welcome.html", 12)]
    [Arguments("\r\n#type Example", "Templates/blank.html", 0)]
    [Arguments("", "Templates/empty.html", 0)]
    public async Task Initialize_ExplicitFileWithoutDirectives_ReportsNoDirectives(string content, string path, int firstLineLength)
    {
        // The check looks only at content, so the file name doesn't matter
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(content, path, TemplateAdditionalText.Explicit)).RunResult;

        await Assert.That(result.Diagnostics.Length).IsEqualTo(1);
        var diagnostic = result.Diagnostics[0];
        await Assert.That(diagnostic.Id).IsEqualTo("SLAP008");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Template has no directives");
        await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            $"Template file '{path}' has no directives; a file added with an AnglePlate Include must start with a directive such as '#type MyTemplate'");
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(path, new LinePosition(0, 0), new LinePosition(0, firstLineLength)));
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(2);
    }

    [Test]
    [Arguments("## Release 1.0\r\n### New Rules")]
    [Arguments("#tyep Example\r\n#model string\r\n#namespace Consumer\r\nHello")]
    [Arguments("\r\n#type Example\r\n#model string\r\n#namespace Consumer\r\nHello")]
    [Arguments("<p>Hello</p>")]
    [Arguments("# Notes")]
    [Arguments("")]
    public async Task Initialize_DiscoveredFileWithoutDirectives_IsSkipped(string content)
    {
        var result = GeneratorTestHelper.Run(
            new TemplateAdditionalText(content, "Templates/README.md"), RenderingTestHelper.Template("valid", path: "Templates/valid.html"));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(RenderingTestHelper.Render(result.Compilation).text).IsEqualTo("valid");
    }

    private static string CreateConflictingTypes()
    {
        const string frameworkNames = """
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

        return $$"""
            namespace Consumer
            {
                {{frameworkNames}}
                public class SlusserLabs { }
                public class TemplateOptions { }
                public class TextTemplate<T> { }
                public class Utf8Template<T> { }
            }
            namespace SlusserLabs.AnglePlates
            {
                {{frameworkNames}}
            }
            """;
    }
}
