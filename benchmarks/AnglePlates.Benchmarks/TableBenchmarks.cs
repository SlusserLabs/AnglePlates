using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SlusserLabs.AnglePlates.Benchmarks.Templates;

namespace SlusserLabs.AnglePlates.Benchmarks;

// A table with an int, a string that sometimes needs encoding, a decimal, a sometimes-null int?, an enum, a DateTime,
// and a double in each row
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TableBenchmarks
{
    private readonly Destinations _destinations = new();
    private TableModel _model = default!;

    [Params(10, 1000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _model = TableModel.Create(Rows);
        Destinations.CheckSame("table", writer => TableTextTemplate.Default.Render(_model, writer), writer => HandWritten.Table(_model, writer));
        Destinations.CheckSame("table", writer => TableUtf8Template.Default.Render(_model, writer), writer => HandWritten.Table(_model, writer));
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Text")]
    public void HandWrittenTextToNull() => HandWritten.Table(_model, TextWriter.Null);

    [Benchmark]
    [BenchmarkCategory("Text")]
    public void TextToNull() => TableTextTemplate.Default.Render(_model, TextWriter.Null);

    [Benchmark]
    [BenchmarkCategory("Text")]
    public void TextToStringWriter() => TableTextTemplate.Default.Render(_model, _destinations.ClearText());

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("UTF-8")]
    public void HandWrittenUtf8ToArrayBufferWriter() => HandWritten.Table(_model, _destinations.ClearUtf8());

    [Benchmark]
    [BenchmarkCategory("UTF-8")]
    public void Utf8ToArrayBufferWriter() => TableUtf8Template.Default.Render(_model, _destinations.ClearUtf8());

    [Benchmark]
    [BenchmarkCategory("UTF-8")]
    public async Task Utf8RenderAsyncToPipe()
    {
        await TableUtf8Template.Default.RenderAsync(_model, _destinations.Pipe.Writer);
        _destinations.DrainPipe();
    }

    [Benchmark]
    [BenchmarkCategory("UTF-8")]
    public async Task Utf8RenderThenFlushToPipe()
    {
        TableUtf8Template.Default.Render(_model, _destinations.Pipe.Writer);
        await _destinations.Pipe.Writer.FlushAsync();
        _destinations.DrainPipe();
    }
}
