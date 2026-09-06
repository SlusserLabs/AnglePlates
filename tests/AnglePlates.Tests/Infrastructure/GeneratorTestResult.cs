using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

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
}
