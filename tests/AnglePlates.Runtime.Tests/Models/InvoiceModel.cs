using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// The parent model for the culture inheritance example
[ExcludeFromCodeCoverage]
public sealed class InvoiceModel
{
    public InvoiceModel(string number, IReadOnlyList<InvoiceLineModel> lines)
    {
        Number = number;
        Lines = lines;
    }

    public string Number { get; }

    public IReadOnlyList<InvoiceLineModel> Lines { get; }
}
