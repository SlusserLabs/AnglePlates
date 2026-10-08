using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class TemplateAdditionalText : AdditionalText
{
    public const string Discovered = "Discovered";
    public const string Explicit = "Explicit";

    private readonly SourceText _text;
    private int _getTextCount;

    // The AnglePlate metadata defaults to Discovered, like a file in the default Templates folder; null leaves the file untagged
    public TemplateAdditionalText(string template, string path = "Templates/example.html", string? anglePlate = Discovered)
    {
        _text = SourceText.From(template);
        Path = path;
        AnglePlate = anglePlate;
    }

    public override string Path { get; }

    public string? AnglePlate { get; }

    public int GetTextCount => Volatile.Read(ref _getTextCount);

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _getTextCount);

        return _text;
    }
}
