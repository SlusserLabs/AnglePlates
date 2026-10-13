using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;

// Renders compiled templates through the text, UTF-8, and pipe paths
[ExcludeFromCodeCoverage]
internal static class TemplateOutput
{
    public static string RenderText<TModel>(TextTemplate<TModel> template, TModel model)
    {
        using var writer = new StringWriter();
        template.Render(model, writer);

        return writer.ToString();
    }

    public static byte[] RenderUtf8<TModel>(Utf8Template<TModel> template, TModel model)
    {
        var buffer = new ArrayBufferWriter<byte>();
        template.Render(model, buffer);

        return buffer.WrittenSpan.ToArray();
    }

    // The pipe has no backpressure, so the whole output is waiting once the render finishes
    public static async Task<byte[]> RenderPipeAsync<TModel>(Utf8Template<TModel> template, TModel model)
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0, useSynchronizationContext: false));
        await template.RenderAsync(model, pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var result = await pipe.Reader.ReadAsync();
        var bytes = result.Buffer.ToArray();
        pipe.Reader.AdvanceTo(result.Buffer.End);
        await pipe.Reader.CompleteAsync();

        return bytes;
    }

    // Checks that Render on both targets and RenderAsync all write the expected text
    public static async Task AssertRendersAsync<TModel>(TextTemplate<TModel> text, Utf8Template<TModel> utf8, TModel model, string expected)
    {
        var expectedHex = Convert.ToHexString(Encoding.UTF8.GetBytes(expected));

        await Assert.That(RenderText(text, model)).IsEqualTo(expected);
        await Assert.That(Convert.ToHexString(RenderUtf8(utf8, model))).IsEqualTo(expectedHex);
        await Assert.That(Convert.ToHexString(await RenderPipeAsync(utf8, model))).IsEqualTo(expectedHex);
    }
}
