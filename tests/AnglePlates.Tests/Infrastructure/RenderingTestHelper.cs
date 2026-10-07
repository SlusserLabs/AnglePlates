using System;
using System.Buffers;
using System.IO;
using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal static class RenderingTestHelper
{
    public static TemplateAdditionalText Template(string body, string type = "Example", string ns = "Consumer", string path = "Templates/example.html")
    {
        return new TemplateAdditionalText($"#type {type}\r\n#model string\r\n#namespace {ns}\r\n#using System\r\n{body}", path);
    }

    public static (string text, byte[] bytes) Render(Compilation compilation, string model = "<value>", string type = "Consumer.Example")
    {
        using var generated = GeneratedAssembly.Load(compilation);
        using var writer = new StringWriter();
        var buffer = new ArrayBufferWriter<byte>();
        Render(generated, type + "TextTemplate", model, writer);
        Render(generated, type + "Utf8Template", model, buffer);

        return (writer.ToString(), buffer.WrittenSpan.ToArray());
    }

    public static void Render(GeneratedAssembly generated, string typeName, string? model, object writer, object? options = null)
    {
        var type = generated.Assembly.GetType(typeName, throwOnError: true)!;
        var instance = Activator.CreateInstance(type, [options]);
        type.GetMethod("Render")!.Invoke(instance, [model, writer]);
    }
}
