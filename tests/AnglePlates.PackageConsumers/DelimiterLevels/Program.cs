using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace Consumers;

// Bracket levels, closers inside C#, and multi-line C# behave the same under the consumer's own compiler
internal static class Program
{
    private const string _expected = "1:7\n2:&lt;\n3:%]\n4:]]]%\n5:]]]\n6:%]]\n7:tail\n8:done\n9:[&lt;value&gt;]\n10:\\&lt;value&gt;\n11:[[x]]\n12:a\n  b\n13:ok";

    private static async Task<int> Main()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var buffer = new ArrayBufferWriter<byte>();
        LevelsTextTemplate.Default.Render("<value>", writer);
        LevelsUtf8Template.Default.Render("<value>", buffer);

        // A pause threshold of zero turns off backpressure, so one read after completing the writer gets all of the output
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
        await LevelsUtf8Template.Default.RenderAsync("<value>", pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var read = await pipe.Reader.ReadAsync();
        var streamed = Encoding.UTF8.GetString(read.Buffer.ToArray());
        pipe.Reader.AdvanceTo(read.Buffer.End);
        await pipe.Reader.CompleteAsync();

        var text = writer.ToString();
        var utf8 = Encoding.UTF8.GetString(buffer.WrittenSpan);
        if (!string.Equals(text, _expected, StringComparison.Ordinal) ||
            !string.Equals(utf8, _expected, StringComparison.Ordinal) ||
            !string.Equals(streamed, _expected, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("DelimiterLevels failed.");
            Console.Error.WriteLine($"Expected: {_expected}");
            Console.Error.WriteLine($"Text:     {text}");
            Console.Error.WriteLine($"UTF-8:    {utf8}");
            Console.Error.WriteLine($"Async:    {streamed}");
            return 1;
        }

        Console.WriteLine("DelimiterLevels passed.");
        return 0;
    }
}
