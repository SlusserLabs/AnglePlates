using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Custom encoders, and writers that fail while a C# variable is written
[ExcludeFromCodeCoverage]
public sealed class WriteFailureTests
{
    [Test]
    public async Task Render_CustomEncoder_EncodesThroughOptionsEncoder()
    {
        // The probe leaves "<" alone, so the output shows the options' encoder ran instead of the HTML default
        var encoder = new ProbeEncoder();
        var options = TestOptions.Create(encoder);

        await TemplateOutput.AssertRendersAsync(new VariableTextTemplate(options), new VariableUtf8Template(options), "<data>", "<data>");
        await Assert.That(encoder.Calls).IsGreaterThan(0);
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
