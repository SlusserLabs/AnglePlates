using System.Buffers;
using System.Globalization;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SlusserLabs.AnglePlates.Benchmarks.Templates;

namespace SlusserLabs.AnglePlates.Benchmarks;

// Measures equivalent lists with plain literals or literals separated by comments and empty blocks
[MemoryDiagnoser]
public class RenderingBenchmarks
{
    private readonly string[] _items = Enumerable.Range(0, 100).Select(index => $"Item <{index}> & details").ToArray();
    private readonly StringWriter _text = new(CultureInfo.InvariantCulture);
    private readonly ArrayBufferWriter<byte> _utf8 = new();
    private TextTemplate<string[]> _textTemplate = default!;
    private Utf8Template<string[]> _utf8Template = default!;

    [Params(false, true)]
    public bool SplitLiterals { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _textTemplate = SplitLiterals ? SplitListTextTemplate.Default : ListTextTemplate.Default;
        _utf8Template = SplitLiterals ? SplitListUtf8Template.Default : ListUtf8Template.Default;
    }

    [Benchmark]
    public void Text()
    {
        _text.GetStringBuilder().Clear();
        _textTemplate.Render(_items, _text);
    }

    [Benchmark]
    public void Utf8()
    {
        _utf8.ResetWrittenCount();
        _utf8Template.Render(_items, _utf8);
    }

    [GlobalCleanup]
    public void Cleanup() => _text.Dispose();
}
