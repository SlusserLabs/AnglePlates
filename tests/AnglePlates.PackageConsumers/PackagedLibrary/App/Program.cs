using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using PackagedLibrary.Library;

namespace PackagedLibrary.App;

internal static class Program
{
    private static async Task<int> Main()
    {
        // The app's own Templates folder must not generate anything, so look the classes up by name
        var assembly = typeof(Program).Assembly;
        var hasPage = assembly.GetType("PackagedLibrary.App.PageTextTemplate") is not null;
        var hasSupportTypes = assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions") is not null;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        InvoiceTextTemplate.Default.Render("<package>", writer);

        // The output is far below the pipe's backpressure threshold, so one read after completing the writer gets all of it
        var pipe = new Pipe();
        await InvoiceUtf8Template.Default.RenderAsync("<package>", pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var read = await pipe.Reader.ReadAsync();
        var streamed = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);
        await pipe.Reader.CompleteAsync();

        const string expected = "Invoice &lt;package&gt;!";
        if (hasPage || hasSupportTypes || !string.Equals(writer.ToString(), expected, StringComparison.Ordinal) ||
            !streamed.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected)))
        {
            Console.Error.WriteLine("PackagedLibrary failed: expected no generated classes in the app and the packaged template to render.");
            return 1;
        }

        Console.WriteLine("PackagedLibrary passed.");
        return 0;
    }
}
