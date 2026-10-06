using System;
using System.IO;
using System.Text;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class ThrowingTextWriter : TextWriter
{
    private readonly Action? _beforeThrow;

    public ThrowingTextWriter(Action? beforeThrow = null)
    {
        _beforeThrow = beforeThrow;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public bool WasDisposed { get; private set; }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        _beforeThrow?.Invoke();
        throw new IOException("Writer failed");
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
