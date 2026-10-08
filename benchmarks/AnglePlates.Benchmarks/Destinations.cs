using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;

namespace SlusserLabs.AnglePlates.Benchmarks;

// Reusable destinations, so a benchmark measures rendering rather than growing a fresh buffer every time
internal sealed class Destinations
{
    public StringWriter Text { get; } = new(CultureInfo.InvariantCulture);

    public ArrayBufferWriter<byte> Utf8 { get; } = new();

    // Without backpressure every flush completes synchronously, so the pipe benchmarks measure rendering and flushing alone
    public Pipe Pipe { get; } = new(new PipeOptions(pauseWriterThreshold: 0, useSynchronizationContext: false));

    public StringWriter ClearText()
    {
        Text.GetStringBuilder().Clear();
        return Text;
    }

    public ArrayBufferWriter<byte> ClearUtf8()
    {
        Utf8.ResetWrittenCount();
        return Utf8;
    }

    public void DrainPipe()
    {
        if (Pipe.Reader.TryRead(out var result))
        {
            Pipe.Reader.AdvanceTo(result.Buffer.End);
        }
    }

    // A baseline that writes something else measures nothing useful, so setup fails instead
    public static void CheckSame(string scenario, Action<TextWriter> template, Action<TextWriter> handWritten)
    {
        using var expected = new StringWriter(CultureInfo.InvariantCulture);
        using var actual = new StringWriter(CultureInfo.InvariantCulture);
        template(expected);
        handWritten(actual);
        if (!string.Equals(expected.ToString(), actual.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The hand-written {scenario} text doesn't match the template.");
        }
    }

    public static void CheckSame(string scenario, Action<IBufferWriter<byte>> template, Action<IBufferWriter<byte>> handWritten)
    {
        var expected = new ArrayBufferWriter<byte>();
        var actual = new ArrayBufferWriter<byte>();
        template(expected);
        handWritten(actual);
        if (!expected.WrittenSpan.SequenceEqual(actual.WrittenSpan))
        {
            throw new InvalidOperationException($"The hand-written {scenario} UTF-8 bytes don't match the template.");
        }
    }
}
