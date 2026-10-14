using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MyApp;

// The order model from the order example in docs/templates.md, in that example's namespace
[ExcludeFromCodeCoverage]
public sealed class OrderModel
{
    public string Number { get; init; } = default!;
    public List<OrderLineModel> Lines { get; } = new();
}
