using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Models;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// TemplateOptions.Culture: which culture converts C# variables, where it applies, and what it leaves alone.
// de-DE is the test culture because its decimal separator is a comma, so a wrong culture shows up in the text.
// Every expectation is written by hand, and AM/PM cultures are avoided because newer ICU versions put U+202F before PM.
[ExcludeFromCodeCoverage]
public sealed class CultureTests
{
    private static readonly CultureInfo _de = CultureInfo.GetCultureInfo("de-DE");

    private static TemplateOptions GermanOptions => new() { Culture = _de };

    [Test]
    public async Task Render_Double_UsesOptionsCulture()
    {
        var options = GermanOptions;

        await TemplateOutput.AssertRendersAsync(new ConversionDoubleTextTemplate(options), new ConversionDoubleUtf8Template(options), 1.5, "1,5");
    }

    [Test]
    public async Task Render_Decimal_UsesOptionsCulture()
    {
        var options = GermanOptions;

        await TemplateOutput.AssertRendersAsync(new ConversionDecimalTextTemplate(options), new ConversionDecimalUtf8Template(options), 1234.5m, "1234,5");
    }

    [Test]
    public async Task Render_DateTime_UsesOptionsCulture()
    {
        var options = GermanOptions;

        await TemplateOutput.AssertRendersAsync(
            new ConversionDateTimeTextTemplate(options), new ConversionDateTimeUtf8Template(options), new DateTime(2026, 10, 7, 13, 5, 0), "07.10.2026 13:05:00");
    }

    [Test]
    public async Task Render_DateTimeOffset_UsesOptionsCulture()
    {
        var options = GermanOptions;
        var value = new DateTimeOffset(2026, 10, 13, 14, 30, 5, TimeSpan.FromHours(2));

        // The HTML encoder writes the + of the offset as &#x2B;
        await TemplateOutput.AssertRendersAsync(
            new ConversionDateTimeOffsetTextTemplate(options), new ConversionDateTimeOffsetUtf8Template(options), value, "13.10.2026 14:30:05 &#x2B;02:00");
    }

    [Test]
    public async Task Render_Dynamic_UsesOptionsCulture()
    {
        var options = GermanOptions;

        await TemplateOutput.AssertRendersAsync(new ConversionDynamicTextTemplate(options), new ConversionDynamicUtf8Template(options), 1.5, "1,5");
    }

    [Test]
    [Arguments(1.5, "1,5")]
    [Arguments(null, "-")]
    public async Task Render_NullableDouble_UsesCultureAndLeavesNullVariableAlone(double? value, string expected)
    {
        var options = new TemplateOptions { Culture = _de, NullVariable = "-" };

        await TemplateOutput.AssertRendersAsync(new CultureNullableDoubleTextTemplate(options), new CultureNullableDoubleUtf8Template(options), value, expected);
    }

    [Test]
    public async Task RenderAsync_GermanCulture_WritesTheSameBytesAsRender()
    {
        var template = new ConversionDecimalUtf8Template(GermanOptions);

        var rendered = TemplateOutput.RenderUtf8(template, 1234.5m);
        var piped = await TemplateOutput.RenderPipeAsync(template, 1234.5m);

        await Assert.That(Encoding.UTF8.GetString(piped)).IsEqualTo("1234,5");
        await Assert.That(Convert.ToHexString(piped)).IsEqualTo(Convert.ToHexString(rendered));
    }

    [Test]
    [Arguments(true, "1,5|1,5")]
    [Arguments(false, "1.5|1.5")]
    public async Task RenderToString_StringWriter_UsesOptionsCulture(bool german, string expected)
    {
        // The C# block writes straight to the writer, so only the StringWriter's culture can change its text
        var options = german ? GermanOptions : new TemplateOptions();

        await Assert.That(new CultureWriterTextTemplate(options).RenderToString(1.5)).IsEqualTo(expected);
    }

    [Test]
    public async Task Render_ChildTemplate_InheritsParentCulture()
    {
        var invoice = new InvoiceModel("7", [new InvoiceLineModel("Tea", 1234.5m)]);

        var german = TemplateOutput.RenderText(new CultureInvoiceTextTemplate(GermanOptions), invoice);
        var invariant = TemplateOutput.RenderText(CultureInvoiceTextTemplate.Default, invoice);

        await Assert.That(german).IsEqualTo("Invoice 7\nTea: 1234,5\n");
        await Assert.That(invariant).IsEqualTo("Invoice 7\nTea: 1234.5\n");
    }

    [Test]
    [NotInParallel]
    public async Task Render_CurrentCultureIsGerman_DefaultOptionsStayInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = _de;
        try
        {
            var french = new TemplateOptions { Culture = CultureInfo.GetCultureInfo("fr-FR") };
            var date = new DateTime(2026, 10, 7, 13, 5, 0);

            // The invariant, de-DE (07.10.2026), and fr-FR texts all differ, so the test can tell which culture ran
            await TemplateOutput.AssertRendersAsync(ConversionDoubleTextTemplate.Default, ConversionDoubleUtf8Template.Default, 1.5, "1.5");
            await TemplateOutput.AssertRendersAsync(ConversionDateTimeTextTemplate.Default, ConversionDateTimeUtf8Template.Default, date, "10/07/2026 13:05:00");
            await TemplateOutput.AssertRendersAsync(
                new ConversionDateTimeTextTemplate(french), new ConversionDateTimeUtf8Template(french), date, "07/10/2026 13:05:00");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    [Arguments(true, "<b>1,5</b>&amp;")]
    [Arguments(false, "<b>1,5</b>&")]
    public async Task Render_CultureAndEncoder_AreIndependent(bool encoded, string expected)
    {
        var options = encoded ? new TemplateOptions { Culture = _de } : new TemplateOptions { Encoder = null, Culture = _de };

        await Assert.That(TemplateOutput.RenderText(new CulturePlainTextTemplate(options), 1.5)).IsEqualTo(expected);
    }

    [Test]
    public async Task Culture_Null_ThrowsArgumentNull()
    {
        var exception = await Assert.That(() => new TemplateOptions { Culture = null! }).ThrowsExactly<ArgumentNullException>();

        await Assert.That(exception!.ParamName).IsEqualTo("Culture");
    }
}
