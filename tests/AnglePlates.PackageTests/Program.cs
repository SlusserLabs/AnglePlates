using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.PackageTests.Templates;

namespace SlusserLabs.AnglePlates.PackageTests;

// Validates the package, then renders its templates through every output path in a clean consumer
internal static class Program
{
    private static async Task Main()
    {
        // The first command-line argument is the program itself
        var arguments = PackageArguments.Parse(Environment.GetCommandLineArgs()[1..]);
        PackageValidator.Validate(arguments);
        ConsumerAssetsValidator.Validate();
        EditorConfigValidator.Validate();

        // The project sets no access options, so the support types and templates use the internal defaults
        if (typeof(TemplateOptions).IsPublic || typeof(TextTemplate<>).IsPublic || typeof(Utf8Template<>).IsPublic || typeof(PackageTextTemplate).IsPublic)
        {
            throw new InvalidOperationException("The generated support types and templates should be internal by default.");
        }

        const string model = "<AnglePlates>";
        const string expected = "Hello &lt;AnglePlates&gt;!";
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        var utf8 = new ArrayBufferWriter<byte>();
        PackageTextTemplate.Default.Render(model, text);
        PackageUtf8Template.Default.Render(model, utf8);
        var streamed = await RenderToPipeAsync(model);
        using var custom = new StringWriter(CultureInfo.InvariantCulture);
        using var explicitTemplate = new StringWriter(CultureInfo.InvariantCulture);
        CustomTextTemplate.Default.Render(model, custom);
        ExplicitTextTemplate.Default.Render(model, explicitTemplate);
        using var nullable = new StringWriter(CultureInfo.InvariantCulture);
        new NullableTextTemplate(new TemplateOptions { NullVariable = "<none>" }).Render(null, nullable);

        if (!string.Equals(text.ToString(), expected, StringComparison.Ordinal) ||
            !utf8.WrittenSpan.SequenceEqual(Encoding.UTF8.GetBytes(expected)) ||
            !streamed.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected)) ||
            !string.Equals(custom.ToString(), "Custom &lt;AnglePlates&gt;!", StringComparison.Ordinal) ||
            !string.Equals(explicitTemplate.ToString(), "Explicit &lt;AnglePlates&gt;!", StringComparison.Ordinal) ||
            !string.Equals(nullable.ToString(), "Nullable &lt;none&gt;!", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The package renderers did not produce the expected text and UTF-8 bytes.");
        }

        Console.WriteLine("Package archive, symbols, template metadata, text, UTF-8, and streamed UTF-8 rendering passed.");
    }

    private static async Task<byte[]> RenderToPipeAsync(string model)
    {
        // The output is far below the pipe's backpressure threshold, so one read after completing the writer gets all of it
        var pipe = new Pipe();
        await PackageUtf8Template.Default.RenderAsync(model, pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var read = await pipe.Reader.ReadAsync();
        var bytes = read.Buffer.ToArray();
        pipe.Reader.AdvanceTo(read.Buffer.End);
        await pipe.Reader.CompleteAsync();

        return bytes;
    }
}
