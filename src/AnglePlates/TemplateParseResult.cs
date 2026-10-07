using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

internal sealed class TemplateParseResult
{
    public List<Token> Tokens { get; } = new();

    public List<TemplateParseError> Errors { get; } = new();

    // Where the first non-directive line starts, which is also where the body starts
    public int DirectiveSectionEnd { get; set; }
}
