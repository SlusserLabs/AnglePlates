using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed record ExpectedCompilerDiagnostic
{
    public ExpectedCompilerDiagnostic(
        string id,
        string message,
        FileLinePositionSpan lineSpan = default,
        TextSpan sourceSpan = default,
        LocationKind locationKind = LocationKind.None,
        string? mappedPath = null,
        LinePositionSpan? mappedSpan = null)
    {
        Id = id;
        Message = message;
        LineSpan = lineSpan;
        SourceSpan = sourceSpan;
        LocationKind = locationKind;

        // Code outside a #line mapping reports the same location either way
        MappedPath = mappedPath ?? lineSpan.Path;
        MappedSpan = mappedSpan ?? lineSpan.Span;
    }

    public string Id { get; }

    public string Message { get; }

    public FileLinePositionSpan LineSpan { get; }

    public TextSpan SourceSpan { get; }

    public LocationKind LocationKind { get; }

    // Compared as a path and span because a mapped FileLinePositionSpan can't be constructed outside Roslyn
    public string? MappedPath { get; }

    public LinePositionSpan MappedSpan { get; }

    public static ExpectedCompilerDiagnostic FromDiagnostic(Diagnostic diagnostic)
    {
        var mapped = diagnostic.Location.GetMappedLineSpan();

        return new ExpectedCompilerDiagnostic(diagnostic.Id, diagnostic.GetMessage(CultureInfo.InvariantCulture),
            diagnostic.Location.GetLineSpan(), diagnostic.Location.SourceSpan, diagnostic.Location.Kind, mapped.Path, mapped.Span);
    }
}
