using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Checks argument validation through the public rendering methods
[ExcludeFromCodeCoverage]
public sealed class SurfaceTests
{
    [Test]
    public async Task Render_NullWriter_ThrowsArgumentNull()
    {
        var text = new VariableTextTemplate();
        var utf8 = new VariableUtf8Template();

        var textException = await Assert.That(() => text.Render("x", (TextWriter)null!)).ThrowsExactly<ArgumentNullException>();
        var utf8Exception = await Assert.That(() => utf8.Render("x", (IBufferWriter<byte>)null!)).ThrowsExactly<ArgumentNullException>();

        await Assert.That(textException!.ParamName).IsEqualTo("writer");
        await Assert.That(utf8Exception!.ParamName).IsEqualTo("writer");
    }

    [Test]
    public async Task RenderAsync_NullWriter_FaultsReturnedTask()
    {
        // RenderAsync is an async method, so the check faults the ValueTask instead of throwing before it returns
        var template = new VariableUtf8Template();
        var render = template.RenderAsync("x", null!);

        await Assert.That(render.IsFaulted).IsTrue();
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await render);
        await Assert.That(exception!.ParamName).IsEqualTo("writer");
    }

    [Test]
    public async Task Render_NullModel_IsAllowedForNullableModelType()
    {
        await Assert.That(new VariableTextTemplate(new TemplateOptions { NullVariable = "none" }).RenderToString(null)).IsEqualTo("none");
    }
}
