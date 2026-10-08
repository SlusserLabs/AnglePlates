using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using SharedTemplates.Library;

namespace SharedTemplates.App;

internal static class Program
{
    private static async Task<int> Main()
    {
        // Each project has its own support types; naming TemplateOptions here would pick this project's copy with CS0436
        var appOptions = typeof(PageTextTemplate).Assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions");
        var libraryOptions = typeof(ReceiptTextTemplate).Assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions");
        using var page = new StringWriter(CultureInfo.InvariantCulture);
        using var receipt = new StringWriter(CultureInfo.InvariantCulture);
        PageTextTemplate.Default.Render("<app>", page);
        ReceiptTextTemplate.Default.Render("<library>", receipt);
        var streamedPage = await RenderToPipeAsync(pipe => PageUtf8Template.Default.RenderAsync("<app>", pipe));
        var streamedReceipt = await RenderToPipeAsync(pipe => ReceiptUtf8Template.Default.RenderAsync("<library>", pipe));

        if (appOptions is null || appOptions.IsPublic || libraryOptions is null || !libraryOptions.IsPublic ||
            !string.Equals(page.ToString(), "Page &lt;app&gt;!", StringComparison.Ordinal) ||
            !string.Equals(receipt.ToString(), "Receipt &lt;library&gt;!", StringComparison.Ordinal) ||
            !streamedPage.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(page.ToString())) ||
            !streamedReceipt.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(receipt.ToString())))
        {
            Console.Error.WriteLine("SharedTemplates failed: expected internal app support types, public library support types, and both templates to render.");
            return 1;
        }

        Console.WriteLine("SharedTemplates passed.");
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
