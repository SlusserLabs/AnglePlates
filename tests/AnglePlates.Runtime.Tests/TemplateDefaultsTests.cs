using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates.Readme;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Every compiled template in this project, created without options and through Default. Both get default-valued
// options, and each template's constructor fallback and Default count toward emitted-code coverage.
[ExcludeFromCodeCoverage]
public sealed class TemplateDefaultsTests
{
    [Test]
    [MethodDataSource(nameof(Templates))]
    public async Task Template_WithoutOptions_UsesDefaultOptions(string template, Func<object> createOptions, Func<object> defaultOptions)
    {
        // The support types are internal, so the data returns objects and the test casts them
        var created = (TemplateOptions)createOptions();
        var shared = (TemplateOptions)defaultOptions();

        await Assert.That(created.Encoder).IsSameReferenceAs(HtmlEncoder.Default);
        await Assert.That(created.NullVariable).IsNull();
        await Assert.That(shared.Encoder).IsSameReferenceAs(HtmlEncoder.Default);
        await Assert.That(shared.NullVariable).IsNull();

        // Default is one shared instance, so its options are the same object on every read
        await Assert.That(defaultOptions()).IsSameReferenceAs(shared);
    }

    public static IEnumerable<(string template, Func<object> createOptions, Func<object> defaultOptions)> Templates()
    {
        yield return ("OrderLineTextTemplate", () => new OrderLineTextTemplate().Options, () => OrderLineTextTemplate.Default.Options);
        yield return ("OrderTextTemplate", () => new OrderTextTemplate().Options, () => OrderTextTemplate.Default.Options);
        yield return ("ConversionBoolTextTemplate", () => new ConversionBoolTextTemplate().Options, () => ConversionBoolTextTemplate.Default.Options);
        yield return ("ConversionBoolUtf8Template", () => new ConversionBoolUtf8Template().Options, () => ConversionBoolUtf8Template.Default.Options);
        yield return ("ConversionByteArrayTextTemplate", () => new ConversionByteArrayTextTemplate().Options, () => ConversionByteArrayTextTemplate.Default.Options);
        yield return ("ConversionByteArrayUtf8Template", () => new ConversionByteArrayUtf8Template().Options, () => ConversionByteArrayUtf8Template.Default.Options);
        yield return ("ConversionCharTextTemplate", () => new ConversionCharTextTemplate().Options, () => ConversionCharTextTemplate.Default.Options);
        yield return ("ConversionCharUtf8Template", () => new ConversionCharUtf8Template().Options, () => ConversionCharUtf8Template.Default.Options);
        yield return ("ConversionCustomClassTextTemplate", () => new ConversionCustomClassTextTemplate().Options, () => ConversionCustomClassTextTemplate.Default.Options);
        yield return ("ConversionCustomClassUtf8Template", () => new ConversionCustomClassUtf8Template().Options, () => ConversionCustomClassUtf8Template.Default.Options);
        yield return ("ConversionDateOnlyTextTemplate", () => new ConversionDateOnlyTextTemplate().Options, () => ConversionDateOnlyTextTemplate.Default.Options);
        yield return ("ConversionDateOnlyUtf8Template", () => new ConversionDateOnlyUtf8Template().Options, () => ConversionDateOnlyUtf8Template.Default.Options);
        yield return ("ConversionDateTimeOffsetTextTemplate", () => new ConversionDateTimeOffsetTextTemplate().Options, () => ConversionDateTimeOffsetTextTemplate.Default.Options);
        yield return ("ConversionDateTimeOffsetUtf8Template", () => new ConversionDateTimeOffsetUtf8Template().Options, () => ConversionDateTimeOffsetUtf8Template.Default.Options);
        yield return ("ConversionDateTimeTextTemplate", () => new ConversionDateTimeTextTemplate().Options, () => ConversionDateTimeTextTemplate.Default.Options);
        yield return ("ConversionDateTimeUtf8Template", () => new ConversionDateTimeUtf8Template().Options, () => ConversionDateTimeUtf8Template.Default.Options);
        yield return ("ConversionDecimalTextTemplate", () => new ConversionDecimalTextTemplate().Options, () => ConversionDecimalTextTemplate.Default.Options);
        yield return ("ConversionDecimalUtf8Template", () => new ConversionDecimalUtf8Template().Options, () => ConversionDecimalUtf8Template.Default.Options);
        yield return ("ConversionDoubleTextTemplate", () => new ConversionDoubleTextTemplate().Options, () => ConversionDoubleTextTemplate.Default.Options);
        yield return ("ConversionDoubleUtf8Template", () => new ConversionDoubleUtf8Template().Options, () => ConversionDoubleUtf8Template.Default.Options);
        yield return ("ConversionDualFormatTextTemplate", () => new ConversionDualFormatTextTemplate().Options, () => ConversionDualFormatTextTemplate.Default.Options);
        yield return ("ConversionDualFormatUtf8Template", () => new ConversionDualFormatUtf8Template().Options, () => ConversionDualFormatUtf8Template.Default.Options);
        yield return ("ConversionDynamicAfterBlockUtf8Template", () => new ConversionDynamicAfterBlockUtf8Template().Options, () => ConversionDynamicAfterBlockUtf8Template.Default.Options);
        yield return ("ConversionDynamicTextTemplate", () => new ConversionDynamicTextTemplate().Options, () => ConversionDynamicTextTemplate.Default.Options);
        yield return ("ConversionDynamicUtf8Template", () => new ConversionDynamicUtf8Template().Options, () => ConversionDynamicUtf8Template.Default.Options);
        yield return ("ConversionEnumTextTemplate", () => new ConversionEnumTextTemplate().Options, () => ConversionEnumTextTemplate.Default.Options);
        yield return ("ConversionEnumUtf8Template", () => new ConversionEnumUtf8Template().Options, () => ConversionEnumUtf8Template.Default.Options);
        yield return ("ConversionFlagsEnumTextTemplate", () => new ConversionFlagsEnumTextTemplate().Options, () => ConversionFlagsEnumTextTemplate.Default.Options);
        yield return ("ConversionFlagsEnumUtf8Template", () => new ConversionFlagsEnumUtf8Template().Options, () => ConversionFlagsEnumUtf8Template.Default.Options);
        yield return ("ConversionFloatTextTemplate", () => new ConversionFloatTextTemplate().Options, () => ConversionFloatTextTemplate.Default.Options);
        yield return ("ConversionFloatUtf8Template", () => new ConversionFloatUtf8Template().Options, () => ConversionFloatUtf8Template.Default.Options);
        yield return ("ConversionFormattableTextTemplate", () => new ConversionFormattableTextTemplate().Options, () => ConversionFormattableTextTemplate.Default.Options);
        yield return ("ConversionFormattableUtf8Template", () => new ConversionFormattableUtf8Template().Options, () => ConversionFormattableUtf8Template.Default.Options);
        yield return ("ConversionGuidTextTemplate", () => new ConversionGuidTextTemplate().Options, () => ConversionGuidTextTemplate.Default.Options);
        yield return ("ConversionGuidUtf8Template", () => new ConversionGuidUtf8Template().Options, () => ConversionGuidUtf8Template.Default.Options);
        yield return ("ConversionImplicitStringTextTemplate", () => new ConversionImplicitStringTextTemplate().Options, () => ConversionImplicitStringTextTemplate.Default.Options);
        yield return ("ConversionImplicitStringUtf8Template", () => new ConversionImplicitStringUtf8Template().Options, () => ConversionImplicitStringUtf8Template.Default.Options);
        yield return ("ConversionIntTextTemplate", () => new ConversionIntTextTemplate().Options, () => ConversionIntTextTemplate.Default.Options);
        yield return ("ConversionIntUtf8Template", () => new ConversionIntUtf8Template().Options, () => ConversionIntUtf8Template.Default.Options);
        yield return ("ConversionLongTextTemplate", () => new ConversionLongTextTemplate().Options, () => ConversionLongTextTemplate.Default.Options);
        yield return ("ConversionLongUtf8Template", () => new ConversionLongUtf8Template().Options, () => ConversionLongUtf8Template.Default.Options);
        yield return ("ConversionNullLiteralTextTemplate", () => new ConversionNullLiteralTextTemplate().Options, () => ConversionNullLiteralTextTemplate.Default.Options);
        yield return ("ConversionNullLiteralUtf8Template", () => new ConversionNullLiteralUtf8Template().Options, () => ConversionNullLiteralUtf8Template.Default.Options);
        yield return ("ConversionNullToStringTextTemplate", () => new ConversionNullToStringTextTemplate().Options, () => ConversionNullToStringTextTemplate.Default.Options);
        yield return ("ConversionNullToStringUtf8Template", () => new ConversionNullToStringUtf8Template().Options, () => ConversionNullToStringUtf8Template.Default.Options);
        yield return ("ConversionNullableIntTextTemplate", () => new ConversionNullableIntTextTemplate().Options, () => ConversionNullableIntTextTemplate.Default.Options);
        yield return ("ConversionNullableIntUtf8Template", () => new ConversionNullableIntUtf8Template().Options, () => ConversionNullableIntUtf8Template.Default.Options);
        yield return ("ConversionTimeOnlyTextTemplate", () => new ConversionTimeOnlyTextTemplate().Options, () => ConversionTimeOnlyTextTemplate.Default.Options);
        yield return ("ConversionTimeOnlyUtf8Template", () => new ConversionTimeOnlyUtf8Template().Options, () => ConversionTimeOnlyUtf8Template.Default.Options);
        yield return ("ConversionTimeSpanTextTemplate", () => new ConversionTimeSpanTextTemplate().Options, () => ConversionTimeSpanTextTemplate.Default.Options);
        yield return ("ConversionTimeSpanUtf8Template", () => new ConversionTimeSpanUtf8Template().Options, () => ConversionTimeSpanUtf8Template.Default.Options);
        yield return ("ArrayModelTextTemplate", () => new ArrayModelTextTemplate().Options, () => ArrayModelTextTemplate.Default.Options);
        yield return ("ArrayModelUtf8Template", () => new ArrayModelUtf8Template().Options, () => ArrayModelUtf8Template.Default.Options);
        yield return ("NestedGenericModelTextTemplate", () => new NestedGenericModelTextTemplate().Options, () => NestedGenericModelTextTemplate.Default.Options);
        yield return ("NestedGenericModelUtf8Template", () => new NestedGenericModelUtf8Template().Options, () => NestedGenericModelUtf8Template.Default.Options);
        yield return ("TupleModelTextTemplate", () => new TupleModelTextTemplate().Options, () => TupleModelTextTemplate.Default.Options);
        yield return ("TupleModelUtf8Template", () => new TupleModelUtf8Template().Options, () => TupleModelUtf8Template.Default.Options);
        yield return ("ValueTypeModelTextTemplate", () => new ValueTypeModelTextTemplate().Options, () => ValueTypeModelTextTemplate.Default.Options);
        yield return ("ValueTypeModelUtf8Template", () => new ValueTypeModelUtf8Template().Options, () => ValueTypeModelUtf8Template.Default.Options);
        yield return ("HelpersTextTemplate", () => new HelpersTextTemplate().Options, () => HelpersTextTemplate.Default.Options);
        yield return ("HelpersUtf8Template", () => new HelpersUtf8Template().Options, () => HelpersUtf8Template.Default.Options);
        yield return ("LiteralsTextTemplate", () => new LiteralsTextTemplate().Options, () => LiteralsTextTemplate.Default.Options);
        yield return ("LiteralsUtf8Template", () => new LiteralsUtf8Template().Options, () => LiteralsUtf8Template.Default.Options);
        yield return ("ModelCasingTextTemplate", () => new ModelCasingTextTemplate().Options, () => ModelCasingTextTemplate.Default.Options);
        yield return ("ModelCasingUtf8Template", () => new ModelCasingUtf8Template().Options, () => ModelCasingUtf8Template.Default.Options);
        yield return ("VariableTextTemplate", () => new VariableTextTemplate().Options, () => VariableTextTemplate.Default.Options);
        yield return ("VariableUtf8Template", () => new VariableUtf8Template().Options, () => VariableUtf8Template.Default.Options);
        yield return ("AsyncBackpressureUtf8Template", () => new AsyncBackpressureUtf8Template().Options, () => AsyncBackpressureUtf8Template.Default.Options);
        yield return ("AsyncBeforeVariableUtf8Template", () => new AsyncBeforeVariableUtf8Template().Options, () => AsyncBeforeVariableUtf8Template.Default.Options);
        yield return ("AsyncBlockEndCrlfUtf8Template", () => new AsyncBlockEndCrlfUtf8Template().Options, () => AsyncBlockEndCrlfUtf8Template.Default.Options);
        yield return ("AsyncBlockEndLfUtf8Template", () => new AsyncBlockEndLfUtf8Template().Options, () => AsyncBlockEndLfUtf8Template.Default.Options);
        yield return ("AsyncBlockThenBlankLinesUtf8Template", () => new AsyncBlockThenBlankLinesUtf8Template().Options, () => AsyncBlockThenBlankLinesUtf8Template.Default.Options);
        yield return ("AsyncCancelLoopUtf8Template", () => new AsyncCancelLoopUtf8Template().Options, () => AsyncCancelLoopUtf8Template.Default.Options);
        yield return ("AsyncChildUtf8Template", () => new AsyncChildUtf8Template().Options, () => AsyncChildUtf8Template.Default.Options);
        yield return ("AsyncCommentAndEmptyBlockUtf8Template", () => new AsyncCommentAndEmptyBlockUtf8Template().Options, () => AsyncCommentAndEmptyBlockUtf8Template.Default.Options);
        yield return ("AsyncForeachIfElseUtf8Template", () => new AsyncForeachIfElseUtf8Template().Options, () => AsyncForeachIfElseUtf8Template.Default.Options);
        yield return ("AsyncForeachIfUtf8Template", () => new AsyncForeachIfUtf8Template().Options, () => AsyncForeachIfUtf8Template.Default.Options);
        yield return ("AsyncIfElseUtf8Template", () => new AsyncIfElseUtf8Template().Options, () => AsyncIfElseUtf8Template.Default.Options);
        yield return ("AsyncLocalUtf8Template", () => new AsyncLocalUtf8Template().Options, () => AsyncLocalUtf8Template.Default.Options);
        yield return ("AsyncLoopUtf8Template", () => new AsyncLoopUtf8Template().Options, () => AsyncLoopUtf8Template.Default.Options);
        yield return ("AsyncNestedLoopUtf8Template", () => new AsyncNestedLoopUtf8Template().Options, () => AsyncNestedLoopUtf8Template.Default.Options);
        yield return ("AsyncNoBlocksUtf8Template", () => new AsyncNoBlocksUtf8Template().Options, () => AsyncNoBlocksUtf8Template.Default.Options);
        yield return ("AsyncParentUtf8Template", () => new AsyncParentUtf8Template().Options, () => AsyncParentUtf8Template.Default.Options);
        yield return ("AsyncPlainUtf8Template", () => new AsyncPlainUtf8Template().Options, () => AsyncPlainUtf8Template.Default.Options);
        yield return ("AsyncSmallPageUtf8Template", () => new AsyncSmallPageUtf8Template().Options, () => AsyncSmallPageUtf8Template.Default.Options);
        yield return ("AsyncThresholdUtf8Template", () => new AsyncThresholdUtf8Template().Options, () => AsyncThresholdUtf8Template.Default.Options);
        yield return ("AsyncWriteOverloadsUtf8Template", () => new AsyncWriteOverloadsUtf8Template().Options, () => AsyncWriteOverloadsUtf8Template.Default.Options);
        yield return ("Readme.GreetingTextTemplate", () => new GreetingTextTemplate().Options, () => GreetingTextTemplate.Default.Options);
        yield return ("Readme.GreetingUtf8Template", () => new GreetingUtf8Template().Options, () => GreetingUtf8Template.Default.Options);
        yield return ("MyApp.OrderLineTextTemplate", () => new MyApp.Templates.OrderLineTextTemplate().Options, () => MyApp.Templates.OrderLineTextTemplate.Default.Options);
        yield return ("MyApp.OrderTextTemplate", () => new MyApp.Templates.OrderTextTemplate().Options, () => MyApp.Templates.OrderTextTemplate.Default.Options);
    }
}
