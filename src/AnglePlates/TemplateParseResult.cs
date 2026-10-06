using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

internal sealed class TemplateParseResult
{
    public List<Token> Tokens { get; } = new();

    public List<TemplateParseError> Errors { get; } = new();
}
