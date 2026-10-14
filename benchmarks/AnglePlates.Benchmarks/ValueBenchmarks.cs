using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SlusserLabs.AnglePlates.Benchmarks.Templates;

namespace SlusserLabs.AnglePlates.Benchmarks;

// One C# variable holding plain ASCII, text that needs HTML encoding, or a 100,000-character plain value,
// with encoding on and off
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ValueBenchmarks
{
    private readonly Destinations _destinations = new();
    private string _value = string.Empty;
    private ValueTextTemplate _text = default!;
    private ValueUtf8Template _utf8 = default!;

    [Params("Plain", "Encoded", "Large")]
    public string Kind { get; set; } = "Plain";

    [Params(true, false)]
    public bool Encode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _value = Kind switch
        {
            "Plain" => new string('a', 200),
            "Encoded" => string.Concat(Enumerable.Repeat("<b>Fish & 'Chips'</b> ", 10)),
            "Large" => new string('a', 100_000),
            _ => throw new InvalidOperationException($"Unknown {nameof(Kind)} '{Kind}'.")
        };

        var options = new TemplateOptions { Encoder = Encode ? HtmlEncoder.Default : null };
        _text = new ValueTextTemplate(options);
        _utf8 = new ValueUtf8Template(options);
        Destinations.CheckSame("value", writer => _text.Render(_value, writer), writer => HandWritten.Value(_value, Encode, writer));
        Destinations.CheckSame("value", writer => _utf8.Render(_value, writer), writer => HandWritten.Value(_value, Encode, writer));
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Text")]
    public void HandWrittenTextToNull() => HandWritten.Value(_value, Encode, TextWriter.Null);

    [Benchmark]
    [BenchmarkCategory("Text")]
    public void TextToNull() => _text.Render(_value, TextWriter.Null);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("UTF-8")]
    public void HandWrittenUtf8ToArrayBufferWriter() => HandWritten.Value(_value, Encode, _destinations.ClearUtf8());

    [Benchmark]
    [BenchmarkCategory("UTF-8")]
    public void Utf8ToArrayBufferWriter() => _utf8.Render(_value, _destinations.ClearUtf8());
}
