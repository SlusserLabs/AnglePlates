using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SlusserLabs.AnglePlates.Benchmarks.Templates;

namespace SlusserLabs.AnglePlates.Benchmarks;

// A mostly static page with two C# variables. Its generated code is already a run of constant writes, which is what
// hand-written code would do, so it has no separate baseline.
[MemoryDiagnoser]
public class LiteralPageBenchmarks
{
    private readonly PageModel _model = new();
    private readonly Destinations _destinations = new();

    [Benchmark]
    public void TextToNull() => PageTextTemplate.Default.Render(_model, TextWriter.Null);

    [Benchmark]
    public void TextToStringWriter() => PageTextTemplate.Default.Render(_model, _destinations.ClearText());

    [Benchmark]
    public void Utf8ToArrayBufferWriter() => PageUtf8Template.Default.Render(_model, _destinations.ClearUtf8());

    [Benchmark]
    public async Task Utf8RenderAsyncToPipe()
    {
        await PageUtf8Template.Default.RenderAsync(_model, _destinations.Pipe.Writer);
        _destinations.DrainPipe();
    }
}
