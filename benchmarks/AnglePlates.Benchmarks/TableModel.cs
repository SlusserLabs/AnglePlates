using System;
using System.Collections.Generic;

namespace SlusserLabs.AnglePlates.Benchmarks;

public sealed class TableModel
{
    private TableModel(List<TableRow> rows)
    {
        Rows = rows;
    }

    public List<TableRow> Rows { get; }

    // Every fourth name needs HTML encoding and every third quantity is null
    public static TableModel Create(int count)
    {
        var rows = new List<TableRow>(count);
        var updated = new DateTime(2026, 10, 7, 13, 5, 0);
        for (int i = 0; i < count; i++)
        {
            var name = i % 4 == 0 ? $"Fish & Chips <{i}>" : $"Item {i}";
            int? quantity = i % 3 == 0 ? null : i * 7;
            rows.Add(new TableRow(i, name, 1234.5m + i, quantity, (RowStatus)(i % 3), updated.AddMinutes(i), i / 8.0));
        }

        return new TableModel(rows);
    }
}
