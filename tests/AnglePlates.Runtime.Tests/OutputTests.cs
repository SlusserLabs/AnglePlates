using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Literals, string variables, the Write and WriteRaw helpers, and template options
[ExcludeFromCodeCoverage]
public sealed class OutputTests
{
    [Test]
    [Arguments(true, "a\uD800b", "a&#xFFFD;b", "61262378464646443B62")]
    [Arguments(true, "a\uDC00", "a&#xFFFD;", "61262378464646443B")]
    [Arguments(false, "a\uD800b", "a\uD800b", "61EFBFBD62")]
    [Arguments(false, "a\uDC00", "a\uDC00", "61EFBFBD")]
    public async Task Render_LoneSurrogate_ReplacesOrPassesThrough(bool encoded, string value, string expectedText, string expectedUtf8Hex)
    {
        // Captured from the pre-1.0 code: the HTML encoder writes U+FFFD, the unencoded text target passes the surrogate through,
        // and the unencoded UTF-8 target writes the UTF-8 replacement character
        var text = new VariableTextTemplate(TestOptions.Create(encoded));
        var utf8 = new VariableUtf8Template(TestOptions.Create(encoded));

        await Assert.That(TemplateOutput.RenderText(text, value)).IsEqualTo(expectedText);
        await Assert.That(Convert.ToHexString(TemplateOutput.RenderUtf8(utf8, value))).IsEqualTo(expectedUtf8Hex);
        await Assert.That(Convert.ToHexString(await TemplateOutput.RenderPipeAsync(utf8, value))).IsEqualTo(expectedUtf8Hex);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_NonAsciiLiterals_WritesExactCharacters(bool encoded)
    {
        // The template holds an e-acute, an emoji, U+2028, a tab, and U+0001; literals are never encoded
        var options = TestOptions.Create(encoded);

        await TemplateOutput.AssertRendersAsync(new LiteralsTextTemplate(options), new LiteralsUtf8Template(options), "unused", "<p>caf\u00E9 \U0001F600 \u2028\t\u0001</p>");
    }

    [Test]
    [Arguments(true, "<b>|&lt;b&gt;")]
    [Arguments(false, "<b>|<b>")]
    public async Task Render_WriteRawAndWriteInBlocks_EncodeOnlyWrite(bool encoded, string expected)
    {
        var options = TestOptions.Create(encoded);

        await TemplateOutput.AssertRendersAsync(new HelpersTextTemplate(options), new HelpersUtf8Template(options), "<b>", expected);
    }

    [Test]
    [Arguments(true, "&lt;b&gt;|&lt;b&gt;")]
    [Arguments(false, "<b>|<b>")]
    public async Task Render_UpperCaseModel_MatchesLowerCaseModel(bool encoded, string expected)
    {
        var options = TestOptions.Create(encoded);

        await TemplateOutput.AssertRendersAsync(new ModelCasingTextTemplate(options), new ModelCasingUtf8Template(options), "<b>", expected);
    }

    [Test]
    [Arguments(true, "<raw>", null, "&lt;raw&gt;")]
    [Arguments(false, "<raw>", null, "<raw>")]
    [Arguments(true, null, null, "")]
    [Arguments(false, null, null, "")]
    [Arguments(true, "", null, "")]
    [Arguments(false, "", null, "")]
    [Arguments(true, null, "<null>", "&lt;null&gt;")]
    [Arguments(false, null, "<null>", "<null>")]
    [Arguments(true, "", "<null>", "")]
    public async Task Render_StringVariable_WritesValueOrNullVariable(bool encoded, string? value, string? nullVariable, string expected)
    {
        var options = TestOptions.Create(encoded, nullVariable);

        await TemplateOutput.AssertRendersAsync(new VariableTextTemplate(options), new VariableUtf8Template(options), value, expected);
    }

    [Test]
    [MethodDataSource(nameof(LongValueCases))]
    public async Task Render_LongEncodedValue_EncodesAcrossBufferChunks(string value)
    {
        var options = TestOptions.Create(encoded: true);

        await TemplateOutput.AssertRendersAsync(new VariableTextTemplate(options), new VariableUtf8Template(options), value, HtmlEncoder.Default.Encode(value));
    }

    public static IEnumerable<string> LongValueCases()
    {
        yield return "<\U0001F600\u00E9\"&";
        yield return new string('<', 100000) + "\U0001F600\u00E9\"&";

        // A surrogate pair just past the first 32K characters must survive as one scalar, wherever the encoder splits its work
        yield return new string('a', 32767) + "\U0001F600" + new string('<', 20000);
    }

    [Test]
    public async Task Constructor_WithOptions_KeepsSameInstance()
    {
        var options = TestOptions.Create(encoded: false);

        await Assert.That(new VariableTextTemplate(options).Options).IsSameReferenceAs(options);
        await Assert.That(new VariableUtf8Template(options).Options).IsSameReferenceAs(options);
    }

    [Test]
    public async Task Constructor_WithoutOptions_UsesSharedHtmlPreset()
    {
        // A template without #encode falls back to the Html preset instead of allocating options per instance
        await Assert.That(new VariableTextTemplate().Options).IsSameReferenceAs(TemplateOptions.Html);
        await Assert.That(new VariableUtf8Template(null).Options).IsSameReferenceAs(TemplateOptions.Html);
        await Assert.That(VariableTextTemplate.Default.Options).IsSameReferenceAs(TemplateOptions.Html);
        await Assert.That(VariableUtf8Template.Default.Options).IsSameReferenceAs(TemplateOptions.Html);
    }
}
