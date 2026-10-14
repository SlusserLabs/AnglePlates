namespace SlusserLabs.AnglePlates;

// One additional file the AnglePlates targets tagged, with its text already read
internal sealed record TemplateFile
{
    public TemplateFile(string path, string content, TemplateOrigin origin)
    {
        Path = path;
        Content = content;
        Origin = origin;
    }

    public string Path { get; }

    public string Content { get; }

    public TemplateOrigin Origin { get; }
}
