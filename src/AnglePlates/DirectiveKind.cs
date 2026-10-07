namespace SlusserLabs.AnglePlates;

internal enum DirectiveKind
{
    None, // The default for tokens that aren't directives, since Token is a struct
    Type,
    Model,
    Namespace,
    Generate,
    Access,
    Using
}
