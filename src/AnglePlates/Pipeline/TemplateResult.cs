using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

// One template's diagnostics and sources; Path and Content are kept as the sort keys for numbering shared hint names
internal sealed record TemplateResult
{
    public TemplateResult(TemplateFile template, List<TemplateDiagnostic> diagnostics, List<GeneratedTemplate> sources)
    {
        Path = template.Path;
        Content = template.Content;
        Diagnostics = new EquatableArray<TemplateDiagnostic>([.. diagnostics]);
        Sources = new EquatableArray<GeneratedTemplate>([.. sources]);
    }

    public string Path { get; }

    public string Content { get; }

    public EquatableArray<TemplateDiagnostic> Diagnostics { get; }

    public EquatableArray<GeneratedTemplate> Sources { get; }
}
