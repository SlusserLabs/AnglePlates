using System;
using System.Collections.Generic;

namespace NativeAot;

// The report template's model, with one property for each kind of value the Native AOT checks render
public sealed class ReportModel
{
    public string Title { get; init; } = string.Empty;

    public Permissions Permissions { get; init; }

    public object Missing { get; init; } = new NullText();

    public decimal Total { get; init; }

    public double Ratio { get; init; }

    public DateTime Generated { get; init; }

    public DateTimeOffset Stamp { get; init; }

    public Guid Id { get; init; }

    // Longer than 32K characters, so the encoders have to work through it in more than one chunk
    public string Notes { get; init; } = string.Empty;

    public List<ReportLine> Lines { get; } = new();
}
