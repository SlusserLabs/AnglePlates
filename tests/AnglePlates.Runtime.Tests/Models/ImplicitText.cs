using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// Converts to string implicitly and has a different ToString, so the output shows which one the C# variable used
[ExcludeFromCodeCoverage]
public sealed class ImplicitText
{
    private readonly string? _text;

    public ImplicitText(string? text)
    {
        _text = text;
    }

    public static implicit operator string?(ImplicitText value) => value._text;

    public override string ToString() => "ToString";
}
