using System;
using System.Buffers;
using System.Collections.Generic;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class CountingCharPool : ArrayPool<char>
{
    private readonly HashSet<char[]> _outstanding = [];

    public int Rents { get; private set; }

    public int Returns { get; private set; }

    public int MaximumRequest { get; private set; }

    public int Outstanding => _outstanding.Count;

    public bool ThrowOnRent { get; set; }

    public int ExtraLength { get; set; }

    public override char[] Rent(int minimumLength)
    {
        if (ThrowOnRent)
        {
            throw new InvalidOperationException("Rent failed");
        }

        var array = new char[minimumLength + ExtraLength];
        _outstanding.Add(array);
        Rents++;
        MaximumRequest = Math.Max(MaximumRequest, minimumLength);

        return array;
    }

    public override void Return(char[] array, bool clearArray = false)
    {
        if (!_outstanding.Remove(array))
        {
            throw new InvalidOperationException("Returned array was not rented from this pool or was already returned");
        }

        Returns++;
    }
}
