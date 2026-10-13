namespace SlusserLabs.AnglePlates;

// Lists the directives in the order the parser tries them and spells each one's name
internal static class DirectiveKindExtensions
{
    public static readonly DirectiveKind[] All =
    [
        DirectiveKind.Type,
        DirectiveKind.Model,
        DirectiveKind.Namespace,
        DirectiveKind.Generate,
        DirectiveKind.Access,
        DirectiveKind.Encode,
        DirectiveKind.Using
    ];

    public static string GetName(this DirectiveKind kind)
    {
        return kind switch
        {
            DirectiveKind.Type => "type",
            DirectiveKind.Model => "model",
            DirectiveKind.Namespace => "namespace",
            DirectiveKind.Generate => "generate",
            DirectiveKind.Access => "access",
            DirectiveKind.Encode => "encode",
            DirectiveKind.Using => "using",
            _ => throw UnexpectedValue.Exception(kind, nameof(kind))
        };
    }
}
