using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SlusserLabs.AnglePlates
{
    // Baby SequenceReader...
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
        public bool IsNext(scoped ReadOnlySpan<T> next, bool advancePast = false)
        {
            ReadOnlySpan<T> unread = _span.Slice(_position);
            if (unread.StartsWith(next))
            {
                if (advancePast)
                {
                    Advance(next.Length);
                }

                return true;
            }

            return false;
        }

        // NOTE: We default advancePastDelimiter to false, however, SequenceReader defaults to true
        public bool TryReadTo(out ReadOnlySpan<T> consumed, T delimiter, bool advancePastDelimiter = false)
        {
            ReadOnlySpan<T> unread = _span.Slice(_position);
            int index = unread.IndexOf(delimiter);
            if (index != -1)
            {
                if (advancePastDelimiter)
                {
                    index += 1;
                }

                consumed = unread.Slice(0, index);
                Advance(index);

                return true;
            }

            consumed = ReadOnlySpan<T>.Empty;
            return false;
        }

        // NOTE: We default advancePastDelimiter to false, however, SequenceReader defaults to true
        public bool TryAdvanceToAny(scoped ReadOnlySpan<T> delimiters, bool advancePastDelimiter = false)
        {
            ReadOnlySpan<T> unread = _span.Slice(_position);
            int index = unread.IndexOfAny(delimiters);
            if (index != -1)
            {
                if (advancePastDelimiter)
                {
                    index += 1;
                }

                Advance(index);
                return true;
            }

            return false;
        }

        public bool TryAdvanceTo(scoped ReadOnlySpan<T> delimiter, bool advancePastDelimiter = false)
        {
            ReadOnlySpan<T> unread = _span.Slice(_position);
            int index = unread.IndexOf(delimiter);
            if (index != -1)
            {
                if (advancePastDelimiter)
                {
                    index += delimiter.Length;
                }

                Advance(index);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceToEnd()
        {
            _position = _span.Length;
        }
    }
}
