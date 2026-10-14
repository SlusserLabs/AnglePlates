using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Models;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
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
        yield return (true, true, "True");
        yield return (false, false, "False");
        yield return (true, new CustomValue("<custom>"), "&lt;custom&gt;");
        yield return (false, new byte[] { 1, 2, 3 }, "System.Byte[]");
        yield return (true, new NullToString(), "");

        // A boxed int? is a boxed int, so the runtime binder picks Write<T> with T as int, never the T? overload
        yield return (true, (int?)7, "7");
    }

    // The tests below pin the 1.0 overload set, where it differs from the captured conversion or decides which conversion runs

    [Test]
    [Arguments(true, null, "|")]
    [Arguments(true, "<null>", "&lt;null&gt;|&lt;null&gt;")]
    [Arguments(false, "<null>", "<null>|<null>")]
    public async Task Render_NullAndDefaultLiterals_WriteNullVariable(bool encoded, string? nullVariable, string expected)
    {
        // [[ null ]] and [[ default ]] bind to Write(string?), so they compile and write NullVariable
        await AssertConvertsAsync(
            o => new ConversionNullLiteralTextTemplate(o), o => new ConversionNullLiteralUtf8Template(o), encoded, "unused", expected, nullVariable);
    }

    [Test]
    [Arguments(true, "<a>")]
    [Arguments(false, "<a>")]
    [Arguments(true, null)]
    public async Task Render_ImplicitStringConversion_UsesToStringLikePre10(bool encoded, string? text)
    {
        // Write<T> is an exact match, so it beats the user-defined conversion to Write(string?), as Convert.ToString did before 1.0
        await AssertConvertsAsync(
            o => new ConversionImplicitStringTextTemplate(o), o => new ConversionImplicitStringUtf8Template(o), encoded, new ImplicitText(text), "ToString", "<null>");
    }

    [Test]
    public async Task Render_FormattableAndConvertibleValue_PrefersConvertibleLikeConvert()
    {
        // The value as itself, as a nullable through the T? overload, as object, and as dynamic all reach Write<T>, which checks
        // IConvertible before IFormattable the way Convert.ToString does
        await AssertConvertsAsync(
            o => new ConversionDualFormatTextTemplate(o), o => new ConversionDualFormatUtf8Template(o), true, new DualFormat(),
            "convertible|convertible|convertible|convertible");
    }

    [Test]
    [MethodDataSource(nameof(DynamicAfterBlockCases))]
    public async Task RenderAsync_DynamicAfterBlock_BindsFlushingWriteAtRunTime(object value, string expected)
    {
        // The first write after a C# block is an await WriteAsync call, which the runtime binder resolves against the
        // private protected overloads from inside the template
        var template = new ConversionDynamicAfterBlockUtf8Template();
        var expectedHex = Convert.ToHexString(Encoding.UTF8.GetBytes(expected));

        await Assert.That(Convert.ToHexString(TemplateOutput.RenderUtf8(template, value))).IsEqualTo(expectedHex);
        await Assert.That(Convert.ToHexString(await TemplateOutput.RenderPipeAsync(template, value))).IsEqualTo(expectedHex);
    }

    public static IEnumerable<(object value, string expected)> DynamicAfterBlockCases()
    {
        yield return (42, "42");
        yield return ("<b>", "&lt;b&gt;");
        yield return (new DualFormat(), "convertible");
        yield return (true, "True");
        yield return (new CustomValue("<custom>"), "&lt;custom&gt;");
        yield return (new byte[] { 1, 2, 3 }, "System.Byte[]");
        yield return ((int?)7, "7");
    }

    [Test]
    [Arguments(42, "42")]
    [Arguments("<b>", "&lt;b&gt;")]
    [Arguments(true, "True")]
    [Arguments(-1.5E+20, "-1.5E&#x2B;20")]
    public async Task Render_DynamicModelMember_ConvertsAtRunTime(object member, string expected)
    {
        // Every member of a #model dynamic template is dynamic too, so the runtime binder picks the overload
        object model = DynamicModel(member);

        await TemplateOutput.AssertRendersAsync<object>(new ConversionDynamicModelTextTemplate(), new ConversionDynamicModelUtf8Template(), model, expected);
    }

    [Test]
    [Arguments(true, "&lt;custom&gt;")]
    [Arguments(false, "System.Byte[]")]
    public async Task Render_DynamicModelReferenceMember_ConvertsAtRunTime(bool custom, string expected)
    {
        object model = DynamicModel(custom ? new CustomValue("<custom>") : new byte[] { 1 });

        // The shared instances use the same options as new ones
        await TemplateOutput.AssertRendersAsync<object>(ConversionDynamicModelTextTemplate.Default, ConversionDynamicModelUtf8Template.Default, model, expected);
    }

    private static ExpandoObject DynamicModel(object member)
    {
        var model = new ExpandoObject();
        ((IDictionary<string, object?>)model)["Value"] = member;

        return model;
    }

    private static async Task AssertConvertsAsync<TModel>(
        Func<TemplateOptions, TextTemplate<TModel>> createText, Func<TemplateOptions, Utf8Template<TModel>> createUtf8, bool encoded, TModel value, string expected,
        string? nullVariable = null)
    {
        var options = TestOptions.Create(encoded, nullVariable);

        await TemplateOutput.AssertRendersAsync(createText(options), createUtf8(options), value, expected);
    }
}
