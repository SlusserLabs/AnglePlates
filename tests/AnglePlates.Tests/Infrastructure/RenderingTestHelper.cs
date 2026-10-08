using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal static class RenderingTestHelper
{
    public static TemplateAdditionalText Template(
        string body, string type = "Example", string ns = "Consumer", string path = "Templates/example.html", string? anglePlate = TemplateAdditionalText.Discovered)
    {
        return new TemplateAdditionalText($"#type {type}\r\n#model string\r\n#namespace {ns}\r\n#using System\r\n{body}", path, anglePlate);
    }

    public static (string text, byte[] bytes) Render(Compilation compilation, string? model = "<value>", string type = "Consumer.Example", string? nullVariable = null)
    {
        using var generated = GeneratedAssembly.Load(compilation);
        var options = Activator.CreateInstance(generated.Assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions", throwOnError: true)!)!;
        options.GetType().GetProperty("NullVariable")!.SetValue(options, nullVariable);
        using var writer = new StringWriter();
        var buffer = new ArrayBufferWriter<byte>();
        Render(generated, type + "TextTemplate", model, writer, options);
        Render(generated, type + "Utf8Template", model, buffer, options);

        // Every rendering case also checks that RenderAsync writes the same bytes as Render
        var streamed = RenderToPipe(generated, type + "Utf8Template", model, options);
        if (!streamed.AsSpan().SequenceEqual(buffer.WrittenSpan))
        {
            throw new InvalidOperationException("RenderAsync wrote different bytes than Render.");
        }

        return (writer.ToString(), buffer.WrittenSpan.ToArray());
    }

    public static void Render(GeneratedAssembly generated, string typeName, string? model, object writer, object? options = null)
    {
        var type = generated.Assembly.GetType(typeName, throwOnError: true)!;
        var instance = Activator.CreateInstance(type, [options]);
        type.GetMethod("Render")!.Invoke(instance, [model, writer]);
    }

    public static ValueTask RenderAsync(
        GeneratedAssembly generated, string typeName, string? model, PipeWriter writer, object? options = null, CancellationToken cancellationToken = default)
    {
        var type = generated.Assembly.GetType(typeName, throwOnError: true)!;
        var instance = Activator.CreateInstance(type, [options]);

        return (ValueTask)type.GetMethod("RenderAsync")!.Invoke(instance, [model, writer, cancellationToken])!;
    }

    // A pipe without backpressure completes every flush synchronously, so the render finishes before this returns
    public static byte[] RenderToPipe(GeneratedAssembly generated, string typeName, string? model, object? options = null)
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0, useSynchronizationContext: false));
        var render = RenderAsync(generated, typeName, model, pipe.Writer, options);
        if (!render.IsCompleted)
        {
            throw new InvalidOperationException("RenderAsync didn't complete on a pipe without backpressure.");
        }

        // The render already completed, so this only rethrows a failure
        render.GetAwaiter().GetResult();
        pipe.Writer.Complete();
        if (!pipe.Reader.TryRead(out var result))
        {
            throw new InvalidOperationException("The completed pipe had nothing to read.");
        }

        var bytes = result.Buffer.ToArray();
        pipe.Reader.AdvanceTo(result.Buffer.End);
        pipe.Reader.Complete();

        return bytes;
    }
}
