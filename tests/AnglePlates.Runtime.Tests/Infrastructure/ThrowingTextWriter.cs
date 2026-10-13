using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;

// Throws on every write and records disposal, so tests can check the template leaves a caller's writer open
[ExcludeFromCodeCoverage]
internal sealed class ThrowingTextWriter : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public bool WasDisposed { get; private set; }

    // Every other TextWriter write overload ends here unless a subclass overrides it
    public override void Write(char value)
    {
        throw new IOException("Writer failed");
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
