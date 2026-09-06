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
        LocationKind locationKind = LocationKind.None)
    {
        Id = id;
        Message = message;
        LineSpan = lineSpan;
        SourceSpan = sourceSpan;
        LocationKind = locationKind;
    }

    public string Id { get; }

    public string Message { get; }

    public FileLinePositionSpan LineSpan { get; }

    public TextSpan SourceSpan { get; }

    public LocationKind LocationKind { get; }

    public static ExpectedCompilerDiagnostic FromDiagnostic(Diagnostic diagnostic)
    {
        return new ExpectedCompilerDiagnostic(diagnostic.Id, diagnostic.GetMessage(CultureInfo.InvariantCulture),
            diagnostic.Location.GetLineSpan(), diagnostic.Location.SourceSpan, diagnostic.Location.Kind);
    }
}
