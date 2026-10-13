using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Public and internal templates and support types, alone and across referenced libraries
public sealed class AccessibilityTests
{
    [Test]
    public async Task Access_PublicTemplateWithInternalSupportTypes_ReportsInconsistentAccessibility()
    {
        var template = RenderingTestHelper.Template("#access public\r\n#generate Text\r\npublic");
        const string file = "Consumer.ExampleTextTemplate.g.cs";
        var expected = new[]
        {
            ExpectedCompilerDiagnostic.Generated("CS0060",
                "Inconsistent accessibility: base class 'TextTemplate<string>' is less accessible than class 'ExampleTextTemplate'", file),
            ExpectedCompilerDiagnostic.Generated("CS0051",
                "Inconsistent accessibility: parameter type 'TemplateOptions' is less accessible than method 'ExampleTextTemplate.ExampleTextTemplate(TemplateOptions?)'", file)
        };

        var result = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), expected);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Access_PublicTemplateInReferencedLibrary_RendersFromConsumer()
    {
        var libraryImage = EmitPublicLibrary("TemplateLibrary", "Library");
        const string consumerSource = """
            namespace App;

            public static class ReceiptRenderer
            {
                public static string Render(string model)
                {
                    var writer = new global::System.IO.StringWriter();
                    global::Library.ReceiptTextTemplate.Default.Render(model, writer);
                    return writer.ToString();
                }
            }
            """;
        var compilation = GeneratorTestHelper.CreateCompilation([consumerSource], references: ReferencesWith(libraryImage));

        var consumer = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([RenderingTestHelper.Template("app")]));
        var warnings = consumer.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning).ToArray();
        using var generated = GeneratedAssembly.Load(consumer.Compilation, [libraryImage]);
        var receipt = generated.Assembly.GetType("App.ReceiptRenderer", throwOnError: true)!.GetMethod("Render")!.Invoke(null, ["<value>"]);

        await Assert.That(consumer.RunResult.Diagnostics.Length).IsEqualTo(0);
        // The consumer's own templates bind to its own support types over the library's public copies
        await Assert.That(warnings.Length).IsGreaterThan(0);
        await Assert.That(warnings.All(warning => warning.Id == "CS0436")).IsTrue();
        await Assert.That(receipt).IsEqualTo("receipt &lt;value&gt;");
        await RenderingTestHelper.AssertRendersAsync(consumer.Compilation, "Consumer.Example", "app");
    }

    [Test]
    public async Task Access_ConsumerOptionsForReferencedLibraryTemplate_ReportsConversionError()
    {
        // TemplateOptions binds to the consumer's own copy, which isn't the type the library's constructor takes
        const string consumerSource = """
            namespace App;

            public static class Options
            {
                public static object Create() => new global::Library.ReceiptTextTemplate(new global::SlusserLabs.AnglePlates.TemplateOptions());
            }
            """;
        var compilation = GeneratorTestHelper.CreateCompilation([consumerSource], references: ReferencesWith(EmitPublicLibrary("TemplateLibrary", "Library")));
        var expected = ExpectedCompilerDiagnostic.Source("CS1503",
            "Argument 1: cannot convert from 'SlusserLabs.AnglePlates.TemplateOptions' to 'SlusserLabs.AnglePlates.TemplateOptions?'",
            "Consumer0.cs", new LinePositionSpan(new LinePosition(4, 77), new LinePosition(4, 130)));

        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver(), [expected]);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Access_TwoReferencedLibrariesWithoutAnglePlates_ReportAmbiguousTemplateOptions()
    {
        // This consumer has no generator, so neither library's support types take precedence
        const string consumerSource = """
            namespace App;

            public static class Options
            {
                public static object Create() => new global::SlusserLabs.AnglePlates.TemplateOptions();
            }
            """;
        var compilation = GeneratorTestHelper.CreateCompilation([consumerSource],
            references: ReferencesWith(EmitPublicLibrary("FirstLibrary", "First"), EmitPublicLibrary("SecondLibrary", "Second")));

        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

        await Assert.That(errors.Select(error => error.Id)).IsEquivalentTo(new[] { "CS0433" });
        await Assert.That(errors[0].GetMessage(CultureInfo.InvariantCulture)).IsEqualTo("The type 'TemplateOptions' exists in both " +
            "'FirstLibrary, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' and 'SecondLibrary, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'");
    }

    // The library opts in to public support types and a public template
    private static byte[] EmitPublicLibrary(string assemblyName, string ns)
    {
        var template = RenderingTestHelper.Template("#access public\r\nreceipt [[ model ]]", type: "Receipt", ns: ns);
        var library = GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(assemblyName: assemblyName),
            GeneratorTestHelper.CreateDriver([template], TestAnalyzerConfigOptionsProvider.WithAccessModifier("public")));
        using var image = new MemoryStream();
        library.Compilation.Emit(image);

        return image.ToArray();
    }

    private static IEnumerable<MetadataReference> ReferencesWith(params byte[][] images)
    {
        return GeneratorTestHelper.CreateCompilation().References.Concat(images.Select(image => MetadataReference.CreateFromImage(image)));
    }
}
