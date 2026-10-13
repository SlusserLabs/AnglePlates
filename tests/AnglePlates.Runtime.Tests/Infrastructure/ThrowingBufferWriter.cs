using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;

// Fails when asked for memory, or only when advanced so the encoder writes into the buffer first
[ExcludeFromCodeCoverage]
internal sealed class ThrowingBufferWriter : IBufferWriter<byte>
{
    private readonly bool _throwOnAdvance;

    public ThrowingBufferWriter(bool throwOnAdvance)
    {
        _throwOnAdvance = throwOnAdvance;
    }

    public void Advance(int count)
    {
        throw new IOException("Writer failed");
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (!_throwOnAdvance)
        {
            throw new IOException("Writer failed");
        }

        return new byte[Math.Max(1, sizeHint)];
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        return GetMemory(sizeHint).Span;
    }
}
