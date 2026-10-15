using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Holds one generator run with shortcuts to its sources, hint names, and tracked step reasons
internal sealed class GeneratorTestResult
{
    public GeneratorTestResult(GeneratorDriver driver, Compilation compilation, GeneratorDriverRunResult runResult)
    {
        Driver = driver;
        Compilation = compilation;
        RunResult = runResult;
    }

    public GeneratorDriver Driver { get; }

    public Compilation Compilation { get; }

    public GeneratorDriverRunResult RunResult { get; }

    public GeneratorRunResult GeneratorResult => RunResult.Results.Single();

    // The template classes without the support types, in generation order
    public string[] TemplateSources => GeneratedSources.Templates(RunResult).Select(source => source.SourceText.ToString()).ToArray();

    public string HintNames()
    {
        return string.Join("\n", GeneratorResult.GeneratedSources.Select(source => source.HintName).Order(StringComparer.Ordinal));
    }

    public GeneratedSourceResult GeneratedSource(string hintName)
    {
        return GeneratorResult.GeneratedSources.Single(source => source.HintName == hintName);
    }

    // The compiler errors located in generated files, which is where an invalid directive value ends up
    public Diagnostic[] GeneratedFileErrors()
    {
        return Compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true)
            .ToArray();
    }

    public IncrementalStepRunReason[] StepReasons(string stepName)
    {
        return GeneratorResult.TrackedSteps[stepName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
    }
}
