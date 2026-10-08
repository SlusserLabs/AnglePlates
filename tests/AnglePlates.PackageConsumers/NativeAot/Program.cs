using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using NativeAot.Templates;

namespace NativeAot;

internal static class Program
{
    private const string _expectedReport =
        "<h1>Q3 &lt;Report&gt;</h1>\n" +
        "<p>Read, Write |1234.5 0.25 10/07/2026 13:05:00</p>\n" +
        "<ul>\n" +
        "<li>Fish &amp; Chips 2 Active</li>\n" +
        "<li>Tea  Suspended</li>\n" +
        "</ul>";

    private static async Task<int> Main(string[] args)
    {
        // The same checks run under JIT and as a native executable, so both must produce these exact bytes
        var model = new ReportModel
        {
            Title = "Q3 <Report>",
            Permissions = Permissions.Read | Permissions.Write,
            Total = 1234.5m,
            Ratio = 0.25,
            Generated = new DateTime(2026, 10, 7, 13, 5, 0)
        };
        model.Lines.Add(new ReportLine("Fish & Chips", 2, Status.Active));
        model.Lines.Add(new ReportLine("Tea", null, Status.Suspended));
        var line = model.Lines[0];
        var expectedLine = Encoding.UTF8.GetBytes("<li>Fish &amp; Chips 2 Active</li>\n");

        using var text = new StringWriter(CultureInfo.InvariantCulture);
        var utf8 = new ArrayBufferWriter<byte>();
        ReportTextTemplate.Default.Render(model, text);
        ReportUtf8Template.Default.Render(model, utf8);
        var streamed = await RenderToPipeAsync(pipe => ReportUtf8Template.Default.RenderAsync(model, pipe));
        var streamedLine = await RenderToPipeAsync(pipe => LineUtf8Template.Default.RenderAsync(line, pipe));
        var expected = Encoding.UTF8.GetBytes(_expectedReport);

        // PublishAot turns off dynamic code even under dotnet run, so the validation script names the run instead
        var mode = args.Length > 0 ? args[0] : "an unnamed run";
        if (!typeof(ReportTextTemplate).IsPublic || !typeof(ReportUtf8Template).IsPublic || typeof(LineTextTemplate).IsPublic || typeof(LineUtf8Template).IsPublic ||
            !string.Equals(text.ToString(), _expectedReport, StringComparison.Ordinal) ||
            !utf8.WrittenSpan.SequenceEqual(expected) ||
            !streamed.AsSpan().SequenceEqual(expected) ||
            !streamedLine.AsSpan().SequenceEqual(expectedLine))
        {
            Console.Error.WriteLine($"NativeAot failed under {mode}: the report didn't render the expected text and bytes, or the template access was wrong.");
            Console.Error.WriteLine(text.ToString());
            return 1;
        }

        Console.WriteLine($"NativeAot passed under {mode}.");
        return 0;
    }

    private static async Task<byte[]> RenderToPipeAsync(Func<PipeWriter, ValueTask> render)
    {
        // The output is far below the pipe's backpressure threshold, so one read after completing the writer gets all of it
        var pipe = new Pipe();
        await render(pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var read = await pipe.Reader.ReadAsync();
        var bytes = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);
        await pipe.Reader.CompleteAsync();

        return bytes;
    }
}
