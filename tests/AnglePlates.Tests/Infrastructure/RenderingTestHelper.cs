using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Compiles ordinary consumer calls and checks text, UTF-8, and async rendering
internal static class RenderingTestHelper
{
    public static TemplateAdditionalText Template(
        string body, string type = "Example", string ns = "Consumer", string path = "Templates/example.html", string? anglePlate = TemplateAdditionalText.Discovered)
    {
        return new TemplateAdditionalText($"#type {type}\r\n#model string\r\n#namespace {ns}\r\n#using System\r\n{body}", path, anglePlate);
    }

    public static async Task<(string text, byte[] bytes)> RenderAsync(
        Compilation compilation, string? model = "<value>", string type = "Consumer.Example", string? nullVariable = null)
    {
        // Callers use metadata names, so escape namespace keywords when writing the consumer's C# names
        type = string.Join(".", type.Split('.').Select(part => "@" + part.TrimStart('@')));
        var consumer = $$"""
            namespace AnglePlatesTestConsumer;

            public static class EntryPoint
            {
                public static async global::System.Threading.Tasks.Task<(string, byte[])> RenderAsync(string? model, string? nullVariable)
                {
                    var options = new global::SlusserLabs.AnglePlates.TemplateOptions { NullVariable = nullVariable };
                    using var text = new global::System.IO.StringWriter();
                    new global::{{type}}TextTemplate(options).Render(model!, text);
                    var bytes = new global::System.Buffers.ArrayBufferWriter<byte>();
                    var utf8 = new global::{{type}}Utf8Template(options);
                    utf8.Render(model!, bytes);

                    using var stream = new global::System.IO.MemoryStream();
                    var pipe = global::System.IO.Pipelines.PipeWriter.Create(
                        stream, new global::System.IO.Pipelines.StreamPipeWriterOptions(leaveOpen: true));
                    await utf8.RenderAsync(model!, pipe);
                    await pipe.CompleteAsync();
                    if (!global::System.MemoryExtensions.SequenceEqual<byte>(bytes.WrittenSpan, stream.ToArray()))
                    {
                        throw new global::System.InvalidOperationException("RenderAsync wrote different bytes than Render.");
                    }

                    return (text.ToString(), bytes.WrittenSpan.ToArray());
                }
            }
            """;
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(consumer, parseOptions));
        using var generated = GeneratedAssembly.Load(compilation);
        var render = generated.GetDelegate<Func<string?, string?, Task<(string, byte[])>>>("AnglePlatesTestConsumer.EntryPoint", "RenderAsync");

        return await render(model, nullVariable);
    }

    public static async Task AssertRendersAsync(Compilation compilation, string type, string expected)
    {
        var (text, bytes) = await RenderAsync(compilation, type: type);

        await Assert.That(text).IsEqualTo(expected);
        await Assert.That(bytes).IsEquivalentTo(Encoding.UTF8.GetBytes(expected));
    }
}
