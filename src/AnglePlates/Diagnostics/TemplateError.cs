using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// A problem the parser or the directive reader found, located by its span in the template text
internal sealed class TemplateError
{
    public TemplateError(DiagnosticDescriptor descriptor, TextSpan span, params string[] arguments)
    {
        Descriptor = descriptor;
        Span = span;
        Arguments = arguments;
    }

    public DiagnosticDescriptor Descriptor { get; }

    public TextSpan Span { get; }

    public string[] Arguments { get; }
}
