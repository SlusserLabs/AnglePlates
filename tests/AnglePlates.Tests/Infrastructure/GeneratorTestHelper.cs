using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Runs the generator against an in-memory compilation and fails when the output doesn't compile as expected
internal static class GeneratorTestHelper
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> _references = new(CreateReferences);

    public static CSharpCompilation CreateCompilation(
        IEnumerable<string>? sources = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp11,
        IEnumerable<MetadataReference>? references = null,
        string assemblyName = "AnglePlates.GeneratedConsumer")
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var trees = (sources ?? []).Select((source, index) =>
            CSharpSyntaxTree.ParseText(source, parseOptions, $"Consumer{index}.cs", Encoding.UTF8));

        return CSharpCompilation.Create(
            assemblyName,
            trees,
            references ?? _references.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static GeneratorDriver CreateDriver(
        IEnumerable<AdditionalText>? additionalTexts = null,
        AnalyzerConfigOptionsProvider? optionsProvider = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp11)
    {
        return CSharpGeneratorDriver.Create(
            [new TemplateGenerator().AsSourceGenerator()],
            additionalTexts,
            new CSharpParseOptions(languageVersion),
            optionsProvider ?? new TestAnalyzerConfigOptionsProvider(),
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    public static GeneratorTestResult Run(params AdditionalText[] templates)
    {
        return Run(CreateCompilation(), CreateDriver(templates));
    }

    public static GeneratorTestResult Run(AnalyzerConfigOptionsProvider optionsProvider, params AdditionalText[] templates)
    {
        return Run(CreateCompilation(), CreateDriver(templates, optionsProvider));
    }

    public static GeneratorTestResult Run(
        Compilation compilation,
        GeneratorDriver driver,
        IEnumerable<ExpectedCompilerDiagnostic>? expectedCompilationErrors = null,
        CancellationToken cancellationToken = default)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, cancellationToken);
        var runResult = driver.GetRunResult();
        foreach (var result in runResult.Results)
        {
            if (result.Exception is not null)
            {
                throw new InvalidOperationException("The template generator threw an exception.", result.Exception);
            }
        }

        // Emit catches errors that syntax and declaration diagnostics alone can miss
        using var assembly = new MemoryStream();
        var emitResult = output.Emit(assembly, cancellationToken: cancellationToken);
        var errors = emitResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        var expectedErrors = (expectedCompilationErrors ?? []).ToArray();
        if (errors.Length != expectedErrors.Length || !expectedErrors.Zip(errors, (expected, actual) => expected.Matches(actual)).All(matches => matches))
        {
            throw new InvalidOperationException("Generated consumer diagnostics did not match the expected errors." + Environment.NewLine +
                "Expected:" + Environment.NewLine + string.Join(Environment.NewLine, expectedErrors.AsEnumerable()) + Environment.NewLine +
                "Actual:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(Describe)));
        }

        return new GeneratorTestResult(driver, output, runResult);
    }

    private static string Describe(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        var mapped = diagnostic.Location.GetMappedLineSpan();
        var mapping = mapped.HasMappedPath ? $" mapped to {mapped.Path}{mapped.Span}" : string.Empty;

        return $"{diagnostic.Id} {diagnostic.GetMessage(CultureInfo.InvariantCulture)} in {Path.GetFileName(lineSpan.Path)}{lineSpan.Span}{mapping}";
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string paths)
        {
            throw new InvalidOperationException("The test host did not provide its platform assemblies.");
        }

        // Only the shared framework, so generated code can't quietly depend on the test's own packages. These references
        // qualify a .NET 10 consumer only, not an older framework compatibility floor.
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);

        return paths.Split(Path.PathSeparator)
            .Where(path => string.Equals(Path.GetDirectoryName(path), frameworkDirectory, StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}
