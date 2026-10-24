namespace SlusserLabs.AnglePlates;

// Lists the directives in the order the parser tries them and spells each one's name
internal static class DirectiveKindHelper
{
    public static readonly DirectiveKind[] All =
    [
        DirectiveKind.Type,
        DirectiveKind.Model,
        DirectiveKind.Namespace,
        DirectiveKind.Generate,
        DirectiveKind.Access,
        DirectiveKind.Using
    ];

    public static string GetName(DirectiveKind kind)
    {
        return kind switch
        {
            DirectiveKind.Type => "type",
            DirectiveKind.Model => "model",
            DirectiveKind.Namespace => "namespace",
            DirectiveKind.Generate => "generate",
            DirectiveKind.Access => "access",
            DirectiveKind.Using => "using",
            _ => throw UnexpectedValue.Exception(kind, nameof(kind))
        };
    }
}
