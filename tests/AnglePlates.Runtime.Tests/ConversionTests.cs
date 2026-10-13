using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Models;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// The output-parity matrix: how a C# variable of each type becomes text on both targets and in RenderAsync.
// Every expected value was captured from the pre-1.0 conversion, Convert.ToString with the invariant culture,
// so a new conversion must reproduce these strings exactly.
[ExcludeFromCodeCoverage]
public sealed class ConversionTests
{
    [Test]
    [Arguments(true, 0, "0")]
    [Arguments(true, -42, "-42")]
    [Arguments(false, int.MaxValue, "2147483647")]
    public async Task Render_Int_MatchesCapturedOutput(bool encoded, int value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionIntTextTemplate(o), o => new ConversionIntUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [Arguments(true, long.MinValue, "-9223372036854775808")]
    [Arguments(false, 1234567890123L, "1234567890123")]
    public async Task Render_Long_MatchesCapturedOutput(bool encoded, long value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionLongTextTemplate(o), o => new ConversionLongUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [Arguments(true, -1.5E+20, "-1.5E&#x2B;20")]
    [Arguments(false, -1.5E+20, "-1.5E+20")]
    [Arguments(true, 1.2345678901234568E+17, "1.2345678901234568E&#x2B;17")]
    [Arguments(true, 0.1, "0.1")]
    [Arguments(true, 1.0 / 3, "0.3333333333333333")]
    [Arguments(true, -0.0, "-0")]
    [Arguments(true, 1E-07, "1E-07")]
    [Arguments(true, double.NaN, "NaN")]
    [Arguments(true, double.PositiveInfinity, "Infinity")]
    [Arguments(false, double.NegativeInfinity, "-Infinity")]
    public async Task Render_Double_MatchesCapturedOutput(bool encoded, double value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionDoubleTextTemplate(o), o => new ConversionDoubleUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [MethodDataSource(nameof(DecimalCases))]
    public async Task Render_Decimal_MatchesCapturedOutput(bool encoded, decimal value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionDecimalTextTemplate(o), o => new ConversionDecimalUtf8Template(o), encoded, value, expected);
    }

    public static IEnumerable<(bool encoded, decimal value, string expected)> DecimalCases()
    {
        yield return (true, 1234.5678m, "1234.5678");
        yield return (true, -0.001m, "-0.001");
        yield return (false, 1.10m, "1.10");
        yield return (false, decimal.MaxValue, "79228162514264337593543950335");
    }

    [Test]
    [Arguments(true, 3.14f, "3.14")]
    [Arguments(true, -1.5E+20f, "-1.5E&#x2B;20")]
    [Arguments(false, -1.5E+20f, "-1.5E+20")]
    [Arguments(true, float.NaN, "NaN")]
    [Arguments(false, float.PositiveInfinity, "Infinity")]
    public async Task Render_Float_MatchesCapturedOutput(bool encoded, float value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionFloatTextTemplate(o), o => new ConversionFloatUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [MethodDataSource(nameof(DateTimeCases))]
    public async Task Render_DateTime_MatchesCapturedOutput(bool encoded, DateTime value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionDateTimeTextTemplate(o), o => new ConversionDateTimeUtf8Template(o), encoded, value, expected);
    }

    public static IEnumerable<(bool encoded, DateTime value, string expected)> DateTimeCases()
    {
        yield return (true, new DateTime(2026, 10, 13, 14, 30, 5, 123, DateTimeKind.Utc), "10/13/2026 14:30:05");
        yield return (false, DateTime.MinValue, "01/01/0001 00:00:00");
    }

    [Test]
    [MethodDataSource(nameof(DateTimeOffsetCases))]
    public async Task Render_DateTimeOffset_MatchesCapturedOutput(bool encoded, DateTimeOffset value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionDateTimeOffsetTextTemplate(o), o => new ConversionDateTimeOffsetUtf8Template(o), encoded, value, expected);
    }

    public static IEnumerable<(bool encoded, DateTimeOffset value, string expected)> DateTimeOffsetCases()
    {
        yield return (true, new DateTimeOffset(2026, 10, 13, 14, 30, 5, TimeSpan.FromHours(2)), "10/13/2026 14:30:05 &#x2B;02:00");
        yield return (false, new DateTimeOffset(2026, 10, 13, 14, 30, 5, TimeSpan.FromHours(2)), "10/13/2026 14:30:05 +02:00");
        yield return (true, new DateTimeOffset(2026, 10, 13, 14, 30, 5, TimeSpan.FromHours(-5)), "10/13/2026 14:30:05 -05:00");
    }

    [Test]
    [MethodDataSource(nameof(TimeSpanCases))]
    public async Task Render_TimeSpan_MatchesCapturedOutput(bool encoded, TimeSpan value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionTimeSpanTextTemplate(o), o => new ConversionTimeSpanUtf8Template(o), encoded, value, expected);
    }

