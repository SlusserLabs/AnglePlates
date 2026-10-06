namespace SlusserLabs.AnglePlates;

internal sealed class TemplateParseError
{
    public TemplateParseError(int start, int length, string closingDelimiter)
    {
        Start = start;
        Length = length;
        ClosingDelimiter = closingDelimiter;
    }

    public int Start { get; }

    public int Length { get; }

    public string ClosingDelimiter { get; }
}
