using System.Globalization;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// A compiler error a test expects. Errors in template C# assert the exact mapped template span but only the generated file,
// so cosmetic changes to the emitted layout don't break them; snapshot tests pin that layout instead.
internal sealed record ExpectedCompilerDiagnostic
{
    private ExpectedCompilerDiagnostic(string id, string message, string fileName, string? mappedPath, LinePositionSpan? span)
    {
        Id = id;
        Message = message;
        FileName = fileName;
        MappedPath = mappedPath;
        Span = span;
    }

    public string Id { get; }

    public string Message { get; }

    // The file name of the generated or consumer tree that holds the error
    public string FileName { get; }

    // The template path from a #line mapping, or null when the error isn't mapped
    public string? MappedPath { get; }

    // The mapped template span, or the span in a consumer source; null checks only the file
    public LinePositionSpan? Span { get; }

    // An error in template C#, reported through a #line mapping in the named generated source
    public static ExpectedCompilerDiagnostic Mapped(string id, string message, string templatePath, LinePositionSpan templateSpan, string generatedFile)
    {
        return new ExpectedCompilerDiagnostic(id, message, generatedFile, templatePath, templateSpan);
    }

    // An error in generated code outside any #line mapping
    public static ExpectedCompilerDiagnostic Generated(string id, string message, string generatedFile)
    {
        return new ExpectedCompilerDiagnostic(id, message, generatedFile, null, null);
    }

    // An error in a consumer source the test wrote, so its exact span is stable
    public static ExpectedCompilerDiagnostic Source(string id, string message, string fileName, LinePositionSpan span)
    {
        return new ExpectedCompilerDiagnostic(id, message, fileName, null, span);
    }

    public bool Matches(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        var mapped = diagnostic.Location.GetMappedLineSpan();
        if (diagnostic.Id != Id || diagnostic.GetMessage(CultureInfo.InvariantCulture) != Message || Path.GetFileName(lineSpan.Path) != FileName)
        {
            return false;
        }

        if (MappedPath is not null)
        {
            return mapped.HasMappedPath && mapped.Path == MappedPath && mapped.Span == Span;
        }

        return !mapped.HasMappedPath && (Span is null || lineSpan.Span == Span);
    }
}
