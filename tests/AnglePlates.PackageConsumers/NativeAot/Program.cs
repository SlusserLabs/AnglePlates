using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NativeAot.Templates;
using SlusserLabs.AnglePlates;

namespace NativeAot;

// Renders every kind of value the templates support and compares exact text and bytes, under JIT and as a native executable
internal static class Program
{
    private const string _notesUnit = "<a&b>";
    private const int _notesRepeat = 8000;

    // The literal text is UTF-8 in the template file and must come out as the same characters and bytes
    private static readonly string _expectedReport =
        "<h1>Q3 &lt;Report&gt;</h1>\n" +
        "<p>Read, Write |1234.5 0.25 10/07/2026 13:05:00</p>\n" +
        "<p>Café ✓ 東京 10/07/2026 13:05:00 &#x2B;02:00 0f8fad5b-d9cb-469f-a165-70867728950e</p>\n" +
        "<pre>" + string.Concat(Enumerable.Repeat("&lt;a&amp;b&gt;", _notesRepeat)) + "</pre>\n" +
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
            Generated = new DateTime(2026, 10, 7, 13, 5, 0),
            Stamp = new DateTimeOffset(2026, 10, 7, 13, 5, 0, TimeSpan.FromHours(2)),
            Id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
            Notes = string.Concat(Enumerable.Repeat(_notesUnit, _notesRepeat))
        };
        model.Lines.Add(new ReportLine("Fish & Chips", 2, Status.Active));
        model.Lines.Add(new ReportLine("Tea", null, Status.Suspended));
        var line = model.Lines[0];
        var expectedLine = Encoding.UTF8.GetBytes("<li>Fish &amp; Chips 2 Active</li>\n");

        var failures = new List<string>();
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        var utf8 = new ArrayBufferWriter<byte>();
        ReportTextTemplate.Default.Render(model, text);
        ReportUtf8Template.Default.Render(model, utf8);
        var streamed = await RenderToPipeAsync(pipe => ReportUtf8Template.Default.RenderAsync(model, pipe));
        var streamedLine = await RenderToPipeAsync(pipe => LineUtf8Template.Default.RenderAsync(line, pipe));
        var expected = Encoding.UTF8.GetBytes(_expectedReport);

        Check(failures, "report text", string.Equals(text.ToString(), _expectedReport, StringComparison.Ordinal));
        Check(failures, "report RenderToString", string.Equals(ReportTextTemplate.Default.RenderToString(model), _expectedReport, StringComparison.Ordinal));
        Check(failures, "report UTF-8", utf8.WrittenSpan.SequenceEqual(expected));
        Check(failures, "report RenderAsync", streamed.AsSpan().SequenceEqual(expected));
        Check(failures, "line RenderAsync", streamedLine.AsSpan().SequenceEqual(expectedLine));
        Check(failures, "template access", typeof(ReportTextTemplate).IsPublic && typeof(ReportUtf8Template).IsPublic &&
            !typeof(LineTextTemplate).IsPublic && !typeof(LineUtf8Template).IsPublic);

        // Each preset and a custom NullVariable, through both renderers
        var options = new OptionsModel("Tom & 'Jérôme' <b>", null);
        CheckOptions(failures, "Html", TemplateOptions.Html, options, "Tom &amp; &#x27;J&#xE9;r&#xF4;me&#x27; &lt;b&gt;|");
        CheckOptions(failures, "Unencoded", TemplateOptions.Unencoded, options, "Tom & 'Jérôme' <b>|");
        CheckOptions(failures, "JavaScript", TemplateOptions.JavaScript, options, "Tom \\u0026 \\u0027J\\u00E9r\\u00F4me\\u0027 \\u003Cb\\u003E|");
        CheckOptions(failures, "NullVariable", new TemplateOptions { NullVariable = "(none)" }, options, "Tom &amp; &#x27;J&#xE9;r&#xF4;me&#x27; &lt;b&gt;|(none)");

        // PublishAot turns off dynamic code even under dotnet run, so the validation script names the run instead
        var mode = args.Length > 0 ? args[0] : "an unnamed run";
        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"NativeAot failed under {mode}: {string.Join("; ", failures)}");
            return 1;
        }

        Console.WriteLine($"NativeAot passed under {mode}.");
        return 0;
    }

    private static void CheckOptions(List<string> failures, string name, TemplateOptions options, OptionsModel model, string expected)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        var utf8 = new ArrayBufferWriter<byte>();
        new OptionsTextTemplate(options).Render(model, text);
        new OptionsUtf8Template(options).Render(model, utf8);

        Check(failures, $"{name} text gave '{text}'", string.Equals(text.ToString(), expected, StringComparison.Ordinal));
        Check(failures, $"{name} UTF-8 gave '{Encoding.UTF8.GetString(utf8.WrittenSpan)}'", utf8.WrittenSpan.SequenceEqual(Encoding.UTF8.GetBytes(expected)));
    }

    private static void Check(List<string> failures, string name, bool passed)
    {
        if (!passed)
        {
            failures.Add(name);
        }
    }

    private static async Task<byte[]> RenderToPipeAsync(Func<PipeWriter, ValueTask> render)
    {
        // A pause threshold of zero turns off backpressure, so the report's long notes can't block a flush, and one read
        // after completing the writer gets all of the output
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
        await render(pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var read = await pipe.Reader.ReadAsync();
        var bytes = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);
        await pipe.Reader.CompleteAsync();

        return bytes;
    }
}
