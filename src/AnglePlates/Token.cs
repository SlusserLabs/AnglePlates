using System.Runtime.CompilerServices;

namespace SlusserLabs.AnglePlates;

internal struct Token
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
