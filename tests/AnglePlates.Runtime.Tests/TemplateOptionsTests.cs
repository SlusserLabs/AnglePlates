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

// The TemplateOptions presets, the #encode directive that picks one, and options that can't change after creation
[ExcludeFromCodeCoverage]
public sealed class TemplateOptionsTests
{
    [Test]
    public async Task Presets_EachEncoder_HasExpectedEncoderAndNoNullVariable()
    {
        await Assert.That(TemplateOptions.Html.Encoder).IsSameReferenceAs(HtmlEncoder.Default);
        await Assert.That(TemplateOptions.Unencoded.Encoder).IsNull();
        await Assert.That(TemplateOptions.JavaScript.Encoder).IsSameReferenceAs(JavaScriptEncoder.Default);
        await Assert.That(TemplateOptions.Url.Encoder).IsSameReferenceAs(UrlEncoder.Default);
        foreach (var preset in new[] { TemplateOptions.Html, TemplateOptions.Unencoded, TemplateOptions.JavaScript, TemplateOptions.Url })
        {
            await Assert.That(preset.NullVariable).IsNull();
        }

        await Assert.That(TemplateOptions.Html).IsSameReferenceAs(TemplateOptions.Html);
        await Assert.That(new TemplateOptions().Encoder).IsSameReferenceAs(HtmlEncoder.Default);
        await Assert.That(new TemplateOptions().NullVariable).IsNull();
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
    public async Task Encode_Directive_FallsBackToMatchingPreset(string encode, string value, string expected)
    {
        var (text, utf8, preset) = CreateEncodeTemplates(encode, options: null);

        await Assert.That(text.Options).IsSameReferenceAs(preset);
        await Assert.That(utf8.Options).IsSameReferenceAs(preset);
        await TemplateOutput.AssertRendersAsync(text, utf8, value, expected);
    }

    [Test]
    [Arguments("none")]
    [Arguments("javascript")]
    [Arguments("url")]
    [Arguments("html")]
    public async Task Encode_Directive_DefaultSharesPreset(string encode)
    {
        TextTemplate<string?> text = encode switch
        {
            "none" => EncodeNoneTextTemplate.Default,
            "javascript" => EncodeJavaScriptTextTemplate.Default,
            "url" => EncodeUrlTextTemplate.Default,
            _ => EncodeHtmlTextTemplate.Default,
        };
        Utf8Template<string?> utf8 = encode switch
        {
            "none" => EncodeNoneUtf8Template.Default,
            "javascript" => EncodeJavaScriptUtf8Template.Default,
            "url" => EncodeUrlUtf8Template.Default,
            _ => EncodeHtmlUtf8Template.Default,
        };
        var (_, _, preset) = CreateEncodeTemplates(encode, options: null);

        await Assert.That(text.Options).IsSameReferenceAs(preset);
        await Assert.That(utf8.Options).IsSameReferenceAs(preset);
    }

    [Test]
    [Arguments("none")]
    [Arguments("javascript")]
    [Arguments("url")]
    [Arguments("html")]
    public async Task Encode_ExplicitOptions_OverridePreset(string encode)
    {
        var options = new TemplateOptions { Encoder = null, NullVariable = "-" };
        var (text, utf8, _) = CreateEncodeTemplates(encode, options);

        await Assert.That(text.Options).IsSameReferenceAs(options);
        await TemplateOutput.AssertRendersAsync(text, utf8, "<a>", "<a>");
        await TemplateOutput.AssertRendersAsync(text, utf8, null, "-");
    }

    [Test]
    public async Task Options_BuiltFromPresetEncoder_LeavesPresetUnchanged()
    {
        // A consumer derives options from a preset with an object initializer, and the preset doesn't change
        var custom = new TemplateOptions { Encoder = TemplateOptions.JavaScript.Encoder, NullVariable = "null" };
        var template = new EncodeNoneTextTemplate(custom);

        await Assert.That(TemplateOutput.RenderText(template, null)).IsEqualTo("null");
        await Assert.That(TemplateOutput.RenderText(template, "'")).IsEqualTo("\\u0027");
        await Assert.That(TemplateOptions.JavaScript.NullVariable).IsNull();
    }

    private static (TextTemplate<string?> Text, Utf8Template<string?> Utf8, TemplateOptions Preset) CreateEncodeTemplates(string encode, TemplateOptions? options)
    {
        return encode switch
        {
            "none" => (new EncodeNoneTextTemplate(options), new EncodeNoneUtf8Template(options), TemplateOptions.Unencoded),
            "javascript" => (new EncodeJavaScriptTextTemplate(options), new EncodeJavaScriptUtf8Template(options), TemplateOptions.JavaScript),
            "url" => (new EncodeUrlTextTemplate(options), new EncodeUrlUtf8Template(options), TemplateOptions.Url),
            "html" => (new EncodeHtmlTextTemplate(options), new EncodeHtmlUtf8Template(options), TemplateOptions.Html),
            _ => throw new ArgumentOutOfRangeException(nameof(encode), encode, "Unknown encoding."),
        };
    }
}
