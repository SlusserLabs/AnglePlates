using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        const string template = "#type Example\r\n#model string\r\n#namespace Consumer\r\n#render Text, Utf8\r\nHello [[ model ]]";
        var source = conflictingNames ? CreateConflictingTypes() : string.Empty;
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp11);
        var referencePaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = referencePaths.Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TemplateGenerator().AsSourceGenerator()],
            [new TemplateAdditionalText(template)],
            parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        await Assert.That(string.Join(Environment.NewLine, diagnostics)).IsEqualTo(string.Empty);
        await Assert.That(driver.GetRunResult().GeneratedTrees.Length).IsEqualTo(5);
        using var assembly = new MemoryStream();
        var result = output.Emit(assembly);
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        await Assert.That(string.Join(Environment.NewLine, errors)).IsEqualTo(string.Empty);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    [Arguments("type", "SLAP001", "Missing type", "type directive (e.g. '#type MyTemplate')")]
    [Arguments("namespace", "SLAP002", "Missing namespace", "namespace directive (e.g. '#namespace MyNamespace')")]
    [Arguments("model", "SLAP003", "Missing model type", "model type directive (e.g. '#model MyModel')")]
    public async Task Initialize_MissingRequiredDirective_ReportsStableDiagnostic(string directive, string id, string title, string message)
    {
        var template = string.Join("\r\n", new[] { "#type Example", "#namespace Consumer", "#model string" }
            .Where(line => !line.StartsWith("#" + directive + " ", StringComparison.Ordinal))) + "\r\nHello";
        var result = RunGenerator(new TemplateAdditionalText(template));

        await Assert.That(result.Diagnostics.Length).IsEqualTo(1);
        var diagnostic = result.Diagnostics[0];
        await Assert.That(diagnostic.Id).IsEqualTo(id);
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo(title);
        await Assert.That(diagnostic.Descriptor.Category).IsEqualTo("SlusserLabs.AnglePlates");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Descriptor.IsEnabledByDefault).IsTrue();
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Template file 'Templates/example.html' is missing a {message}.");
        await Assert.That(diagnostic.Location).IsEqualTo(Location.None);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Initialize_AllRequiredDirectivesMissing_ReportsAllDiagnostics()
    {
        var result = RunGenerator(new TemplateAdditionalText("#render Text\r\nHello"));

        await Assert.That(string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id))).IsEqualTo("SLAP001,SLAP002,SLAP003");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(3);
    }

    [Test]
    [Arguments("Templates/invalid.html")]
    [Arguments("AnalyzerReleases.Shipped.md")]
    [Arguments("AnalyzerReleases.Unshipped.md")]
    public async Task Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics(string path)
    {
        var result = RunGenerator(new TemplateAdditionalText("## Release 1.0\r\n### New Rules", path));

        await Assert.That(string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id))).IsEqualTo("SLAP001,SLAP002,SLAP003");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(3);
    }

    private static GeneratorDriverRunResult RunGenerator(params AdditionalText[] additionalTexts)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp11);
        var referencePaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var compilation = CSharpCompilation.Create(
            "Consumer",
            references: referencePaths.Select(path => MetadataReference.CreateFromFile(path)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new TemplateGenerator().AsSourceGenerator()], additionalTexts, parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        using var assembly = new MemoryStream();
        var result = output.Emit(assembly);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return driver.GetRunResult();
    }

    private static string CreateConflictingTypes()
    {
        const string frameworkNames = """
            public class System { }
            public class Convert { }
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
