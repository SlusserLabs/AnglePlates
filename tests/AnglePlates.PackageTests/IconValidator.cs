using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace SlusserLabs.AnglePlates.PackageTests;

// Checks that the packed icon is a square PNG that nuget.org shows sharply without a large download
internal static class IconValidator
{
    private const int _maximumBytes = 100 * 1024;
    private const int _minimumSize = 128;
    private const int _maximumSize = 512;
    private static readonly byte[] _pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Returns every problem, so one run lists them all
    public static IReadOnlyList<string> Validate(byte[] icon)
    {
        var problems = new List<string>();
        if (icon.Length > _maximumBytes)
        {
            problems.Add($"icon.png is {icon.Length} bytes; the limit is {_maximumBytes}.");
        }

        // A PNG starts with its 8-byte signature and then the IHDR chunk: a 4-byte length, "IHDR", and the big-endian width and height
        if (icon.Length < 24 || !icon.AsSpan(0, 8).SequenceEqual(_pngSignature) || !icon.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            problems.Add("icon.png isn't a PNG file.");

            return problems;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(icon.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(icon.AsSpan(20, 4));
        if (width != height || width < _minimumSize || width > _maximumSize)
        {
            problems.Add($"icon.png is {width}x{height} px; it must be square and between {_minimumSize} and {_maximumSize} px.");
        }

        return problems;
    }
}
