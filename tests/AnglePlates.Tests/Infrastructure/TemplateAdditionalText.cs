using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class TemplateAdditionalText : AdditionalText
{
    private readonly SourceText _text;

    public TemplateAdditionalText(string template, string path = "Templates/example.html")
    {
        _text = SourceText.From(template);
        Path = path;
    }

    public override string Path { get; }

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        return _text;
    }
}
