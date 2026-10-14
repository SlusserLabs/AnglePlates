using System.Diagnostics.CodeAnalysis;

namespace MyApp;

// The README's greeting model, in the README's namespace
[ExcludeFromCodeCoverage]
public sealed record GreetingModel
{
    public string Name { get; init; } = default!;
    public string? Message { get; init; }
}
