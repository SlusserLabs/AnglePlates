using System;
using System.Buffers;
using System.IO;
using System.Text;
using SlusserLabs.AnglePlates.Samples.HelloWorld.Templates;
using SlusserLabs.AnglePlates.Samples.HelloWorld.ViewModels;

namespace SlusserLabs.AnglePlates.Samples.HelloWorld;

internal static class Program
{
    private static void Main()
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
        string text = writer.ToString();
        if (!buffer.WrittenSpan.SequenceEqual(Encoding.UTF8.GetBytes(text)))
        {
            throw new InvalidOperationException("Text and UTF-8 rendering produced different output.");
        }

        Console.WriteLine(text);
    }
}
