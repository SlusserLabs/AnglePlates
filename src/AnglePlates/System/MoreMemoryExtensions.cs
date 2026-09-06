using System.Runtime.CompilerServices;

namespace System;

internal static class MoreMemoryExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrimStartPosition(this ReadOnlySpan<char> span, int position, ReadOnlySpan<char> trimChars)
    {
        int end = span.Length;
        while (position != end && trimChars.IndexOf(span[position]) != -1)
        {
            position++;
        }

        return position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrimEndPosition(this ReadOnlySpan<char> span, int position, ReadOnlySpan<char> trimChars)
    {
        while (position != 0 && trimChars.IndexOf(span[position - 1]) != -1)
        {
            position--;
        }

        return position;
    }
}
