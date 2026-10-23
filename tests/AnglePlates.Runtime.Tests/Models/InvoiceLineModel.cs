using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// The child model for the culture inheritance example; the decimal price is what the culture changes
[ExcludeFromCodeCoverage]
public sealed class InvoiceLineModel
{
    public InvoiceLineModel(string name, decimal price)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; }

    public decimal Price { get; }
}
