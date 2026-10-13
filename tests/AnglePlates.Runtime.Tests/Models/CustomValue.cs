using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// Implements neither IFormattable nor IConvertible, so only ToString decides its text
[ExcludeFromCodeCoverage]
public sealed class CustomValue
{
    private readonly string _text;

    public CustomValue(string text)
    {
        _text = text;
    }

    public override string ToString() => _text;
}
