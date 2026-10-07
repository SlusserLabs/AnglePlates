using System;

namespace SlusserLabs.AnglePlates;

internal static class DirectiveKindExtensions
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

    public static string GetName(this DirectiveKind kind)
    {
        return kind switch
        {
            DirectiveKind.Type => "type",
            DirectiveKind.Model => "model",
            DirectiveKind.Namespace => "namespace",
            DirectiveKind.Generate => "generate",
            DirectiveKind.Access => "access",
            DirectiveKind.Using => "using",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
