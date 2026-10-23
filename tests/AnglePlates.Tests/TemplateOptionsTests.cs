using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Consumer code against the emitted options and extension types, compiled through the generator
public sealed class TemplateOptionsTests
{
    [Test]
    [Arguments("Encoder", "null")]
    [Arguments("NullVariable", "null")]
    [Arguments("Culture", "global::System.Globalization.CultureInfo.InvariantCulture")]
    public async Task Options_AssignedThroughDefault_ReportsInitOnlyError(string property, string value)
    {
        // A template's shared options can't change after creation, so Default can't be reconfigured for every caller
        var consumerSource = $$"""
            namespace App;

            internal static class Configure
            {
                public static void Run() => global::Consumer.ExampleTextTemplate.Default.Options.{{property}} = {{value}};
            }
            """;
        // The diagnostic covers the assignment target, including the chosen property
        var span = new LinePositionSpan(new LinePosition(4, 32), new LinePosition(4, 85 + property.Length));
        var expected = ExpectedCompilerDiagnostic.Source(
            "CS8852", $"Init-only property or indexer 'TemplateOptions.{property}' can only be assigned in an object initializer, or on 'this' or 'base' in an instance constructor or an 'init' accessor.",
            "Consumer0.cs", span);
        var compilation = GeneratorTestHelper.CreateCompilation([consumerSource]);

        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("#generate Text\r\nx")]), [expected]);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task RenderToString_OnGeneratedTemplate_InfersModelType()
    {
        // TModel comes from the conversion to the TextTemplate<string> base class, with no type argument at the call
        const string consumerSource = """
            using SlusserLabs.AnglePlates;

            namespace App;

            public static class Renderer
            {
                public static string Render(string model) => global::Consumer.ExampleTextTemplate.Default.RenderToString(model);
            }
            """;
        var compilation = GeneratorTestHelper.CreateCompilation([consumerSource]);
        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("#generate Text\r\n<p>[[ model ]]</p>")]));
        using var generated = GeneratedAssembly.Load(result.Compilation);

        var output = generated.GetDelegate<Func<string, string>>("App.Renderer", "Render")("<value>");

        await Assert.That(output).IsEqualTo("<p>&lt;value&gt;</p>");
    }
}
