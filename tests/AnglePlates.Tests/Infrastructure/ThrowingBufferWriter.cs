using System;
using System.Buffers;
using System.IO;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class ThrowingBufferWriter : IBufferWriter<byte>
{
    private readonly Action? _beforeThrow;
    private readonly bool _throwOnAdvance;

    public ThrowingBufferWriter(Action? beforeThrow = null, bool throwOnAdvance = false)
    {
        _beforeThrow = beforeThrow;
        _throwOnAdvance = throwOnAdvance;
    }

    public void Advance(int count)
    {
        Fail();
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (!_throwOnAdvance)
        {
            Fail();
        }

        return new byte[Math.Max(1, sizeHint)];
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        return GetMemory(sizeHint).Span;
    }

    private void Fail()
    {
        _beforeThrow?.Invoke();
        throw new IOException("Writer failed");
    }
}
