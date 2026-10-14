namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// One link or image target in a Markdown file, with the 1-based line it's on
internal sealed record MarkdownLink
{
    public MarkdownLink(string target, int line)
    {
        Target = target;
        Line = line;
    }

    public string Target { get; }

    public int Line { get; }

    public override string ToString()
    {
        return $"line {Line}: {Target}";
    }
}
