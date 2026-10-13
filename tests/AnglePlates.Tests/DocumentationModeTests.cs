using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Consumers with GenerateDocumentationFile parse every file, generated ones included, with documentation diagnostics on
public sealed class DocumentationModeTests
{
    [Test]
    [Arguments("internal")]
    [Arguments("public")]
    public async Task GeneratedSources_WithDocumentationDiagnostics_CompileWithoutWarnings(string accessModifier)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp11, DocumentationMode.Diagnose);
        var template = RenderingTestHelper.Template("#access " + accessModifier + "\r\n<p>[% if (model.Length > 0) { %][[ model ]][% } %]</p>");
        var compilation = GeneratorTestHelper.CreateCompilation();
        var driver = GeneratorTestHelper.CreateDriver([template], TestAnalyzerConfigOptionsProvider.WithAccessModifier(accessModifier))
            .WithUpdatedParseOptions(parseOptions);

        var result = GeneratorTestHelper.Run(compilation, driver);
        var warnings = result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning);

        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(6);
        await Assert.That(result.RunResult.GeneratedTrees.All(tree => tree.Options.DocumentationMode == DocumentationMode.Diagnose)).IsTrue();
        await Assert.That(string.Join(", ", warnings.Select(diagnostic => $"{diagnostic.Id} {diagnostic.GetMessage()}"))).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task KeywordTypeArgumentInCref_WithDocumentationDiagnostics_ReportsWarning()
    {
        // The positive control: the cref form the support types used to have warns under the same parse options
        const string source = """
            /// <summary>Writes to an <see cref="System.Buffers.IBufferWriter{byte}" />.</summary>
            public static class Sample
            {
            }
            """;
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp11, DocumentationMode.Diagnose);
        var compilation = GeneratorTestHelper.CreateCompilation().AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, parseOptions));

        var ids = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning).Select(diagnostic => diagnostic.Id);

        await Assert.That(string.Join(", ", ids.Distinct().Order())).IsEqualTo("CS1584, CS1658");
    }
}
