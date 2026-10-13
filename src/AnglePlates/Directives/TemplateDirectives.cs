using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

// The validated directive values of a template that has no directive errors
internal sealed class TemplateDirectives
{
    public TemplateDirectives(
        string type, string @namespace, string modelType, GenerationTarget generationTarget, string accessModifier, IReadOnlyList<string> usings)
    {
        Type = type;
        Namespace = @namespace;
        ModelType = modelType;
        GenerationTarget = generationTarget;
        AccessModifier = accessModifier;
        Usings = usings;
    }

    public string Type { get; }

    public string Namespace { get; }

    public string ModelType { get; }

    public GenerationTarget GenerationTarget { get; }

    public string AccessModifier { get; }

    // In template order, without repeats
    public IReadOnlyList<string> Usings { get; }
}
