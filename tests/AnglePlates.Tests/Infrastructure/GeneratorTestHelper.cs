using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VerifyTests;
using VerifyTUnit;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

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
        var errors = emitResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(ExpectedCompilerDiagnostic.FromDiagnostic).ToArray();
        var expectedErrors = (expectedCompilationErrors ?? []).ToArray();
        if (!errors.SequenceEqual(expectedErrors))
        {
            throw new InvalidOperationException("Generated consumer diagnostics did not match the expected errors." + Environment.NewLine +
                "Expected:" + Environment.NewLine + string.Join(Environment.NewLine, expectedErrors.AsEnumerable()) + Environment.NewLine +
                "Actual:" + Environment.NewLine + string.Join(Environment.NewLine, errors.AsEnumerable()));
        }

        return new GeneratorTestResult(driver, output, runResult);
    }

    public static SettingsTask VerifyAsync(
        Compilation compilation,
        GeneratorDriver driver,
        IEnumerable<ExpectedCompilerDiagnostic>? expectedCompilationErrors = null,
        [CallerFilePath] string sourceFile = "")
    {
        var result = Run(compilation, driver, expectedCompilationErrors);

        return Verifier.Verify(result.RunResult, sourceFile: sourceFile);
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string paths)
        {
            throw new InvalidOperationException("The test host did not provide its platform assemblies.");
        }

        // Host references qualify this .NET 10 consumer only, not an older framework compatibility floor
        return paths.Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}
