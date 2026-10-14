using System;
using System.Diagnostics.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// Implements IFormattable and IConvertible with different text, so the output shows which interface the conversion used.
// Convert.ToString prefers IConvertible, and Write<T> checks the interfaces in the same order.
[ExcludeFromCodeCoverage]
public readonly struct DualFormat : IFormattable, IConvertible
{
    public string ToString(string? format, IFormatProvider? formatProvider) => "formattable";

    public string ToString(IFormatProvider? provider) => "convertible";

    public TypeCode GetTypeCode() => TypeCode.Object;

    public bool ToBoolean(IFormatProvider? provider) => throw new InvalidCastException();

    public byte ToByte(IFormatProvider? provider) => throw new InvalidCastException();

    public char ToChar(IFormatProvider? provider) => throw new InvalidCastException();

    public DateTime ToDateTime(IFormatProvider? provider) => throw new InvalidCastException();

    public decimal ToDecimal(IFormatProvider? provider) => throw new InvalidCastException();

    public double ToDouble(IFormatProvider? provider) => throw new InvalidCastException();

    public short ToInt16(IFormatProvider? provider) => throw new InvalidCastException();

    public int ToInt32(IFormatProvider? provider) => throw new InvalidCastException();

    public long ToInt64(IFormatProvider? provider) => throw new InvalidCastException();

    public sbyte ToSByte(IFormatProvider? provider) => throw new InvalidCastException();

    public float ToSingle(IFormatProvider? provider) => throw new InvalidCastException();

    public object ToType(Type conversionType, IFormatProvider? provider) => throw new InvalidCastException();

    public ushort ToUInt16(IFormatProvider? provider) => throw new InvalidCastException();

    public uint ToUInt32(IFormatProvider? provider) => throw new InvalidCastException();

    public ulong ToUInt64(IFormatProvider? provider) => throw new InvalidCastException();

    public override string ToString() => "plain";
}
