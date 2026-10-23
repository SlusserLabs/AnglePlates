using System.Buffers;
using System.Globalization;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SlusserLabs.AnglePlates.Benchmarks.Templates;

namespace SlusserLabs.AnglePlates.Benchmarks;

// Measures a loop with encoded values through the text and UTF-8 writers
[MemoryDiagnoser]
public class RenderingBenchmarks
{
    private readonly string[] _items = Enumerable.Range(0, 100).Select(index => $"Item <{index}> & details").ToArray();
    private readonly StringWriter _text = new(CultureInfo.InvariantCulture);
    private readonly ArrayBufferWriter<byte> _utf8 = new();

    [Benchmark]
    public void Text()
    {
        _text.GetStringBuilder().Clear();
        ListTextTemplate.Default.Render(_items, _text);
    }

    [Benchmark]
    public void Utf8()
    {
        _utf8.ResetWrittenCount();
        ListUtf8Template.Default.Render(_items, _utf8);
    }

    [GlobalCleanup]
    public void Cleanup() => _text.Dispose();
}
