using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Samples.HelloWorld.Templates;
using SlusserLabs.AnglePlates.Samples.HelloWorld.ViewModels;

namespace SlusserLabs.AnglePlates.Samples.HelloWorld;

internal static class Program
{
    private static async Task Main()
    {
        var model = new HelloWorldViewModel
        {
            Title = "Hello, world!",
        };
        model.Paragraphs.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
        model.Paragraphs.Add("Aenean at aliquam nulla, id convallis lorem.");

        using var writer = new StringWriter();
        HelloWorldTextTemplate.Default.Render(model, writer);

        var buffer = new ArrayBufferWriter<byte>();
        HelloWorldUtf8Template.Default.Render(model, buffer);

        // The page is far below the pipe's backpressure threshold, so one read after completing the writer gets all of it
        var pipe = new Pipe();
        await HelloWorldUtf8Template.Default.RenderAsync(model, pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var read = await pipe.Reader.ReadAsync();
        var streamed = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);
        await pipe.Reader.CompleteAsync();

        string text = writer.ToString();
        var expected = Encoding.UTF8.GetBytes(text);
        if (!buffer.WrittenSpan.SequenceEqual(expected) || !streamed.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidOperationException("Text, UTF-8, and streamed UTF-8 rendering produced different output.");
        }

        Console.WriteLine(text);
    }
}
