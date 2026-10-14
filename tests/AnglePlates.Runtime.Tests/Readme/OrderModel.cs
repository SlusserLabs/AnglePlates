using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MyApp;

// The README's order model, in the README's namespace
[ExcludeFromCodeCoverage]
public sealed class OrderModel
{
    public string Number { get; init; } = default!;
    public List<OrderLineModel> Lines { get; } = new();
}
