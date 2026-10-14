using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;

// A custom encoder that claims every character needs encoding but copies it unchanged, so a template that writes "<"
// through it proves the options' encoder ran instead of the HTML default
[ExcludeFromCodeCoverage]
internal sealed class ProbeEncoder : TextEncoder
{
    public int Calls { get; private set; }

    public override int MaxOutputCharactersPerInputCharacter => 2;

    public override bool WillEncode(int unicodeScalar) => true;

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
    {
        Calls++;

        return textLength == 0 ? -1 : 0;
    }

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
    {
        var rune = new Rune(unicodeScalar);
        numberOfCharactersWritten = rune.Utf16SequenceLength;
        if (bufferLength < numberOfCharactersWritten)
        {
            return false;
        }

        rune.EncodeToUtf16(new Span<char>(buffer, bufferLength));

        return true;
    }

    public override OperationStatus Encode(
        ReadOnlySpan<char> source, Span<char> destination, out int charsConsumed, out int charsWritten, bool isFinalBlock = true)
    {
        Calls++;
        int count = Math.Min(source.Length, destination.Length);
        source.Slice(0, count).CopyTo(destination);
        charsConsumed = count;
        charsWritten = count;

        return count == source.Length ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
    }
}
