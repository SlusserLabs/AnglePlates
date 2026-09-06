using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using SlusserLabs.AnglePlates.PackageTests.Templates;

namespace SlusserLabs.AnglePlates.PackageTests;

internal static class Program
{
    private static void Main()
    {
        const string model = "<AnglePlates>";
        const string expected = "Hello &lt;AnglePlates&gt;!";
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        var utf8 = new ArrayBufferWriter<byte>();
        PackageTextTemplate.Default.Render(model, text);
        PackageUtf8Template.Default.Render(model, utf8);

        if (!string.Equals(text.ToString(), expected, StringComparison.Ordinal) ||
            !utf8.WrittenSpan.SequenceEqual(Encoding.UTF8.GetBytes(expected)))
        {
            throw new InvalidOperationException("The package renderers did not produce the expected text and UTF-8 bytes.");
        }

        Console.WriteLine("Package text and UTF-8 rendering passed.");
    }
}
