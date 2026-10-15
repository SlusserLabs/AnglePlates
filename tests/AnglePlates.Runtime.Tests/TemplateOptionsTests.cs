using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// The default values of TemplateOptions, the encoders it can be given, and options that can't change after creation
[ExcludeFromCodeCoverage]
public sealed class TemplateOptionsTests
{
    [Test]
    public async Task Options_New_HasDefaultValues()
    {
        var options = new TemplateOptions();

        await Assert.That(options.Encoder).IsSameReferenceAs(HtmlEncoder.Default);
        await Assert.That(options.NullVariable).IsNull();
    }

    [Test]
    public async Task Options_Properties_AreInitOnlyAndClassIsSealed()
    {
        // The compiler rejects assignments after creation (AnglePlates.Tests pins CS8852), so only the metadata is visible here
        var properties = typeof(TemplateOptions).GetProperties().Where(property => property.SetMethod is not null).ToArray();

        await Assert.That(typeof(TemplateOptions).IsSealed).IsTrue();
        await Assert.That(properties.Select(property => property.Name).Order()).IsEquivalentTo(new[] { "Encoder", "NullVariable" });
        foreach (var property in properties)
        {
            await Assert.That(property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit))).IsTrue();
        }
    }

    [Test]
    [Arguments("none", "<a b>&", "<a b>&")]
    [Arguments("javascript", "<a b>&", "\\u003Ca b\\u003E\\u0026")]
    [Arguments("url", "<a b>&", "%3Ca%20b%3E%26")]
    [Arguments("html", "<a b>&", "&lt;a b&gt;&amp;")]
    public async Task Encoder_FromOptions_EncodesVariables(string encoder, string value, string expected)
    {
        var options = new TemplateOptions { Encoder = CreateEncoder(encoder) };

        await TemplateOutput.AssertRendersAsync(new VariableTextTemplate(options), new VariableUtf8Template(options), value, expected);
    }

    [Test]
    [Arguments("none")]
    [Arguments("javascript")]
    [Arguments("url")]
    [Arguments("html")]
    public async Task Options_WithEncoderAndNullVariable_KeepsBoth(string encoder)
    {
        var options = new TemplateOptions { Encoder = CreateEncoder(encoder), NullVariable = "-" };
        var text = new VariableTextTemplate(options);
        var utf8 = new VariableUtf8Template(options);

        await Assert.That(text.Options).IsSameReferenceAs(options);
        await TemplateOutput.AssertRendersAsync(text, utf8, null, "-");
    }

    private static TextEncoder? CreateEncoder(string encoder)
    {
        return encoder switch
        {
            "none" => null,
            "javascript" => JavaScriptEncoder.Default,
            "url" => UrlEncoder.Default,
            "html" => HtmlEncoder.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(encoder), encoder, "Unknown encoder."),
        };
    }
}
