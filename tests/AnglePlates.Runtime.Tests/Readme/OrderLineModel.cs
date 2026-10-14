using System.Diagnostics.CodeAnalysis;

namespace MyApp;

// The order line model from the order example in docs/templates.md, in that example's namespace
[ExcludeFromCodeCoverage]
public sealed class OrderLineModel
{
    public string Name { get; init; } = default!;
    public int Quantity { get; init; }
}
