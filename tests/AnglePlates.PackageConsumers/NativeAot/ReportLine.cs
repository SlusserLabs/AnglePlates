namespace NativeAot;

public sealed class ReportLine
{
    public ReportLine(string name, int? quantity, Status status)
    {
        Name = name;
        Quantity = quantity;
        Status = status;
    }

    public string Name { get; }

    public int? Quantity { get; }

    public Status Status { get; }
}
