using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Custom encoders and writers that fail or misbehave while a C# variable is written
[ExcludeFromCodeCoverage]
public sealed class WriteFailureTests
{
    [Test]
    [Arguments(false, "throw", "Encoder failed")]
    [Arguments(true, "throw", "Encoder failed")]
    [Arguments(false, "invalid", "Invalid data found in string. Does this string contain unpaired surrogates?")]
    [Arguments(true, "invalid", "Invalid data found in string. Does this string contain unpaired surrogates?")]
    [Arguments(false, "incomplete", "Encoder needs more data, but all data was provided. Does this string end with an unpaired surrogate?")]
    [Arguments(true, "incomplete", "Encoder needs more data, but all data was provided. Does this string end with an unpaired surrogate?")]
    [Arguments(false, "stalled", "Encoder made no progress with the temporary buffer.")]
    [Arguments(true, "stalled", "Encoder made no progress with the temporary buffer.")]
    public async Task Render_EncoderFailure_ThrowsInvalidOperation(bool utf8, string mode, string message)
    {
        var options = TestOptions.Create(new ProbeEncoder(mode));
        Action render = utf8
            ? () => TemplateOutput.RenderUtf8(new VariableUtf8Template(options), "data")
            : () => TemplateOutput.RenderText(new VariableTextTemplate(options), "data");

        var exception = await Assert.That(render).ThrowsExactly<InvalidOperationException>();

        await Assert.That(exception!.Message).IsEqualTo(message);
    }

    [Test]
    public async Task Render_OverflowProneExpansion_ClampsAndWritesValue()
    {
        // The probe claims int.MaxValue output characters per input character, so the rental size must clamp without overflowing
        var options = TestOptions.Create(new ProbeEncoder("copy"));

        await TemplateOutput.AssertRendersAsync(new VariableTextTemplate(options), new VariableUtf8Template(options), "data", "data");
    }

    [Test]
    public async Task Render_TextWriterFailure_ThrowsWithoutDisposingWriter()
    {
        var template = new VariableTextTemplate(TestOptions.Create(encoded: true));
        using var writer = new ThrowingTextWriter();

        await Assert.That(() => template.Render("<data>", writer)).ThrowsExactly<IOException>();
        await Assert.That(writer.WasDisposed).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Render_BufferWriterFailure_Throws(bool throwOnAdvance)
    {
        var template = new VariableUtf8Template(TestOptions.Create(encoded: true));
        var writer = new ThrowingBufferWriter(throwOnAdvance);

        await Assert.That(() => template.Render("<data>", writer)).ThrowsExactly<IOException>();
    }
}
