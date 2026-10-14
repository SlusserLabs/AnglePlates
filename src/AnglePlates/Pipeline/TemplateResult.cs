using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

// One template's diagnostics and sources; Path and Content are kept as the sort keys for numbering shared hint names
internal sealed record TemplateResult
{
    public TemplateResult(TemplateFile template, List<TemplateDiagnostic> diagnostics, List<GeneratedTemplate> sources, TemplateDiagnostic? publicAccess = null)
    {
        Path = template.Path;
        Content = template.Content;
        Diagnostics = new EquatableArray<TemplateDiagnostic>([.. diagnostics]);
        Sources = new EquatableArray<GeneratedTemplate>([.. sources]);
        PublicAccess = publicAccess;
    }

    public string Path { get; }

    public string Content { get; }

    public EquatableArray<TemplateDiagnostic> Diagnostics { get; }

    public EquatableArray<GeneratedTemplate> Sources { get; }

    // SLAP005 at the #access value of a public template. Whether it applies depends on the support types' access
    // modifier, which this step doesn't see, so AddTemplateSources decides whether to report it.
    public TemplateDiagnostic? PublicAccess { get; }
}
