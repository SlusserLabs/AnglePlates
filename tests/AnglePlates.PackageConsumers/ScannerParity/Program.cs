using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;

namespace Consumers;

// Closers inside C# strings, comments, and brackets end blocks the same way under the consumer's own compiler
internal static class Program
{
    private const string _expected = "1:7\n2:&lt;\n3:%]\n4:]]]%\n5:7\n6:%]&lt;%]\n7:tail\n8:done\n9:&lt;value&gt;";

    private static int Main()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var buffer = new ArrayBufferWriter<byte>();
        ParityTextTemplate.Default.Render("<value>", writer);
        ParityUtf8Template.Default.Render("<value>", buffer);

        var text = writer.ToString();
        var utf8 = Encoding.UTF8.GetString(buffer.WrittenSpan);
        if (!string.Equals(text, _expected, StringComparison.Ordinal) || !string.Equals(utf8, _expected, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("ScannerParity failed.");
            Console.Error.WriteLine($"Expected: {_expected}");
            Console.Error.WriteLine($"Text:     {text}");
            Console.Error.WriteLine($"UTF-8:    {utf8}");
            return 1;
        }

        Console.WriteLine("ScannerParity passed.");
        return 0;
    }
}
