using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// The parent model for the child-template example
[ExcludeFromCodeCoverage]
public sealed class OrderModel
{
    public OrderModel(string number, IReadOnlyList<OrderLineModel> lines)
    {
        Number = number;
        Lines = lines;
    }

    public string Number { get; }

    public IReadOnlyList<OrderLineModel> Lines { get; }
}
