using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates.Readme;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Every compiled template in this project, created without options and through Default. Both use the preset that the
// template's #encode directive picks, and each template's constructor fallback and Default counts toward emitted-code coverage.
[ExcludeFromCodeCoverage]
public sealed class TemplateDefaultsTests
{
    [Test]
    [MethodDataSource(nameof(Templates))]
    public async Task Template_WithoutOptions_UsesEncodePreset(
        string template, Func<object> createOptions, Func<object> defaultOptions, string preset)
    {
        // The support types are internal, so the data names the preset instead of holding it
        TemplateOptions expected = preset switch
        {
            "Html" => TemplateOptions.Html,
            "Unencoded" => TemplateOptions.Unencoded,
            "JavaScript" => TemplateOptions.JavaScript,
            "Url" => TemplateOptions.Url,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown preset."),
        };

        await Assert.That(createOptions()).IsSameReferenceAs(expected);
        await Assert.That(defaultOptions()).IsSameReferenceAs(expected);
    }

    public static IEnumerable<(string template, Func<object> createOptions, Func<object> defaultOptions, string preset)> Templates()
    {
        yield return ("OrderLineTextTemplate", () => new OrderLineTextTemplate().Options, () => OrderLineTextTemplate.Default.Options, "Html");
        yield return ("OrderTextTemplate", () => new OrderTextTemplate().Options, () => OrderTextTemplate.Default.Options, "Html");
        yield return ("ConversionBoolTextTemplate", () => new ConversionBoolTextTemplate().Options, () => ConversionBoolTextTemplate.Default.Options, "Html");
        yield return ("ConversionBoolUtf8Template", () => new ConversionBoolUtf8Template().Options, () => ConversionBoolUtf8Template.Default.Options, "Html");
        yield return ("ConversionByteArrayTextTemplate", () => new ConversionByteArrayTextTemplate().Options, () => ConversionByteArrayTextTemplate.Default.Options, "Html");
        yield return ("ConversionByteArrayUtf8Template", () => new ConversionByteArrayUtf8Template().Options, () => ConversionByteArrayUtf8Template.Default.Options, "Html");
        yield return ("ConversionCharTextTemplate", () => new ConversionCharTextTemplate().Options, () => ConversionCharTextTemplate.Default.Options, "Html");
        yield return ("ConversionCharUtf8Template", () => new ConversionCharUtf8Template().Options, () => ConversionCharUtf8Template.Default.Options, "Html");
        yield return ("ConversionCustomClassTextTemplate", () => new ConversionCustomClassTextTemplate().Options, () => ConversionCustomClassTextTemplate.Default.Options, "Html");
        yield return ("ConversionCustomClassUtf8Template", () => new ConversionCustomClassUtf8Template().Options, () => ConversionCustomClassUtf8Template.Default.Options, "Html");
        yield return ("ConversionDateOnlyTextTemplate", () => new ConversionDateOnlyTextTemplate().Options, () => ConversionDateOnlyTextTemplate.Default.Options, "Html");
        yield return ("ConversionDateOnlyUtf8Template", () => new ConversionDateOnlyUtf8Template().Options, () => ConversionDateOnlyUtf8Template.Default.Options, "Html");
        yield return ("ConversionDateTimeOffsetTextTemplate", () => new ConversionDateTimeOffsetTextTemplate().Options, () => ConversionDateTimeOffsetTextTemplate.Default.Options, "Html");
        yield return ("ConversionDateTimeOffsetUtf8Template", () => new ConversionDateTimeOffsetUtf8Template().Options, () => ConversionDateTimeOffsetUtf8Template.Default.Options, "Html");
        yield return ("ConversionDateTimeTextTemplate", () => new ConversionDateTimeTextTemplate().Options, () => ConversionDateTimeTextTemplate.Default.Options, "Html");
        yield return ("ConversionDateTimeUtf8Template", () => new ConversionDateTimeUtf8Template().Options, () => ConversionDateTimeUtf8Template.Default.Options, "Html");
        yield return ("ConversionDecimalTextTemplate", () => new ConversionDecimalTextTemplate().Options, () => ConversionDecimalTextTemplate.Default.Options, "Html");
        yield return ("ConversionDecimalUtf8Template", () => new ConversionDecimalUtf8Template().Options, () => ConversionDecimalUtf8Template.Default.Options, "Html");
        yield return ("ConversionDoubleTextTemplate", () => new ConversionDoubleTextTemplate().Options, () => ConversionDoubleTextTemplate.Default.Options, "Html");
        yield return ("ConversionDoubleUtf8Template", () => new ConversionDoubleUtf8Template().Options, () => ConversionDoubleUtf8Template.Default.Options, "Html");
        yield return ("ConversionDualFormatTextTemplate", () => new ConversionDualFormatTextTemplate().Options, () => ConversionDualFormatTextTemplate.Default.Options, "Html");
        yield return ("ConversionDualFormatUtf8Template", () => new ConversionDualFormatUtf8Template().Options, () => ConversionDualFormatUtf8Template.Default.Options, "Html");
        yield return ("ConversionDynamicAfterBlockUtf8Template", () => new ConversionDynamicAfterBlockUtf8Template().Options, () => ConversionDynamicAfterBlockUtf8Template.Default.Options, "Html");
        yield return ("ConversionDynamicTextTemplate", () => new ConversionDynamicTextTemplate().Options, () => ConversionDynamicTextTemplate.Default.Options, "Html");
        yield return ("ConversionDynamicUtf8Template", () => new ConversionDynamicUtf8Template().Options, () => ConversionDynamicUtf8Template.Default.Options, "Html");
        yield return ("ConversionEnumTextTemplate", () => new ConversionEnumTextTemplate().Options, () => ConversionEnumTextTemplate.Default.Options, "Html");
        yield return ("ConversionEnumUtf8Template", () => new ConversionEnumUtf8Template().Options, () => ConversionEnumUtf8Template.Default.Options, "Html");
        yield return ("ConversionFlagsEnumTextTemplate", () => new ConversionFlagsEnumTextTemplate().Options, () => ConversionFlagsEnumTextTemplate.Default.Options, "Html");
        yield return ("ConversionFlagsEnumUtf8Template", () => new ConversionFlagsEnumUtf8Template().Options, () => ConversionFlagsEnumUtf8Template.Default.Options, "Html");
        yield return ("ConversionFloatTextTemplate", () => new ConversionFloatTextTemplate().Options, () => ConversionFloatTextTemplate.Default.Options, "Html");
        yield return ("ConversionFloatUtf8Template", () => new ConversionFloatUtf8Template().Options, () => ConversionFloatUtf8Template.Default.Options, "Html");
        yield return ("ConversionFormattableTextTemplate", () => new ConversionFormattableTextTemplate().Options, () => ConversionFormattableTextTemplate.Default.Options, "Html");
        yield return ("ConversionFormattableUtf8Template", () => new ConversionFormattableUtf8Template().Options, () => ConversionFormattableUtf8Template.Default.Options, "Html");
        yield return ("ConversionGuidTextTemplate", () => new ConversionGuidTextTemplate().Options, () => ConversionGuidTextTemplate.Default.Options, "Html");
        yield return ("ConversionGuidUtf8Template", () => new ConversionGuidUtf8Template().Options, () => ConversionGuidUtf8Template.Default.Options, "Html");
        yield return ("ConversionImplicitStringTextTemplate", () => new ConversionImplicitStringTextTemplate().Options, () => ConversionImplicitStringTextTemplate.Default.Options, "Html");
        yield return ("ConversionImplicitStringUtf8Template", () => new ConversionImplicitStringUtf8Template().Options, () => ConversionImplicitStringUtf8Template.Default.Options, "Html");
        yield return ("ConversionIntTextTemplate", () => new ConversionIntTextTemplate().Options, () => ConversionIntTextTemplate.Default.Options, "Html");
        yield return ("ConversionIntUtf8Template", () => new ConversionIntUtf8Template().Options, () => ConversionIntUtf8Template.Default.Options, "Html");
        yield return ("ConversionLongTextTemplate", () => new ConversionLongTextTemplate().Options, () => ConversionLongTextTemplate.Default.Options, "Html");
        yield return ("ConversionLongUtf8Template", () => new ConversionLongUtf8Template().Options, () => ConversionLongUtf8Template.Default.Options, "Html");
        yield return ("ConversionNullLiteralTextTemplate", () => new ConversionNullLiteralTextTemplate().Options, () => ConversionNullLiteralTextTemplate.Default.Options, "Html");
        yield return ("ConversionNullLiteralUtf8Template", () => new ConversionNullLiteralUtf8Template().Options, () => ConversionNullLiteralUtf8Template.Default.Options, "Html");
        yield return ("ConversionNullToStringTextTemplate", () => new ConversionNullToStringTextTemplate().Options, () => ConversionNullToStringTextTemplate.Default.Options, "Html");
        yield return ("ConversionNullToStringUtf8Template", () => new ConversionNullToStringUtf8Template().Options, () => ConversionNullToStringUtf8Template.Default.Options, "Html");
        yield return ("ConversionNullableIntTextTemplate", () => new ConversionNullableIntTextTemplate().Options, () => ConversionNullableIntTextTemplate.Default.Options, "Html");
        yield return ("ConversionNullableIntUtf8Template", () => new ConversionNullableIntUtf8Template().Options, () => ConversionNullableIntUtf8Template.Default.Options, "Html");
        yield return ("ConversionTimeOnlyTextTemplate", () => new ConversionTimeOnlyTextTemplate().Options, () => ConversionTimeOnlyTextTemplate.Default.Options, "Html");
        yield return ("ConversionTimeOnlyUtf8Template", () => new ConversionTimeOnlyUtf8Template().Options, () => ConversionTimeOnlyUtf8Template.Default.Options, "Html");
        yield return ("ConversionTimeSpanTextTemplate", () => new ConversionTimeSpanTextTemplate().Options, () => ConversionTimeSpanTextTemplate.Default.Options, "Html");
        yield return ("ConversionTimeSpanUtf8Template", () => new ConversionTimeSpanUtf8Template().Options, () => ConversionTimeSpanUtf8Template.Default.Options, "Html");
        yield return ("EncodeHtmlTextTemplate", () => new EncodeHtmlTextTemplate().Options, () => EncodeHtmlTextTemplate.Default.Options, "Html");
        yield return ("EncodeHtmlUtf8Template", () => new EncodeHtmlUtf8Template().Options, () => EncodeHtmlUtf8Template.Default.Options, "Html");
        yield return ("EncodeJavaScriptTextTemplate", () => new EncodeJavaScriptTextTemplate().Options, () => EncodeJavaScriptTextTemplate.Default.Options, "JavaScript");
        yield return ("EncodeJavaScriptUtf8Template", () => new EncodeJavaScriptUtf8Template().Options, () => EncodeJavaScriptUtf8Template.Default.Options, "JavaScript");
        yield return ("EncodeNoneTextTemplate", () => new EncodeNoneTextTemplate().Options, () => EncodeNoneTextTemplate.Default.Options, "Unencoded");
        yield return ("EncodeNoneUtf8Template", () => new EncodeNoneUtf8Template().Options, () => EncodeNoneUtf8Template.Default.Options, "Unencoded");
        yield return ("EncodeUrlTextTemplate", () => new EncodeUrlTextTemplate().Options, () => EncodeUrlTextTemplate.Default.Options, "Url");
        yield return ("EncodeUrlUtf8Template", () => new EncodeUrlUtf8Template().Options, () => EncodeUrlUtf8Template.Default.Options, "Url");
        yield return ("ArrayModelTextTemplate", () => new ArrayModelTextTemplate().Options, () => ArrayModelTextTemplate.Default.Options, "Html");
        yield return ("ArrayModelUtf8Template", () => new ArrayModelUtf8Template().Options, () => ArrayModelUtf8Template.Default.Options, "Html");
        yield return ("NestedGenericModelTextTemplate", () => new NestedGenericModelTextTemplate().Options, () => NestedGenericModelTextTemplate.Default.Options, "Html");
        yield return ("NestedGenericModelUtf8Template", () => new NestedGenericModelUtf8Template().Options, () => NestedGenericModelUtf8Template.Default.Options, "Html");
        yield return ("TupleModelTextTemplate", () => new TupleModelTextTemplate().Options, () => TupleModelTextTemplate.Default.Options, "Html");
        yield return ("TupleModelUtf8Template", () => new TupleModelUtf8Template().Options, () => TupleModelUtf8Template.Default.Options, "Html");
        yield return ("ValueTypeModelTextTemplate", () => new ValueTypeModelTextTemplate().Options, () => ValueTypeModelTextTemplate.Default.Options, "Html");
        yield return ("ValueTypeModelUtf8Template", () => new ValueTypeModelUtf8Template().Options, () => ValueTypeModelUtf8Template.Default.Options, "Html");
        yield return ("HelpersTextTemplate", () => new HelpersTextTemplate().Options, () => HelpersTextTemplate.Default.Options, "Html");
        yield return ("HelpersUtf8Template", () => new HelpersUtf8Template().Options, () => HelpersUtf8Template.Default.Options, "Html");
        yield return ("LiteralsTextTemplate", () => new LiteralsTextTemplate().Options, () => LiteralsTextTemplate.Default.Options, "Html");
        yield return ("LiteralsUtf8Template", () => new LiteralsUtf8Template().Options, () => LiteralsUtf8Template.Default.Options, "Html");
        yield return ("ModelCasingTextTemplate", () => new ModelCasingTextTemplate().Options, () => ModelCasingTextTemplate.Default.Options, "Html");
        yield return ("ModelCasingUtf8Template", () => new ModelCasingUtf8Template().Options, () => ModelCasingUtf8Template.Default.Options, "Html");
        yield return ("VariableTextTemplate", () => new VariableTextTemplate().Options, () => VariableTextTemplate.Default.Options, "Html");
        yield return ("VariableUtf8Template", () => new VariableUtf8Template().Options, () => VariableUtf8Template.Default.Options, "Html");
        yield return ("AsyncBackpressureUtf8Template", () => new AsyncBackpressureUtf8Template().Options, () => AsyncBackpressureUtf8Template.Default.Options, "Html");
        yield return ("AsyncBeforeVariableUtf8Template", () => new AsyncBeforeVariableUtf8Template().Options, () => AsyncBeforeVariableUtf8Template.Default.Options, "Html");
        yield return ("AsyncBlockEndCrlfUtf8Template", () => new AsyncBlockEndCrlfUtf8Template().Options, () => AsyncBlockEndCrlfUtf8Template.Default.Options, "Html");
        yield return ("AsyncBlockEndLfUtf8Template", () => new AsyncBlockEndLfUtf8Template().Options, () => AsyncBlockEndLfUtf8Template.Default.Options, "Html");
        yield return ("AsyncBlockThenBlankLinesUtf8Template", () => new AsyncBlockThenBlankLinesUtf8Template().Options, () => AsyncBlockThenBlankLinesUtf8Template.Default.Options, "Html");
        yield return ("AsyncCancelLoopUtf8Template", () => new AsyncCancelLoopUtf8Template().Options, () => AsyncCancelLoopUtf8Template.Default.Options, "Html");
        yield return ("AsyncChildUtf8Template", () => new AsyncChildUtf8Template().Options, () => AsyncChildUtf8Template.Default.Options, "Html");
        yield return ("AsyncCommentAndEmptyBlockUtf8Template", () => new AsyncCommentAndEmptyBlockUtf8Template().Options, () => AsyncCommentAndEmptyBlockUtf8Template.Default.Options, "Html");
        yield return ("AsyncForeachIfElseUtf8Template", () => new AsyncForeachIfElseUtf8Template().Options, () => AsyncForeachIfElseUtf8Template.Default.Options, "Html");
        yield return ("AsyncForeachIfUtf8Template", () => new AsyncForeachIfUtf8Template().Options, () => AsyncForeachIfUtf8Template.Default.Options, "Html");
        yield return ("AsyncIfElseUtf8Template", () => new AsyncIfElseUtf8Template().Options, () => AsyncIfElseUtf8Template.Default.Options, "Html");
        yield return ("AsyncLocalUtf8Template", () => new AsyncLocalUtf8Template().Options, () => AsyncLocalUtf8Template.Default.Options, "Html");
        yield return ("AsyncLoopUtf8Template", () => new AsyncLoopUtf8Template().Options, () => AsyncLoopUtf8Template.Default.Options, "Html");
        yield return ("AsyncNestedLoopUtf8Template", () => new AsyncNestedLoopUtf8Template().Options, () => AsyncNestedLoopUtf8Template.Default.Options, "Html");
        yield return ("AsyncNoBlocksUtf8Template", () => new AsyncNoBlocksUtf8Template().Options, () => AsyncNoBlocksUtf8Template.Default.Options, "Html");
        yield return ("AsyncParentUtf8Template", () => new AsyncParentUtf8Template().Options, () => AsyncParentUtf8Template.Default.Options, "Html");
        yield return ("AsyncPlainUtf8Template", () => new AsyncPlainUtf8Template().Options, () => AsyncPlainUtf8Template.Default.Options, "Html");
        yield return ("AsyncSmallPageUtf8Template", () => new AsyncSmallPageUtf8Template().Options, () => AsyncSmallPageUtf8Template.Default.Options, "Html");
        yield return ("AsyncThresholdUtf8Template", () => new AsyncThresholdUtf8Template().Options, () => AsyncThresholdUtf8Template.Default.Options, "Html");
        yield return ("AsyncWriteOverloadsUtf8Template", () => new AsyncWriteOverloadsUtf8Template().Options, () => AsyncWriteOverloadsUtf8Template.Default.Options, "Html");
        yield return ("Readme.GreetingTextTemplate", () => new GreetingTextTemplate().Options, () => GreetingTextTemplate.Default.Options, "Html");
        yield return ("Readme.GreetingUtf8Template", () => new GreetingUtf8Template().Options, () => GreetingUtf8Template.Default.Options, "Html");
        yield return ("MyApp.OrderLineTextTemplate", () => new MyApp.Templates.OrderLineTextTemplate().Options, () => MyApp.Templates.OrderLineTextTemplate.Default.Options, "Html");
        yield return ("MyApp.OrderTextTemplate", () => new MyApp.Templates.OrderTextTemplate().Options, () => MyApp.Templates.OrderTextTemplate.Default.Options, "Html");
    }
}
