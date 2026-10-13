using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// A struct model for the value-type model case
[ExcludeFromCodeCoverage]
public readonly struct Point
{
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }
}
