using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SlusserLabs.AnglePlates
{
    public struct Token
    {
        public TokenType Type;
        public int Start;
        public int End;
        public bool TrimPrevious;
        public bool TrimNext;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int offset, int length) GetOffsetAndLength()
        {
            return (Start, End - Start);
        }
    }
}
