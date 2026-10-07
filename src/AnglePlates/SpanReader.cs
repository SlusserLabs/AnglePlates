using System;
using System.Runtime.CompilerServices;

namespace SlusserLabs.AnglePlates;

// Reads contiguous spans without allocating
internal ref struct SpanReader<T> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySpan<T> _span;
    private int _position;

    public SpanReader(ReadOnlySpan<T> span)
    {
        _span = span;
    }

    public readonly ReadOnlySpan<T> Span => _span;

    public readonly int Position => _position;

    public readonly bool End => _position == _span.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _position += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceToEnd()
    {
        _position = _span.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNext(scoped ReadOnlySpan<T> next, bool advancePast = false)
    {
        if (!_span.Slice(_position).StartsWith(next))
        {
            return false;
        }

        if (advancePast)
        {
            Advance(next.Length);
        }

        return true;
    }

    // Stops at the delimiter; returns false without moving when it isn't found
    public bool TryAdvanceTo(scoped ReadOnlySpan<T> delimiter)
    {
        return TryAdvanceBy(_span.Slice(_position).IndexOf(delimiter));
    }

    // Stops at the first of any delimiter; returns false without moving when none is found
    public bool TryAdvanceToAny(scoped ReadOnlySpan<T> delimiters)
    {
        return TryAdvanceBy(_span.Slice(_position).IndexOfAny(delimiters));
    }

    // Moves past the delimiter; returns false without moving when it isn't found
    public bool TryAdvancePast(T delimiter)
    {
        int index = _span.Slice(_position).IndexOf(delimiter);

        return TryAdvanceBy(index < 0 ? index : index + 1);
    }

    private bool TryAdvanceBy(int index)
    {
        if (index < 0)
        {
            return false;
        }

        Advance(index);

        return true;
    }
}