    public static IEnumerable<(bool encoded, TimeSpan value, string expected)> TimeSpanCases()
    {
        yield return (true, new TimeSpan(1, 2, 3, 4, 500), "1.02:03:04.5000000");
        yield return (true, TimeSpan.FromMinutes(-90), "-01:30:00");
        yield return (false, TimeSpan.Zero, "00:00:00");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_Guid_MatchesCapturedOutput(bool encoded)
    {
        var value = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

        await AssertConvertsAsync(o => new ConversionGuidTextTemplate(o), o => new ConversionGuidUtf8Template(o), encoded, value, "0f8fad5b-d9cb-469f-a165-70867728950e");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Render_DateOnly_MatchesCapturedOutput(bool encoded)
    {
        await AssertConvertsAsync(o => new ConversionDateOnlyTextTemplate(o), o => new ConversionDateOnlyUtf8Template(o), encoded, new DateOnly(2026, 10, 13), "10/13/2026");
    }

    [Test]
    [MethodDataSource(nameof(TimeOnlyCases))]
    public async Task Render_TimeOnly_MatchesCapturedOutput(bool encoded, TimeOnly value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionTimeOnlyTextTemplate(o), o => new ConversionTimeOnlyUtf8Template(o), encoded, value, expected);
    }

    public static IEnumerable<(bool encoded, TimeOnly value, string expected)> TimeOnlyCases()
    {
        yield return (true, new TimeOnly(14, 30, 5, 250), "14:30");
        yield return (false, TimeOnly.MinValue, "00:00");
    }

    [Test]
    [Arguments(true, true, "True")]
    [Arguments(false, false, "False")]
    public async Task Render_Bool_MatchesCapturedOutput(bool encoded, bool value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionBoolTextTemplate(o), o => new ConversionBoolUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [Arguments(true, 'a', "a")]
    [Arguments(true, '<', "&lt;")]
    [Arguments(false, '<', "<")]
    [Arguments(true, '\'', "&#x27;")]
    [Arguments(true, '\u00E9', "&#xE9;")]
    [Arguments(false, '\u00E9', "\u00E9")]
    public async Task Render_Char_MatchesCapturedOutput(bool encoded, char value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionCharTextTemplate(o), o => new ConversionCharUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [Arguments(true, Color.Blue, "Blue")]
    [Arguments(false, (Color)42, "42")]
    public async Task Render_Enum_MatchesCapturedOutput(bool encoded, Color value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionEnumTextTemplate(o), o => new ConversionEnumUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [Arguments(true, Permissions.Read | Permissions.Write, "Read, Write")]
    [Arguments(false, Permissions.Read | Permissions.Write, "Read, Write")]
    [Arguments(true, Permissions.None, "None")]
    [Arguments(true, (Permissions)8, "8")]
    [Arguments(true, (Permissions)9, "9")]
    public async Task Render_FlagsEnum_MatchesCapturedOutput(bool encoded, Permissions value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionFlagsEnumTextTemplate(o), o => new ConversionFlagsEnumUtf8Template(o), encoded, value, expected);
    }

    [Test]
    [Arguments(true, 5, null, "5")]
    [Arguments(false, 5, null, "5")]
    [Arguments(true, null, null, "")]
    [Arguments(true, null, "<null>", "&lt;null&gt;")]
    [Arguments(false, null, "<null>", "<null>")]
    public async Task Render_NullableInt_MatchesCapturedOutput(bool encoded, int? value, string? nullVariable, string expected)
    {
        await AssertConvertsAsync(o => new ConversionNullableIntTextTemplate(o), o => new ConversionNullableIntUtf8Template(o), encoded, value, expected, nullVariable);
    }

    [Test]
    [Arguments(true, "<custom & 'value'>", "&lt;custom &amp; &#x27;value&#x27;&gt;")]
    [Arguments(false, "<custom & 'value'>", "<custom & 'value'>")]
    [Arguments(true, null, "&lt;null&gt;")]
    [Arguments(false, null, "<null>")]
    public async Task Render_CustomClass_MatchesCapturedOutput(bool encoded, string? text, string expected)
    {
        var value = text is null ? null : new CustomValue(text);

        await AssertConvertsAsync(o => new ConversionCustomClassTextTemplate(o), o => new ConversionCustomClassUtf8Template(o), encoded, value, expected, "<null>");
    }

    [Test]
    [Arguments(true, true, "fmt=null;inv=True")]
    [Arguments(false, true, "fmt=null;inv=True")]
    [Arguments(true, false, "&lt;null&gt;")]
    [Arguments(false, false, "<null>")]
    public async Task Render_FormattableClass_MatchesCapturedOutput(bool encoded, bool hasValue, string expected)
    {
        var value = hasValue ? new FormatProbe() : null;

        await AssertConvertsAsync(o => new ConversionFormattableTextTemplate(o), o => new ConversionFormattableUtf8Template(o), encoded, value, expected, "<null>");
    }

    [Test]
    [Arguments(true, true, "System.Byte[]")]
    [Arguments(false, true, "System.Byte[]")]
    [Arguments(true, false, "&lt;null&gt;")]
    [Arguments(false, false, "<null>")]
    public async Task Render_ByteArray_MatchesCapturedOutput(bool encoded, bool hasValue, string expected)
    {
        var value = hasValue ? new byte[] { 1, 2, 3 } : null;

        await AssertConvertsAsync(o => new ConversionByteArrayTextTemplate(o), o => new ConversionByteArrayUtf8Template(o), encoded, value, expected, "<null>");
    }

    [Test]
    [Arguments(true, null, "")]
    [Arguments(false, null, "")]
    [Arguments(true, "<null>", "&lt;null&gt;")]
    [Arguments(false, "<null>", "<null>")]
    public async Task Render_ToStringReturnsNull_WritesNullVariable(bool encoded, string? nullVariable, string expected)
    {
        await AssertConvertsAsync(
            o => new ConversionNullToStringTextTemplate(o), o => new ConversionNullToStringUtf8Template(o), encoded, new NullToString(), expected, nullVariable);
    }

    [Test]
    [MethodDataSource(nameof(DynamicCases))]
    public async Task Render_Dynamic_MatchesCapturedOutput(bool encoded, object value, string expected)
    {
        await AssertConvertsAsync(o => new ConversionDynamicTextTemplate(o), o => new ConversionDynamicUtf8Template(o), encoded, value, expected);
    }

    public static IEnumerable<(bool encoded, object value, string expected)> DynamicCases()
    {
        yield return (true, 42, "42");
        yield return (false, 42, "42");
        yield return (true, "<b>", "&lt;b&gt;");
        yield return (false, "<b>", "<b>");
        yield return (true, -1.5E+20, "-1.5E&#x2B;20");
    }

    private static async Task AssertConvertsAsync<TModel>(
        Func<TemplateOptions, TextTemplate<TModel>> createText, Func<TemplateOptions, Utf8Template<TModel>> createUtf8, bool encoded, TModel value, string expected,
        string? nullVariable = null)
    {
        var options = TestOptions.Create(encoded, nullVariable);

        await TemplateOutput.AssertRendersAsync(createText(options), createUtf8(options), value, expected);
    }
}
