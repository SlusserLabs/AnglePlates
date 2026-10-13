using System;
using System.Collections;
using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

// An array compared by its elements, so pipeline values holding one can be cached between generator runs. It keeps the
// array it's given without copying, so the caller hands over ownership and must not change the array afterward.
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[] items)
    {
        _items = items;
    }

    public int Count => _items?.Length ?? 0;

    public T this[int index] => (_items ?? [])[index];

    public bool Equals(EquatableArray<T> other)
    {
        var items = _items ?? [];
        var otherItems = other._items ?? [];
        if (items.Length != otherItems.Length)
        {
            return false;
        }

        for (var i = 0; i < items.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(items[i], otherItems[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in _items ?? [])
        {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)(_items ?? [])).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
