using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

public sealed class BoilerplateTests
{
    [Test]
    [Arguments(false, 1, 0)]
    [Arguments(true, 1, 0)]
    [Arguments(false, 100000, 0)]
    [Arguments(true, 100000, 0)]
    [Arguments(false, 100000, 100)]
    [Arguments(true, 100000, 100)]
    public async Task Write_EncodedValues_ReturnsEachRentedArrayOnce(bool utf8, int length, int extraLength)
    {
        var pool = new CountingCharPool { ExtraLength = extraLength };
        using var generated = CreateRenderer();
        var options = CreateOptions(generated, pool, HtmlEncoder.Default);
        var value = new string('<', length) + "\U0001F600é\"&";
        var expected = HtmlEncoder.Default.Encode(value);

        for (int i = 0; i < 2; i++)
        {
            var actual = Render(generated, options, value, utf8);
            await Assert.That(actual).IsEqualTo(expected);
            await Assert.That(pool.Rents).IsEqualTo(i + 1);
            await Assert.That(pool.Returns).IsEqualTo(i + 1);
            await Assert.That(pool.Outstanding).IsEqualTo(0);
        }

        await Assert.That(pool.MaximumRequest).IsLessThanOrEqualTo(32768);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Write_UnicodeAtChunkBoundary_PreservesCompleteScalars(bool utf8)
    {
        using var generated = CreateRenderer();
        var pool = new CountingCharPool();
        var options = CreateOptions(generated, pool, HtmlEncoder.Default);
        var value = new string('a', 32767) + "\U0001F600" + new string('<', 20000);

        await Assert.That(Render(generated, options, value, utf8)).IsEqualTo(HtmlEncoder.Default.Encode(value));
        await Assert.That(pool.Rents).IsEqualTo(1);
        await Assert.That(pool.Returns).IsEqualTo(1);
        await Assert.That(pool.Outstanding).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Write_RawNullAndEmptyValues_AvoidsUnneededRentals(bool utf8)
    {
        using var generated = CreateRenderer();
        var pool = new CountingCharPool();
        var options = CreateOptions(generated, pool, null);

        await Assert.That(Render(generated, options, "<raw>", utf8)).IsEqualTo("<raw>");
        await Assert.That(Render(generated, options, null, utf8)).IsEqualTo(string.Empty);
        await Assert.That(Render(generated, options, string.Empty, utf8)).IsEqualTo(string.Empty);
        options.GetType().GetProperty("Encoder")!.SetValue(options, HtmlEncoder.Default);
        await Assert.That(Render(generated, options, null, utf8)).IsEqualTo(string.Empty);
        await Assert.That(Render(generated, options, string.Empty, utf8)).IsEqualTo(string.Empty);
        await Assert.That(pool.Rents).IsEqualTo(0);
        options.GetType().GetProperty("NullVariable")!.SetValue(options, "<null>");
        await Assert.That(Render(generated, options, null, utf8)).IsEqualTo("&lt;null&gt;");
        await Assert.That(pool.Rents).IsEqualTo(1);
        await Assert.That(pool.Returns).IsEqualTo(1);
    }

    [Test]
    [Arguments(false, "throw")]
    [Arguments(true, "throw")]
    [Arguments(false, "invalid")]
    [Arguments(true, "invalid")]
    [Arguments(false, "incomplete")]
    [Arguments(true, "incomplete")]
    [Arguments(false, "stalled")]
    [Arguments(true, "stalled")]
    public async Task Write_EncoderFailure_ReturnsBuffer(bool utf8, string mode)
    {
        using var generated = CreateRenderer();
        var pool = new CountingCharPool();
        var options = CreateOptions(generated, pool, new ProbeEncoder(mode));
        var exception = await Assert.That(() => Render(generated, options, "data", utf8)).ThrowsExactly<TargetInvocationException>();

        await Assert.That(exception!.InnerException is InvalidOperationException).IsTrue();
        if (mode == "stalled")
        {
            await Assert.That(exception.InnerException!.Message).IsEqualTo("Encoder made no progress with the temporary buffer.");
        }

        await Assert.That(pool.Rents).IsEqualTo(1);
        await Assert.That(pool.Returns).IsEqualTo(1);
        await Assert.That(pool.Outstanding).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Write_OverflowProneExpansion_ClampsBeforeRenting(bool utf8)
    {
        using var generated = CreateRenderer();
        var pool = new CountingCharPool();
        var options = CreateOptions(generated, pool, new ProbeEncoder("copy"));

        await Assert.That(Render(generated, options, "data", utf8)).IsEqualTo("data");
        await Assert.That(pool.MaximumRequest).IsEqualTo(32768);
        await Assert.That(pool.Returns).IsEqualTo(1);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Write_WriterFailure_ReturnsBufferToOriginalPool(bool utf8, bool throwOnAdvance)
    {
        using var generated = CreateRenderer();
        var pool = new CountingCharPool();
        var replacement = new CountingCharPool();
        var options = CreateOptions(generated, pool, HtmlEncoder.Default);
        void ChangePool() => options.GetType().GetProperty("BufferPool")!.SetValue(options, replacement);
        using var textWriter = new ThrowingTextWriter(ChangePool);
        object writer = textWriter;
        if (utf8)
        {
            writer = new ThrowingBufferWriter(ChangePool, throwOnAdvance);
        }

        var exception = await Assert.That(() => RenderingTestHelper.Render(generated,
            utf8 ? "Consumer.ExampleUtf8Template" : "Consumer.ExampleTextTemplate", "<data>", writer, options)).ThrowsExactly<TargetInvocationException>();

        await Assert.That(exception!.InnerException is IOException).IsTrue();
        await Assert.That(pool.Rents).IsEqualTo(1);
        await Assert.That(pool.Returns).IsEqualTo(1);
        await Assert.That(pool.Outstanding).IsEqualTo(0);
        await Assert.That(replacement.Returns).IsEqualTo(0);
        await Assert.That(textWriter.WasDisposed).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Write_RentFailure_DoesNotReturnUnacquiredBuffer(bool utf8)
    {
        using var generated = CreateRenderer();
        var pool = new CountingCharPool { ThrowOnRent = true };
        var options = CreateOptions(generated, pool, HtmlEncoder.Default);
        var exception = await Assert.That(() => Render(generated, options, "data", utf8)).ThrowsExactly<TargetInvocationException>();

        await Assert.That(exception!.InnerException!.Message).IsEqualTo("Rent failed");
        await Assert.That(pool.Rents).IsEqualTo(0);
        await Assert.That(pool.Returns).IsEqualTo(0);
    }

    private static GeneratedAssembly CreateRenderer()
    {
        return GeneratedAssembly.Load(GeneratorTestHelper.Run(RenderingTestHelper.Template("[[ model ]]")).Compilation);
    }

    private static object CreateOptions(GeneratedAssembly generated, CountingCharPool pool, TextEncoder? encoder)
    {
        var type = generated.Assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions", throwOnError: true)!;
        var options = Activator.CreateInstance(type)!;
        type.GetProperty("BufferPool")!.SetValue(options, pool);
        type.GetProperty("Encoder")!.SetValue(options, encoder);

        return options;
    }

    private static string Render(GeneratedAssembly generated, object options, string? model, bool utf8)
    {
        if (utf8)
        {
            var buffer = new ArrayBufferWriter<byte>();
            RenderingTestHelper.Render(generated, "Consumer.ExampleUtf8Template", model, buffer, options);

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        using var writer = new StringWriter();
        RenderingTestHelper.Render(generated, "Consumer.ExampleTextTemplate", model, writer, options);

        return writer.ToString();
    }
}
