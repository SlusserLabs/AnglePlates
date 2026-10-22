using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using PackageSmoke;
using SlusserLabs.AnglePlates;

// Checks the installed package with the same program under the JIT and Native AOT
internal static class Program
{
    private static async Task Main()
    {
        const string expected = "<p>Hello, Ada &amp; Grace! café</p>12";
        using var text = new StringWriter();
        GreetingTextTemplate.Default.Render("Ada & Grace", text);
        AssertEqual(expected, text.ToString());

        var bytes = new ArrayBufferWriter<byte>();
        GreetingUtf8Template.Default.Render("Ada & Grace", bytes);
        AssertEqual(expected, Encoding.UTF8.GetString(bytes.WrittenSpan));

        using var stream = new MemoryStream();
        var pipe = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        await GreetingUtf8Template.Default.RenderAsync("Ada & Grace", pipe);
        await pipe.CompleteAsync();
        AssertEqual(expected, Encoding.UTF8.GetString(stream.ToArray()));

        var plain = new EmailTextTemplate(new TemplateOptions { Encoder = null });
        AssertEqual("Hello, Ada & Grace!", plain.RenderToString("Ada & Grace"));
        Console.WriteLine("Package rendering passed.");
    }

    private static void AssertEqual(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }
}
