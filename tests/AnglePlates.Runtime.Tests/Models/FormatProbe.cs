using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// A reference type that implements IFormattable and writes the format and provider it received, so the matrix shows which overload ran
[ExcludeFromCodeCoverage]
public sealed class FormatProbe : IFormattable
{
    public override string ToString() => "plain";

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var invariant = ReferenceEquals(formatProvider, CultureInfo.InvariantCulture);

        return $"fmt={format ?? "null"};inv={invariant}";
    }
}
