using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

// The validated directive values of a template that has no directive errors
internal sealed class TemplateDirectives
{
    public TemplateDirectives(
        string type,
        string @namespace,
        string modelType,
        GenerationTarget generationTarget,
        string accessModifier,
        TemplateEncoding encoding,
        IReadOnlyList<string> usings)
    {
        Type = type;
        Namespace = @namespace;
        ModelType = modelType;
        GenerationTarget = generationTarget;
        AccessModifier = accessModifier;
        Encoding = encoding;
        Usings = usings;
    }

    public string Type { get; }

    public string Namespace { get; }

    public string ModelType { get; }

    public GenerationTarget GenerationTarget { get; }

    public string AccessModifier { get; }

    public TemplateEncoding Encoding { get; }

    // In template order, without repeats
    public IReadOnlyList<string> Usings { get; }
}
