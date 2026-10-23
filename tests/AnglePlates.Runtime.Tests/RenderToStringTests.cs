using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// The RenderToString extension on text templates
[ExcludeFromCodeCoverage]
public sealed class RenderToStringTests
{
    [Test]
    public async Task RenderToString_DefaultTemplate_InfersModelAndEncodes()
    {
        // TModel is inferred from the conversion to TextTemplate<string?>, with no type argument at the call
        await Assert.That(VariableTextTemplate.Default.RenderToString("<x>")).IsEqualTo("&lt;x&gt;");
    }

    [Test]
    public async Task RenderToString_NonAsciiLiterals_MatchesStreamingRender()
    {
        var template = new LiteralsTextTemplate(new TemplateOptions { Encoder = null });

        await Assert.That(template.RenderToString("unused")).IsEqualTo(TemplateOutput.RenderText(template, "unused"));
    }

    [Test]
    public async Task RenderToString_NullTemplate_ThrowsArgumentNull()
    {
        var exception = await Assert.That(() => TemplateExtensions.RenderToString<string>(null!, "x")).ThrowsExactly<ArgumentNullException>();

        await Assert.That(exception!.ParamName).IsEqualTo("template");
    }
}
