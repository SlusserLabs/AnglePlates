namespace SlusserLabs.AnglePlates;

// What a body token holds: literal text, a C# variable, a C# block, or a comment
internal enum TokenType
{
    Literal,
    Variable,
    Code,
    Comment
}
