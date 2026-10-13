namespace SlusserLabs.AnglePlates;

// The directives a template can start with
internal enum DirectiveKind
{
    Type,
    Model,
    Namespace,
    Generate,
    Access,
    Encode,
    Using
}
