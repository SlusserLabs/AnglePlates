using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;

// Fails in a chosen way, or copies its input while claiming a huge expansion so the rental size must clamp
[ExcludeFromCodeCoverage]
internal sealed class ProbeEncoder : TextEncoder
{
    private readonly string _mode;

    public ProbeEncoder(string mode)
    {
        _mode = mode;
    }

    public override int MaxOutputCharactersPerInputCharacter => int.MaxValue;

    public override bool WillEncode(int unicodeScalar) => true;

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength) => 0;

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
    {
        throw new NotSupportedException();
    }

    public override OperationStatus Encode(ReadOnlySpan<char> source, Span<char> destination, out int charsConsumed, out int charsWritten,
        bool isFinalBlock = true)
    {
        charsConsumed = 0;
        charsWritten = 0;

        switch (_mode)
        {
            case "throw":
                throw new InvalidOperationException("Encoder failed");
            case "invalid":
                return OperationStatus.InvalidData;
            case "incomplete":
                return OperationStatus.NeedMoreData;
            case "stalled":
                return OperationStatus.DestinationTooSmall;
            default:
                int count = Math.Min(source.Length, destination.Length);
                source.Slice(0, count).CopyTo(destination);
                charsConsumed = count;
                charsWritten = count;

                return count == source.Length ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
        }
    }
}
