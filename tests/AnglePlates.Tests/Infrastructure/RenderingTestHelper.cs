using System;
using System.Buffers;
using System.IO;
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

        return (writer.ToString(), buffer.WrittenSpan.ToArray());
    }

    public static void Render(GeneratedAssembly generated, string typeName, string? model, object writer, object? options = null)
    {
        var type = generated.Assembly.GetType(typeName, throwOnError: true)!;
        var instance = Activator.CreateInstance(type, [options]);
        type.GetMethod("Render")!.Invoke(instance, [model, writer]);
    }
}
