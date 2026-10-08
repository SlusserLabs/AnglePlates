using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

internal sealed class DirectiveError
{
    public DirectiveError(DiagnosticDescriptor descriptor, TextSpan span, params string[] arguments)
    {
        Descriptor = descriptor;
        Span = span;
        Arguments = arguments;
    }

    public DiagnosticDescriptor Descriptor { get; }

    public TextSpan Span { get; }

    public string[] Arguments { get; }
}
