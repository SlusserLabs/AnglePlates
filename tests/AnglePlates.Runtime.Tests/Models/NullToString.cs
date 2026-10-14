using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// Returns null from ToString, so the C# variable writes NullVariable instead
[ExcludeFromCodeCoverage]
public sealed class NullToString
{
    public override string? ToString() => null;
}
