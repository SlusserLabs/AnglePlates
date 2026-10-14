namespace SlusserLabs.AnglePlates;

// One generated class's source, with its hint name before any number and the .g.cs suffix
internal sealed record GeneratedTemplate
{
    public GeneratedTemplate(string hintPrefix, string source, bool isPublic)
    {
        HintPrefix = hintPrefix;
        Source = source;
        IsPublic = isPublic;
    }

    public string HintPrefix { get; }

    public string Source { get; }

    // A public class only compiles against public support types, so the outputs skip it when they're internal
    public bool IsPublic { get; }
}
