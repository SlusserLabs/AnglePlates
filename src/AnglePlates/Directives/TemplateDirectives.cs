using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// The validated directive values of a template that has no directive errors, with derived #type and #namespace values filled in
internal sealed class TemplateDirectives
{
    public TemplateDirectives(
        string type,
        string @namespace,
        string modelType,
        GenerationTarget generationTarget,
        string accessModifier,
        TextSpan accessSpan,
        TemplateEncoding encoding,
        IReadOnlyList<string> usings)
    {
        Type = type;
        Namespace = @namespace;
        ModelType = modelType;
        GenerationTarget = generationTarget;
        AccessModifier = accessModifier;
        AccessSpan = accessSpan;
        Encoding = encoding;
        Usings = usings;
    }

    public string Type { get; }

    public string Namespace { get; }

    public string ModelType { get; }

    public GenerationTarget GenerationTarget { get; }

    // Always lowercase, whatever casing the template used
    public string AccessModifier { get; }

    // Where SLAP005 points when the template is public; an empty span when #access is left out
    public TextSpan AccessSpan { get; }

    public TemplateEncoding Encoding { get; }

    // In template order, without repeats
    public IReadOnlyList<string> Usings { get; }
}
