namespace SlusserLabs.AnglePlates;

// The project values every template needs; a change here re-runs every template but not the support types
internal sealed record TemplateContext
{
    public TemplateContext(string projectDirectory)
    {
        ProjectDirectory = projectDirectory;
    }

    // Empty when MSBuild doesn't supply ProjectDir, as in hand-built compilations
    public string ProjectDirectory { get; }
}
