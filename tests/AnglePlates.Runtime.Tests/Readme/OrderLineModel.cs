using System.Diagnostics.CodeAnalysis;

namespace MyApp;

// The README's order line model, in the README's namespace
[ExcludeFromCodeCoverage]
public sealed class OrderLineModel
{
    public string Name { get; init; } = default!;
    public int Quantity { get; init; }
}
