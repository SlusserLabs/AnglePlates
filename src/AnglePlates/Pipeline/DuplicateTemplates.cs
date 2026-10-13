namespace SlusserLabs.AnglePlates;

// The hint names that more than one source shares, ignoring case, and those sources in the order that numbers them
internal sealed record DuplicateTemplates
{
    public DuplicateTemplates(string[] prefixes, GeneratedTemplate[] sources)
    {
        Prefixes = new EquatableArray<string>(prefixes);
        Sources = new EquatableArray<GeneratedTemplate>(sources);
    }

    public EquatableArray<string> Prefixes { get; }

    public EquatableArray<GeneratedTemplate> Sources { get; }
}
