using System;

namespace SlusserLabs.AnglePlates.Benchmarks;

public sealed class TableRow
{
    public TableRow(int id, string name, decimal price, int? quantity, RowStatus status, DateTime updated, double ratio)
    {
        Id = id;
        Name = name;
        Price = price;
        Quantity = quantity;
        Status = status;
        Updated = updated;
        Ratio = ratio;
    }

    public int Id { get; }

    public string Name { get; }

    public decimal Price { get; }

    public int? Quantity { get; }

    public RowStatus Status { get; }

    public DateTime Updated { get; }

    public double Ratio { get; }
}
