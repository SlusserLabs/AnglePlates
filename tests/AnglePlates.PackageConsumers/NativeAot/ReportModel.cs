using System;
using System.Collections.Generic;

namespace NativeAot;

public sealed class ReportModel
{
    public string Title { get; init; } = string.Empty;

    public Permissions Permissions { get; init; }

    public object Missing { get; init; } = new NullText();

    public decimal Total { get; init; }

    public double Ratio { get; init; }

    public DateTime Generated { get; init; }

    public List<ReportLine> Lines { get; } = new();
}
