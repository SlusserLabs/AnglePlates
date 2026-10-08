using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// Everything needed to report a template diagnostic later, kept as plain values so a cached template can report it again
internal sealed record TemplateDiagnostic
{
    public TemplateDiagnostic(DiagnosticDescriptor descriptor, string path, TextSpan span, LinePositionSpan lineSpan, EquatableArray<string> arguments)
    {
        Descriptor = descriptor;
        Path = path;
        Span = span;
        LineSpan = lineSpan;
        Arguments = arguments;
    }

    public DiagnosticDescriptor Descriptor { get; }

    public string Path { get; }

    public TextSpan Span { get; }

    public LinePositionSpan LineSpan { get; }

    public EquatableArray<string> Arguments { get; }

    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(Descriptor, Location.Create(Path, Span, LineSpan), Arguments.Cast<object>().ToArray());
    }
}
