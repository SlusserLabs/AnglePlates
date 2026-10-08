using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SlusserLabs.AnglePlates.Benchmarks.Templates;

namespace SlusserLabs.AnglePlates.Benchmarks;

// A template with 3,000 C# variables, each of which declares its own local in the render method
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ManyVariablesBenchmarks
{
    private const int _count = 3000;
    private const string _value = "value";
    private readonly Destinations _destinations = new();

    [GlobalSetup]
    public void Setup()
    {
        Destinations.CheckSame("variables", writer => VariablesTextTemplate.Default.Render(_value, writer), writer => HandWritten.Variables(_value, _count, writer));
        Destinations.CheckSame("variables", writer => VariablesUtf8Template.Default.Render(_value, writer), writer => HandWritten.Variables(_value, _count, writer));
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Text")]
    public void HandWrittenTextToNull() => HandWritten.Variables(_value, _count, TextWriter.Null);

    [Benchmark]
    [BenchmarkCategory("Text")]
    public void TextToNull() => VariablesTextTemplate.Default.Render(_value, TextWriter.Null);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("UTF-8")]
    public void HandWrittenUtf8ToArrayBufferWriter() => HandWritten.Variables(_value, _count, _destinations.ClearUtf8());

    [Benchmark]
    [BenchmarkCategory("UTF-8")]
    public void Utf8ToArrayBufferWriter() => VariablesUtf8Template.Default.Render(_value, _destinations.ClearUtf8());
}
