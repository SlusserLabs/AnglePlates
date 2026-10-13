using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Consumer types named like the framework and support types mustn't capture names in emitted code, which is global::-qualified
public sealed class NameCollisionTests
{
    private const string _frameworkNames = """
            public class System { }
            public class Convert { }
            public class Globalization { }
            public class CultureInfo { }
            public class Span<T> { }
            public class ReadOnlySpan<T> { }
            public class OperationStatus { }
            public class MethodImplAttribute { }
            public class MethodImplOptions { }
            public class TextWriter { }
            public class ReadOnlyMemory<T> { }
            public class ArrayPool<T> { }
            public class TextEncoder { }
            public class HtmlEncoder { }
            public class Encoding { }
            public class Math { }
            public class Lazy<T> { }
            public class IBufferWriter<T> { }
            public class PipeWriter { }
            public class CancellationToken { }
            public class ValueTask { }
            public class ValueTask<T> { }
            public class FlushResult { }
        """;

    // Names declared in the template's own namespace and in the support type namespace
    private const string _consumerConflicts = $$"""
        namespace Consumer
        {
        {{_frameworkNames}}
            public class SlusserLabs { }
            public class TemplateOptions { }
            public class TextTemplate<T> { }
            public class Utf8Template<T> { }
        }
        namespace SlusserLabs.AnglePlates
        {
        {{_frameworkNames}}
        }
        """;

    // Names brought into scope by a #using directive
    private const string _importedConflicts = """
        namespace Conflicts
        {
            public class System { }
            public class Convert { }
            public class CultureInfo { }
            public class Lazy<T> { }
            public class Span<T> { }
            public class ReadOnlyMemory<T> { }
            public class IBufferWriter<T> { }
            public class TextWriter { }
            public class SlusserLabs { }
            public class TemplateOptions { }
            public class TextTemplate { }
            public class TextTemplate<T> { }
            public class Utf8Template { }
            public class Utf8Template<T> { }
            public class ExampleTextTemplate { }
            public class ExampleUtf8Template { }
        }
        """;

    [Test]
    [Arguments("", "", "Hello [[ model ]]", "Hello &lt;value&gt;")]
    [Arguments(_consumerConflicts, "", "Hello [[ model ]]", "Hello &lt;value&gt;")]
    [Arguments(_importedConflicts, "#using Conflicts\r\n", "Hello [[ model ]]", "Hello &lt;value&gt;")]
    [Arguments("global using System.Text;", "#using System.Text\r\n", "> [[ new StringBuilder(model) ]]", "> &lt;value&gt;")]
    public async Task ConsumerSources_WithCollidingNames_CompileWithoutWarnings(string consumerSource, string usingDirective, string body, string expected)
    {
        // Run fails the test when the generated consumer has any emit error; the consumer has no implicit imports
        var template = TemplateText.Header + usingDirective + body;
        var compilation = GeneratorTestHelper.CreateCompilation(consumerSource.Length == 0 ? [] : [consumerSource]);
        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([new TemplateAdditionalText(template)]));
        var warnings = result.Compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning);
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(5);
        await Assert.That(string.Join(", ", warnings.Select(d => $"{d.Id} {d.GetMessage()}"))).IsEqualTo(string.Empty);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }
}
