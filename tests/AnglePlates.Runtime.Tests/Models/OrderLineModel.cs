using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// The child model for the child-template example
[ExcludeFromCodeCoverage]
public sealed class OrderLineModel
{
    public OrderLineModel(string name, int quantity)
    {
        Name = name;
        Quantity = quantity;
    }

    public string Name { get; }

    public int Quantity { get; }
}
